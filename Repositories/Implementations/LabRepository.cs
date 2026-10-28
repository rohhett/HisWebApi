using HISWEBAPI.Configuration;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Domain;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Services;
using HISWEBAPI.Utilities;
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Utility = HISWEBAPI.Utilities.Utility;

namespace HISWEBAPI.Repositories.Implementations
{
    public class LabRepository : Interfaces.ILabRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        // ─── Cache Keys ───────────────────────────────────────────────────────
        private const string CACHE_ORGANISM_GROUP = "_Lab_OrganismGroup_All";
        private const string CACHE_ORGANISM_NAME = "_Lab_OrganismName_All";
        private const string CACHE_ANTIBIOTIC_GROUP = "_Lab_AntibioticGroup_All";
        private const string CACHE_ANTIBIOTIC_NAME = "_Lab_AntibioticName_All";

        public LabRepository(
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

        public ServiceResult<CreateUpdateSampleTypeMasterResponse> CreateUpdateSampleTypeMaster(
            CreateUpdateSampleTypeMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSampleTypeMaster called. SampleTypeId={request.SampleTypeId}, SampleType={request.SampleType}");

                var result = _sqlHelper.DML(
                    "IU_SampleTypeMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        sampleTypeId = request.SampleTypeId,
                        containerColorId = request.ContainerColorId,
                        sampleType = request.SampleType,
                        isActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        Result = 0
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_SampleTypeMaster_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared SampleTypeMaster cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate sample type name: {request.SampleType}");
                    return ServiceResult<CreateUpdateSampleTypeMasterResponse>.Failure(
                        alert.Type,
                        "Sample Type name already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateSampleTypeMasterResponse { SampleTypeId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.SampleTypeId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Sample Type {(request.SampleTypeId == 0 ? "created" : "updated")} successfully. SampleTypeId={resultValue}");

                    return ServiceResult<CreateUpdateSampleTypeMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.SampleTypeId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Sample Type operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateSampleTypeMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSampleTypeMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<SampleTypeMasterModel>> GetAllSampleTypeMaster(int? isActive = null)
        {
            try
            {
                _log.Info($"GetAllSampleTypeMaster called. IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL sample types
                string cacheKey = "_SampleTypeMaster_All";

                // Try to get all sample types from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<SampleTypeMasterModel> allSampleTypes;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SampleTypeMaster data retrieved from cache. Key={cacheKey}");
                    allSampleTypes = JsonSerializer.Deserialize<List<SampleTypeMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"SampleTypeMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL sample types from database (no parameters)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetAllSampleTypeMaster",
                        CommandType.StoredProcedure
                    );

                    allSampleTypes = dataTable?.AsEnumerable().Select(row => new SampleTypeMasterModel
                    {
                        SampleTypeId = row.Field<int>("SampleTypeId"),
                        SampleType = row.Field<string>("SampleType") ?? string.Empty,
                        ContainerColorId = row.Field<int>("ContainerColorId"),
                        ColorName = row.Field<string>("ColorName") ?? string.Empty,
                        ColorCode = row.Field<string>("ColorCode") ?? string.Empty,
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<SampleTypeMasterModel>();

                    // Store ALL sample types in cache (permanent until manually cleared)
                    if (allSampleTypes.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allSampleTypes);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All SampleTypeMaster data cached permanently. Key={cacheKey}, Count={allSampleTypes.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<SampleTypeMasterModel> filteredSampleTypes = allSampleTypes;

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredSampleTypes = filteredSampleTypes.Where(s => s.IsActive == isActive.Value).ToList();
                }

                if (!filteredSampleTypes.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No sample types found for IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<SampleTypeMasterModel>>.Failure(
                        alert.Type,
                        "No sample types found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredSampleTypes.Count} sample type(s) from cache");

                return ServiceResult<IEnumerable<SampleTypeMasterModel>>.Success(
                    filteredSampleTypes,
                    "Info",
                    $"{filteredSampleTypes.Count} sample type(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SampleTypeMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<SampleContainerColorMasterModel>> GetSampleContainerColorMaster()
        {
            try
            {
                _log.Info("GetSampleContainerColorMaster called.");

                // Cache key for container colors
                string cacheKey = "_SampleContainerColor_All";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<SampleContainerColorMasterModel> containerColors;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SampleContainerColor data retrieved from cache. Key={cacheKey}");
                    containerColors = JsonSerializer.Deserialize<List<SampleContainerColorMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"SampleContainerColor cache miss. Fetching data from database. Key={cacheKey}");

                    // Fetch data from database
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetSampleContainerColorMaster",
                        CommandType.StoredProcedure
                    );

                    containerColors = dataTable?.AsEnumerable().Select(row => new SampleContainerColorMasterModel
                    {
                        ColorId = row.Field<int>("ColorId"),
                        ColorName = row.Field<string>("ColorName") ?? string.Empty,
                        ColorCode = row.Field<string>("ColorCode") ?? string.Empty
                    }).ToList() ?? new List<SampleContainerColorMasterModel>();

                    // Store in cache (permanent until manually cleared)
                    if (containerColors.Any())
                    {
                        var serialized = JsonSerializer.Serialize(containerColors);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"SampleContainerColor data cached permanently. Key={cacheKey}, Count={containerColors.Count}");
                    }
                }

                if (!containerColors.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No container colors found");
                    return ServiceResult<IEnumerable<SampleContainerColorMasterModel>>.Failure(
                        alert.Type,
                        "No container colors found",
                        404
                    );
                }

                _log.Info($"Retrieved {containerColors.Count} container color(s) from cache");

                return ServiceResult<IEnumerable<SampleContainerColorMasterModel>>.Success(
                    containerColors,
                    "Info",
                    $"{containerColors.Count} container color(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SampleContainerColorMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CreateUpdateLabMethodMasterResponse> CreateUpdateLabMethodMaster(
         CreateUpdateLabMethodMasterRequest request,
         AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateLabMethodMaster called. MethodId={request.MethodId}, Method={request.Method}");

                var result = _sqlHelper.DML(
                    "IU_LabMethodMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        methodId = request.MethodId,
                        method = request.Method,
                        IsActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        Result = 0
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_LabMethodMaster_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared LabMethodMaster cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate method name: {request.Method}");
                    return ServiceResult<CreateUpdateLabMethodMasterResponse>.Failure(
                        alert.Type,
                        "Method Name Already Exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateLabMethodMasterResponse { MethodId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.MethodId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Lab Method {(request.MethodId == 0 ? "created" : "updated")} successfully. MethodId={resultValue}");

                    return ServiceResult<CreateUpdateLabMethodMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        request.MethodId == 0 ? "Method Saved Successfully" : "Method Updated Successfully",
                        request.MethodId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Lab Method operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateLabMethodMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateLabMethodMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<LabMethodMasterModel>> GetLabMethodMaster(int? isActive = null)
        {
            try
            {
                _log.Info($"GetLabMethodMaster called. IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL lab methods
                string cacheKey = "_LabMethodMaster_All";

                // Try to get all lab methods from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<LabMethodMasterModel> allLabMethods;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"LabMethodMaster data retrieved from cache. Key={cacheKey}");
                    allLabMethods = JsonSerializer.Deserialize<List<LabMethodMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"LabMethodMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL lab methods from database (no parameters)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetLabMethodMaster",
                        CommandType.StoredProcedure
                    );

                    allLabMethods = dataTable?.AsEnumerable().Select(row => new LabMethodMasterModel
                    {
                        MethodId = row.Field<int>("MethodId"),
                        Method = row.Field<string>("Method") ?? string.Empty,
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<LabMethodMasterModel>();

                    // Store ALL lab methods in cache (permanent until manually cleared)
                    if (allLabMethods.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allLabMethods);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All LabMethodMaster data cached permanently. Key={cacheKey}, Count={allLabMethods.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<LabMethodMasterModel> filteredLabMethods = allLabMethods;

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredLabMethods = filteredLabMethods.Where(m => m.IsActive == isActive.Value).ToList();
                }

                if (!filteredLabMethods.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No lab methods found for IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<LabMethodMasterModel>>.Failure(
                        alert.Type,
                        "No lab methods found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredLabMethods.Count} lab method(s) from cache");

                return ServiceResult<IEnumerable<LabMethodMasterModel>>.Success(
                    filteredLabMethods,
                    "Info",
                    $"{filteredLabMethods.Count} lab method(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<LabMethodMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }



        public ServiceResult<CreateUpdateSampleRemarksMasterResponse> CreateUpdateSampleRemarksMaster(
            CreateUpdateSampleRemarksMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSampleRemarksMaster called. SampleRemarksID={request.SampleRemarksID}, SampleRemarks={request.SampleRemarks}");

                var result = _sqlHelper.DML(
                    "IU_SampleRemarksMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        SampleRemarksID = request.SampleRemarksID,
                        SampleRemarks = request.SampleRemarks,
                        IsActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        Result = 0
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_SampleRemarksMaster_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared SampleRemarksMaster cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate sample remarks: {request.SampleRemarks}");
                    return ServiceResult<CreateUpdateSampleRemarksMasterResponse>.Failure(
                        alert.Type,
                        "Sample Remarks Already Exists.",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateSampleRemarksMasterResponse { SampleRemarksID = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.SampleRemarksID == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Sample Remarks {(request.SampleRemarksID == 0 ? "created" : "updated")} successfully. SampleRemarksID={resultValue}");

                    return ServiceResult<CreateUpdateSampleRemarksMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        request.SampleRemarksID == 0 ? "Sample Remarks Saved Successfully" : "Sample Remarks Updated Successfully",
                        request.SampleRemarksID == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Sample Remarks operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateSampleRemarksMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSampleRemarksMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<SampleRemarksMasterModel>> GetSampleRemarksMaster(int? isActive = null)
        {
            try
            {
                _log.Info($"GetSampleRemarksMaster called. IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL sample remarks
                string cacheKey = "_SampleRemarksMaster_All";

                // Try to get all sample remarks from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<SampleRemarksMasterModel> allSampleRemarks;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SampleRemarksMaster data retrieved from cache. Key={cacheKey}");
                    allSampleRemarks = JsonSerializer.Deserialize<List<SampleRemarksMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"SampleRemarksMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getSampleRemarksMaster",
                        CommandType.StoredProcedure
                    );

                    allSampleRemarks = dataTable?.AsEnumerable().Select(row => new SampleRemarksMasterModel
                    {
                        SampleRemarksID = row.Field<int>("SampleRemarksID"),
                        SampleRemarks = row.Field<string>("SampleRemarks") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<SampleRemarksMasterModel>();

                    // Store ALL data in cache (permanent until manually cleared)
                    if (allSampleRemarks.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allSampleRemarks);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All SampleRemarksMaster data cached permanently. Key={cacheKey}, Count={allSampleRemarks.Count}");
                    }
                }

                // Filter in memory based on isActive (always from cache)
                List<SampleRemarksMasterModel> filteredSampleRemarks = allSampleRemarks;

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredSampleRemarks = filteredSampleRemarks.Where(s => s.IsActive == isActive.Value).ToList();
                }

                if (!filteredSampleRemarks.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No sample remarks found for IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<SampleRemarksMasterModel>>.Failure(
                        alert.Type,
                        "No sample remarks found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredSampleRemarks.Count} sample remark(s) from cache");

                return ServiceResult<IEnumerable<SampleRemarksMasterModel>>.Success(
                    filteredSampleRemarks,
                    "Info",
                    $"{filteredSampleRemarks.Count} sample remark(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SampleRemarksMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CreateUpdateSampleRejectionRemarksMasterResponse> CreateUpdateSampleRejectionRemarksMaster(
            CreateUpdateSampleRejectionRemarksMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSampleRejectionRemarksMaster called. SampleRejectionRemarksID={request.SampleRejectionRemarksID}, SampleRejectionRemarks={request.SampleRejectionRemarks}");

                var result = _sqlHelper.DML(
                    "IU_SampleRejectionRemarksMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        SampleRejectionRemarksID = request.SampleRejectionRemarksID,
                        SampleRejectionRemarks = request.SampleRejectionRemarks,
                        IsActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        Result = 0
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_SampleRejectionRemarksMaster_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared SampleRejectionRemarksMaster cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate sample rejection remarks: {request.SampleRejectionRemarks}");
                    return ServiceResult<CreateUpdateSampleRejectionRemarksMasterResponse>.Failure(
                        alert.Type,
                        "Sample Rejection Remarks Already Exists.",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateSampleRejectionRemarksMasterResponse { SampleRejectionRemarksID = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.SampleRejectionRemarksID == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Sample Rejection Remarks {(request.SampleRejectionRemarksID == 0 ? "created" : "updated")} successfully. SampleRejectionRemarksID={resultValue}");

                    return ServiceResult<CreateUpdateSampleRejectionRemarksMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        request.SampleRejectionRemarksID == 0 ? "Sample Rejection Remarks Saved Successfully" : "Sample Rejection Remarks Updated Successfully",
                        request.SampleRejectionRemarksID == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Sample Rejection Remarks operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateSampleRejectionRemarksMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSampleRejectionRemarksMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<SampleRejectionRemarksMasterModel>> GetSampleRejectionRemarksMaster(int? isActive = null)
        {
            try
            {
                _log.Info($"GetSampleRejectionRemarksMaster called. IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL sample rejection remarks
                string cacheKey = "_SampleRejectionRemarksMaster_All";

                // Try to get all sample rejection remarks from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<SampleRejectionRemarksMasterModel> allRejectionRemarks;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SampleRejectionRemarksMaster data retrieved from cache. Key={cacheKey}");
                    allRejectionRemarks = JsonSerializer.Deserialize<List<SampleRejectionRemarksMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"SampleRejectionRemarksMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getSampleRejectionRemarksMaster",
                        CommandType.StoredProcedure
                    );

                    allRejectionRemarks = dataTable?.AsEnumerable().Select(row => new SampleRejectionRemarksMasterModel
                    {
                        SampleRejectionRemarksID = row.Field<int>("SampleRejectionRemarksID"),
                        SampleRejectionRemarks = row.Field<string>("SampleRejectionRemarks") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<SampleRejectionRemarksMasterModel>();

                    // Store ALL data in cache (permanent until manually cleared)
                    if (allRejectionRemarks.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allRejectionRemarks);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All SampleRejectionRemarksMaster data cached permanently. Key={cacheKey}, Count={allRejectionRemarks.Count}");
                    }
                }

                // Filter in memory based on isActive (always from cache)
                List<SampleRejectionRemarksMasterModel> filteredRejectionRemarks = allRejectionRemarks;

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredRejectionRemarks = filteredRejectionRemarks.Where(s => s.IsActive == isActive.Value).ToList();
                }

                if (!filteredRejectionRemarks.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No sample rejection remarks found for IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<SampleRejectionRemarksMasterModel>>.Failure(
                        alert.Type,
                        "No sample rejection remarks found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredRejectionRemarks.Count} sample rejection remark(s) from cache");

                return ServiceResult<IEnumerable<SampleRejectionRemarksMasterModel>>.Success(
                    filteredRejectionRemarks,
                    "Info",
                    $"{filteredRejectionRemarks.Count} sample rejection remark(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SampleRejectionRemarksMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<CreateUpdateFieldBoyMasterResponse> CreateUpdateFieldBoyMaster(
            CreateUpdateFieldBoyMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateFieldBoyMaster called. FieldBoyId={request.FieldBoyId}, FieldBoyName={request.FieldBoyName}");

                var result = _sqlHelper.DML(
                    "IU_FieldBoyMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        FieldBoyId = request.FieldBoyId,
                        FieldBoyName = request.FieldBoyName,
                        IsActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        Result = 0
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_FieldBoyMaster_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared FieldBoyMaster cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate field boy name: {request.FieldBoyName}");
                    return ServiceResult<CreateUpdateFieldBoyMasterResponse>.Failure(
                        alert.Type,
                        "Field Boy Name Already Exists.",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateFieldBoyMasterResponse { FieldBoyId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.FieldBoyId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Field Boy {(request.FieldBoyId == 0 ? "created" : "updated")} successfully. FieldBoyId={resultValue}");

                    return ServiceResult<CreateUpdateFieldBoyMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        request.FieldBoyId == 0 ? "Field Boy Master Saved Successfully" : "Field Boy Master Updated Successfully",
                        request.FieldBoyId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Field Boy operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateFieldBoyMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateFieldBoyMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<FieldBoyMasterModel>> GetFieldBoyMaster(int? isActive = null)
        {
            try
            {
                _log.Info($"GetFieldBoyMaster called. IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL field boys
                string cacheKey = "_FieldBoyMaster_All";

                // Try to get all field boys from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<FieldBoyMasterModel> allFieldBoys;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"FieldBoyMaster data retrieved from cache. Key={cacheKey}");
                    allFieldBoys = JsonSerializer.Deserialize<List<FieldBoyMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"FieldBoyMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getFieldBoyMaster",
                        CommandType.StoredProcedure
                    );

                    allFieldBoys = dataTable?.AsEnumerable().Select(row => new FieldBoyMasterModel
                    {
                        FieldBoyId = row.Field<int>("FieldBoyId"),
                        FieldBoyName = row.Field<string>("FieldBoyName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<FieldBoyMasterModel>();

                    // Store ALL data in cache (permanent until manually cleared)
                    if (allFieldBoys.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allFieldBoys);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All FieldBoyMaster data cached permanently. Key={cacheKey}, Count={allFieldBoys.Count}");
                    }
                }

                // Filter in memory based on isActive (always from cache)
                List<FieldBoyMasterModel> filteredFieldBoys = allFieldBoys;

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredFieldBoys = filteredFieldBoys.Where(f => f.IsActive == isActive.Value).ToList();
                }

                if (!filteredFieldBoys.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No field boys found for IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<FieldBoyMasterModel>>.Failure(
                        alert.Type,
                        "No field boys found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredFieldBoys.Count} field boy(s) from cache");

                return ServiceResult<IEnumerable<FieldBoyMasterModel>>.Success(
                    filteredFieldBoys,
                    "Info",
                    $"{filteredFieldBoys.Count} field boy(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<FieldBoyMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<ServiceItemMasterResponse> CreateUpdateInvestigationServiceItemMaster(
            CreateUpdateServiceItemRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateInvestigationServiceItemMaster called. ServiceItemId={request.ServiceItemId}, Name={request.Name}");

                var result = _sqlHelper.DML(
                    "IU_ServiceItemMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @hospId = globalValues.hospId,
                        @serviceItemId = request.ServiceItemId,
                        @categoryId = request.CategoryId,
                        @subCategoryId = request.SubCategoryId,
                        @subSubCategoryId = request.SubSubCategoryId,
                        @name = request.Name,
                        @code = request.Code ?? string.Empty,
                        @reportTypeId = request.ReportTypeId,
                        @reportType = request.ReportType,
                        @isSampleRequired = request.IsSampleRequired,
                        @sampleTypeId = request.SampleTypeId,
                        @sampleTypeList = request.SampleTypeList,
                        @labMethodId = request.LabMethodId,
                        @forGenderId = request.ForGenderId,
                        @forGender = request.ForGender,
                        @isOutSource = request.IsOutSource,
                        @isPrintAlone = request.IsPrintAlone,
                        @isSampleSegregationRequired = request.IsSampleSegregationRequired,
                        @isDepartmentReceivingRequired = request.IsDepartmentReceivingRequired,
                        @isActive = request.IsActive,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress,
                        @ShortName = request.ShortName,
                        @SampleVolume = request.SampleVolume,
                        @InvestigationComment = request.InvestigationComment,
                        @tatInMin = request.TatInMin,
                        @SNOMEDCode = request.SNOMEDCode,
                        @isRequiredSeparatePerformingDoctor = request.IsRequiredSeparatePerformingDoctor,
                        @doctorDepartmentIds = request.DoctorDepartmentIds
                      
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                // -1 = duplicate name or code
                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate Name or Code for ServiceItem: {request.Name}");
                    return ServiceResult<ServiceItemMasterResponse>.Failure(
                        dupAlert.Type,
                        "Investigation Name or Code already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    // Clear Redis cache so next GET re-fetches fresh data
                    _distributedCache.Remove("_ServiceItemMaster_All");
                    _distributedCache.Remove("_ServiceInvestigationItemMaster_All");
                    _log.Info($"Cleared ServiceItemMaster cache after save/update. ServiceItemId={resultValue}");

                    var responseData = new ServiceItemMasterResponse { ServiceItemId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ServiceItemId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"ServiceItem {(request.ServiceItemId == 0 ? "created" : "updated")} successfully. ServiceItemId={resultValue}");

                    return ServiceResult<ServiceItemMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.ServiceItemId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<ServiceItemMasterResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<ServiceItemMasterResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }
        public ServiceResult<IEnumerable<ServiceItemMasterModel>> GetInvestigationServiceItemList(
    int? serviceItemId,
    int? isActive,
    string categoryTypeId,
    string categoryId,
    int? subCategoryId,
    int? subSubCategoryId,
    int? labTypeId,
int? reportTypeId,
    string serviceName)
        {
            try
            {
                _log.Info($"GetInvestigationServiceItemList called. ServiceItemId={serviceItemId}, IsActive={isActive}, CategoryId={categoryId}, SubCategoryId={subCategoryId}, SubSubCategoryId={subSubCategoryId}, ServiceName={serviceName}");

                const string cacheKey = "_ServiceInvestigationItemMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<ServiceItemMasterModel> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("ServiceItemMaster data retrieved from Redis cache.");
                    allItems = System.Text.Json.JsonSerializer.Deserialize<List<ServiceItemMasterModel>>(cachedData)
                               ?? new List<ServiceItemMasterModel>();
                }
                else
                {
                    _log.Info("ServiceItemMaster not in cache. Fetching from DB via SP.");

                    DataTable dt = _sqlHelper.GetDataTable(
                        "S_GetServiceItemMaster",
                        CommandType.StoredProcedure,
                        new { }
                    );

                    allItems = dt.AsEnumerable().Select(row => new ServiceItemMasterModel
                    {
                        ServiceItemId = row.Field<int>("ServiceItemId"),
                        HospId = row.Field<int>("HospId"),
                        CategoryTypeId = row.Field<int>("CategoryTypeId"),
                        CategoryId = row.Field<int>("CategoryId"),
                        SubCategoryId = row.Field<int>("SubCategoryId"),
                        SubSubCategoryId = row.Field<int>("SubSubCategoryId"),
                        Name = row.Field<string>("Name") ?? string.Empty,
                        Code = row.Field<string>("Code") ?? string.Empty,
                        LabTypeId = row.Field<int>("LabTypeId"),
                        ReportTypeId = row.Field<int?>("ReportTypeId"),
                        ReportType = row.Field<string>("ReportType") ?? string.Empty,
                        IsSampleRequired = row.Field<int?>("IsSampleRequired"),
                        SampleTypeId = row.Field<int?>("SampleTypeId"),
                        SampleTypeIdList = row.Field<string>("SampleTypeIdList") ?? string.Empty,
                        LabMethodId = row.Field<int?>("LabMethodId"),
                        ForGenderId = row.Field<int?>("ForGenderId"),
                        ForGender = row.Field<string>("ForGender") ?? string.Empty,
                        IsOutSource = row.Field<int>("IsOutSource"),
                        IsPrintAlone = row.Field<int?>("IsPrintAlone"),
                        IsSampleSegregationRequired = row.Field<int?>("IsSampleSegregationRequired"),
                        IsDepartmentReceivingRequired = row.Field<int?>("IsDepartmentReceivingRequired"),
                        ShortName = row.Field<string>("ShortName") ?? string.Empty,
                        SampleVolume = row.Field<string>("SampleVolume") ?? string.Empty,
                        InvestigationComment = row.Field<string>("InvestigationComment") ?? string.Empty,
                        TatInMin = row.Field<int?>("tatInMin") ?? 0,
                        IsActive = row.Field<int?>("IsActive") ?? 0,
                        SNOMEDCode = row.Field<string>("SNOMEDCode") ?? string.Empty,
                        DoctorDepartmentIds = row.Field<string>("DoctorDepartmentIds") ?? string.Empty,
                        IsRequiredSeparatePerformingDoctor = row.Field<int?>("IsRequiredSeparatePerformingDoctor") ?? 0
                    }).ToList();

                    if (allItems.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"ServiceItemMaster cached permanently. Count={allItems.Count}");
                    }
                }

                // ── In-memory filters — each is independent, all null = return all ──

                if (serviceItemId.HasValue && serviceItemId.Value > 0)
                {
                    allItems = allItems.Where(s => s.ServiceItemId == serviceItemId.Value).ToList();
                    _log.Info($"Filtered by ServiceItemId={serviceItemId}. Count={allItems.Count}");
                }

                if (isActive.HasValue)
                {
                    allItems = allItems.Where(s => s.IsActive == isActive.Value).ToList();
                    _log.Info($"Filtered by IsActive={isActive}. Count={allItems.Count}");
                }

                if (!string.IsNullOrWhiteSpace(categoryTypeId))
                {
                    var categoryTypeIds = categoryTypeId
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => int.TryParse(id.Trim(), out int parsed) ? parsed : 0)
                        .Where(id => id > 0)
                        .ToHashSet();

                    if (categoryTypeIds.Any())
                    {
                        allItems = allItems.Where(s => categoryTypeIds.Contains(s.CategoryTypeId)).ToList();
                        _log.Info($"Filtered by CategoryTypeIds={categoryTypeId}. Count={allItems.Count}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(categoryId))
                {
                    var categoryIds = categoryId
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => int.TryParse(id.Trim(), out int parsed) ? parsed : 0)
                        .Where(id => id > 0)
                        .ToHashSet();

                    if (categoryIds.Any())
                    {
                        allItems = allItems.Where(s => categoryIds.Contains(s.CategoryId)).ToList();
                        _log.Info($"Filtered by CategoryIds={categoryId}. Count={allItems.Count}");
                    }
                }

                if (subCategoryId.HasValue && subCategoryId.Value > 0)
                {
                    allItems = allItems.Where(s => s.SubCategoryId == subCategoryId.Value).ToList();
                    _log.Info($"Filtered by SubCategoryId={subCategoryId}. Count={allItems.Count}");
                }

                if (subSubCategoryId.HasValue && subSubCategoryId.Value > 0)
                {
                    allItems = allItems.Where(s => s.SubSubCategoryId == subSubCategoryId.Value).ToList();
                    _log.Info($"Filtered by SubSubCategoryId={subSubCategoryId}. Count={allItems.Count}");
                }

                if (reportTypeId.HasValue && reportTypeId.Value > 0)
                {
                    allItems = allItems.Where(s => s.ReportTypeId == reportTypeId.Value).ToList();
                    _log.Info($"Filtered by reportTypeId={reportTypeId}. Count={allItems.Count}");
                }


                if (labTypeId.HasValue && labTypeId.Value > 0)
                {
                    allItems = allItems.Where(s => s.LabTypeId == labTypeId.Value).ToList();
                    _log.Info($"Filtered by LabTypeId={labTypeId}. Count={allItems.Count}");
                }

                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    allItems = allItems
                        .Where(s => s.Name.Contains(serviceName.Trim(), StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    _log.Info($"Filtered by ServiceName='{serviceName}'. Count={allItems.Count}");
                }

                if (!allItems.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<ServiceItemMasterModel>>.Failure(alert.Type, "No service items found", 404);
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<ServiceItemMasterModel>>.Success(
                    allItems,
                    successAlert.Type,
                    $"{allItems.Count} service item(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<ServiceItemMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<ObservationMasterModel>> GetObservationMaster(
          int? observationId = null,
          int? isActive = null)
        {
            try
            {
                _log.Info($"GetObservationMaster called. observationId={observationId}, isActive={isActive}");

                List<ObservationMasterModel> allObservations;
                var cachedData = _distributedCache.GetString("_ObservationMaster_All");

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("ObservationMaster data retrieved from Redis cache.");
                    allObservations = JsonSerializer.Deserialize<List<ObservationMasterModel>>(cachedData)
                                      ?? new List<ObservationMasterModel>();
                }
                else
                {
                    _log.Info("Cache miss. Fetching ObservationMaster from database.");
                    var dt = _sqlHelper.GetDataTable(
                        "S_getObservationMaster",
                        CommandType.StoredProcedure);

                    allObservations = dt?.AsEnumerable().Select(row =>
                        new ObservationMasterModel
                        {
                            ObservationId = row.Field<int>("ObservationId"),
                            ObservationName = row.Field<string>("ObservationName") ?? string.Empty,
                            Prefix = row.Field<string>("Prefix") ?? string.Empty,
                            Suffix = row.Field<string>("Suffix") ?? string.Empty,
                            Method = row.Field<string>("Method") ?? string.Empty,
                            MethodId = row.Field<int>("MethodId"),
                            ShowInDischargeSummary = row.Field<int>("ShowInDischargeSummary"),
                            RoundUp = row.Field<string>("RoundUp") ?? string.Empty,
                            FieldTypeId = row.Field<int>("FieldTypeId"),
                            IsActive = row.Field<int>("IsActive")
                        }).ToList()
                    ?? new List<ObservationMasterModel>();

                    if (allObservations.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allObservations);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,   // Persist until manually cleared
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString("_ObservationMaster_All", serialized, cacheOptions);
                        _log.Info($"ObservationMaster cached. Key={"_ObservationMaster_All"}, Count={allObservations.Count}");
                    }
                }

                IEnumerable<ObservationMasterModel> result = allObservations;

                if (observationId.HasValue && observationId.Value > 0)
                    result = result.Where(o => o.ObservationId == observationId.Value);

                if (isActive.HasValue)
                    result = result.Where(o => o.IsActive == isActive.Value);

                var filtered = result.ToList();

                if (!filtered.Any())
                {
                    var notFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No ObservationMaster records matched the filter criteria.");
                    return ServiceResult<IEnumerable<ObservationMasterModel>>.Failure(
                        notFound.Type,
                        notFound.Message,
                        404);
                }

                _log.Info($"GetObservationMaster returning {filtered.Count} record(s).");
                return ServiceResult<IEnumerable<ObservationMasterModel>>.Success(
                    filtered,
                    "Info",
                    $"{filtered.Count} observation(s) retrieved successfully.",
                    200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<ObservationMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500);
            }
        }

      
        public ServiceResult<CreateUpdateObservationMasterResponse> CreateUpdateObservationMaster(
            CreateUpdateObservationMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateObservationMaster called. ObservationId={request.ObservationId}, " +
                          $"ObservationName={request.ObservationName}");

                var result = _sqlHelper.DML(
                    "IU_ObservationMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @ObservationId = request.ObservationId,
                        @ObservationName = request.ObservationName,
                        @PrefixName = request.PrefixName,
                        @SuffixName = request.SuffixName,
                        @MethodId = request.MethodId,
                        @userId = globalValues.userId,
                        @HospId = globalValues.hospId,
                        @IpAddress = globalValues.ipAddress,
                        @ShowInDS = request.ShowInDS,
                        @roundUp = request.RoundUp,
                        @fieldType = request.FieldType,
                        @fieldTypeId = request.FieldTypeId
                    },
                    new { result = 0 }   // output parameter seed
                );

                // SP returns -1 for duplicate name, 0 for unexpected failure
                if (result <= 0)
                {
                    _log.Warn($"ObservationMaster DML returned {result} for Name='{request.ObservationName}'.");

                    var dupAlert = result == -1
                        ? _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS")
                        : _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");

                    return ServiceResult<CreateUpdateObservationMasterResponse>.Failure(
                        dupAlert.Type,
                        dupAlert.Message,
                        409);
                }

                _distributedCache.Remove("_ObservationMaster_All");
                _log.Info($"Cache invalidated. Key={"_ObservationMaster_All"}");

                bool isUpdate = request.ObservationId > 0;
                var alertCode = isUpdate ? "DATA_UPDATED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY";
                var okAlert = _messageService.GetMessageAndTypeByAlertCode(alertCode);

                _log.Info($"ObservationMaster {(isUpdate ? "updated" : "created")} successfully. " +
                          $"ObservationId={result}");

                return ServiceResult<CreateUpdateObservationMasterResponse>.Success(
                    new CreateUpdateObservationMasterResponse { ObservationId = result },
                    okAlert.Type,
                    okAlert.Message,
                    200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateObservationMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500);
            }
        }


       

        public ServiceResult<IEnumerable<InvastigationObservationMappingModel>>
            GetInvastigationObservationMapping(int investigationId)
        {
            try
            {
                _log.Info($"GetInvastigationObservationMapping called. InvastigationId={investigationId}");

                string cacheKey = $"_InvastigationObservationMapping_{investigationId}";

                List<InvastigationObservationMappingModel> mappings;
                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"Cache hit. Key={cacheKey}");
                    mappings = JsonSerializer.Deserialize<List<InvastigationObservationMappingModel>>(cachedData)
                               ?? new List<InvastigationObservationMappingModel>();
                }
                else
                {
                    _log.Info($"Cache miss. Fetching from DB. Key={cacheKey}");

                    var dt = _sqlHelper.GetDataTable(
                        "S_GetInvastigationObservationMappingDetailsByInvastigationId",
                        CommandType.StoredProcedure,
                        new { @InvastigationId = investigationId });

                    mappings = dt?.AsEnumerable().Select(row =>
                        new InvastigationObservationMappingModel
                        {
                            MappingId = row.Field<int>("MappingId"),
                            InvastigationId = row.Field<int>("InvastigationId"),
                            ObservationId = row.Field<int>("ObservationId"),
                            ObservationName = row.Field<string>("ObservationName") ?? string.Empty,
                            Method = row.Field<string>("Method") ?? string.Empty,
                            MethodId = row.Field<int>("MethodId"),
                            IsHeader = row.Field<bool>("IsHeader"),
                            IsBold = row.Field<bool>("IsBold"),
                            IsUnderLine = row.Field<bool>("IsUnderLine"),
                            IsMandatory = row.Field<int>("IsMandatory"),
                            RoundUp = row.Field<string>("RoundUp") ?? string.Empty
                        }).ToList()
                    ?? new List<InvastigationObservationMappingModel>();

                    if (mappings.Any())
                    {
                        var serialized = JsonSerializer.Serialize(mappings);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"Cached {mappings.Count} row(s). Key={cacheKey}");
                    }
                }

                if (!mappings.Any())
                {
                    var notFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No mapping found for InvastigationId={investigationId}");
                    return ServiceResult<IEnumerable<InvastigationObservationMappingModel>>.Failure(
                        notFound.Type, notFound.Message, 404);
                }

                _log.Info($"Returning {mappings.Count} mapping row(s) for InvastigationId={investigationId}");
                return ServiceResult<IEnumerable<InvastigationObservationMappingModel>>.Success(
                    mappings, "Info", $"{mappings.Count} mapping(s) retrieved successfully.", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<InvastigationObservationMappingModel>>.Failure(
                    alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<SubmitInvastigationObservationMappingResponse>
            SubmitInvastigationObservationMapping(
                SubmitInvastigationObservationMappingRequest request,
                AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SubmitInvastigationObservationMapping called. " +
                          $"InvastigationId={request.InvastigationId}, " +
                          $"ObservationCount={request.Observations?.Count ?? 0}");

                // 1. Delete all active rows for this investigation
                _sqlHelper.DML(
                    "D_InvastigationObservationMapping",
                    CommandType.StoredProcedure,
                    new { @InvastigationId = request.InvastigationId },
                    new { result = 0 });

                _log.Info($"Deleted existing mappings for InvastigationId={request.InvastigationId}");

                // 2. Insert each row
                int insertedCount = 0;

                if (request.Observations != null && request.Observations.Any())
                {
                    foreach (var obs in request.Observations)
                    {
                        if (obs.InvastigationId <= 0 || obs.ObservationId <= 0)
                        {
                            _log.Warn($"Skipping row – InvastigationId={obs.InvastigationId}, " +
                                      $"ObservationId={obs.ObservationId} (must be > 0).");
                            continue;
                        }

                        _sqlHelper.DML(
                            "IU_InvastigationObservationMapping",
                            CommandType.StoredProcedure,
                            new
                            {
                                @MappingId = 0,
                                @InvastigationId = obs.InvastigationId,
                                @ObservationId = obs.ObservationId,
                                @IsHeader = obs.IsHeader,
                                @IsBold = obs.IsBold,
                                @IsUnderLine = obs.IsUnderLine,
                                @IsMandatory = obs.IsMandatory,
                                @UserId = globalValues.userId,
                                @HospId = globalValues.hospId,
                                @IPAddress = globalValues.ipAddress
                            },
                            new { result = 0 });

                        insertedCount++;
                    }
                }

                _log.Info($"Inserted {insertedCount} row(s) for InvastigationId={request.InvastigationId}");

                // 3. Invalidate only the affected investigationId cache key
                string cacheKey = $"_InvastigationObservationMapping_{request.InvastigationId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cache invalidated. Key={cacheKey}");

                var okAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SubmitInvastigationObservationMappingResponse>.Success(
                    new SubmitInvastigationObservationMappingResponse
                    {
                        InvastigationId = request.InvastigationId,
                        InsertedCount = insertedCount
                    },
                    okAlert.Type, okAlert.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SubmitInvastigationObservationMappingResponse>.Failure(
                    alert.Type, alert.Message, 500);
            }
        }

       
        private static string RangeMasterCacheKey(int observationId, string gender)
            => $"_InvastigationObservationRangeMaster_{observationId}_{gender}";

        public ServiceResult<IEnumerable<InvastigationObservationRangeMasterModel>>
            GetInvastigationObservationRangeMaster(int observationId, string gender)
        {
            try
            {
                _log.Info($"GetInvastigationObservationRangeMaster called. " +
                          $"ObservationId={observationId}, Gender={gender}");

                string cacheKey = RangeMasterCacheKey(observationId, gender);

                List<InvastigationObservationRangeMasterModel> ranges;
                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"Cache hit. Key={cacheKey}");
                    ranges = JsonSerializer.Deserialize<List<InvastigationObservationRangeMasterModel>>(cachedData)
                             ?? new List<InvastigationObservationRangeMasterModel>();
                }
                else
                {
                    _log.Info($"Cache miss. Fetching from DB. Key={cacheKey}");

                    var dt = _sqlHelper.GetDataTable(
                        "S_InvastigationObservationRangeMaster",
                        CommandType.StoredProcedure,
                        new
                        {
                            @ObservationId = observationId,
                            @Gender = gender
                        });

                    ranges = dt?.AsEnumerable().Select(row =>
                        new InvastigationObservationRangeMasterModel
                        {
                            Id = row.Field<int>("Id"),
                            ObservationId = row.Field<int>("ObservationId"),
                            ObservationName = row.Field<string>("ObservationName") ?? string.Empty,
                            Gender = row.Field<string>("Gender") ?? string.Empty,
                            FromAge = row.Field<string>("FromAge") ?? string.Empty,
                            ToAge = row.Field<string>("ToAge") ?? string.Empty,
                            IsActive = row.Field<int>("IsActive"),
                            DefaultValue = row.Field<string>("DefaultValue") ?? string.Empty,
                            MinValue = row.Field<string>("MinValue") ?? string.Empty,
                            MaxValue = row.Field<string>("MaxValue") ?? string.Empty,
                            Unit = row.Field<string>("Unit") ?? string.Empty,
                            DisplayValue = row.Field<string>("DisplayValue") ?? string.Empty
                        }).ToList()
                    ?? new List<InvastigationObservationRangeMasterModel>();

                    if (ranges.Any())
                    {
                        var serialized = JsonSerializer.Serialize(ranges);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"Cached {ranges.Count} row(s). Key={cacheKey}");
                    }
                }

                if (!ranges.Any())
                {
                    var notFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No ranges found for ObservationId={observationId}, Gender={gender}");
                    return ServiceResult<IEnumerable<InvastigationObservationRangeMasterModel>>.Failure(
                        notFound.Type, notFound.Message, 404);
                }

                _log.Info($"Returning {ranges.Count} range row(s).");
                return ServiceResult<IEnumerable<InvastigationObservationRangeMasterModel>>.Success(
                    ranges, "Info", $"{ranges.Count} range(s) retrieved successfully.", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<InvastigationObservationRangeMasterModel>>.Failure(
                    alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<SubmitInvastigationObservationRangeMasterResponse>
            SubmitInvastigationObservationRangeMaster(
                SubmitInvastigationObservationRangeMasterRequest request,
                AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SubmitInvastigationObservationRangeMaster called. " +
                          $"ObservationId={request.ObservationId}, Gender={request.Gender}, " +
                          $"RangeCount={request.Ranges?.Count ?? 0}");

                // 1. Delete — mirrors D_InvastigationObservationRangeMaster SP logic
                //    Gender='B' → deletes ALL genders for that observation
                //    Gender='M'/'F' → deletes 'B' rows + the specific gender rows
                _sqlHelper.DML(
                    "D_InvastigationObservationRangeMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @ObservationId = request.ObservationId,
                        @Gender = request.Gender
                    },
                    new { result = 0 });

                _log.Info($"Deleted existing ranges. ObservationId={request.ObservationId}, Gender={request.Gender}");

                int insertedCount = 0;

                if (request.Ranges != null && request.Ranges.Any())
                {
                    foreach (var row in request.Ranges)
                    {
                        _sqlHelper.DML(
                            "IU_InvastigationObservationRangeMaster",
                            CommandType.StoredProcedure,
                            new
                            {
                                @ObservationId = row.ObservationId,
                                @Gender = row.Gender,
                                @FromAge = row.FromAge,
                                @ToAge = row.ToAge,
                                @DefaultValue = row.DefaultValue,
                                @MinValue = row.MinValue,
                                @MaxValue = row.MaxValue,
                                @Unit = row.Unit,
                                @DisplayValue = row.DisplayValue,
                                @IpAddress = globalValues.ipAddress,
                                @UserId = globalValues.userId
                            },
                            new { result = 0 });

                        insertedCount++;
                    }
                }

                _log.Info($"Inserted {insertedCount} range row(s). ObservationId={request.ObservationId}");

                // 3. Invalidate cache for this ObservationId + Gender combo
                //    Also clear 'B' key because SP deletes 'B' rows when Gender is M/F
                var keysToInvalidate = new List<string>
        {
            RangeMasterCacheKey(request.ObservationId, request.Gender)
        };

                if (request.Gender == "M" || request.Gender == "F")
                    keysToInvalidate.Add(RangeMasterCacheKey(request.ObservationId, "B"));

                if (request.Gender == "B")
                {
                    keysToInvalidate.Add(RangeMasterCacheKey(request.ObservationId, "M"));
                    keysToInvalidate.Add(RangeMasterCacheKey(request.ObservationId, "F"));
                }

                foreach (var key in keysToInvalidate)
                {
                    _distributedCache.Remove(key);
                    _log.Info($"Cache invalidated. Key={key}");
                }

                var okAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SubmitInvastigationObservationRangeMasterResponse>.Success(
                    new SubmitInvastigationObservationRangeMasterResponse
                    {
                        ObservationId = request.ObservationId,
                        Gender = request.Gender,
                        InsertedCount = insertedCount
                    },
                    okAlert.Type, okAlert.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SubmitInvastigationObservationRangeMasterResponse>.Failure(
                    alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<LabFormulaMasterModel>> GetFormulaMasterByObservationId(int observationId)
        {
            try
            {
                _log.Info($"GetFormulaMasterByObservationId called. ObservationId={observationId}");

                string cacheKey = $"_LabFormulaMaster_Obs{observationId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<LabFormulaMasterModel> formulaComponents;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"LabFormulaMaster data retrieved from cache. Key={cacheKey}");
                    formulaComponents = JsonSerializer.Deserialize<List<LabFormulaMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"LabFormulaMaster cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetFormulaMasterByObservationId",
                        CommandType.StoredProcedure,
                        new { observationId = observationId }
                    );

                    formulaComponents = dataTable?.AsEnumerable().Select(row => new LabFormulaMasterModel
                    {
                        FormulaText = row.Field<string>("FormulaText") ?? string.Empty,
                        TypeId = row.Field<int?>("TypeId") ?? 0,
                        Type = row.Field<string>("Type") ?? string.Empty,
                        Component = row.Field<string>("Component") ?? string.Empty,
                        SequenceNo = row.Field<int?>("SequenceNo") ?? 0,
                        FormulaExpressionRight = row.Field<string>("FormulaExpressionRight") ?? string.Empty
                    }).ToList() ?? new List<LabFormulaMasterModel>();

                    if (formulaComponents.Any())
                    {
                        var serialized = JsonSerializer.Serialize(formulaComponents);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"LabFormulaMaster data cached permanently. Key={cacheKey}, Count={formulaComponents.Count}");
                    }
                }

                if (!formulaComponents.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No formula found for ObservationId={observationId}");
                    return ServiceResult<IEnumerable<LabFormulaMasterModel>>.Failure(
                        alert.Type,
                        $"No formula found for ObservationId: {observationId}",
                        404
                    );
                }

                _log.Info($"Retrieved {formulaComponents.Count} formula component(s) from cache for ObservationId={observationId}");

                return ServiceResult<IEnumerable<LabFormulaMasterModel>>.Success(
                    formulaComponents,
                    "Info",
                    $"{formulaComponents.Count} formula component(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<LabFormulaMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<LabFormulaMasterResponse> CreateUpdateLabFormulaMaster(
            CreateUpdateLabFormulaMasterRequest request,
            AllGlobalValues globalValues)
        {
           
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);
            try
            {
                _log.Info($"CreateUpdateLabFormulaMaster called. ObservationId={request.observationId}");

                var formulaIdResult = _sqlHelper.DML(tnx, "IU_LabFormulaMaster", CommandType.StoredProcedure, new
                {
                    observationId = request.observationId,
                    formulaText = request.formulaText ?? string.Empty,
                    formulaExpression = request.formulaExpression ?? string.Empty,
                    formulaExpressionRight = request.formulaExpressionRight ?? string.Empty,
                    userId = globalValues.userId,
                    ipAddress = globalValues.ipAddress
                },
                new { result = 0 });

                int formulaId = Convert.ToInt32(formulaIdResult);

                if (formulaId <= 0)
                {
                    tnx.Rollback();
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error($"Failed to create/update formula master. ObservationId={request.observationId}");
                    return ServiceResult<LabFormulaMasterResponse>.Failure(
                        alert.Type,
                        "Formula Id could not be generated",
                        500
                    );
                }

                _sqlHelper.DML(tnx, "D_LabFormulaMasterComponents", CommandType.StoredProcedure, new
                {
                    formulaId = formulaId
                });

                _log.Info($"Deleted existing formula components for FormulaId={formulaId}");

                if (request.formulaComponents != null && request.formulaComponents.Any())
                {
                    foreach (var component in request.formulaComponents)
                    {
                        _sqlHelper.DML(tnx, "I_LabFormulaMasterComponents", CommandType.StoredProcedure, new
                        {
                            formulaId = formulaId,
                            typeId = component.typeId,
                            type = component.type,
                            component = component.component,
                            sequenceNo = component.sequenceNo
                        });
                    }
                    _log.Info($"Inserted {request.formulaComponents.Count} formula component(s) for FormulaId={formulaId}");
                }

                tnx.Commit();
                _log.Info($"Transaction committed. FormulaId={formulaId}");

                // Clear related cache entries
                _distributedCache.Remove($"_LabFormulaMaster_Obs{request.observationId}");
                GlobalFunctions.ClearCacheByPattern(_configuration, "_ObservationFormula_Inv*");


                var responseData = new LabFormulaMasterResponse { FormulaId = formulaId };
                var successAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");

                return ServiceResult<LabFormulaMasterResponse>.Success(
                    responseData,
                    successAlert.Type,
                    successAlert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                if (tnx != null)
                {
                    try { tnx.Rollback(); _log.Error("Transaction rolled back due to error"); }
                    catch (Exception rollbackEx) { _log.Error($"Error during rollback: {rollbackEx.Message}"); }
                }

                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<LabFormulaMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }



        public ServiceResult<IEnumerable<ObservationFormulaByInvestigationModel>> GetObservationFormulaByInvestigationId(int investigationId)
        {
            try
            {
                _log.Info($"GetObservationFormulaByInvestigationId called. InvestigationId={investigationId}");

                string cacheKey = $"_ObservationFormula_Inv{investigationId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<ObservationFormulaByInvestigationModel> observationFormulas;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"ObservationFormula data retrieved from cache. Key={cacheKey}");
                    observationFormulas = JsonSerializer.Deserialize<List<ObservationFormulaByInvestigationModel>>(cachedData);
                }
                else
                {
                    _log.Info($"ObservationFormula cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getObservationFormulaByInvestigationId",
                        CommandType.StoredProcedure,
                        new { InvestigationId = investigationId }
                    );

                    observationFormulas = dataTable?.AsEnumerable().Select(row => new ObservationFormulaByInvestigationModel
                    {
                        InvestigationName = row.Field<string>("InvestigationName") ?? string.Empty,
                        ObservationName = row.Field<string>("ObservationName") ?? string.Empty,
                        FormulaText = row.Field<string>("FormulaText") ?? string.Empty,
                        ObservationId = row.Field<int>("ObservationId"),
                        InvastigationId = row.Field<int>("InvastigationId"),
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn"),
                    }).ToList() ?? new List<ObservationFormulaByInvestigationModel>();

                    if (observationFormulas.Any())
                    {
                        var serialized = JsonSerializer.Serialize(observationFormulas);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"ObservationFormula data cached permanently. Key={cacheKey}, Count={observationFormulas.Count}");
                    }
                }

                if (!observationFormulas.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No observation formulas found for InvestigationId={investigationId}");
                    return ServiceResult<IEnumerable<ObservationFormulaByInvestigationModel>>.Failure(
                        alert.Type,
                        $"No observation formulas found for InvestigationId: {investigationId}",
                        404
                    );
                }

                _log.Info($"Retrieved {observationFormulas.Count} observation formula(s) from cache for InvestigationId={investigationId}");

                return ServiceResult<IEnumerable<ObservationFormulaByInvestigationModel>>.Success(
                    observationFormulas,
                    "Info",
                    $"{observationFormulas.Count} observation formula(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<ObservationFormulaByInvestigationModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

       

        public ServiceResult<string> DeleteLabFormulaByObservationid(int Observationid, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeleteLabFormulaByObservationid called. Observationid={Observationid}");

                var result = _sqlHelper.DML(
                    "D_deleteLabFormulaByObservationid",
                    CommandType.StoredProcedure,
                    new { Observationid = Observationid }
                );

                // Clear related cache entries
                GlobalFunctions.ClearCacheByPattern(_configuration, "_ObservationFormula_Inv*");

                _log.Info($"Lab formula deleted successfully for Observationid={Observationid}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Lab formula deleted successfully",
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

        public ServiceResult<IEnumerable<Dictionary<string, object>>> SearchPatientInvestigationForSampleManagement(
      int branchId, int typeId, string uhid, string ipdNo, string labNo,
      string fromDate, string toDate, string barCode, int subCategoryId,
      int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId,int statusId, int sampleTypeId, int roomTypeId, string isUrgent)
        {
            try
            {
                _log.Info($"SearchPatientInvestigationForSampleManagement called. BranchId={branchId}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_searchPatientInvestigationForSampleManagement",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @typeId = typeId,
                        @uhid = uhid,
                        @ipdNo = ipdNo,
                        @labNo = labNo,
                        @fromDate = Utility.getDateTime(fromDate).ToString("yyyy-MM-dd"),
                        @toDate = Utility.getDateTime(toDate).ToString("yyyy-MM-dd"),
                        @barCode = barCode,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @investigationId = investigationId,
                        @patientName = patientName,
                        @roleId = roleId,
                        @corporateId = corporateId,
                        @statusId= statusId,
                        @sampleTypeId = sampleTypeId,
                        @roomTypeId = roomTypeId,
                        @isUrgent = isUrgent
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patient investigation records found.");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable rows to raw Dictionary list — no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"SearchPatientInvestigationForSampleManagement returned {result.Count} record(s).");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} record(s) retrieved successfully",
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


        public ServiceResult<IEnumerable<Dictionary<string, object>>> SearchPatientInvestigationForSampleDepartmentReceiving(
 int branchId, int typeId, string uhid, string ipdNo, string labNo,
 string fromDate, string toDate, string barCode, int subCategoryId,
 int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId, int sampleTypeId, int roomTypeId, string isUrgent)
        {
            try
            {
                _log.Info($"SearchPatientInvestigationForSampleDepartmentReceiving called. BranchId={branchId}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_searchPatientInvestigationForSampleDepartmentReceiving",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @typeId = typeId,
                        @uhid = uhid,
                        @ipdNo = ipdNo,
                        @labNo = labNo,
                        @fromDate = Utility.getDateTime(fromDate).ToString("yyyy-MM-dd"),
                        @toDate = Utility.getDateTime(toDate).ToString("yyyy-MM-dd"),
                        @barCode = barCode,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @investigationId = investigationId,
                        @patientName = patientName,
                        @roleId = roleId,
                        @corporateId = corporateId,
                        @statusId = statusId,
                        @sampleTypeId = sampleTypeId,
                        @roomTypeId = roomTypeId,
                        @isUrgent = isUrgent
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patient investigation records found.");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable rows to raw Dictionary list — no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"SearchPatientInvestigationForSampleDepartmentReceiving returned {result.Count} record(s).");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} record(s) retrieved successfully",
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

        public ServiceResult<IEnumerable<Dictionary<string, object>>> SearchPatientInvestigationForSampleSegregation(
   int branchId, int typeId, string uhid, string ipdNo, string labNo,
   string fromDate, string toDate, string barCode, int subCategoryId,
   int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId, int sampleTypeId, int roomTypeId, string isUrgent)
        {
            try
            {
                _log.Info($"SearchPatientInvestigationForSampleSegregation called. BranchId={branchId}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_searchPatientInvestigationForSampleSegregation",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @typeId = typeId,
                        @uhid = uhid,
                        @ipdNo = ipdNo,
                        @labNo = labNo,
                        @fromDate = Utility.getDateTime(fromDate).ToString("yyyy-MM-dd"),
                        @toDate = Utility.getDateTime(toDate).ToString("yyyy-MM-dd"),
                        @barCode = barCode,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @investigationId = investigationId,
                        @patientName = patientName,
                        @roleId = roleId,
                        @corporateId = corporateId,
                        @statusId = statusId,
                        @sampleTypeId = sampleTypeId,
                        @roomTypeId = roomTypeId,
                        @isUrgent = isUrgent
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patient investigation records found.");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable rows to raw Dictionary list — no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"SearchPatientInvestigationForSampleSegregation returned {result.Count} record(s).");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} record(s) retrieved successfully",
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


            public ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForSampleProcessingPathology(
  int branchId, int typeId, string uhid, string ipdNo, string labNo,
  string fromDate, string toDate, string barCode, int subCategoryId,
  int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId,int canSampleCollect)
        {
            try
            {
                _log.Info($"searchPatientInvestigationForSampleProcessingPathology called. BranchId={branchId}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_SearchPatientInvestigationForSampleProcessingPathology",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @typeId = typeId,
                        @uhid = uhid,
                        @ipdNo = ipdNo,
                        @labNo = labNo,
                        @fromDate = Utility.getDateTime(fromDate).ToString("yyyy-MM-dd"),
                        @toDate = Utility.getDateTime(toDate).ToString("yyyy-MM-dd"),
                        @barCode = barCode,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @investigationId = investigationId,
                        @patientName = patientName,
                        @roleId = roleId,
                        @corporateId = corporateId,
                        @statusId = statusId,
                        @canSampleCollect= canSampleCollect
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patient investigation records found.");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable rows to raw Dictionary list — no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"searchPatientInvestigationForSampleProcessingPathology returned {result.Count} record(s).");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} record(s) retrieved successfully",
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


        public ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForSampleProcessingRadiology(
int branchId, int typeId, string uhid, string ipdNo, string labNo,
string fromDate, string toDate, string barCode, int subCategoryId,
int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId)
        {
            try
            {
                _log.Info($"searchPatientInvestigationForSampleProcessingRadiology called. BranchId={branchId}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_SearchPatientInvestigationForSampleProcessingRadiology",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @typeId = typeId,
                        @uhid = uhid,
                        @ipdNo = ipdNo,
                        @labNo = labNo,
                        @fromDate = Utility.getDateTime(fromDate).ToString("yyyy-MM-dd"),
                        @toDate = Utility.getDateTime(toDate).ToString("yyyy-MM-dd"),
                        @barCode = barCode,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @investigationId = investigationId,
                        @patientName = patientName,
                        @roleId = roleId,
                        @corporateId = corporateId,
                        @statusId = statusId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patient investigation records found.");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable rows to raw Dictionary list — no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"searchPatientInvestigationForSampleProcessingRadiology returned {result.Count} record(s).");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} record(s) retrieved successfully",
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


        public ServiceResult<string> UpdateSampleStatus(UpdateSampleStatusRequest request, AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"UpdateSampleStatus called. Sample count={request.Samples.Count}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                foreach (var r in request.Samples)
                {
                    var isExists = Convert.ToInt32(_sqlHelper.ExecuteScalar(tnx,
                        "S_ValidateBarcodes",
                        CommandType.StoredProcedure,
                        new { barCode = r.BarCode, labNo = r.LabNo }
                    ));

                    if (isExists > 0)
                    {
                        tnx.Rollback();
                        _log.Warn($"Barcode {r.BarCode} is already in use for another lab number.");
                        var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                        return ServiceResult<string>.Failure(
                            alert.Type,
                            $"Barcode '{r.BarCode}' is already used. Please try a different barcode.",
                            409
                        );
                    }

                    _sqlHelper.DML(tnx, "U_UpdateSampleStatus", CommandType.StoredProcedure, new
                    {
                        @patientInvestigationId = r.PatientInvestigationId,
                        @barCode = r.BarCode,
                        @statusId = r.StatusId,
                        @defaultSampleTypeId = r.DefaultSampleTypeId,
                        @departmentId = r.DepartmentId,
                        @sampleDateTime = Utility.getDateTime(r.sampleDateTime).ToString("yyyy-MM-dd HH:mm:ss"),
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    });

                    _log.Info($"Sample status updated. PatientInvestigationId={r.PatientInvestigationId}, BarCode={r.BarCode}, StatusId={r.StatusId}");
                }

                tnx.Commit();
                _log.Info($"UpdateSampleStatus transaction committed. {request.Samples.Count} record(s) updated.");

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{request.Samples.Count} sample status(es) updated successfully",
                    successAlert.Type,
                    successAlert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch (Exception rbEx) { _log.Error($"Rollback failed: {rbEx.Message}"); }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }


        public ServiceResult<string> RejectSampleStatus(RejectSampleStatusRequest request, AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"RejectSampleStatus called. Sample count={request.Samples.Count}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                foreach (var item in request.Samples)
                {
                    _sqlHelper.DML(tnx, "U_RejectSampleStatus", CommandType.StoredProcedure, new
                    {
                        @patientInvestigationId = item.PatientInvestigationId,
                        @statusId = item.StatusId,
                        @cancellationReason = item.CancellationReason,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    });

                    _log.Info($"RejectSampleStatus updated. PatientInvestigationId={item.PatientInvestigationId}, StatusId={item.StatusId}");
                }

                tnx.Commit();
                _log.Info($"RejectSampleStatus transaction committed. {request.Samples.Count} record(s) updated.");

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{request.Samples.Count} sample status record(s) updated successfully",
                    successAlert.Type,
                    "Record(s) Updated Successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch (Exception rbEx) { _log.Error($"Rollback failed: {rbEx.Message}"); }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<string> UpdateReportApproval(UpdateReportApprovalRequest request, AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"UpdateReportApproval called. PatientInvestigationCount={request.PatientInvestigationIds.Count}, ApprovedByDoctorId={request.ApprovedByDoctorId}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                var patientInvestigationIds = request.PatientInvestigationIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                var patientInvestigationIdList = string.Join(",", patientInvestigationIds);

                _sqlHelper.DML(tnx, "U_UpdateReportApproval", CommandType.StoredProcedure, new
                {
                    @patientInvestigationIdList = patientInvestigationIdList,
                    @approvedByDoctorId = request.ApprovedByDoctorId,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });

                if (_configuration.GetValue<bool>("SMSFlags:SendReportCollectionSMS"))
                {
                    foreach (var patientInvestigationId in patientInvestigationIds)
                    {
                        var sms = new SMS(_sqlHelper, tnx)
                        {
                            branchId = request.BranchId,
                            SMSType = SMSType.ReportCollection,
                            patientInvestigationId = patientInvestigationId
                        };

                        sms.Insert();
                    }
                }

                tnx.Commit();
                _log.Info($"UpdateReportApproval transaction committed. {patientInvestigationIds.Count} report(s) approved.");

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{patientInvestigationIds.Count} report(s) approved successfully",
                    successAlert.Type,
                    "Reports Approved Successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch (Exception rbEx) { _log.Error($"Rollback failed: {rbEx.Message}"); }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }
        public ServiceResult<object> GetPatientInvestigationDetails(int branchId, string uhid, int labNo, int visitId)
        {
            try
            {
                _log.Info($"GetPatientInvestigationDetails called. BranchId={branchId}, UHID={uhid}, LabNo={labNo}, VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientInvestigationDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @uhid = uhid,
                        @labNo = labNo,
                        @visitId = visitId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alertNotFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No investigation details found for LabNo={labNo}, VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alertNotFound.Type,
                        alertNotFound.Message,
                        404
                    );
                }

                // Convert DataTable rows to a list of dictionaries to return raw SP data as-is
                var result = dataTable.AsEnumerable().Select(row =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (DataColumn col in dataTable.Columns)
                    {
                        dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                    }
                    return dict;
                }).ToList();

                _log.Info($"Retrieved {result.Count} investigation record(s) for LabNo={labNo}, VisitId={visitId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert.Type,
                    $"{result.Count} record(s) retrieved successfully",
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

 
        public ServiceResult<string> CreateUpdatePatientInvestigationRemark(
    CreateUpdatePatientInvestigationRemarkRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdatePatientInvestigationRemark called. Id={request.Id}, PatientInvestigationId={request.PatientInvestigationId}");

                var result = _sqlHelper.DML("IU_createUpdatePatientInvestigationRemark", CommandType.StoredProcedure, new
                {
                    @id = request.Id,
                    @PatientInvestigationId = request.PatientInvestigationId,
                    @testRemark = request.TestRemark,
                    @testComment = request.TestComment,
                    @testCommentId = request.TestCommentId,
                    @isInternal = request.IsInternal,
                    @userId = globalValues.userId,
                    @hospId = globalValues.hospId,
                    @branchId = 0, // pass branchId if available in AllGlobalValues
                    @IpAddress = globalValues.ipAddress
                });

                // Clear cache for this PatientInvestigationId
                string cacheKey = $"_PatientInvestigationRemark_{request.PatientInvestigationId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                var alert = _messageService.GetMessageAndTypeByAlertCode(
                    request.Id == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                );

                _log.Info($"PatientInvestigationRemark {(request.Id == 0 ? "created" : "updated")} successfully.");

                return ServiceResult<string>.Success(
                    request.Id == 0 ? "Remark saved successfully" : "Remark updated successfully",
                    alert.Type,
                    alert.Message,
                    request.Id == 0 ? 201 : 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetPatientInvestigationRemark(int patientInvestigationId)
        {
            try
            {
                _log.Info($"GetPatientInvestigationRemark called. PatientInvestigationId={patientInvestigationId}");

                string cacheKey = $"_PatientInvestigationRemark_{patientInvestigationId}";

                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PatientInvestigationRemark data retrieved from cache. Key={cacheKey}");
                    var cachedResult = System.Text.Json.JsonSerializer.Deserialize<object>(cachedData);
                    return ServiceResult<object>.Success(cachedResult, "Info", "Remarks retrieved successfully", 200);
                }

                _log.Info($"PatientInvestigationRemark cache miss. Fetching from database. Key={cacheKey}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientInvestigationRemark",
                    CommandType.StoredProcedure,
                    new { @PatientInvestigationId = patientInvestigationId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No remarks found for PatientInvestigationId={patientInvestigationId}");
                    return ServiceResult<object>.Failure(alert.Type, "No remarks found", 404);
                }

                // Serialize DataTable rows to JSON
                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                var serialized = System.Text.Json.JsonSerializer.Serialize(rows);

                // Store in cache
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = null,
                    SlidingExpiration = null
                };
                _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                _log.Info($"PatientInvestigationRemark data cached. Key={cacheKey}, Count={rows.Count}");

                var result = System.Text.Json.JsonSerializer.Deserialize<object>(serialized);

                return ServiceResult<object>.Success(result, "Info", $"{rows.Count} remark(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }
        public ServiceResult<string> DeletePatientInvestigationRemark(int remarkId, int patientInvestigationId)
        {
            try
            {
                _log.Info($"DeletePatientInvestigationRemark called. RemarkId={remarkId}, PatientInvestigationId={patientInvestigationId}");

                var result = _sqlHelper.DML("D_DeletePatientInvestigationRemark", CommandType.StoredProcedure, new
                {
                    @id = remarkId
                });

                // Clear cache for this PatientInvestigationId
                string cacheKey = $"_PatientInvestigationRemark_{patientInvestigationId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                _log.Info($"PatientInvestigationRemark deleted successfully. RemarkId={remarkId}");

                return ServiceResult<string>.Success(
                    "Remark deleted successfully", alert.Type, alert.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }
        public ServiceResult<string> CreateUpdateInvestigationDocumentNameMaster(
    CreateUpdateInvestigationDocumentNameMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateInvestigationDocumentNameMaster called. DocumentId={request.DocumentId}, DocumentName={request.DocumentName}");

                var result = _sqlHelper.DML("IU_InvestigationDocumentNameMaster", CommandType.StoredProcedure, new
                {
                    @DocumentId = request.DocumentId,
                    @DocumentName = request.DocumentName,
                    @UserId = globalValues.userId,
                    @IPAddress = globalValues.ipAddress
                },
                new { result = 0 });

                // Clear master list cache after any change
                _distributedCache.Remove("_InvestigationDocumentNameMaster_All");
                _log.Info("Cleared InvestigationDocumentNameMaster cache");

                if (result < 0)
                {
                    var alertDup = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate document name: {request.DocumentName}");
                    return ServiceResult<string>.Failure(
                        alertDup.Type,
                        "Document name already exists",
                        409
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode(
                    request.DocumentId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                );

                _log.Info($"InvestigationDocumentNameMaster {(request.DocumentId == 0 ? "created" : "updated")} successfully.");

                return ServiceResult<string>.Success(
                    request.DocumentId == 0 ? "Document name saved successfully" : "Document name updated successfully",
                    alert.Type,
                    alert.Message,
                    request.DocumentId == 0 ? 201 : 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<InvestigationDocumentNameMasterModel>> GetInvestigationDocumentNameMaster()
        {
            try
            {
                _log.Info("GetInvestigationDocumentNameMaster called.");

                string cacheKey = "_InvestigationDocumentNameMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<InvestigationDocumentNameMasterModel> documents;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"InvestigationDocumentNameMaster data retrieved from cache. Key={cacheKey}");
                    documents = System.Text.Json.JsonSerializer.Deserialize<List<InvestigationDocumentNameMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"InvestigationDocumentNameMaster cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetInvestigationDocumentNameMaster",
                        CommandType.StoredProcedure
                    );

                    documents = dataTable?.AsEnumerable().Select(row => new InvestigationDocumentNameMasterModel
                    {
                        DocumentId = row.Field<int>("DocumentId"),
                        Name = row.Field<string>("Name") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        CreatedBy = row.Field<int?>("CreatedBy")
                    }).ToList() ?? new List<InvestigationDocumentNameMasterModel>();

                    if (documents.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(documents);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"InvestigationDocumentNameMaster data cached permanently. Key={cacheKey}, Count={documents.Count}");
                    }
                }

                if (!documents.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No investigation document names found.");
                    return ServiceResult<IEnumerable<InvestigationDocumentNameMasterModel>>.Failure(
                        alert.Type, "No document names found", 404);
                }

                _log.Info($"Retrieved {documents.Count} investigation document name(s) from cache.");

                return ServiceResult<IEnumerable<InvestigationDocumentNameMasterModel>>.Success(
                    documents, "Info", $"{documents.Count} document name(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<InvestigationDocumentNameMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> InsertPatientInvestigationDocument(
       InsertPatientInvestigationDocumentRequest request,
       AllGlobalValues globalValues,
       string uploadedFilePath)
        {
            try
            {
                _log.Info($"InsertPatientInvestigationDocument called. PatientInvestigationId={request.PatientInvestigationId}, InvestigationDocumentNameId={request.InvestigationDocumentNameId}");

                var result = _sqlHelper.DML("IU_PatientInvestigationDocumentList", CommandType.StoredProcedure, new
                {
                    @PatientInvestigationId = request.PatientInvestigationId,
                    @InvestigationDocumentNameId = request.InvestigationDocumentNameId,
                    @UploadFileLocation = uploadedFilePath,
                    @UserId = globalValues.userId,
                    @IPAddress = globalValues.ipAddress
                },
                new { result = 0 });

                // Clear dynamic cache for this PatientInvestigationId
                string cacheKey = $"_PatientInvestigationDocument_{request.PatientInvestigationId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                if (result < 0)
                {
                    var alertDup = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Document already exists for PatientInvestigationId={request.PatientInvestigationId}, InvestigationDocumentNameId={request.InvestigationDocumentNameId}");
                    return ServiceResult<string>.Failure(
                        alertDup.Type,
                        "Document already exists for this investigation",
                        409
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                _log.Info($"PatientInvestigationDocument inserted successfully. PatientInvestigationId={request.PatientInvestigationId}");

                return ServiceResult<string>.Success(
                    "Document saved successfully",
                    alert.Type,
                    alert.Message,
                    201
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetPatientInvestigationDocumentList(int patientInvestigationId)
        {
            try
            {
                _log.Info($"GetPatientInvestigationDocumentList called. PatientInvestigationId={patientInvestigationId}");

                string cacheKey = $"_PatientInvestigationDocument_{patientInvestigationId}";

                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PatientInvestigationDocument data retrieved from cache. Key={cacheKey}");
                    var cachedResult = System.Text.Json.JsonSerializer.Deserialize<object>(cachedData);
                    return ServiceResult<object>.Success(cachedResult, "Info", "Documents retrieved successfully", 200);
                }

                _log.Info($"PatientInvestigationDocument cache miss. Fetching from database. Key={cacheKey}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientInvestigationDocumentList",
                    CommandType.StoredProcedure,
                    new { @PatientInvestigationId = patientInvestigationId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No documents found for PatientInvestigationId={patientInvestigationId}");
                    return ServiceResult<object>.Failure(alert.Type, "No documents found", 404);
                }

                // Serialize DataTable rows directly as raw data
                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                var serialized = System.Text.Json.JsonSerializer.Serialize(rows);

                // Store in cache permanently until manually cleared
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = null,
                    SlidingExpiration = null
                };
                _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                _log.Info($"PatientInvestigationDocument data cached. Key={cacheKey}, Count={rows.Count}");

                var result = System.Text.Json.JsonSerializer.Deserialize<object>(serialized);

                return ServiceResult<object>.Success(result, "Info", $"{rows.Count} document(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> DeletePatientInvestigationDocument(int patientDocumentId, int patientInvestigationId)
        {
            try
            {
                _log.Info($"DeletePatientInvestigationDocument called. PatientDocumentId={patientDocumentId}, PatientInvestigationId={patientInvestigationId}");

                var result = _sqlHelper.DML("D_deletePatientInvestigationDocument", CommandType.StoredProcedure, new
                {
                    @patientDocumentId = patientDocumentId
                },
                new { result = 0 });

                // Clear dynamic cache for this PatientInvestigationId
                string cacheKey = $"_PatientInvestigationDocument_{patientInvestigationId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info($"PatientInvestigationDocument deleted successfully. PatientDocumentId={patientDocumentId}");
                    return ServiceResult<string>.Success(
                        "Document deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    _log.Warn($"Failed to delete PatientInvestigationDocument. PatientDocumentId={patientDocumentId}");
                    return ServiceResult<string>.Failure(alert.Type, "Something went wrong. Please try again later.", 400);
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetPatientTabularReportForResultEntry(int patientInvestigationId)
        {
            try
            {
                _log.Info($"GetPatientTabularReportForResultEntry called. PatientInvestigationId={patientInvestigationId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_getPatientTabularReportForResultEntry",
                    CommandType.StoredProcedure,
                    new { @patientInvestigationId = patientInvestigationId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No tabular report found for PatientInvestigationId={patientInvestigationId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Return raw data without manual mapping — serialize DataTable rows as list of dicts
                var rows = dataTable.AsEnumerable()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        ))
                    .ToList<object>();

                _log.Info($"Retrieved {rows.Count} tabular report row(s) for PatientInvestigationId={patientInvestigationId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rows,
                    alert1.Type,
                    $"{rows.Count} record(s) retrieved successfully",
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

        public ServiceResult<object> GetPatientFreeTextReportForResultEntry(int patientInvestigationId)
        {
            try
            {
                _log.Info($"GetPatientFreeTextReportForResultEntry called. PatientInvestigationId={patientInvestigationId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_getPatientFreeTextReportForResultEntry",
                    CommandType.StoredProcedure,
                    new { @patientInvestigationId = patientInvestigationId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No free text report found for PatientInvestigationId={patientInvestigationId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Return raw data without manual mapping
                var rows = dataTable.AsEnumerable()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        ))
                    .ToList<object>();

                _log.Info($"Retrieved {rows.Count} free text report row(s) for PatientInvestigationId={patientInvestigationId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rows,
                    alert1.Type,
                    $"{rows.Count} record(s) retrieved successfully",
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

        public ServiceResult<object> GetAllInvestigationNameOfPatient(int branchId, string uhid, int labNo, int labTypeId, int visitId)
        {
            try
            {
                _log.Info($"GetAllInvestigationNameOfPatient called. BranchId={branchId}, UHID={uhid}, LabNo={labNo}, LabTypeId={labTypeId}, VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetAllInvestigationNameOfPatient",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @uhid = uhid,
                        @labTypeId = labTypeId,
                        @labNo = labNo,
                        @visitId = visitId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alertNotFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No investigation names found for LabNo={labNo}, LabTypeId={labTypeId}, VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alertNotFound.Type,
                        alertNotFound.Message,
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (DataColumn col in dataTable.Columns)
                    {
                        dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                    }
                    return dict;
                }).ToList();

                _log.Info($"Retrieved {result.Count} investigation name record(s) for LabNo={labNo}, LabTypeId={labTypeId}, VisitId={visitId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert.Type,
                    $"{result.Count} record(s) retrieved successfully",
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


        public ServiceResult<string> SavePatientTabularReport(SavePatientTabularReportRequest request, AllGlobalValues globalValues)
        {
                SqlConnection con = null;
                SqlTransaction tnx = null;
                try
                {
                    _log.Info($"SavePatientTabularReport called. PatientInvestigationId={request.PatientInvestigationId}, InvestigationId={request.InvestigationId}, TabularReport Count={request.TabularReport?.Count ?? 0}");

                    var connectionString = _configuration.GetConnectionString("ConnectionString");
                    if (string.IsNullOrEmpty(connectionString))
                        throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                    con = new SqlConnection(connectionString);
                    con.Open();
                    tnx = CustomSqlHelper.getSqlTransaction(con);

                    // Step 1: Update investigation details (mark result done, save comment, abnormal flag)
                    _sqlHelper.DML(tnx, "U_PatientInvestigationDetailsForLabResults", CommandType.StoredProcedure, new
                {
                    @patientInvestigationId = request.PatientInvestigationId,
                    @investigationComments = request.InvestigationComments ?? (object)DBNull.Value,
                    @isAbnormalResults = request.IsAbnormalResult,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });

                // Step 2: Save each tabular result row
                if (request.TabularReport != null && request.TabularReport.Any())
                {
                    foreach (var r in request.TabularReport)
                    {
                        _sqlHelper.DML(tnx, "IU_PatientInvestigationResultsTabularReports", CommandType.StoredProcedure, new
                        {
                            @patientInvestigationId = request.PatientInvestigationId,
                            @observationId = r.ObservationId,
                            @resultValue = r.ResultValue ?? (object)DBNull.Value,
                            @minValue = r.MinValue ?? (object)DBNull.Value,
                            @maxValue = r.MaxValue ?? (object)DBNull.Value,
                            @displayRange = r.DisplayRange ?? (object)DBNull.Value,
                            @unit = r.Unit ?? (object)DBNull.Value,
                            @machineResult = r.MachineResult ?? (object)DBNull.Value,
                            @machineDisplayRange = r.MachineDisplayRange ?? (object)DBNull.Value,
                            @machineUnit = r.MachineUnit ?? (object)DBNull.Value,
                            @sampleRemark = r.SampleRemark ?? (object)DBNull.Value,
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress,
                            @isHeader = r.IsHeader,
                            @isResultBold = r.IsResultBold
                        });
                    }
                }

                tnx.Commit();
                _log.Info($"TabularReport saved successfully for PatientInvestigationId={request.PatientInvestigationId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Results saved successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                if (tnx != null)
                {
                    try { tnx.Rollback(); _log.Error("Transaction rolled back due to error"); }
                    catch (Exception rollbackEx) { _log.Error($"Rollback error: {rollbackEx.Message}"); }
                }

                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<string> SavePatientFreeTextReport(SavePatientFreeTextReportRequest request, AllGlobalValues globalValues)
        {
           
                SqlConnection con = null;
                SqlTransaction tnx = null;
                try
                {
                    _log.Info($"SavePatientFreeTextReport called. PatientInvestigationId={request.PatientInvestigationId}, InvestigationId={request.InvestigationId}, TemplateId={request.TemplateId}");

                    var connectionString = _configuration.GetConnectionString("ConnectionString");
                    if (string.IsNullOrEmpty(connectionString))
                        throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                    con = new SqlConnection(connectionString);
                    con.Open();
                    tnx = CustomSqlHelper.getSqlTransaction(con);

                    // Step 1: Update investigation details
                    _sqlHelper.DML(tnx, "U_PatientInvestigationDetailsForLabResults", CommandType.StoredProcedure, new
                {
                    @patientInvestigationId = request.PatientInvestigationId,
                    @investigationComments = request.InvestigationComments ?? (object)DBNull.Value,
                    @isAbnormalResults = request.IsAbnormalResult,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });

                // Step 2: Save free text result
                _sqlHelper.DML(tnx, "IU_PatientInvestigationResultsFreeTextReports", CommandType.StoredProcedure, new
                {
                    @patientInvestigationId = request.PatientInvestigationId,
                    @resultValue = request.ResultValue ?? (object)DBNull.Value,
                    @templateId = request.TemplateId,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });

                tnx.Commit();
                _log.Info($"FreeTextReport saved successfully for PatientInvestigationId={request.PatientInvestigationId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Results saved successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                if (tnx != null)
                {
                    try { tnx.Rollback(); _log.Error("Transaction rolled back due to error"); }
                    catch (Exception rollbackEx) { _log.Error($"Rollback error: {rollbackEx.Message}"); }
                }

                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        //public ServiceResult<string> CreateUpdateInvastigationTemplateCommentMaster(List<InvastigationTemplateCommentMasterRequest> request, AllGlobalValues globalValues)
        //{
        //    try
        //    {
        //        if (request == null || !request.Any())
        //        {
        //            var invalidAlert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
        //            return ServiceResult<string>.Failure(invalidAlert.Type, "Request data is required", 400);
        //        }

        //        var item = request[0];
        //        _log.Info($"CreateUpdateInvastigationTemplateCommentMaster called. Id={item.Id}, TypeId={item.TypeId}, Type={item.Type}");

        //        var result = _sqlHelper.DML(
        //            "IU_InvastigationTemplateCommentMaster",
        //            CommandType.StoredProcedure,
        //            new
        //            {
        //                @Id = item.Id,
        //                @TypeId = item.TypeId,
        //                @Type = item.Type,
        //                @Name = item.Name,
        //                @ContentValue = item.ContentValue,
        //                @IsActive = item.IsActive,
        //                @UserId = globalValues.userId,
        //                @IpAddress = globalValues.ipAddress
        //            },
        //            new { result = 0 }
        //        );

        //        _distributedCache.Remove("_InvestigationTemplateComments_All");
        //        _log.Info("Cleared cache for all investigation template comments.");

        //        if (result <= 0)
        //        {
        //            var duplicateAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
        //            return ServiceResult<string>.Failure(duplicateAlert.Type, $"{item.Type} name already exists", 409);
        //        }

        //        var alert = _messageService.GetMessageAndTypeByAlertCode(item.Id > 0 ? "DATA_UPDATED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY");
        //        var successMessage = item.Id > 0 ? $"{item.Type} updated successfully" : $"{item.Type} added successfully";
        //        return ServiceResult<string>.Success(successMessage, alert.Type, successMessage, item.Id > 0 ? 200 : 201);
        //    }
        //    catch (Exception ex)
        //    {
        //        LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
        //        var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
        //        return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
        //    }
        //}

        public ServiceResult<object> CreateUpdateInvastigationTemplateCommentMaster(List<InvastigationTemplateCommentMasterRequest> request, AllGlobalValues globalValues)
        {
            try
            {
                if (request == null || !request.Any())
                {
                    var invalidAlert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(invalidAlert.Type, "Request data is required", 400);
                }

                var item = request[0];
                _log.Info($"CreateUpdateInvastigationTemplateCommentMaster called. Id={item.Id}, TypeId={item.TypeId}, Type={item.Type}");

                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@Id", item.Id),
            new SqlParameter("@TypeId", item.TypeId),
            new SqlParameter("@Type", item.Type),
            new SqlParameter("@Name", item.Name),
            new SqlParameter("@ContentValue", item.ContentValue),
            new SqlParameter("@IsActive", item.IsActive),
            new SqlParameter("@UserId", globalValues.userId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                _sqlHelper.RunProcedure("IU_InvastigationTemplateCommentMaster", parameters);

                int result = parameters[8].Value != DBNull.Value
                    ? Convert.ToInt32(parameters[8].Value)
                    : 0;

                _distributedCache.Remove("_InvestigationTemplateComments_All");
                _log.Info("Cleared cache for all investigation template comments.");

                if (result == -1)
                {
                    var duplicateAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    return ServiceResult<object>.Failure(duplicateAlert.Type, $"{item.Type} name already exists", 409);
                }

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode(item.Id > 0 ? "DATA_UPDATED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY");
                    var successMessage = item.Id > 0 ? $"{item.Type} updated successfully" : $"{item.Type} added successfully";
                    return ServiceResult<object>.Success(new { itemId = result }, alert.Type, successMessage, item.Id > 0 ? 200 : 201);
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }


        public ServiceResult<object> GetInvastigationTemplateCommentMaster(int id, int typeId)
        {
            try
            {
                _log.Info($"GetInvastigationTemplateCommentMaster called. Id={id}, TypeId={typeId}");

                var dataSet = _sqlHelper.GetDataSet(
                    "S_InvastigationTemplateCommentMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @Id = id,
                        @TypeId = typeId
                    }
                );

                if (dataSet == null || dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 404);
                }

                var rows = ConvertDataTableToRawData(dataSet.Tables[0]);
                return ServiceResult<object>.Success(rows, "Info", $"{rows.Count} record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetAllInvestigationTemplateComments(int? isActive = null, int? typeId = null)
        {
            try
            {
                const string cacheKey = "_InvestigationTemplateComments_All";
                _log.Info($"GetAllInvestigationTemplateComments called. IsActive={isActive?.ToString() ?? "All"}, TypeId={typeId?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allRows;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"Investigation template comments retrieved from cache. Key={cacheKey}");
                    allRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"Cache miss. Fetching from database. Key={cacheKey}");
                    var dataTable = _sqlHelper.GetDataTable("S_GetAllInvestigationTemplateComments", CommandType.StoredProcedure);

                    if (dataTable == null || dataTable.Rows.Count == 0)
                    {
                        var notFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                        return ServiceResult<object>.Failure(notFound.Type, notFound.Message, 404);
                    }

                    allRows = ConvertDataTableToRawData(dataTable);
                    CacheRawData(cacheKey, allRows);
                }

                // Filter in memory
                var filtered = allRows.AsEnumerable();

                if (isActive.HasValue)
                {
                    filtered = filtered.Where(r =>
                        r.TryGetValue("IsActive", out var val) &&
                        val != null &&
                        Convert.ToInt32(val.ToString()) == isActive.Value);
                }

                if (typeId.HasValue)
                {
                    filtered = filtered.Where(r =>
                        r.TryGetValue("TypeId", out var val) &&
                        val != null &&
                        Convert.ToInt32(val.ToString()) == typeId.Value);
                }

                var result = filtered.ToList();

                if (!result.Any())
                {
                    var notFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(notFound.Type, notFound.Message, 404);
                }

                return ServiceResult<object>.Success(result, "Info", $"{result.Count} record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> CreateUpdateObservationLOVMaster(CreateUpdateObservationLOVMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateObservationLOVMaster called. LOVId={request.LOVId}, LOVName={request.LOVName}");

                var result = _sqlHelper.DML(
                    "IU_ObservationListOfValuesMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @LOVId = request.LOVId,
                        @LOVName = request.LOVName,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { @Result = 0 }
                );

                _distributedCache.Remove("_ObservationListOfValuesMaster_All");
                _log.Info("Cleared cache for observation list of values.");

                if (Convert.ToInt32(result) < 0)
                {
                    var duplicateAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    return ServiceResult<string>.Failure(duplicateAlert.Type, "LOV Name Already Exists.", 409);
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode(request.LOVId > 0 ? "DATA_UPDATED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success("LOV Saved Successfully", alert.Type, "LOV Saved Successfully", request.LOVId > 0 ? 200 : 201);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, "Server Error Found.", 500);
            }
        }

        public ServiceResult<object> GetObservationListOfValuesMaster()
        {
            try
            {
                const string cacheKey = "_ObservationListOfValuesMaster_All";
                _log.Info("GetObservationListOfValuesMaster called.");

                var cachedData = _distributedCache.GetString(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"ObservationListOfValuesMaster data retrieved from cache. Key={cacheKey}");
                    var cachedResult = JsonSerializer.Deserialize<object>(cachedData);
                    return ServiceResult<object>.Success(cachedResult, "Info", "Data retrieved successfully", 200);
                }

                var dataTable = _sqlHelper.GetDataTable("S_ObservationListOfValuesMaster", CommandType.StoredProcedure);
                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 404);
                }

                var rows = ConvertDataTableToRawData(dataTable);
                CacheRawData(cacheKey, rows);
                return ServiceResult<object>.Success(rows, "Info", $"{rows.Count} record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> SaveInvestigationTemplateInterpretationMappings(List<InvestigationTemplateInterpretationMappingRequest> mappingItems, AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                if (mappingItems == null || !mappingItems.Any())
                {
                    var invalidAlert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<string>.Failure(invalidAlert.Type, "Mapping items are required", 400);
                }

                _log.Info($"SaveInvestigationTemplateInterpretationMappings called. InvestigationId={mappingItems[0].investigationId}, TypeId={mappingItems[0].typeId}, Count={mappingItems.Count}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                _sqlHelper.DML(tnx, "D_InvestigationTemplateInterpretationMapping", CommandType.StoredProcedure, new
                {
                    @investigationId = mappingItems[0].investigationId,
                    @typeId = mappingItems[0].typeId
                });

                foreach (var item in mappingItems.Where(x => x.itemid > 0))
                {
                    _sqlHelper.DML(tnx, "I_InvestigationTemplateInterpretationMapping", CommandType.StoredProcedure, new
                    {
                        @typeId = item.typeId,
                        @type = item.type,
                        @investigationId = item.investigationId,
                        @itemId = item.itemid,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    });
                }

                tnx.Commit();
                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success("Mapping Updated Successfully", alert.Type, "Mapping Updated Successfully", 200);
            }
            catch (Exception ex)
            {
                tnx?.Rollback();
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open)
                        con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<object> GetInvestigationTemplateInterpretationMappings(int investigationId)
        {
            try
            {
                _log.Info($"GetInvestigationTemplateInterpretationMappings called. InvestigationId={investigationId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_InvestigationTemplateInterpretationMappingByInvestigationId",
                    CommandType.StoredProcedure,
                    new { @investigationId = investigationId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 404);
                }

                var rows = ConvertDataTableToRawData(dataTable);
                return ServiceResult<object>.Success(rows, "Info", $"{rows.Count} record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> SaveObservationCommentsLOVsMappings(List<ObservationCommentLOVsMappingRequest> mappingItems, AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                if (mappingItems == null || !mappingItems.Any())
                {
                    var invalidAlert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<string>.Failure(invalidAlert.Type, "Mapping items are required", 400);
                }

                _log.Info($"SaveObservationCommentsLOVsMappings called. ObservationId={mappingItems[0].observationId}, TypeId={mappingItems[0].typeId}, Count={mappingItems.Count}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                _sqlHelper.DML(tnx, "D_ObservationCommentLOVsMapping", CommandType.StoredProcedure, new
                {
                    @typeId = mappingItems[0].typeId,
                    @observationId = mappingItems[0].observationId
                });

                foreach (var item in mappingItems.Where(x => x.itemid > 0))
                {
                    _sqlHelper.DML(tnx, "I_ObservationCommentLOVsMapping", CommandType.StoredProcedure, new
                    {
                        @typeId = item.typeId,
                        @type = item.type,
                        @observationId = item.observationId,
                        @itemId = item.itemid,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    });
                }

                tnx.Commit();
                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success("Mapping Updated Successfully", alert.Type, "Mapping Updated Successfully", 200);
            }
            catch (Exception ex)
            {
                tnx?.Rollback();
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx?.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open)
                        con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<object> GetObservationCommentLOVsMappings(int observationId)
        {
            try
            {
                _log.Info($"GetObservationCommentLOVsMappings called. ObservationId={observationId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_ObservationCommentLOVsMappingByObservationId",
                    CommandType.StoredProcedure,
                    new { @observationId = observationId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 404);
                }

                var rows = ConvertDataTableToRawData(dataTable);
                return ServiceResult<object>.Success(rows, "Info", $"{rows.Count} record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        private List<Dictionary<string, object>> ConvertDataTableToRawData(DataTable dataTable)
        {
            return dataTable.AsEnumerable()
                .Select(row => dataTable.Columns.Cast<DataColumn>().ToDictionary(
                    col => col.ColumnName,
                    col => row[col] == DBNull.Value ? null : row[col]))
                .ToList();
        }

        private void CacheRawData(string cacheKey, object rows)
        {
            var serialized = JsonSerializer.Serialize(rows);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = null,
                SlidingExpiration = null
            };
            _distributedCache.SetString(cacheKey, serialized, cacheOptions);
            _log.Info($"Cached data permanently. Key={cacheKey}");
        }

        public ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForLaboratoryHelpDesk(
int branchId, int typeId, string uhid, string ipdNo, string labNo,
string fromDate, string toDate, string barCode, int subCategoryId,
int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId)
        {
            try
            {
                _log.Info($"searchPatientInvestigationForLaboratoryHelpDesk called. BranchId={branchId}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_SearchPatientInvestigationForLaboratoryHelpDesk",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @typeId = typeId,
                        @uhid = uhid,
                        @ipdNo = ipdNo,
                        @labNo = labNo,
                        @fromDate = Utility.getDateTime(fromDate).ToString("yyyy-MM-dd"),
                        @toDate = Utility.getDateTime(toDate).ToString("yyyy-MM-dd"),
                        @barCode = barCode,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @investigationId = investigationId,
                        @patientName = patientName,
                        @roleId = roleId,
                        @corporateId = corporateId,
                        @statusId = statusId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patient investigation records found.");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable rows to raw Dictionary list — no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"searchPatientInvestigationForLaboratoryHelpDesk returned {result.Count} record(s).");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} record(s) retrieved successfully",
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


        #region Histo Template Master

        public ServiceResult<CreateUpdateHistoTemplateResponse> CreateUpdateHistoTemplateMaster(
            CreateUpdateHistoTemplateRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateHistoTemplateMaster called. Id={request.Id}, TypeId={request.TypeId}, Name={request.Name}");

                var result = _sqlHelper.DML(
                    "IU_HistoTemplateMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        Id = request.Id,
                        TypeId = request.TypeId,
                        Type = request.Type,
                        Name = request.Name,
                        ContentValue = request.ContentValue,
                        IsActive = request.IsActive,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache for this TypeId
                string cacheKey = $"_HistoTemplate_Type{request.TypeId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared HistoTemplate cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate template name: {request.Name} for TypeId={request.TypeId}");
                    return ServiceResult<CreateUpdateHistoTemplateResponse>.Failure(
                        alert.Type,
                        $"{request.Type} Name already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateHistoTemplateResponse { Id = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.Id == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    _log.Info($"HistoTemplate {(request.Id == 0 ? "created" : "updated")} successfully. Id={resultValue}");
                    return ServiceResult<CreateUpdateHistoTemplateResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.Id == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"HistoTemplate operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateHistoTemplateResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateHistoTemplateResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<HistoTemplateMasterModel>> GetHistoTemplateMaster(int typeId)
        {
            try
            {
                _log.Info($"GetHistoTemplateMaster called. TypeId={typeId}");

                string cacheKey = $"_HistoTemplate_Type{typeId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<HistoTemplateMasterModel> templates;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"HistoTemplate data retrieved from cache. Key={cacheKey}");
                    templates = System.Text.Json.JsonSerializer.Deserialize<List<HistoTemplateMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"HistoTemplate cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_HistoTemplateMaster",
                        CommandType.StoredProcedure,
                        new { TypeId = typeId }
                    );

                    templates = dataTable?.AsEnumerable().Select(row => new HistoTemplateMasterModel
                    {
                        Id = row.Field<int>("Id"),
                        TypeId = row.Field<int>("Typeid"),
                        Type = row.Field<string>("Type") ?? string.Empty,
                        Name = row.Field<string>("Name") ?? string.Empty,
                        ContentValue = row.Field<string>("ContentValue") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        IpAddress = row.Field<string>("IpAddress") ?? string.Empty
                    }).ToList() ?? new List<HistoTemplateMasterModel>();

                    if (templates.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(templates);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"HistoTemplate data cached permanently. Key={cacheKey}, Count={templates.Count}");
                    }
                }

                if (!templates.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No HistoTemplate found for TypeId={typeId}");
                    return ServiceResult<IEnumerable<HistoTemplateMasterModel>>.Failure(
                        alert.Type, $"No templates found for TypeId: {typeId}", 404
                    );
                }

                _log.Info($"Retrieved {templates.Count} HistoTemplate(s) from cache");
                return ServiceResult<IEnumerable<HistoTemplateMasterModel>>.Success(
                    templates, "Info", $"{templates.Count} template(s) retrieved successfully", 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<HistoTemplateMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        #endregion

        #region Specimen Master

        public ServiceResult<CreateUpdateSpecimenMasterResponse> CreateUpdateSpecimenMaster(
            CreateUpdateSpecimenMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSpecimenMaster called. ID={request.ID}, SpecimenName={request.SpecimenName}");

                var result = _sqlHelper.DML(
                    "IU_SpecimenMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        ID = request.ID,
                        SpecimenName = request.SpecimenName,
                        IsActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear specimen master cache
                _distributedCache.Remove("_SpecimenMaster_All");
                _log.Info("Cleared SpecimenMaster cache");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate specimen name: {request.SpecimenName}");
                    return ServiceResult<CreateUpdateSpecimenMasterResponse>.Failure(
                        alert.Type, "Specimen Name Already Exists", 409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateSpecimenMasterResponse { ID = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ID == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    _log.Info($"Specimen {(request.ID == 0 ? "created" : "updated")} successfully. ID={resultValue}");
                    return ServiceResult<CreateUpdateSpecimenMasterResponse>.Success(
                        responseData, alert.Type, alert.Message, request.ID == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateSpecimenMasterResponse>.Failure(alert1.Type, alert1.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSpecimenMasterResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<SpecimenMasterModel>> GetSpecimenMaster()
        {
            try
            {
                _log.Info("GetSpecimenMaster called.");

                string cacheKey = "_SpecimenMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<SpecimenMasterModel> specimens;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SpecimenMaster data retrieved from cache. Key={cacheKey}");
                    specimens = System.Text.Json.JsonSerializer.Deserialize<List<SpecimenMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"SpecimenMaster cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getSpecimenMaster",
                        CommandType.StoredProcedure
                    );

                    specimens = dataTable?.AsEnumerable().Select(row => new SpecimenMasterModel
                    {
                        ID = row.Field<int>("ID"),
                        SpecimenName = row.Field<string>("SpecimenName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<SpecimenMasterModel>();

                    if (specimens.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(specimens);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"SpecimenMaster data cached permanently. Key={cacheKey}, Count={specimens.Count}");
                    }
                }

                if (!specimens.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<SpecimenMasterModel>>.Failure(alert.Type, "No specimens found", 404);
                }

                return ServiceResult<IEnumerable<SpecimenMasterModel>>.Success(
                    specimens, "Info", $"{specimens.Count} specimen(s) retrieved successfully", 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SpecimenMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        #endregion

        #region Specimen Mapping Master

        public ServiceResult<CreateUpdateSpecimenMappingResponse> CreateUpdateSpecimenMappingMaster(
            CreateUpdateSpecimenMappingRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSpecimenMappingMaster called. SpecimenNameId={request.SpecimenNameId}");

                var result = _sqlHelper.DML(
                    "IU_HistoSpecimenMappingMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        SpecimenNameId = request.SpecimenNameId,
                        GrossIdList = request.GrossIdList,
                        MicroscopicIdList = request.MicroscopicIdList,
                        ImpressionIdList = request.ImpressionIdList,
                        IsActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                // Clear specimen mapping cache for this specimen
                string cacheKey = $"_SpecimenMapping_{request.SpecimenNameId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared SpecimenMapping cache. Key={cacheKey}");

                var responseData = new CreateUpdateSpecimenMappingResponse { SpecimenNameId = request.SpecimenNameId };
                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                _log.Info($"Specimen mapping updated successfully. SpecimenNameId={request.SpecimenNameId}");
                return ServiceResult<CreateUpdateSpecimenMappingResponse>.Success(
                    responseData, alert1.Type, "Mapping Updated Successfully", 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSpecimenMappingResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<SpecimenMappingMasterModel> GetSpecimenMappingMaster(int specimenNameId)
        {
            try
            {
                _log.Info($"GetSpecimenMappingMaster called. SpecimenNameId={specimenNameId}");

                string cacheKey = $"_SpecimenMapping_{specimenNameId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                SpecimenMappingMasterModel mapping;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SpecimenMapping data retrieved from cache. Key={cacheKey}");
                    mapping = System.Text.Json.JsonSerializer.Deserialize<SpecimenMappingMasterModel>(cachedData);
                }
                else
                {
                    _log.Info($"SpecimenMapping cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getSpecimenMappingMaster",
                        CommandType.StoredProcedure,
                        new { SpecimenNameId = specimenNameId }
                    );

                    if (dataTable == null || dataTable.Rows.Count == 0)
                    {
                        var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                        _log.Info($"No mapping found for SpecimenNameId={specimenNameId}");
                        return ServiceResult<SpecimenMappingMasterModel>.Failure(
                            alert.Type, $"No mapping found for SpecimenNameId: {specimenNameId}", 404
                        );
                    }

                    var row = dataTable.Rows[0];
                    mapping = new SpecimenMappingMasterModel
                    {
                        SpecimenNameId = row.Field<int>("SpecimenNameId"),
                        GrossIdList = row.Field<string>("GrossIdList") ?? string.Empty,
                        MicroscopicIdList = row.Field<string>("MicroscopicIdList") ?? string.Empty,
                        ImpressionIdList = row.Field<string>("ImpressionIdList") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    };

                    var serialized = System.Text.Json.JsonSerializer.Serialize(mapping);
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = null,
                        SlidingExpiration = null
                    };
                    _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                    _log.Info($"SpecimenMapping data cached permanently. Key={cacheKey}");
                }

                return ServiceResult<SpecimenMappingMasterModel>.Success(
                    mapping, "Info", "Specimen mapping retrieved successfully", 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SpecimenMappingMasterModel>.Failure(alert.Type, alert.Message, 500);
            }
        }

        #endregion

        #region Histo Pending Reason Master

        public ServiceResult<CreateUpdateHistoPendingReasonResponse> CreateUpdateHistoPendingReasonMaster(
            CreateUpdateHistoPendingReasonRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateHistoPendingReasonMaster called. ID={request.ID}, PendingReason={request.PendingReason}");

                var result = _sqlHelper.DML(
                    "IU_HistoPendingReasonMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        ID = request.ID,
                        PendingReason = request.PendingReason,
                        IsActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                _distributedCache.Remove("_HistoPendingReason_All");
                _log.Info("Cleared HistoPendingReason cache");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate pending reason: {request.PendingReason}");
                    return ServiceResult<CreateUpdateHistoPendingReasonResponse>.Failure(
                        alert.Type, "Pending Reason Already Exists", 409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateHistoPendingReasonResponse { ID = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ID == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    _log.Info($"Pending reason {(request.ID == 0 ? "created" : "updated")} successfully. ID={resultValue}");
                    return ServiceResult<CreateUpdateHistoPendingReasonResponse>.Success(
                        responseData, alert.Type, alert.Message, request.ID == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateHistoPendingReasonResponse>.Failure(alert1.Type, alert1.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateHistoPendingReasonResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<HistoPendingReasonMasterModel>> GetHistoPendingReasonMaster()
        {
            try
            {
                _log.Info("GetHistoPendingReasonMaster called.");

                string cacheKey = "_HistoPendingReason_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<HistoPendingReasonMasterModel> reasons;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"HistoPendingReason data retrieved from cache. Key={cacheKey}");
                    reasons = System.Text.Json.JsonSerializer.Deserialize<List<HistoPendingReasonMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"HistoPendingReason cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getHistoPendingReasonMaster",
                        CommandType.StoredProcedure
                    );

                    reasons = dataTable?.AsEnumerable().Select(row => new HistoPendingReasonMasterModel
                    {
                        ID = row.Field<int>("ID"),
                        PendingReason = row.Field<string>("PendingReason") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<HistoPendingReasonMasterModel>();

                    if (reasons.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(reasons);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"HistoPendingReason data cached permanently. Key={cacheKey}, Count={reasons.Count}");
                    }
                }

                if (!reasons.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<HistoPendingReasonMasterModel>>.Failure(
                        alert.Type, "No pending reasons found", 404
                    );
                }

                return ServiceResult<IEnumerable<HistoPendingReasonMasterModel>>.Success(
                    reasons, "Info", $"{reasons.Count} pending reason(s) retrieved successfully", 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<HistoPendingReasonMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        #endregion

        #region Histo Immuno Antibiotic Master

        public ServiceResult<CreateUpdateHistoImmunoAntibioticResponse> CreateUpdateHistoImmunoAntibioticMaster(
            CreateUpdateHistoImmunoAntibioticRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateHistoImmunoAntibioticMaster called. ID={request.ID}, AntibioticName={request.AntibioticName}");

                var result = _sqlHelper.DML(
                    "IU_HistoImmunoAntibioticMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        ID = request.ID,
                        AntibioticName = request.AntibioticName,
                        IsActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                _distributedCache.Remove("_HistoImmunoAntibiotic_All");
                _log.Info("Cleared HistoImmunoAntibiotic cache");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate antibiotic name: {request.AntibioticName}");
                    return ServiceResult<CreateUpdateHistoImmunoAntibioticResponse>.Failure(
                        alert.Type, "Antibiotic Name Already Exists", 409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateHistoImmunoAntibioticResponse { ID = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ID == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    _log.Info($"Antibiotic {(request.ID == 0 ? "created" : "updated")} successfully. ID={resultValue}");
                    return ServiceResult<CreateUpdateHistoImmunoAntibioticResponse>.Success(
                        responseData, alert.Type, alert.Message, request.ID == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateHistoImmunoAntibioticResponse>.Failure(alert1.Type, alert1.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateHistoImmunoAntibioticResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<HistoImmunoAntibioticMasterModel>> GetHistoImmunoAntibioticMaster()
        {
            try
            {
                _log.Info("GetHistoImmunoAntibioticMaster called.");

                string cacheKey = "_HistoImmunoAntibiotic_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<HistoImmunoAntibioticMasterModel> antibiotics;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"HistoImmunoAntibiotic data retrieved from cache. Key={cacheKey}");
                    antibiotics = System.Text.Json.JsonSerializer.Deserialize<List<HistoImmunoAntibioticMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"HistoImmunoAntibiotic cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getHistoImmunoAntibioticMaster",
                        CommandType.StoredProcedure
                    );

                    antibiotics = dataTable?.AsEnumerable().Select(row => new HistoImmunoAntibioticMasterModel
                    {
                        ID = row.Field<int>("ID"),
                        AntibioticName = row.Field<string>("AntibioticName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<HistoImmunoAntibioticMasterModel>();

                    if (antibiotics.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(antibiotics);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"HistoImmunoAntibiotic data cached permanently. Key={cacheKey}, Count={antibiotics.Count}");
                    }
                }

                if (!antibiotics.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<HistoImmunoAntibioticMasterModel>>.Failure(
                        alert.Type, "No antibiotics found", 404
                    );
                }

                return ServiceResult<IEnumerable<HistoImmunoAntibioticMasterModel>>.Success(
                    antibiotics, "Info", $"{antibiotics.Count} antibiotic(s) retrieved successfully", 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<HistoImmunoAntibioticMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        #endregion



        // ─────────────────────────────────────────────────────────────────────
        // ORGANISM GROUP
        // ─────────────────────────────────────────────────────────────────────

        public ServiceResult<CreateUpdateOrganismGroupResponse> CreateUpdateOrganismGroup(
            CreateUpdateOrganismGroupRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateOrganismGroup called. OrganismGroupId={request.OrganismGroupId}, Name={request.OrganismGroupName}");

                var result = _sqlHelper.DML(
                    "IU_OrganismGroupMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @organismGroupId = request.OrganismGroupId,
                        @organismGroupName = request.OrganismGroupName,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate organism group name: {request.OrganismGroupName}");
                    return ServiceResult<CreateUpdateOrganismGroupResponse>.Failure(
                        alert.Type, "Organism Group Name Already Exists", 409);
                }

                // Clear cache
                _distributedCache.Remove(CACHE_ORGANISM_GROUP);
                _log.Info($"Cleared cache: {CACHE_ORGANISM_GROUP}");

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateOrganismGroupResponse { OrganismGroupId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.OrganismGroupId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"OrganismGroup {(request.OrganismGroupId == 0 ? "created" : "updated")} successfully. Id={resultValue}");
                    return ServiceResult<CreateUpdateOrganismGroupResponse>.Success(
                        responseData, alert.Type, alert.Message,
                        request.OrganismGroupId == 0 ? 201 : 200);
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateOrganismGroupResponse>.Failure(alert1.Type, alert1.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateOrganismGroupResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<OrganismGroupModel>> GetOrganismGroupList()
        {
            try
            {
                _log.Info("GetOrganismGroupList called.");

                var cachedData = _distributedCache.GetString(CACHE_ORGANISM_GROUP);
                List<OrganismGroupModel> list;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"OrganismGroup data retrieved from cache. Key={CACHE_ORGANISM_GROUP}");
                    list = JsonSerializer.Deserialize<List<OrganismGroupModel>>(cachedData);
                }
                else
                {
                    _log.Info($"OrganismGroup cache miss. Fetching from database.");

                    var dataTable = _sqlHelper.GetDataTable("S_getOrganismGroupMaster", CommandType.StoredProcedure);

                    list = dataTable?.AsEnumerable().Select(row => new OrganismGroupModel
                    {
                        OrganismGroupId = row.Field<int>("OrganismGroupId"),
                        OrganismGroupName = row.Field<string>("OrganismGroupName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<OrganismGroupModel>();

                    if (list.Any())
                    {
                        var serialized = JsonSerializer.Serialize(list);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_ORGANISM_GROUP, serialized, cacheOptions);
                        _log.Info($"OrganismGroup cached permanently. Count={list.Count}");
                    }
                }

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<OrganismGroupModel>>.Failure(alert.Type, "No organism groups found", 404);
                }

                return ServiceResult<IEnumerable<OrganismGroupModel>>.Success(
                    list, "Info", $"{list.Count} organism group(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<OrganismGroupModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ORGANISM NAME
        // ─────────────────────────────────────────────────────────────────────

        public ServiceResult<CreateUpdateOrganismNameResponse> CreateUpdateOrganismName(
            CreateUpdateOrganismNameRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateOrganismName called. OrganismNameId={request.OrganismNameId}, Name={request.OrganismName}");

                var result = _sqlHelper.DML(
                    "IU_OrganismNameMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @OrganismNameId = request.OrganismNameId,
                        @OrganismName = request.OrganismName,
                        @organismGroupId = request.OrganismGroupId,
                        @IsActive = request.IsActive,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate organism name: {request.OrganismName}");
                    return ServiceResult<CreateUpdateOrganismNameResponse>.Failure(
                        alert.Type, "Organism Name Already Exists", 409);
                }

                _distributedCache.Remove(CACHE_ORGANISM_NAME);
                _log.Info($"Cleared cache: {CACHE_ORGANISM_NAME}");

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateOrganismNameResponse { OrganismNameId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.OrganismNameId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"OrganismName {(request.OrganismNameId == 0 ? "created" : "updated")} successfully. Id={resultValue}");
                    return ServiceResult<CreateUpdateOrganismNameResponse>.Success(
                        responseData, alert.Type, alert.Message,
                        request.OrganismNameId == 0 ? 201 : 200);
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateOrganismNameResponse>.Failure(alert1.Type, alert1.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateOrganismNameResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<OrganismNameModel>> GetOrganismNameList()
        {
            try
            {
                _log.Info("GetOrganismNameList called.");

                var cachedData = _distributedCache.GetString(CACHE_ORGANISM_NAME);
                List<OrganismNameModel> list;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"OrganismName data retrieved from cache. Key={CACHE_ORGANISM_NAME}");
                    list = JsonSerializer.Deserialize<List<OrganismNameModel>>(cachedData);
                }
                else
                {
                    _log.Info("OrganismName cache miss. Fetching from database.");

                    var dataTable = _sqlHelper.GetDataTable("S_getOrganismNameMaster", CommandType.StoredProcedure);

                    list = dataTable?.AsEnumerable().Select(row => new OrganismNameModel
                    {
                        OrganismNameId = row.Field<int>("OrganismNameId"),
                        OrganismName = row.Field<string>("OrganismName") ?? string.Empty,
                        OrganismGroupId = row.Field<int>("OrganismGroupId"),
                        OrganismGroup = row.Field<string>("OrganismGroup") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<OrganismNameModel>();

                    if (list.Any())
                    {
                        var serialized = JsonSerializer.Serialize(list);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_ORGANISM_NAME, serialized, cacheOptions);
                        _log.Info($"OrganismName cached permanently. Count={list.Count}");
                    }
                }

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<OrganismNameModel>>.Failure(alert.Type, "No organism names found", 404);
                }

                return ServiceResult<IEnumerable<OrganismNameModel>>.Success(
                    list, "Info", $"{list.Count} organism name(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<OrganismNameModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ANTIBIOTIC GROUP
        // ─────────────────────────────────────────────────────────────────────

        public ServiceResult<CreateUpdateAntibioticGroupResponse> CreateUpdateAntibioticGroup(
            CreateUpdateAntibioticGroupRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateAntibioticGroup called. AntibioticGroupId={request.AntibioticGroupId}, Name={request.AntibioticGroupName}");

                var result = _sqlHelper.DML(
                    "IU_AntibioticGroupMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @AntibioticGroupId = request.AntibioticGroupId,
                        @AntibioticGroupName = request.AntibioticGroupName,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate antibiotic group name: {request.AntibioticGroupName}");
                    return ServiceResult<CreateUpdateAntibioticGroupResponse>.Failure(
                        alert.Type, "Antibiotic Group Name Already Exists", 409);
                }

                _distributedCache.Remove(CACHE_ANTIBIOTIC_GROUP);
                _log.Info($"Cleared cache: {CACHE_ANTIBIOTIC_GROUP}");

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateAntibioticGroupResponse { AntibioticGroupId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.AntibioticGroupId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"AntibioticGroup {(request.AntibioticGroupId == 0 ? "created" : "updated")} successfully. Id={resultValue}");
                    return ServiceResult<CreateUpdateAntibioticGroupResponse>.Success(
                        responseData, alert.Type, alert.Message,
                        request.AntibioticGroupId == 0 ? 201 : 200);
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateAntibioticGroupResponse>.Failure(alert1.Type, alert1.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateAntibioticGroupResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<AntibioticGroupModel>> GetAntibioticGroupList()
        {
            try
            {
                _log.Info("GetAntibioticGroupList called.");

                var cachedData = _distributedCache.GetString(CACHE_ANTIBIOTIC_GROUP);
                List<AntibioticGroupModel> list;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"AntibioticGroup data retrieved from cache. Key={CACHE_ANTIBIOTIC_GROUP}");
                    list = JsonSerializer.Deserialize<List<AntibioticGroupModel>>(cachedData);
                }
                else
                {
                    _log.Info("AntibioticGroup cache miss. Fetching from database.");

                    var dataTable = _sqlHelper.GetDataTable("S_getAntibioticGroupMaster", CommandType.StoredProcedure);

                    list = dataTable?.AsEnumerable().Select(row => new AntibioticGroupModel
                    {
                        AntibioticGroupId = row.Field<int>("AntibioticGroupId"),
                        AntibioticGroupName = row.Field<string>("AntibioticGroupName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<AntibioticGroupModel>();

                    if (list.Any())
                    {
                        var serialized = JsonSerializer.Serialize(list);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_ANTIBIOTIC_GROUP, serialized, cacheOptions);
                        _log.Info($"AntibioticGroup cached permanently. Count={list.Count}");
                    }
                }

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<AntibioticGroupModel>>.Failure(alert.Type, "No antibiotic groups found", 404);
                }

                return ServiceResult<IEnumerable<AntibioticGroupModel>>.Success(
                    list, "Info", $"{list.Count} antibiotic group(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<AntibioticGroupModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ANTIBIOTIC NAME
        // ─────────────────────────────────────────────────────────────────────

        public ServiceResult<CreateUpdateAntibioticNameResponse> CreateUpdateAntibioticName(
            CreateUpdateAntibioticNameRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateAntibioticName called. AntibioticNameId={request.AntibioticNameId}, Name={request.AntibioticName}");

                var result = _sqlHelper.DML(
                    "IU_AntibioticNameMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @AntibioticNameId = request.AntibioticNameId,
                        @AntibioticName = request.AntibioticName,
                        @AntibioticGroupId = request.AntibioticGroupId,
                        @IsActive = request.IsActive,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate antibiotic name: {request.AntibioticName}");
                    return ServiceResult<CreateUpdateAntibioticNameResponse>.Failure(
                        alert.Type, "Antibiotic Name Already Exists", 409);
                }

                _distributedCache.Remove(CACHE_ANTIBIOTIC_NAME);
                _log.Info($"Cleared cache: {CACHE_ANTIBIOTIC_NAME}");

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateAntibioticNameResponse { AntibioticNameId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.AntibioticNameId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"AntibioticName {(request.AntibioticNameId == 0 ? "created" : "updated")} successfully. Id={resultValue}");
                    return ServiceResult<CreateUpdateAntibioticNameResponse>.Success(
                        responseData, alert.Type, alert.Message,
                        request.AntibioticNameId == 0 ? 201 : 200);
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateAntibioticNameResponse>.Failure(alert1.Type, alert1.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateAntibioticNameResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<AntibioticNameModel>> GetAntibioticNameList()
        {
            try
            {
                _log.Info("GetAntibioticNameList called.");

                var cachedData = _distributedCache.GetString(CACHE_ANTIBIOTIC_NAME);
                List<AntibioticNameModel> list;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"AntibioticName data retrieved from cache. Key={CACHE_ANTIBIOTIC_NAME}");
                    list = JsonSerializer.Deserialize<List<AntibioticNameModel>>(cachedData);
                }
                else
                {
                    _log.Info("AntibioticName cache miss. Fetching from database.");

                    var dataTable = _sqlHelper.GetDataTable("S_getAntibioticNameMaster", CommandType.StoredProcedure);

                    list = dataTable?.AsEnumerable().Select(row => new AntibioticNameModel
                    {
                        AntibioticNameId = row.Field<int>("AntibioticNameId"),
                        AntibioticName = row.Field<string>("AntibioticName") ?? string.Empty,
                        AntibioticGroupId = row.Field<int>("AntibioticGroupId"),
                        AntibioticGroup = row.Field<string>("AntibioticGroup") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<AntibioticNameModel>();

                    if (list.Any())
                    {
                        var serialized = JsonSerializer.Serialize(list);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_ANTIBIOTIC_NAME, serialized, cacheOptions);
                        _log.Info($"AntibioticName cached permanently. Count={list.Count}");
                    }
                }

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<AntibioticNameModel>>.Failure(alert.Type, "No antibiotic names found", 404);
                }

                return ServiceResult<IEnumerable<AntibioticNameModel>>.Success(
                    list, "Info", $"{list.Count} antibiotic name(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<AntibioticNameModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // MICRO TEMPLATE
        // ─────────────────────────────────────────────────────────────────────

        public ServiceResult<CreateUpdateMicroTemplateResponse> CreateUpdateMicroTemplate(
            CreateUpdateMicroTemplateRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateMicroTemplate called. Id={request.Id}, TypeId={request.TypeId}, Name={request.Name}");

                var result = _sqlHelper.DML(
                    "IU_MicroTemplateMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @Id = request.Id,
                        @TypeId = request.TypeId,
                        @Type = request.Type,
                        @Name = request.Name,
                        @ContentValue = request.ContentValue ?? (object)DBNull.Value,
                        @IsActive = request.IsActive,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate micro template name: {request.Name} for TypeId={request.TypeId}");
                    return ServiceResult<CreateUpdateMicroTemplateResponse>.Failure(
                        alert.Type, $"{request.Type} Name Already Exists", 409);
                }

                // Clear dynamic cache key for this typeId
                string cacheKey = $"_Lab_MicroTemplate_Type{request.TypeId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache: {cacheKey}");

                if (resultValue > 0 || request.Id > 0)
                {
                    var responseData = new CreateUpdateMicroTemplateResponse { Id = resultValue > 0 ? resultValue : request.Id };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.Id == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"MicroTemplate {(request.Id == 0 ? "created" : "updated")} successfully. Id={responseData.Id}");
                    return ServiceResult<CreateUpdateMicroTemplateResponse>.Success(
                        responseData, alert.Type, $"{request.Type} {alert.Message}",
                        request.Id == 0 ? 201 : 200);
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateMicroTemplateResponse>.Failure(alert1.Type, alert1.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateMicroTemplateResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<MicroTemplateMasterModel>> GetMicroTemplateList(int typeId)
        {
            try
            {
                _log.Info($"GetMicroTemplateList called. TypeId={typeId}");

                string cacheKey = $"_Lab_MicroTemplate_Type{typeId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<MicroTemplateMasterModel> list;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"MicroTemplate data retrieved from cache. Key={cacheKey}");
                    list = JsonSerializer.Deserialize<List<MicroTemplateMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"MicroTemplate cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_MicroTemplateMaster",
                        CommandType.StoredProcedure,
                        new { @Typeid = typeId });

                    list = dataTable?.AsEnumerable().Select(row => new MicroTemplateMasterModel
                    {
                        Id = row.Field<int>("Id"),
                        TypeId = row.Field<int>("Typeid"),
                        Type = row.Field<string>("Type") ?? string.Empty,
                        Name = row.Field<string>("Name") ?? string.Empty,
                        ContentValue = row.Field<string>("ContentValue") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        IpAddress = row.Field<string>("IpAddress") ?? string.Empty
                    }).ToList() ?? new List<MicroTemplateMasterModel>();

                    if (list.Any())
                    {
                        var serialized = JsonSerializer.Serialize(list);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"MicroTemplate cached permanently. Key={cacheKey}, Count={list.Count}");
                    }
                }

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<MicroTemplateMasterModel>>.Failure(
                        alert.Type, $"No micro templates found for TypeId: {typeId}", 404);
                }

                return ServiceResult<IEnumerable<MicroTemplateMasterModel>>.Success(
                    list, "Info", $"{list.Count} template(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<MicroTemplateMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // MICRO MAPPING
        // ─────────────────────────────────────────────────────────────────────

        public ServiceResult<string> CreateUpdateMicroMapping(
            CreateUpdateMicroMappingRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateMicroMapping called. OrganismId={request.OrganismId}, Items={request.MicroMappings?.Count}");

                // Step 1: Delete existing mappings for this organism
                _sqlHelper.DML(
                    "D_deleteMicroMappingMaster",
                    CommandType.StoredProcedure,
                    new { @OrganismId = request.OrganismId });

                _log.Info($"Deleted existing micro mappings for OrganismId={request.OrganismId}");

                // Step 2: Insert new mappings
                int insertedCount = 0;
                if (request.MicroMappings != null && request.MicroMappings.Any())
                {
                    foreach (var m in request.MicroMappings)
                    {
                        _sqlHelper.DML(
                            "I_MicroMappingMaster",
                            CommandType.StoredProcedure,
                            new
                            {
                                @hospId = globalValues.hospId,
                                @OrganismId = request.OrganismId,
                                @OrganismName = m.OrganismName,
                                @AntibioticName = m.AntibioticName,
                                @AntibioticNameId = m.AntibioticNameId,
                                @AntibioticClassName = m.AntibioticClassName,
                                @BreakPoint = m.BreakPoint ?? (object)DBNull.Value,
                                @SDD = m.SDD ?? (object)DBNull.Value,
                                @RefRangeI = m.RefRangeI ?? (object)DBNull.Value,
                                @RefRangeS = m.RefRangeS ?? (object)DBNull.Value,
                                @RefRangeR = m.RefRangeR ?? (object)DBNull.Value,
                                @Resistant = m.Resistant ?? (object)DBNull.Value,
                                @AntibioticIdList = request.AntibioticIdList,
                                @AntibioticClassId = request.AntibioticClassId,
                                @userId = globalValues.userId,
                                @IpAddress = globalValues.ipAddress
                            });

                        insertedCount++;
                    }
                }

                // Clear dynamic cache key for this organism
                string cacheKey = $"_Lab_MicroMapping_Organism{request.OrganismId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache: {cacheKey}. Inserted {insertedCount} mappings.");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"Mapping Updated Successfully. {insertedCount} record(s) inserted.",
                    alert1.Type, alert1.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<MicroMappingModel>> GetMicroMappingByOrganismId(int organismId)
        {
            try
            {
                _log.Info($"GetMicroMappingByOrganismId called. OrganismId={organismId}");

                string cacheKey = $"_Lab_MicroMapping_Organism{organismId}";
                var cachedData = _distributedCache.GetString(cacheKey);
                List<MicroMappingModel> list;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"MicroMapping data retrieved from cache. Key={cacheKey}");
                    list = JsonSerializer.Deserialize<List<MicroMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"MicroMapping cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getMicroMappingMaster",
                        CommandType.StoredProcedure,
                        new { @OrganismId = organismId });

                    list = dataTable?.AsEnumerable().Select(row => new MicroMappingModel
                    {
                        AntibioticIdList = row.Field<string>("AntibioticIdList") ?? string.Empty,
                        OrganismId = row.Field<int>("OrganismId"),
                        OrganismName = row.Field<string>("OrganismName") ?? string.Empty,
                        AntibioticName = row.Field<string>("AntibioticName") ?? string.Empty,
                        AntibioticNameId = row.Field<int>("AntibioticNameId"),
                        AntibioticClassName = row.Field<string>("AntibioticClassName") ?? string.Empty,
                        AntibioticClassId = row.Field<int>("AntibioticClassId"),
                        BreakPoint = row.Field<string>("BreakPoint") ?? string.Empty,
                        SDD = row.Field<string>("SDD") ?? string.Empty,
                        RefRangeI = row.Field<string>("RefRangeI") ?? string.Empty,
                        RefRangeS = row.Field<string>("RefRangeS") ?? string.Empty,
                        RefRangeR = row.Field<string>("RefRangeR") ?? string.Empty,
                        Resistant = row.Field<string>("Resistant") ?? string.Empty
                    }).ToList() ?? new List<MicroMappingModel>();

                    if (list.Any())
                    {
                        var serialized = JsonSerializer.Serialize(list);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"MicroMapping cached permanently. Key={cacheKey}, Count={list.Count}");
                    }
                }

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<MicroMappingModel>>.Failure(
                        alert.Type, $"No micro mapping found for OrganismId: {organismId}", 404);
                }

                return ServiceResult<IEnumerable<MicroMappingModel>>.Success(
                    list, "Info", $"{list.Count} mapping(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<MicroMappingModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForSampleProcessingHisto(
 int branchId, int typeId, string uhid, string ipdNo, string labNo,
 string fromDate, string toDate, string barCode, int subCategoryId,
 int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId, int canSampleCollect)
        {
            try
            {
                _log.Info($"searchPatientInvestigationForSampleProcessingHisto called. BranchId={branchId}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_SearchPatientInvestigationForSampleProcessingHisto",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @typeId = typeId,
                        @uhid = uhid,
                        @ipdNo = ipdNo,
                        @labNo = labNo,
                        @fromDate = Utility.getDateTime(fromDate).ToString("yyyy-MM-dd"),
                        @toDate = Utility.getDateTime(toDate).ToString("yyyy-MM-dd"),
                        @barCode = barCode,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @investigationId = investigationId,
                        @patientName = patientName,
                        @roleId = roleId,
                        @corporateId = corporateId,
                        @statusId = statusId,
                        @canSampleCollect = canSampleCollect
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patient investigation records found.");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable rows to raw Dictionary list — no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"searchPatientInvestigationForSampleProcessingHisto returned {result.Count} record(s).");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} record(s) retrieved successfully",
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



        public ServiceResult<IEnumerable<Dictionary<string, object>>> searchPatientInvestigationForSampleProcessingMicro(
 int branchId, int typeId, string uhid, string ipdNo, string labNo,
 string fromDate, string toDate, string barCode, int subCategoryId,
 int subSubCategoryId, int investigationId, string patientName, int roleId, int corporateId, int statusId, int canSampleCollect)
        {
            try
            {
                _log.Info($"searchPatientInvestigationForSampleProcessingMicro called. BranchId={branchId}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_SearchPatientInvestigationForSampleProcessingMicro",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @typeId = typeId,
                        @uhid = uhid,
                        @ipdNo = ipdNo,
                        @labNo = labNo,
                        @fromDate = Utility.getDateTime(fromDate).ToString("yyyy-MM-dd"),
                        @toDate = Utility.getDateTime(toDate).ToString("yyyy-MM-dd"),
                        @barCode = barCode,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @investigationId = investigationId,
                        @patientName = patientName,
                        @roleId = roleId,
                        @corporateId = corporateId,
                        @statusId = statusId,
                        @canSampleCollect = canSampleCollect
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patient investigation records found.");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable rows to raw Dictionary list — no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"searchPatientInvestigationForSampleProcessingMicro returned {result.Count} record(s).");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} record(s) retrieved successfully",
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
       
        public ServiceResult<object> GetSampleJourneyByPatientInvestigationId(int patientInvestigationId)
        {
            try
            {
                _log.Info($"GetSampleJourneyByPatientInvestigationId called. PatientInvestigationId={patientInvestigationId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetSampleJourneyByPatientInvestigationId",
                    CommandType.StoredProcedure,
                    new { @PatientInvestigationId = patientInvestigationId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No sample journey found for PatientInvestigationId={patientInvestigationId}");
                    return ServiceResult<object>.Failure(alert.Type, "No sample journey found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetSampleJourneyByPatientInvestigationId retrieved {result.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    "Sample journey retrieved successfully",
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
