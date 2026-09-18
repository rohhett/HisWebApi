using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using HISWEBAPI.Utilities;
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using System.Data;
using System.Reflection;
using System.Text.Json;

namespace HISWEBAPI.Repositories.Implementations
{
    public class EMRRepository : IEMRRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string CACHE_KEY_AllergyMaster_All = "_AllergyMaster_All";

        public EMRRepository(
            ICustomSqlHelper sqlHelper,
            IResponseMessageService messageService,
            IDistributedCache distributedCache,
            IConfiguration configuration)
        {
            _sqlHelper = sqlHelper;
            _messageService = messageService;
            _distributedCache = distributedCache;
            _configuration = configuration;
        }

        public ServiceResult<object> GetAllergyMasterList(int? isActive, int? allergyTypeId)
        {
            try
            {
                _log.Info($"GetAllergyMasterList called. IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_AllergyMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"AllergyMaster data retrieved from cache. Key={CACHE_KEY_AllergyMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"AllergyMaster cache miss. Fetching from database. Key={CACHE_KEY_AllergyMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetAllergyMasterList",
                        CommandType.StoredProcedure
                    );

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>()
                            .ToDictionary(
                                col => col.ColumnName,
                                col => row[col] == DBNull.Value ? null : row[col]
                            )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_AllergyMaster_All, serialized, cacheOptions);
                        _log.Info($"AllergyMaster data cached permanently. Key={CACHE_KEY_AllergyMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by IsActive
                if (isActive.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("IsActive", out var val) && val != null)
                            return val.ToString() == isActive.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by IsActive={isActive.Value}. Count={allItems.Count}");
                }

                if (allergyTypeId.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("AllergyTypeId", out var val) && val != null)
                            return val.ToString() == allergyTypeId.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by AllergyTypeId={allergyTypeId.Value}. Count={allItems.Count}");
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No allergy records found",
                        404
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} allergy record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> CreateUpdateAllergyMaster(
            CreateUpdateAllergyMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateAllergyMaster called. AllergyId={request.AllergyId}, AllergyName={request.AllergyName}");

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@AllergyId",     SqlDbType.Int)          { Value = request.AllergyId },
                    new SqlParameter("@AllergyName",   SqlDbType.NVarChar, 256){ Value = request.AllergyName },
                    new SqlParameter("@AllergyTypeId", SqlDbType.Int)          { Value = request.AllergyTypeId },
                    new SqlParameter("@AllergyType",   SqlDbType.NVarChar, 256){ Value = request.AllergyType ?? (object)DBNull.Value },
                    new SqlParameter("@snomedCode",    SqlDbType.NVarChar, 100){ Value = request.SnomedCode ?? (object)DBNull.Value },
                    new SqlParameter("@active",        SqlDbType.Int)          { Value = request.Active },
                    new SqlParameter("@userId",        SqlDbType.Int)          { Value = globalValues.userId },
                    new SqlParameter("@IpAddress",     SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
                    new SqlParameter("@Result",        SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_AllergyMaster", parameters);

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate AllergyName: {request.AllergyName}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Allergy name already exists",
                        409
                    );
                }

                if (result > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_AllergyMaster_All);
                    _log.Info($"Cleared AllergyMaster cache. AllergyId={result}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.AllergyId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    return ServiceResult<object>.Success(
                        new { AllergyId = result },
                        alert.Type,
                        alert.Message,
                        request.AllergyId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> DeleteAllergyMaster(int allergyId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeleteAllergyMaster called. AllergyId={allergyId}");

                var result = _sqlHelper.DML(
                    "U_DeleteAllergyMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @allergyId = allergyId
                    }
                );

                if (result > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_AllergyMaster_All);
                    _log.Info($"Cleared AllergyMaster cache after delete. AllergyId={allergyId}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "Allergy deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"AllergyMaster not found for AllergyId={allergyId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Allergy not found",
                        404
                    );
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        private const string CACHE_KEY_SaltNameMaster_All = "_SaltNameMaster_All";

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetSaltNameMasterList(string saltName = null)
        {
            try
            {
                _log.Info($"GetSaltNameMasterList called. SaltName={saltName ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_SaltNameMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SaltNameMaster data retrieved from cache. Key={CACHE_KEY_SaltNameMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"SaltNameMaster cache miss. Fetching data from database. Key={CACHE_KEY_SaltNameMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetSaltNameMasterList",
                        CommandType.StoredProcedure
                    );

                    allItems = ConvertDataTableToList(dataTable);

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_SaltNameMaster_All, serialized, cacheOptions);
                        _log.Info($"SaltNameMaster data cached permanently. Key={CACHE_KEY_SaltNameMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by SaltName; blank/null = return all
                List<Dictionary<string, object>> filteredItems = allItems;

                if (!string.IsNullOrWhiteSpace(saltName))
                {
                    filteredItems = filteredItems.Where(row =>
                    {
                        if (row.TryGetValue("SaltName", out var val) && val != null)
                            return val.ToString().Contains(saltName.Trim(), StringComparison.OrdinalIgnoreCase);
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by SaltName='{saltName}'. Count={filteredItems.Count}");
                }

                if (!filteredItems.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No salt names found");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        "No salt names found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredItems.Count} salt name(s) from cache");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    filteredItems,
                    "Info",
                    $"{filteredItems.Count} salt name(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        /// <summary>
        /// Converts a DataTable to a list of dictionaries so raw stored procedure
        /// columns are returned without requiring a fixed model mapping.
        /// </summary>
        private List<Dictionary<string, object>> ConvertDataTableToList(DataTable dataTable)
        {
            var result = new List<Dictionary<string, object>>();

            if (dataTable == null)
                return result;

            foreach (DataRow row in dataTable.Rows)
            {
                var rowDict = new Dictionary<string, object>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    rowDict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }
                result.Add(rowDict);
            }

            return result;
        }


        // ─── Patient Allergy Details ──────────────────────────────────────────────────

        public ServiceResult<CreateUpdatePatientAllergyDetailsResponse> CreateUpdatePatientAllergyDetails(
            CreateUpdatePatientAllergyDetailsRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdatePatientAllergyDetails called. Id={request.Id}, PatientId={request.PatientId}, AllergyId={request.AllergyId}");

                var result = _sqlHelper.ExecuteScalar(
                    "IU_PatientAllergyDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @Id = request.Id,
                        @patientId = request.PatientId,
                        @AllergyId = request.AllergyId,
                        @AllergyName = request.AllergyName ?? (object)DBNull.Value,
                        @AllergyTypeId = request.AllergyTypeId,
                        @AllergyType = request.AllergyType ?? (object)DBNull.Value,
                        @Reaction = request.Reaction ?? (object)DBNull.Value,
                        @Remarks = request.Remarks ?? (object)DBNull.Value,
                        @InteractionSeverity = request.InteractionSeverity ?? (object)DBNull.Value,
                        @ClinicalStatus = request.ClinicalStatus ?? (object)DBNull.Value,
                        @VerificationStatus = request.VerificationStatus ?? (object)DBNull.Value,
                        @SnomedCode = request.SnomedCode ?? (object)DBNull.Value,
                        @NotKnownAllergy = request.NotKnownAllergy,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate allergy entry. PatientId={request.PatientId}, AllergyId={request.AllergyId}");
                    return ServiceResult<CreateUpdatePatientAllergyDetailsResponse>.Failure(
                        dupAlert.Type,
                        "This allergy is already recorded for the patient",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    // Clear cache for this patient's allergy list
                    _distributedCache.Remove($"_PatientAllergyDetails_{request.PatientId}");
                    _log.Info($"Cleared PatientAllergyDetails cache for PatientId={request.PatientId}");

                    var responseData = new CreateUpdatePatientAllergyDetailsResponse { Id = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.Id == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"PatientAllergyDetails {(request.Id == 0 ? "created" : "updated")} successfully. Id={resultValue}");

                    return ServiceResult<CreateUpdatePatientAllergyDetailsResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.Id == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"PatientAllergyDetails operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdatePatientAllergyDetailsResponse>.Failure(
                    failAlert.Type,
                    failAlert.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdatePatientAllergyDetailsResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetPatientAllergyDetailList(int patientId)
        {
            try
            {
                _log.Info($"GetPatientAllergyDetailList called. PatientId={patientId}");

                string cacheKey = $"_PatientAllergyDetails_{patientId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> rawData;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PatientAllergyDetails retrieved from cache. Key={cacheKey}");
                    rawData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"PatientAllergyDetails cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetPatientAllergyDetailList",
                        CommandType.StoredProcedure,
                        new { @patientId = patientId }
                    );

                    rawData = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (rawData.Any())
                    {
                        var serialized = JsonSerializer.Serialize(rawData);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"PatientAllergyDetails cached. Key={cacheKey}, Count={rawData.Count}");
                    }
                }

                if (!rawData.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No allergy details found for PatientId={patientId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No allergy details found for this patient",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                _log.Info($"Retrieved {rawData.Count} allergy record(s) for PatientId={patientId}");

                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} allergy record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> DeletePatientAllergyDetails(
            DeletePatientAllergyDetailsRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeletePatientAllergyDetails called. Id={request.Id}, PatientId={request.PatientId}");

                _sqlHelper.DML(
                    "U_DeletePatientAllergyDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @id = request.Id,
                        @patientId = request.PatientId,
                        @deactivationRemarks = request.DeactivationRemarks ?? (object)DBNull.Value
                    }
                );

                // Clear cache for this patient's allergy list
                _distributedCache.Remove($"_PatientAllergyDetails_{request.PatientId}");
                _log.Info($"Cleared PatientAllergyDetails cache for PatientId={request.PatientId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"PatientAllergyDetails deleted successfully. Id={request.Id}, PatientId={request.PatientId}");

                return ServiceResult<string>.Success(
                    "Allergy record deleted successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        private const string CACHE_KEY_DiagnosisMaster_All = "_DiagnosisMaster_All";

        public ServiceResult<object> GetDiagnosisMasterList(int? isActive)
        {
            try
            {
                _log.Info($"GetDiagnosisMasterList called. IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_DiagnosisMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DiagnosisMaster data retrieved from cache. Key={CACHE_KEY_DiagnosisMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"DiagnosisMaster cache miss. Fetching from database. Key={CACHE_KEY_DiagnosisMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetDiagnosisMasterList",
                        CommandType.StoredProcedure
                    );

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>()
                            .ToDictionary(
                                col => col.ColumnName,
                                col => row[col] == DBNull.Value ? null : row[col]
                            )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_DiagnosisMaster_All, serialized, cacheOptions);
                        _log.Info($"DiagnosisMaster data cached permanently. Key={CACHE_KEY_DiagnosisMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by IsActive; null = return all
                if (isActive.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("IsActive", out var val) && val != null)
                            return val.ToString() == isActive.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by IsActive={isActive.Value}. Count={allItems.Count}");
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No diagnosis records found",
                        404
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} diagnosis record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> CreateUpdateDiagnosisMaster(
            CreateUpdateDiagnosisMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDiagnosisMaster called. DiagnosisId={request.DiagnosisId}, DiagnosisName={request.DiagnosisName}");

                var parameters = new SqlParameter[]
                {
            new SqlParameter("@DiagnosisId",   SqlDbType.Int)          { Value = request.DiagnosisId },
            new SqlParameter("@DiagnosisName", SqlDbType.NVarChar, 256){ Value = request.DiagnosisName },
            new SqlParameter("@snomedCode",    SqlDbType.NVarChar, 100){ Value = request.SnomedCode ?? (object)DBNull.Value },
            new SqlParameter("@active",        SqlDbType.Int)          { Value = request.Active },
            new SqlParameter("@userId",        SqlDbType.Int)          { Value = globalValues.userId },
            new SqlParameter("@IpAddress",     SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
            new SqlParameter("@Result",        SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_DiagnosisMaster", parameters);

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate DiagnosisName: {request.DiagnosisName}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Diagnosis name already exists",
                        409
                    );
                }

                if (result > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_DiagnosisMaster_All);
                    _log.Info($"Cleared DiagnosisMaster cache. DiagnosisId={result}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.DiagnosisId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    return ServiceResult<object>.Success(
                        new { DiagnosisId = result },
                        alert.Type,
                        alert.Message,
                        request.DiagnosisId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        private const string CACHE_KEY_ProcedureMaster_All = "_ProcedureMaster_All";

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetProcedureMasterList(int? isActive)
        {
            try
            {
                _log.Info($"GetProcedureMasterList called. IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_ProcedureMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"ProcedureMaster data retrieved from cache. Key={CACHE_KEY_ProcedureMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"ProcedureMaster cache miss. Fetching from database. Key={CACHE_KEY_ProcedureMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetProcedureMasterList",
                        CommandType.StoredProcedure
                    );

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>()
                            .ToDictionary(
                                col => col.ColumnName,
                                col => row[col] == DBNull.Value ? null : row[col]
                            )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_ProcedureMaster_All, serialized, cacheOptions);
                        _log.Info($"ProcedureMaster data cached permanently. Key={CACHE_KEY_ProcedureMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by IsActive; null = return all
                if (isActive.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("IsActive", out var val) && val != null)
                            return val.ToString() == isActive.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by IsActive={isActive.Value}. Count={allItems.Count}");
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        notFoundAlert.Type,
                        "No procedure records found",
                        404
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} procedure record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                    alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> CreateUpdateProcedureMaster(
            CreateUpdateProcedureMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateProcedureMaster called. ProcedureId={request.ProcedureId}, ProcedureName={request.ProcedureName}");

                var parameters = new SqlParameter[]
                {
            new SqlParameter("@ProcedureId",   SqlDbType.Int)          { Value = request.ProcedureId },
            new SqlParameter("@ProcedureName", SqlDbType.NVarChar, 256){ Value = request.ProcedureName },
            new SqlParameter("@snomedCode",    SqlDbType.NVarChar, 100){ Value = request.SnomedCode ?? (object)DBNull.Value },
            new SqlParameter("@active",        SqlDbType.Int)          { Value = request.Active },
            new SqlParameter("@userId",        SqlDbType.Int)          { Value = globalValues.userId },
            new SqlParameter("@IpAddress",     SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
            new SqlParameter("@Result",        SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_ProcedureMaster", parameters);

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate ProcedureName: {request.ProcedureName}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Procedure name already exists",
                        409
                    );
                }

                if (result > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_ProcedureMaster_All);
                    _log.Info($"Cleared ProcedureMaster cache. ProcedureId={result}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ProcedureId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    return ServiceResult<object>.Success(
                        new { ProcedureId = result },
                        alert.Type,
                        alert.Message,
                        request.ProcedureId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        private const string CACHE_KEY_EMRSectionMaster_All = "_EMRSectionMaster_All";

        public ServiceResult<object> CreateUpdateEMRSectionMaster(
            CreateUpdateEMRSectionMasterRequest request,
            AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"CreateUpdateEMRSectionMaster called. SectionId={request.SectionId}, SectionName={request.SectionName}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Insert or Update EMRSectionMaster
                var parameters = new SqlParameter[]
                {
            new SqlParameter("@sectionId",   SqlDbType.Int)          { Value = request.SectionId },
            new SqlParameter("@sectionName", SqlDbType.NVarChar, 256){ Value = request.SectionName },
            new SqlParameter("@displayName", SqlDbType.NVarChar, 256){ Value = request.DisplayName ?? (object)DBNull.Value },
            new SqlParameter("@isActive",    SqlDbType.Int)          { Value = request.IsActive },
            new SqlParameter("@userId",      SqlDbType.Int)          { Value = globalValues.userId },
            new SqlParameter("@IpAddress",   SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
            new SqlParameter("@Result",      SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long sectionResult = _sqlHelper.RunProcedureInsert("IU_EMRSectionMaster", parameters);

                if (sectionResult == -1)
                {
                    tnx.Rollback();
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate SectionName: {request.SectionName}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Section name already exists",
                        409
                    );
                }

                if (sectionResult <= 0)
                {
                    tnx.Rollback();
                    var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error($"IU_EMRSectionMaster returned unexpected result: {sectionResult}");
                    return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
                }

                int resolvedSectionId = (int)sectionResult;
                _log.Info($"EMRSectionMaster {(request.SectionId == 0 ? "inserted" : "updated")}. SectionId={resolvedSectionId}");

                // Step 2 – Delete existing header mappings for this section
                _sqlHelper.DML(tnx, "D_EMRSectionHeaderMapping", CommandType.StoredProcedure, new
                {
                    @sectionId = resolvedSectionId
                });
                _log.Info($"Deleted existing EMRSectionHeaderMapping for SectionId={resolvedSectionId}");

                // Step 3 – Insert new header mappings
                int insertedCount = 0;
                if (request.HeaderMappings != null && request.HeaderMappings.Any())
                {
                    foreach (var item in request.HeaderMappings)
                    {
                        _sqlHelper.DML(tnx, "I_EMRSectionHeaderMapping", CommandType.StoredProcedure, new
                        {
                            @sectionId = resolvedSectionId,
                            @headerId = item.HeaderId,
                            @sequenceNo = item.SequenceNo,
                            @userId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        });
                        insertedCount++;
                    }
                }

                tnx.Commit();
                _log.Info($"CreateUpdateEMRSectionMaster committed. SectionId={resolvedSectionId}, MappingsInserted={insertedCount}");

                // Invalidate cache after successful write
                _distributedCache.Remove(CACHE_KEY_EMRSectionMaster_All);
                _log.Info($"Cleared EMRSectionMaster cache. Key={CACHE_KEY_EMRSectionMaster_All}");

                var alert = _messageService.GetMessageAndTypeByAlertCode(
                    request.SectionId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                );
                return ServiceResult<object>.Success(
                    new { SectionId = resolvedSectionId, MappingsInserted = insertedCount },
                    alert.Type,
                    alert.Message,
                    request.SectionId == 0 ? 201 : 200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch { /* swallow */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == System.Data.ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<object> GetEMRSectionMaster(int? isActive)
        {
            try
            {
                _log.Info($"GetEMRSectionMaster called. IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_EMRSectionMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"EMRSectionMaster data retrieved from cache. Key={CACHE_KEY_EMRSectionMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"EMRSectionMaster cache miss. Fetching from database. Key={CACHE_KEY_EMRSectionMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetEMRSectionMaster",
                        CommandType.StoredProcedure
                    );

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_EMRSectionMaster_All, serialized, cacheOptions);
                        _log.Info($"EMRSectionMaster cached permanently. Key={CACHE_KEY_EMRSectionMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by IsActive; null = return all
                if (isActive.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("IsActive", out var val) && val != null)
                            return val.ToString() == isActive.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by IsActive={isActive.Value}. Count={allItems.Count}");
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn("No EMR section records found.");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No EMR section records found", 404);
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} EMR section record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetEMRSectionHeaderMapping(int sectionId)
        {
            try
            {
                _log.Info($"GetEMRSectionHeaderMapping called. SectionId={sectionId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetEMRSectionHeaderMapping",
                    CommandType.StoredProcedure,
                    new { @sectionId = sectionId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No header mappings found for SectionId={sectionId}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No header mappings found for this section",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetEMRSectionHeaderMapping retrieved {result.Count} record(s) for SectionId={sectionId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert.Type,
                    $"{result.Count} header mapping(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetEMRSectionDepartmentMapping(int typeId, int relatedToId)
        {
            try
            {
                _log.Info($"GetEMRSectionDepartmentMapping called. TypeId={typeId}, RelatedToId={relatedToId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetEMRSectionDepartmentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @typeId = typeId,
                        @relatedToId = relatedToId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No EMRSection department mapping found for TypeId={typeId}, RelatedToId={relatedToId}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No EMRSection department mapping found",
                        404
                    );
                }

                var rawData = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>()
                        .ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                ).ToList();

                _log.Info($"GetEMRSectionDepartmentMapping retrieved {rawData.Count} record(s)");

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> SaveEMRSectionDepartmentMapping(
            SaveEMRSectionDepartmentMappingRequest request,
            AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"SaveEMRSectionDepartmentMapping called. TypeId={request.TypeId}, RelatedToId={request.RelatedToId}, Items={request.HeaderMappingData?.Count ?? 0}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Delete existing mappings
                _sqlHelper.DML(tnx, "D_DeleteEMRSectionDepartmentMapping", CommandType.StoredProcedure, new
                {
                    @typeId = request.TypeId,
                    @relatedToId = request.RelatedToId
                });
                _log.Info($"Deleted existing mappings for TypeId={request.TypeId}, RelatedToId={request.RelatedToId}");

                // Step 2 – Insert new mappings
                int insertedCount = 0;
                if (request.HeaderMappingData != null && request.HeaderMappingData.Any())
                {
                    foreach (var item in request.HeaderMappingData)
                    {
                        _sqlHelper.DML(tnx, "I_EMRSectionDepartmentMapping", CommandType.StoredProcedure, new
                        {
                            @hospId = globalValues.hospId,
                            @typeId = request.TypeId,
                            @typeName = request.TypeName ?? string.Empty,
                            @SectionId = item.SectionId,
                            @retatedToId = request.RelatedToId,
                            @sequenceNo = item.SequenceNo,
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        });
                        insertedCount++;
                    }
                }

                tnx.Commit();
                _log.Info($"SaveEMRSectionDepartmentMapping committed. Inserted={insertedCount}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{insertedCount} mapping(s) saved successfully",
                    alert.Type,
                    "Mapping Updated Successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch { /* swallow */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == System.Data.ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }
        private const string CACHE_KEY_EMRSectionScoreFormula = "_EMRSectionScoreFormula_Section{0}";

        public ServiceResult<object> GetEMRSectionScoreFormula(int sectionId)
        {
            try
            {
                _log.Info($"GetEMRSectionScoreFormula called. SectionId={sectionId}");

                string cacheKey = string.Format(CACHE_KEY_EMRSectionScoreFormula, sectionId);

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"EMRSectionScoreFormula data retrieved from cache. Key={cacheKey}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"EMRSectionScoreFormula cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetEMRSectionScoreFormula",
                        CommandType.StoredProcedure,
                        new { @sectionId = sectionId }
                    );

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"EMRSectionScoreFormula cached permanently. Key={cacheKey}, Count={allItems.Count}");
                    }
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No score formula found for SectionId={sectionId}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No score formula found for this section",
                        404
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} score formula record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> SaveEMRSectionScoreFormula(
            SaveEMRSectionScoreFormulaRequest request,
            AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"SaveEMRSectionScoreFormula called. SectionId={request.SectionId}, Items={request.FormulaItems?.Count ?? 0}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Delete existing formula rows for this section
                _sqlHelper.DML(tnx, "D_EMRSectionScoreFormula", CommandType.StoredProcedure, new
                {
                    @sectionId = request.SectionId
                });
                _log.Info($"Deleted existing EMRSectionScoreFormula for SectionId={request.SectionId}");

                // Step 2 – Insert new formula rows
                int insertedCount = 0;
                if (request.FormulaItems != null && request.FormulaItems.Any())
                {
                    foreach (var item in request.FormulaItems)
                    {
                        _sqlHelper.DML(tnx, "I_EMRSectionScoreFormula", CommandType.StoredProcedure, new
                        {
                            @SectionId = request.SectionId,
                            @HeaderId = item.HeaderId,
                            @ReferenceName = item.ReferenceName ?? (object)DBNull.Value,
                            @FormulaDefinition = item.FormulaDefinition ?? (object)DBNull.Value,
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        });
                        insertedCount++;
                    }
                }

                tnx.Commit();
                _log.Info($"SaveEMRSectionScoreFormula committed. SectionId={request.SectionId}, Inserted={insertedCount}");

                // Invalidate per-section cache after successful write
                string cacheKey = string.Format(CACHE_KEY_EMRSectionScoreFormula, request.SectionId);
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared EMRSectionScoreFormula cache. Key={cacheKey}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    new { SectionId = request.SectionId, InsertedCount = insertedCount },
                    alert.Type,
                    "Score formula saved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch { /* swallow */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == System.Data.ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        private const string CACHE_KEY_EMRSectionAttributeCondition = "_EMRSectionAttributeCondition_Section{0}";

        public ServiceResult<object> GetEMRSectionAttributeCondition(int sectionId)
        {
            try
            {
                _log.Info($"GetEMRSectionAttributeCondition called. SectionId={sectionId}");

                string cacheKey = string.Format(CACHE_KEY_EMRSectionAttributeCondition, sectionId);

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"EMRSectionAttributeCondition data retrieved from cache. Key={cacheKey}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"EMRSectionAttributeCondition cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetEMRSectionAttributeCondition",
                        CommandType.StoredProcedure,
                        new { @sectionId = sectionId }
                    );

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"EMRSectionAttributeCondition cached permanently. Key={cacheKey}, Count={allItems.Count}");
                    }
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No attribute conditions found for SectionId={sectionId}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No attribute conditions found for this section",
                        404
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} attribute condition record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> SaveEMRSectionAttributeCondition(
            SaveEMRSectionAttributeConditionRequest request,
            AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"SaveEMRSectionAttributeCondition called. SectionId={request.SectionId}, Groups={request.AttributeConditions?.Count ?? 0}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Delete existing conditions for this section
                _sqlHelper.DML(tnx, "D_EMRSectionAttributeCondition", CommandType.StoredProcedure, new
                {
                    @sectionId = request.SectionId
                });
                _log.Info($"Deleted existing EMRSectionAttributeCondition for SectionId={request.SectionId}");

                // Step 2 – Flatten groups and insert each condition row
                int insertedCount = 0;
                if (request.AttributeConditions != null && request.AttributeConditions.Any())
                {
                    foreach (var group in request.AttributeConditions)
                    {
                        if (group.Conditions == null || !group.Conditions.Any())
                            continue;

                        foreach (var item in group.Conditions)
                        {
                            _sqlHelper.DML(tnx, "I_EMRSectionAttributeCondition", CommandType.StoredProcedure, new
                            {
                                @sectionId = request.SectionId,
                                @targetHeaderId = group.TargetHeaderId,
                                @headerId = item.HeaderId,
                                @operator = item.Operator,
                                @value = item.Value ?? (object)DBNull.Value,
                                @connector = item.Connector ?? (object)DBNull.Value,
                                @userId = globalValues.userId,
                                @ipAddress = globalValues.ipAddress
                            });
                            insertedCount++;
                        }
                    }
                }

                tnx.Commit();
                _log.Info($"SaveEMRSectionAttributeCondition committed. SectionId={request.SectionId}, Inserted={insertedCount}");

                // Invalidate per-section cache after successful write
                string cacheKey = string.Format(CACHE_KEY_EMRSectionAttributeCondition, request.SectionId);
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared EMRSectionAttributeCondition cache. Key={cacheKey}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    new { SectionId = request.SectionId, InsertedCount = insertedCount },
                    alert.Type,
                    "Attribute conditions saved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch { /* swallow */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == System.Data.ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<object> DeleteEMRSectionAttributeCondition(int id)
        {
            try
            {
                _log.Info($"DeleteEMRSectionAttributeCondition called. Id={id}");

                // Fetch SectionId before delete so we can clear the correct cache key
                var dataTable = _sqlHelper.GetDataTable(
                    "SELECT SectionId FROM EMRSectionAttributeCondition WHERE Id = @id",
                    CommandType.Text,
                    new { @id = id }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"EMRSectionAttributeCondition not found for Id={id}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "Attribute condition not found",
                        404
                    );
                }

                int sectionId = Convert.ToInt32(dataTable.Rows[0]["SectionId"]);

                var result = _sqlHelper.DML(
                    "D_EMRSectionAttributeConditionById",
                    CommandType.StoredProcedure,
                    new { @id = id }
                );

                if (result > 0)
                {
                    // Invalidate per-section cache after successful delete
                    string cacheKey = string.Format(CACHE_KEY_EMRSectionAttributeCondition, sectionId);
                    _distributedCache.Remove(cacheKey);
                    _log.Info($"Cleared EMRSectionAttributeCondition cache. Key={cacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    return ServiceResult<object>.Success(
                        new { Id = id, SectionId = sectionId },
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    _log.Warn($"Delete operation returned 0 rows affected for Id={id}");
                    return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetEMRHeaderQueryResult(int headerId)
        {
            try
            {
                _log.Info($"GetEMRHeaderQueryResult called. HeaderId={headerId}");

                DataTable dataTable = null;

                try
                {
                    dataTable = _sqlHelper.GetDataTable(
                        "S_GetEMRHeaderQueryResult",
                        CommandType.StoredProcedure,
                        new { headerId }
                    );
                }
                catch (IndexOutOfRangeException)
                {
                    // Stored procedure returned no result set
                    dataTable = null;
                }

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");

                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No options found for this header",
                        404);
                }

                var result = dataTable.AsEnumerable()
                    .Select(row => dataTable.Columns.Cast<DataColumn>()
                    .ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]))
                    .ToList();

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");

                return ServiceResult<object>.Success(
                    result,
                    success.Type,
                    $"{result.Count} option(s) retrieved successfully",
                    200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        private static string CacheKey(int doctorId) => $"_DoctorFavouriteEMRSections_{doctorId}";

        public ServiceResult<string> SaveDoctorFavouriteEMRSections(
            SaveDoctorFavouriteEMRSectionsRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveDoctorFavouriteEMRSections called. DoctorId={request.DoctorId}, SectionCount={request.SectionIds?.Count ?? 0}");

                // Delete-then-insert pattern
                _sqlHelper.DML(
                    "D_DoctorWiseFavouriteEMRSectionsMapping",
                    CommandType.StoredProcedure,
                    new { @DoctorId = request.DoctorId }
                );

                if (request.SectionIds != null)
                {
                    foreach (var sectionId in request.SectionIds)
                    {
                        _sqlHelper.DML(
                            "I_DoctorWiseFavouriteEMRSectionsMapping",
                            CommandType.StoredProcedure,
                            new
                            {
                                @DoctorId = request.DoctorId,
                                @SectionId = sectionId,
                                @userId = globalValues.userId,
                                @IpAddress = globalValues.ipAddress
                            }
                        );
                    }
                }

                // Invalidate cache after successful commit
                _distributedCache.Remove(CacheKey(request.DoctorId));
                _log.Info($"Cleared DoctorFavouriteEMRSections cache. DoctorId={request.DoctorId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Favourite EMR sections saved successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetDoctorFavouriteEMRSections(int doctorId)
        {
            try
            {
                _log.Info($"GetDoctorFavouriteEMRSections called. DoctorId={doctorId}");

                string cacheKey = CacheKey(doctorId);
                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> rawData;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorFavouriteEMRSections retrieved from cache. Key={cacheKey}");
                    rawData = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorFavouriteEMRSections cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_DoctorWiseFavouriteEMRSectionsMapping",
                        CommandType.StoredProcedure,
                        new { @DoctorId = doctorId }
                    );

                    // Raw DataTable -> List<Dictionary<string,object>>, no model mapping
                    rawData = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (rawData.Any())
                    {
                        var serialized = JsonSerializer.Serialize(rawData);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"DoctorFavouriteEMRSections cached permanently. Key={cacheKey}, Count={rawData.Count}");
                    }
                }

                if (!rawData.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No favourite EMR sections found for DoctorId={doctorId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No favourite EMR sections found for this doctor",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} favourite EMR section(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CreateUpdateChiefComplaintMasterResponse> CreateUpdateChiefComplaintMaster(
    CreateUpdateChiefComplaintMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateChiefComplaintMaster called. ComplaintId={request.ComplaintId}, ComplaintName={request.ComplaintName}");

                // IU_ChiefComplaintMaster sets @Result via a true OUTPUT parameter with no trailing
                // SELECT @Result;, so RunProcedureInsert is required here (reads the OUTPUT param directly).
                long resultValue = _sqlHelper.RunProcedureInsert(
                    "IU_ChiefComplaintMaster",
                    new IDataParameter[]
                    {
                new SqlParameter("@complaintId", request.ComplaintId),
                new SqlParameter("@complaintName", request.ComplaintName),
                new SqlParameter("@snomedCode", (object)request.SnomedCode ?? DBNull.Value),
                new SqlParameter("@isActive", request.IsActive),
                new SqlParameter("@UserId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int result = Convert.ToInt32(resultValue);

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate ComplaintName: {request.ComplaintName}");
                    return ServiceResult<CreateUpdateChiefComplaintMasterResponse>.Failure(
                        dupAlert.Type,
                        "Complaint Name already exists",
                        409
                    );
                }

                if (result > 0)
                {
                    // Clear cache so next GET re-fetches fresh data
                    _distributedCache.Remove("_ChiefComplaintMaster_All");
                    _log.Info($"Cleared ChiefComplaintMaster cache. ComplaintId={result}");

                    var responseData = new CreateUpdateChiefComplaintMasterResponse { ComplaintId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ComplaintId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"ChiefComplaint {(request.ComplaintId == 0 ? "created" : "updated")} successfully. ComplaintId={result}");

                    return ServiceResult<CreateUpdateChiefComplaintMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.ComplaintId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateChiefComplaintMasterResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateChiefComplaintMasterResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetChiefComplaintMasterList(int? isActive)
        {
            try
            {
                _log.Info($"GetChiefComplaintMasterList called. IsActive={isActive?.ToString() ?? "All"}");

                const string cacheKey = "_ChiefComplaintMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allComplaints;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"ChiefComplaintMaster data retrieved from cache. Key={cacheKey}");
                    allComplaints = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"ChiefComplaintMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetChiefComplaintMasterList",
                        CommandType.StoredProcedure
                    );

                    allComplaints = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    // Store ALL complaints in cache (no expiration)
                    if (allComplaints.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allComplaints);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All ChiefComplaintMaster data cached permanently. Key={cacheKey}, Count={allComplaints.Count}");
                    }
                }

                // Filter in memory based on isActive (always from cache)
                List<Dictionary<string, object>> filteredComplaints = allComplaints;

               

                if (isActive.HasValue)
                {
                    filteredComplaints = filteredComplaints.Where(row =>
                    {
                        if (row.TryGetValue("isActive", out var val) && val != null)
                            return val.ToString() == isActive.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by IsActive={isActive.Value}. Count={filteredComplaints.Count}");
                }

                if (!filteredComplaints.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No chief complaints found for IsActive: {isActive?.ToString() ?? "All"}");
                    return ServiceResult<object>.Failure(alert.Type, "No chief complaints found", 404);
                }

                _log.Info($"Retrieved {filteredComplaints.Count} chief complaint(s) from cache");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    filteredComplaints,
                    alert1.Type,
                    $"{filteredComplaints.Count} chief complaint(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> SaveDoctorFavouriteTableEntry(SaveDoctorFavouriteTableEntryRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveDoctorFavouriteTableEntry called. DoctorId={request.DoctorId}, EntityId={request.EntityId}, RecordId={request.RecordId}, IsFavorite={request.IsFavorite}");

                // Entry arrives as a JSON object/element from the client — convert to its JSON string
                // representation since the DB column (NVARCHAR(MAX)) stores JSON as text.
                string entryJson = request.Entry.GetRawText();

                // IU_DoctorFavouriteTableEntries has no @Result output parameter — plain DML is correct here.
                _sqlHelper.DML(
                    "IU_DoctorFavouriteTableEntries",
                    CommandType.StoredProcedure,
                    new
                    {
                        @DoctorId = request.DoctorId,
                        @EntityId = request.EntityId,
                        @RecordId = request.RecordId,
                        @IsFavorite = request.IsFavorite,
                        @Entry = entryJson,
                        @UserId = globalValues.userId
                    });

                _log.Info($"SaveDoctorFavouriteTableEntry completed. DoctorId={request.DoctorId}, EntityId={request.EntityId}, RecordId={request.RecordId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Doctor favourite entry saved successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetDoctorFavouriteTableEntries(int doctorId, int entityId, int recordId)
        {
            try
            {
                _log.Info($"GetDoctorFavouriteTableEntries called. DoctorId={doctorId}, EntityId={entityId}, RecordId={recordId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetDoctorFavouriteTableEntries",
                    CommandType.StoredProcedure,
                    new
                    {
                        @DoctorId = doctorId,
                        @EntityId = entityId,
                        @RecordId = recordId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No favourite entries found for DoctorId={doctorId}, EntityId={entityId}, RecordId={recordId}");
                    return ServiceResult<object>.Failure(alert.Type, "No favourite entries found", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetDoctorFavouriteTableEntries retrieved {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} favourite entry(ies) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> DeleteDoctorFavouriteTableEntry(int id)
        {
            try
            {
                _log.Info($"DeleteDoctorFavouriteTableEntry called. Id={id}");

                _sqlHelper.DML(
                    "D_DoctorFavouriteTableEntries",
                    CommandType.StoredProcedure,
                    new { @Id = id });

                _log.Info($"DeleteDoctorFavouriteTableEntry completed. Id={id}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Doctor favourite entry deleted successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> DeleteRecordByTableName(int id, string tableName, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeleteRecordByTableName called. Id={id}, TableName={tableName}");

                _sqlHelper.DML(
                    "D_DeleteRecordByTableName",
                    CommandType.StoredProcedure,
                    new
                    {
                        @Id = id,
                        @TableName = tableName
                    });

                // Clear relevant master cache since the underlying table data has changed
                if (tableName.Equals("ChiefComplaintMaster", StringComparison.OrdinalIgnoreCase))
                {
                    _distributedCache.Remove("_ChiefComplaintMaster_All");
                    _log.Info("Cleared ChiefComplaintMaster cache after delete.");
                }
                else if (tableName.Equals("AllergyMaster", StringComparison.OrdinalIgnoreCase))
                {
                    _distributedCache.Remove("_AllergyMaster_All");
                    _log.Info("Cleared AllergyMaster cache after delete.");
                }

                _log.Info($"DeleteRecordByTableName completed. Id={id}, TableName={tableName}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"Record deleted successfully from {tableName}",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }


        public ServiceResult<string> UploadEMRControlDocument(
           UploadEMRControlDocumentRequest request,
           AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UploadEMRControlDocument called. HeaderId={request.HeaderId}, DocumentId={request.DocumentId}");

                // Validate file
                if (request.DocumentFile == null || request.DocumentFile.Length == 0)
                {
                    var alertFile = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<string>.Failure(
                        alertFile.Type,
                        "Document file is required",
                        400
                    );
                }

                // Upload file to DMS
                var fileUploadHelper = new FileUploadHelper(_configuration);
                var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                    request.DocumentFile,
                    "EMRControlDocuments"
                );

                if (!uploadSuccess)
                {
                    _log.Error($"EMR control document upload failed: {uploadError}");
                    var alertUpload = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    return ServiceResult<string>.Failure(
                        alertUpload.Type,
                        $"Document file upload failed: {uploadError}",
                        500
                    );
                }

                _log.Info($"EMR control document uploaded successfully: {filePath}");

                // Save to database (upsert on DocumentId + HeaderId)
                _sqlHelper.DML(
                    "IU_EMRControlDocumentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @documentId = request.DocumentId,
                        @imageName = request.ImageName,
                        @headerId = request.HeaderId,
                        @documentPath = filePath,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                // Clear cache for this header's documents
                _distributedCache.Remove($"_EMRControlDocumentMapping_{request.HeaderId}");
                _log.Info($"Cleared EMRControlDocumentMapping cache for HeaderId={request.HeaderId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    filePath,
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetEMRControlDocumentMapping(int headerId)
        {
            try
            {
                _log.Info($"GetEMRControlDocumentMapping called. HeaderId={headerId}");

                string cacheKey = $"_EMRControlDocumentMapping_{headerId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> documents;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"EMRControlDocumentMapping retrieved from cache. Key={cacheKey}");
                    documents = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"EMRControlDocumentMapping cache miss. Fetching from database. Key={cacheKey}");

                    // Raw DataTable -> List<Dictionary<string,object>> (no model mapping,
                    // so any new columns added to the SP automatically flow through)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_EMRControlDocumentMapping",
                        CommandType.StoredProcedure,
                        new { @headerId = headerId }
                    );

                    documents = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (documents.Any())
                    {
                        var serialized = JsonSerializer.Serialize(documents);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"EMRControlDocumentMapping cached. Key={cacheKey}, Count={documents.Count}");
                    }
                }

                if (documents == null || !documents.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No EMR control documents found for HeaderId={headerId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No documents found for this header",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    documents,
                    alert1.Type,
                    $"{documents.Count} document(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> DeleteEMRControlDocumentMapping(
            int headerId,
            int documentId,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeleteEMRControlDocumentMapping called. HeaderId={headerId}, DocumentId={documentId}");

                _sqlHelper.DML(
                    "D_EMRControlDocumentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @headerId = headerId,
                        @documentId = documentId
                    }
                );

                // Clear cache for this header's documents
                _distributedCache.Remove($"_EMRControlDocumentMapping_{headerId}");
                _log.Info($"Cleared EMRControlDocumentMapping cache for HeaderId={headerId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "EMR control document mapping deleted successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        private const string CACHE_KEY_DoseMaster_All = "_DoseMaster_All";

        public ServiceResult<object> GetDoseMasterList(int? doseId, int? isActive)
        {
            try
            {
                _log.Info($"GetDoseMasterList called. DoseId={doseId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_DoseMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoseMaster data retrieved from cache. Key={CACHE_KEY_DoseMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"DoseMaster cache miss. Fetching from database. Key={CACHE_KEY_DoseMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetDoseMasterList",
                        CommandType.StoredProcedure
                    );

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_DoseMaster_All, serialized, cacheOptions);
                        _log.Info($"DoseMaster data cached permanently. Key={CACHE_KEY_DoseMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by DoseId; null = return all
                if (doseId.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("DoseId", out var val) && val != null)
                            return val.ToString() == doseId.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by DoseId={doseId.Value}. Count={allItems.Count}");
                }

                // In-memory filter by IsActive; null = return all
                if (isActive.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("IsActive", out var val) && val != null)
                            return val.ToString() == isActive.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by IsActive={isActive.Value}. Count={allItems.Count}");
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn("No dose records found.");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No dose records found",
                        404
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} dose record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> CreateUpdateDoseMaster(
            CreateUpdateDoseMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDoseMaster called. DoseId={request.DoseId}, Dose={request.Dose}");

                var parameters = new SqlParameter[]
                {
            new SqlParameter("@doseId",         SqlDbType.Int)          { Value = request.DoseId },
            new SqlParameter("@dose",           SqlDbType.NVarChar, 100){ Value = request.Dose },
            new SqlParameter("@doseTimes",      SqlDbType.NVarChar, 256){ Value = request.DoseTimes      ?? (object)DBNull.Value },
            new SqlParameter("@doseTimeLabels", SqlDbType.NVarChar, 256){ Value = request.DoseTimeLabels ?? (object)DBNull.Value },
            new SqlParameter("@isActive",       SqlDbType.Int)          { Value = request.IsActive },
            new SqlParameter("@userId",         SqlDbType.Int)          { Value = globalValues.userId },
            new SqlParameter("@ipAddress",      SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
            new SqlParameter("@Result",         SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_DoseMaster", parameters);

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate Dose: {request.Dose}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Dose already exists",
                        409
                    );
                }

                if (result > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_DoseMaster_All);
                    _log.Info($"Cleared DoseMaster cache. DoseId={result}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.DoseId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    return ServiceResult<object>.Success(
                        new { DoseId = result },
                        alert.Type,
                        alert.Message,
                        request.DoseId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UploadEMRDocument(
    UploadEMRDocumentRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UploadEMRDocument called. VisitId={request.VisitId}, DocumentId={request.DocumentId}");

                // Validate file
                if (request.DocumentFile == null || request.DocumentFile.Length == 0)
                {
                    var alertFile = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<string>.Failure(
                        alertFile.Type,
                        "Document file is required",
                        400
                    );
                }

                // Upload file to DMS
                var fileUploadHelper = new FileUploadHelper(_configuration);
                var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                    request.DocumentFile,
                    "EMRDocuments"
                );

                if (!uploadSuccess)
                {
                    _log.Error($"EMR document upload failed: {uploadError}");
                    var alertUpload = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    return ServiceResult<string>.Failure(
                        alertUpload.Type,
                        $"Document file upload failed: {uploadError}",
                        500
                    );
                }

                _log.Info($"EMR document uploaded successfully: {filePath}");

                // Save to database (upsert on DocumentId + VisitId)
                _sqlHelper.DML(
                    "IU_EMRDocumentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @documentId = request.DocumentId,
                        @VisitId = request.VisitId,
                        @documentPath = filePath,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                // Clear cache for this visit's documents
                _distributedCache.Remove($"_EMRDocumentMapping_{request.VisitId}");
                _log.Info($"Cleared EMRDocumentMapping cache for VisitId={request.VisitId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    filePath,
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetEMRDocumentMapping(int visitId)
        {
            try
            {
                _log.Info($"GetEMRDocumentMapping called. VisitId={visitId}");

                string cacheKey = $"_EMRDocumentMapping_{visitId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> documents;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"EMRDocumentMapping retrieved from cache. Key={cacheKey}");
                    documents = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"EMRDocumentMapping cache miss. Fetching from database. Key={cacheKey}");

                    // Raw DataTable -> List<Dictionary<string,object>> (no model mapping,
                    // so any new columns added to the SP automatically flow through)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_EMRDocumentMapping",
                        CommandType.StoredProcedure,
                        new { @VisitId = visitId }
                    );

                    documents = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (documents.Any())
                    {
                        var serialized = JsonSerializer.Serialize(documents);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"EMRDocumentMapping cached. Key={cacheKey}, Count={documents.Count}");
                    }
                }

                if (documents == null || !documents.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No EMR documents found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No documents found for this visit",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    documents,
                    alert1.Type,
                    $"{documents.Count} document(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetEMRSectionHeaderMappingByDoctorId(int doctorId, int usedForPatientTypeId)
        {
            try
            {
                _log.Info($"GetEMRSectionHeaderMappingByDoctorId called. DoctorId={doctorId}, UsedForPatientTypeId={usedForPatientTypeId}");

                // Raw DataTable -> List<Dictionary<string,object>> (no model mapping,
                // so any new columns added to the SP automatically flow through)
                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetEMRSectionHeaderMappingByDoctorId",
                    CommandType.StoredProcedure,
                    new
                    {
                        @doctorId = doctorId,
                        @usedForPatientTypeId = usedForPatientTypeId
                    }
                );

                var mappings = dataTable?.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList() ?? new List<Dictionary<string, object>>();

                if (!mappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No EMR section header mappings found for DoctorId={doctorId}, UsedForPatientTypeId={usedForPatientTypeId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No section/header mappings found for this doctor",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    mappings,
                    alert1.Type,
                    $"{mappings.Count} mapping(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<SavePatientConsultationResponse> SavePatientConsultation(
            SavePatientConsultationRequest request,
            AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                var c = request.ConsultationDetails;

                _log.Info($"SavePatientConsultation called. DoctorId={c.DoctorId}, PatientId={c.PatientId}, VisitId={c.VisitId}, VisitTypeId={c.VisitTypeId}");

                // ── 0. Patient Vital ────────────────────────────────────────
                var patientVitalIdResult = _sqlHelper.DML(
                    tnx,
                    "IU_SavePatientVitalDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @patientVitalId = c.PatientVitalId,
                        @visitId = c.VisitId,
                        @patientId = c.PatientId,
                        @vitalDateTime = c.VitalDateTime,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    },
                    new { result = 0 }   // <-- REQUIRED: forces DML to use ExecuteScalar() and read SELECT @Result
                );

                int patientVitalId = Convert.ToInt32(patientVitalIdResult);
                _log.Info($"PatientVitalDetails saved. PatientVitalId={patientVitalId}");

                if (request.PatientVitalValue != null && request.PatientVitalValue.Any())
                {
                    foreach (var v in request.PatientVitalValue)
                    {
                        _sqlHelper.DML(
                            tnx,
                            "I_SavePatientVitalValue",
                            CommandType.StoredProcedure,
                            new
                            {
                                @patientVitalId = patientVitalId,   // now the real Id, not 0
                                @vitalId = v.VitalId,
                                @vitalValue = (object)v.VitalValue ?? DBNull.Value,
                                @userId = globalValues.userId,
                                @ipAddress = globalValues.ipAddress
                            });
                    }
                }

                // ── 1. IU_DoctorConsultations ────────────────────────────────────────
                _sqlHelper.DML(
                    tnx,
                    "IU_DoctorConsultations",
                    CommandType.StoredProcedure,
                    new
                    {
                        @doctorId = c.DoctorId,
                        @patientId = c.PatientId,
                        @visitId = c.VisitId,
                        @visitTypeId = c.VisitTypeId,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    });

                // ── 2. IU_PatientDoctorHeadersData (loop) ────────────────────────────
                if (request.ConsultationHeadersData != null && request.ConsultationHeadersData.Any())
                {
                    foreach (var h in request.ConsultationHeadersData)
                    {
                        _sqlHelper.DML(
                            tnx,
                            "IU_PatientDoctorHeadersData",
                            CommandType.StoredProcedure,
                            new
                            {
                                @id = h.DataId,
                                @patientId = c.PatientId,
                                @visitId = c.VisitId,
                                @sectionId = h.SectionId,
                                @headerId = h.HeaderId,
                                @controlTypeId = h.ControlTypeId,
                                @templateId = h.TemplateId,
                                @headerValue = (object)h.HeaderValue ?? DBNull.Value,
                                @userId = globalValues.userId,
                                @ipAddress = globalValues.ipAddress
                            });
                    }
                }

                // ── 3. U_PatientOutFileClose ──────────────────────────────────────────
                if (c.IsTemperatureRoomOut == 0)
                {
                    _sqlHelper.DML(
                    tnx,
                    "U_PatientOutFileClose",
                    CommandType.StoredProcedure,
                    new
                    {
                        @visitId = c.VisitId,
                        @isFileClosed = c.IsFileClosed,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    });
                }
                // ── 4. U_OutPatientTempRoom ──────────────────────────────────────────
                if (c.IsTemperatureRoomOut > 0)
                {
                    _sqlHelper.DML(
                  tnx,
                  "U_OutPatientTempRoom",
                  CommandType.StoredProcedure,
                  new
                  {
                      @visitId = c.VisitId,
                      @userId = globalValues.userId,
                      @ipAddress = globalValues.ipAddress
                  });
                }

                tnx.Commit();
                _log.Info($"SavePatientConsultation committed. VisitId={c.VisitId}, PatientId={c.PatientId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SavePatientConsultationResponse>.Success(
                    new SavePatientConsultationResponse
                    {
                        VisitId = c.VisitId,
                        PatientId = c.PatientId,
                        DoctorId = c.DoctorId
                    },
                    alert.Type,
                    "Consultation saved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SavePatientConsultationResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        public ServiceResult<object> GetDoctorConsultationByVisitId(int visitId)
        {
            try
            {
                _log.Info($"GetDoctorConsultationByVisitId called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetDoctorConsultationByVisitId",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No consultation found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No consultation found for the given visit",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetDoctorConsultationByVisitId retrieved {result.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    "Consultation details retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }
        public ServiceResult<object> GetPatientVisitDetailsByPatientId(int patientId)
        {
            try
            {
                _log.Info($"GetPatientVisitDetailsByPatientId called. PatientId={patientId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientVisitDetailsByPatientId",
                    CommandType.StoredProcedure,
                    new
                    {
                        @patientId = patientId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No visit details found for PatientId={patientId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No visit details found for the given patient",
                        404
                    );
                }

                // Raw DataTable -> List<Dictionary<string,object>> projection (no model mapping)
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetPatientVisitDetailsByPatientId retrieved {result.Count} record(s) for PatientId={patientId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} visit(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetVitalDepartmentMappingByDoctorId(int doctorId)
        {
            try
            {
                _log.Info($"GetVitalDepartmentMappingByDoctorId called. DoctorId={doctorId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetVitalDepartmentMappingByDoctorId",
                    CommandType.StoredProcedure,
                    new { @doctorId = doctorId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No vital mapping found for DoctorId={doctorId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No vital mapping found for the given doctor",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetVitalDepartmentMappingByDoctorId retrieved {result.Count} record(s) for DoctorId={doctorId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} vital(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }
        public ServiceResult<object> GetPatientVital(int patientId, int visitId = 0)
        {
            try
            {
                _log.Info($"GetPatientVital called. PatientId={patientId}, VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientVital",
                    CommandType.StoredProcedure,
                    new
                    {
                        @patientId = patientId,
                        @visitId = visitId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No vitals found for PatientId={patientId}, VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        $"No vitals found for PatientId: {patientId}",
                        404
                    );
                }

                // Raw DataTable -> Dictionary projection (no model mapping),
                // so any new columns added to the SP surface automatically.
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetPatientVital retrieved {result.Count} record(s) for PatientId={patientId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        private const string CACHE_KEY_TemplateCategoryMaster_All = "_TemplateCategoryMaster_All";

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetTemplateCategoryMasterList()
        {
            try
            {
                _log.Info("GetTemplateCategoryMasterList called.");

                var cachedData = _distributedCache.GetString(CACHE_KEY_TemplateCategoryMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    var dataTable = _sqlHelper.GetDataTable("S_GetTemplateCategoryMasterList", CommandType.StoredProcedure);

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>()
                            .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        _distributedCache.SetString(CACHE_KEY_TemplateCategoryMaster_All, serialized,
                            new DistributedCacheEntryOptions { AbsoluteExpiration = null, SlidingExpiration = null });
                    }
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        notFoundAlert.Type, "No template category records found", 404);
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    allItems, alert.Type, $"{allItems.Count} template category record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(alert.Type, alert.Message, 500);
            }
        }







        public ServiceResult<object> CreateUpdateTemplateCategoryMaster(
            CreateUpdateTemplateCategoryMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateTemplateCategoryMaster called. TemplateCategoryId={request.TemplateCategoryId}, TemplateCategoryName={request.TemplateCategoryName}");

                var parameters = new SqlParameter[]
                {
            new SqlParameter("@TemplateCategoryId",   SqlDbType.Int)          { Value = request.TemplateCategoryId },
            new SqlParameter("@TemplateCategoryName", SqlDbType.NVarChar, 256){ Value = request.TemplateCategoryName },
            new SqlParameter("@userId",        SqlDbType.Int)          { Value = globalValues.userId },
            new SqlParameter("@IpAddress",     SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
            new SqlParameter("@Result",        SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_TemplateCategoryMaster", parameters);

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate TemplateCategoryName: {request.TemplateCategoryName}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Procedure name already exists",
                        409
                    );
                }

                if (result > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_TemplateCategoryMaster_All);
                    _log.Info($"Cleared TemplateCategoryMaster cache. TemplateCategoryId={result}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.TemplateCategoryId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    return ServiceResult<object>.Success(
                        new { TemplateCategoryId = result },
                        alert.Type,
                        alert.Message,
                        request.TemplateCategoryId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        private const string CACHE_KEY_EMRTemplateMaster_All = "_EMRTemplateMaster_All";

        public ServiceResult<object> CreateUpdateEMRTemplateMaster(
            CreateUpdateEMRTemplateMasterRequest request,
            AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"CreateUpdateEMRTemplateMaster called. TemplateId={request.TemplateId}, TemplateName={request.TemplateName}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Insert or Update EMRTemplateMaster
                var parameters = new SqlParameter[]
                {
            new SqlParameter("@TemplateId",   SqlDbType.Int)          { Value = request.TemplateId },
            new SqlParameter("@TemplateName", SqlDbType.NVarChar, 256){ Value = request.TemplateName },
            new SqlParameter("@displayName", SqlDbType.NVarChar, 256){ Value = request.DisplayName ?? (object)DBNull.Value },
            new SqlParameter("@templateCategoryId",    SqlDbType.Int)          { Value = request.TemplateCategoryId },
            new SqlParameter("@isActive",    SqlDbType.Int)          { Value = request.IsActive },
            new SqlParameter("@isMultipleEntryAllow",    SqlDbType.Int)          { Value = request.IsMultipleEntryAllow },
            new SqlParameter("@applicableTo",    SqlDbType.Int)          { Value = request.ApplicableTo },
            new SqlParameter("@userId",      SqlDbType.Int)          { Value = globalValues.userId },
            new SqlParameter("@IpAddress",   SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
            new SqlParameter("@Result",      SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long sectionResult = _sqlHelper.RunProcedureInsert("IU_EMRTemplateMaster", parameters);

                if (sectionResult == -1)
                {
                    tnx.Rollback();
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate TemplateName: {request.TemplateName}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Template name already exists",
                        409
                    );
                }

                if (sectionResult <= 0)
                {
                    tnx.Rollback();
                    var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error($"IU_EMRTemplateMaster returned unexpected result: {sectionResult}");
                    return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
                }

                int resolvedTemplateId = (int)sectionResult;
                _log.Info($"EMRTemplateMaster {(request.TemplateId == 0 ? "inserted" : "updated")}. TemplateId={resolvedTemplateId}");

                // Step 2 – Delete existing Section mappings for this section
                _sqlHelper.DML(tnx, "D_EMRTemplateSectionMapping", CommandType.StoredProcedure, new
                {
                    @TemplateId = resolvedTemplateId
                });
                _log.Info($"Deleted existing EMRTemplateSectionMapping for TemplateId={resolvedTemplateId}");

                // Step 3 – Insert new Section mappings
                int insertedCount = 0;
                if (request.SectionMappings != null && request.SectionMappings.Any())
                {
                    foreach (var item in request.SectionMappings)
                    {
                        _sqlHelper.DML(tnx, "I_EMRTemplateSectionMapping", CommandType.StoredProcedure, new
                        {
                            @TemplateId = resolvedTemplateId,
                            @SectionId = item.SectionId,
                            @sequenceNo = item.SequenceNo,
                            @userId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        });
                        insertedCount++;
                    }
                }

                tnx.Commit();
                _log.Info($"CreateUpdateEMRTemplateMaster committed. TemplateId={resolvedTemplateId}, MappingsInserted={insertedCount}");

                // Invalidate cache after successful write
                _distributedCache.Remove(CACHE_KEY_EMRTemplateMaster_All);
                _log.Info($"Cleared EMRTemplateMaster cache. Key={CACHE_KEY_EMRTemplateMaster_All}");

                var alert = _messageService.GetMessageAndTypeByAlertCode(
                    request.TemplateId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                );
                return ServiceResult<object>.Success(
                    new { TemplateId = resolvedTemplateId, MappingsInserted = insertedCount },
                    alert.Type,
                    alert.Message,
                    request.TemplateId == 0 ? 201 : 200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch { /* swallow */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == System.Data.ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<object> GetEMRTemplateMaster(int? isActive)
        {
            try
            {
                _log.Info($"GetEMRTemplateMaster called. IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_EMRTemplateMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"EMRTemplateMaster data retrieved from cache. Key={CACHE_KEY_EMRTemplateMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"EMRTemplateMaster cache miss. Fetching from database. Key={CACHE_KEY_EMRTemplateMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetEMRTemplateMaster",
                        CommandType.StoredProcedure
                    );

                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_EMRTemplateMaster_All, serialized, cacheOptions);
                        _log.Info($"EMRTemplateMaster cached permanently. Key={CACHE_KEY_EMRTemplateMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by IsActive; null = return all
                if (isActive.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("IsActive", out var val) && val != null)
                            return val.ToString() == isActive.Value.ToString();
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by IsActive={isActive.Value}. Count={allItems.Count}");
                }

                if (!allItems.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn("No EMR section records found.");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No EMR section records found", 404);
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} EMR section record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetEMRTemplateSectionMapping(int TemplateId)
        {
            try
            {
                _log.Info($"GetEMRTemplateSectionMapping called. TemplateId={TemplateId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetEMRTemplateSectionMapping",
                    CommandType.StoredProcedure,
                    new { @TemplateId = TemplateId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No Section mappings found for TemplateId={TemplateId}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No Section mappings found for this section",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetEMRTemplateSectionMapping retrieved {result.Count} record(s) for TemplateId={TemplateId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert.Type,
                    $"{result.Count} Section mapping(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetEMRTemplateDepartmentMapping(int typeId, int relatedToId)
        {
            try
            {
                _log.Info($"GetEMRTemplateDepartmentMapping called. TypeId={typeId}, RelatedToId={relatedToId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetEMRTemplateDepartmentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @typeId = typeId,
                        @relatedToId = relatedToId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No EMRTemplate department mapping found for TypeId={typeId}, RelatedToId={relatedToId}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No EMRTemplate department mapping found",
                        404
                    );
                }

                var rawData = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>()
                        .ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                ).ToList();

                _log.Info($"GetEMRTemplateDepartmentMapping retrieved {rawData.Count} record(s)");

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> SaveEMRTemplateDepartmentMapping(
            SaveEMRTemplateDepartmentMappingRequest request,
            AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"SaveEMRTemplateDepartmentMapping called. TypeId={request.TypeId}, RelatedToId={request.RelatedToId}, Items={request.SectionMappingData?.Count ?? 0}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Delete existing mappings
                _sqlHelper.DML(tnx, "D_DeleteEMRTemplateDepartmentMapping", CommandType.StoredProcedure, new
                {
                    @typeId = request.TypeId,
                    @relatedToId = request.RelatedToId
                });
                _log.Info($"Deleted existing mappings for TypeId={request.TypeId}, RelatedToId={request.RelatedToId}");

                // Step 2 – Insert new mappings
                int insertedCount = 0;
                if (request.SectionMappingData != null && request.SectionMappingData.Any())
                {
                    foreach (var item in request.SectionMappingData)
                    {
                        _sqlHelper.DML(tnx, "I_EMRTemplateDepartmentMapping", CommandType.StoredProcedure, new
                        {
                            @hospId = globalValues.hospId,
                            @typeId = request.TypeId,
                            @typeName = request.TypeName ?? string.Empty,
                            @TemplateId = item.TemplateId,
                            @retatedToId = request.RelatedToId,
                            @sequenceNo = item.SequenceNo,
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        });
                        insertedCount++;
                    }
                }

                tnx.Commit();
                _log.Info($"SaveEMRTemplateDepartmentMapping committed. Inserted={insertedCount}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{insertedCount} mapping(s) saved successfully",
                    alert.Type,
                    "Mapping Updated Successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch { /* swallow */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == System.Data.ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<object> GetEMRTemplateSectionMappingByDoctorId(int doctorId, int usedForPatientTypeId,int applicableToId)
        {
            try
            {
                _log.Info($"GetEMRTemplateSectionMappingByDoctorId called. DoctorId={doctorId}, UsedForPatientTypeId={usedForPatientTypeId}");

                // Raw DataTable -> List<Dictionary<string,object>> (no model mapping,
                // so any new columns added to the SP automatically flow through).
                // No caching here — result depends on doctorId + patientTypeId (dynamic, not master data).
                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetEMRTemplateSectionMappingByDoctorId",
                    CommandType.StoredProcedure,
                    new
                    {
                        @doctorId = doctorId,
                        @usedForPatientTypeId = usedForPatientTypeId,
                        @applicableToId = applicableToId
                    }
                );

                var mappings = dataTable?.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList() ?? new List<Dictionary<string, object>>();

                if (!mappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No EMR template section mappings found for DoctorId={doctorId}, UsedForPatientTypeId={usedForPatientTypeId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No template/section mappings found for this doctor",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    mappings,
                    alert1.Type,
                    $"{mappings.Count} mapping(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CreateUpdateCarePlanResponse> CreateUpdateCarePlan(
    CreateUpdateCarePlanRequest request,
    AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"CreateUpdateCarePlan called. CarePlanId={request.CarePlanId}, DoctorId={request.DoctorId}, CarePlanName={request.CarePlanName}");

                // IU_CarePlanMaster uses a true OUTPUT parameter (no trailing SELECT @Result;),
                // so RunProcedureInsert is required here (reads the OUTPUT param value directly).
                long resultValue = _sqlHelper.RunProcedureInsert(
                    "IU_CarePlanMaster",
                    new IDataParameter[]
                    {
                new SqlParameter("@carePlanId", request.CarePlanId),
                new SqlParameter("@doctorId", request.DoctorId),
                new SqlParameter("@carePlanName", request.CarePlanName),
                new SqlParameter("@userId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int result = Convert.ToInt32(resultValue);

                if (result == -1)
                {
                    tnx.Rollback();
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate CarePlanName: {request.CarePlanName} for DoctorId={request.DoctorId}");
                    return ServiceResult<CreateUpdateCarePlanResponse>.Failure(
                        dupAlert.Type,
                        "Care plan name already exists for this doctor",
                        409
                    );
                }

                int carePlanId = result;
                _log.Info($"CarePlanMaster {(request.CarePlanId == 0 ? "inserted" : "updated")}. CarePlanId={carePlanId}");

                // Delete existing mappings, then reinsert (covers both insert & update)
                _sqlHelper.DML(tnx, "D_CarePlanMapping", CommandType.StoredProcedure, new
                {
                    @carePlanId = carePlanId
                });
                _log.Info($"Deleted existing CarePlanMapping for CarePlanId={carePlanId}");

                int insertedCount = 0;
                if (request.HeadersData != null && request.HeadersData.Any())
                {
                    foreach (var item in request.HeadersData)
                    {
                        _sqlHelper.DML(tnx, "I_CarePlanMapping", CommandType.StoredProcedure, new
                        {
                            @carePlanId = carePlanId,
                            @sectionId = item.SectionId,
                            @headerId = item.HeaderId,
                            @controlTypeId = item.ControlTypeId,
                            @headerValue = (object)item.HeaderValue ?? DBNull.Value,
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        });
                        insertedCount++;
                    }
                }

                tnx.Commit();
                _log.Info($"CreateUpdateCarePlan committed. CarePlanId={carePlanId}, MappingsInserted={insertedCount}");

                var alert = _messageService.GetMessageAndTypeByAlertCode(
                    request.CarePlanId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                );

                return ServiceResult<CreateUpdateCarePlanResponse>.Success(
                    new CreateUpdateCarePlanResponse { CarePlanId = carePlanId },
                    alert.Type,
                    alert.Message,
                    request.CarePlanId == 0 ? 201 : 200
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateCarePlanResponse>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        public ServiceResult<object> GetCarePlanMaster(int doctorId)
        {
            try
            {
                _log.Info($"GetCarePlanMaster called. DoctorId={doctorId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetCarePlanMaster",
                    CommandType.StoredProcedure,
                    new { @DoctorId = doctorId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No care plans found for DoctorId={doctorId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No care plans found for this doctor",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetCarePlanMaster retrieved {result.Count} record(s) for DoctorId={doctorId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} care plan(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCarePlanDetails(int carePlanId)
        {
            try
            {
                _log.Info($"GetCarePlanDetails called. CarePlanId={carePlanId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetCarePlanDetails",
                    CommandType.StoredProcedure,
                    new { @carePlanId = carePlanId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No care plan details found for CarePlanId={carePlanId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No care plan details found",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetCarePlanDetails retrieved {result.Count} record(s) for CarePlanId={carePlanId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }
    }
}