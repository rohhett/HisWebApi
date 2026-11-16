using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;

namespace HISWEBAPI.Repositories.Implementations
{
    public partial class StoreRepository : IStoreRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public StoreRepository(
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

        public ServiceResult<CreateUpdateVendorMasterResponse> CreateUpdateVendorMaster(
            CreateUpdateVendorMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateVendorMaster called. VendorId={request.VendorId}, VendorName={request.VendorName}");

                SqlParameter[] parameters = new SqlParameter[]
                {
                    new SqlParameter("@hospId", globalValues.hospId),
                    new SqlParameter("@vendorId", request.VendorId),
                    new SqlParameter("@typeId", request.TypeId),
                    new SqlParameter("@type", request.Type),
                    new SqlParameter("@vendorName", request.VendorName),
                    new SqlParameter("@contactNo", request.ContactNo ?? (object)DBNull.Value),
                    new SqlParameter("@email", request.Email ?? (object)DBNull.Value),
                    new SqlParameter("@dlNo", request.DLNO ?? (object)DBNull.Value),
                    new SqlParameter("@gstinNo", request.GSTINNo ?? (object)DBNull.Value),
                    new SqlParameter("@address", request.Address ?? (object)DBNull.Value),
                    new SqlParameter("@countryId", request.CountryId ?? (object)DBNull.Value),
                    new SqlParameter("@stateId", request.StateId ?? (object)DBNull.Value),
                    new SqlParameter("@districtId", request.DistrictId ?? (object)DBNull.Value),
                    new SqlParameter("@cityId", request.CityId ?? (object)DBNull.Value),
                    new SqlParameter("@Pincode", (object?)request.Pincode ?? DBNull.Value),
                    new SqlParameter("@creditDays", (object?)request.CreditDays ?? DBNull.Value),
                    new SqlParameter("@mappingBranch", request.MappingBranch ?? (object)DBNull.Value),
                    new SqlParameter("@isActive", request.IsActive),
                    new SqlParameter("@userId", globalValues.userId),
                    new SqlParameter("@IpAddress", globalValues.ipAddress),
                    new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_VendorMaster", parameters);

                // Clear all vendor cache entries
                ClearVendorCache();

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate vendor name attempted: {request.VendorName}");
                    return ServiceResult<CreateUpdateVendorMasterResponse>.Failure(
                        alert.Type,
                        "Vendor name already exists",
                        409
                    );
                }

                if (request.VendorId == 0)
                {
                    var responseData = new CreateUpdateVendorMasterResponse { VendorId = (int)result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Vendor created successfully. VendorId={result}");
                    return ServiceResult<CreateUpdateVendorMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var responseData = new CreateUpdateVendorMasterResponse { VendorId = (int)result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Vendor updated successfully. VendorId={result}");
                    return ServiceResult<CreateUpdateVendorMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateVendorMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<VendorMasterModel>> GetVendorMasterList(
            int? vendorId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetVendorMasterList called. VendorId={vendorId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL vendors
                string cacheKey = "_VendorMaster_All";

                // Try to get all vendors from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<VendorMasterModel> allVendors;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"VendorMaster data retrieved from cache. Key={cacheKey}");
                    allVendors = System.Text.Json.JsonSerializer.Deserialize<List<VendorMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"VendorMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL vendors from database (no filter in SP call)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getdllVendorMasterList",
                        CommandType.StoredProcedure,
                        new { activeStatus = "" } // Empty string returns all
                    );

                    allVendors = dataTable?.AsEnumerable().Select(row => new VendorMasterModel
                    {
                        VendorId = row.Field<int>("VendorId"),
                        TypeId = row.Field<int?>("TypeId") ?? 0,
                        Type = row.Field<string>("Type") ?? string.Empty,
                        VendorName = row.Field<string>("VendorName") ?? string.Empty,
                        ContactNo = row.Field<string>("ContactNo") ?? string.Empty,
                        Email = row.Field<string>("Email") ?? string.Empty,
                        DLNO = row.Field<string>("DLNO") ?? string.Empty,
                        GSTINNo = row.Field<string>("GSTINNo") ?? string.Empty,
                        Address = row.Field<string>("Address") ?? string.Empty,
                        FullAddress = row.Field<string>("fullAddress") ?? string.Empty,
                        CountryId = row.Field<int?>("CountryId"),
                        StateId = row.Field<int?>("StateId"),
                        DistrictId = row.Field<int?>("DistrictId"),
                        CityId = row.Field<int?>("CityId"),
                        Pincode = row.Field<int?>("Pincode"),
                        CreditDays = row.Field<int?>("CreditDays"),
                        IsContactVerified = row.Field<int>("IsContactVerified"),
                        IsEmailVerified = row.Field<int>("IsEmailVerified"),
                        MappingBranch = row.Field<string>("MappingBranch") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn")
                    }).ToList() ?? new List<VendorMasterModel>();

                    // Store ALL vendors in cache (permanent until manually cleared)
                    if (allVendors.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allVendors);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All VendorMaster data cached permanently. Key={cacheKey}, Count={allVendors.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<VendorMasterModel> filteredVendors = allVendors;

                if (vendorId.HasValue)
                {
                    _log.Info($"Filtering cached data by VendorId: {vendorId.Value}");
                    filteredVendors = filteredVendors.Where(v => v.VendorId == vendorId.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredVendors = filteredVendors.Where(v => v.IsActive == isActive.Value).ToList();
                }

                if (!filteredVendors.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No vendors found for VendorId={vendorId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<VendorMasterModel>>.Failure(
                        alert.Type,
                        "No vendors found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredVendors.Count} vendor(s) from cache");

                return ServiceResult<IEnumerable<VendorMasterModel>>.Success(
                    filteredVendors,
                    "Info",
                    $"{filteredVendors.Count} vendor(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<VendorMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        #region Vendor Excel upload

        // New vendors from the Medical Vendor Master upload are Medical (VendorMaster TypeId 1) and always active.
        private const int VendorUploadTypeId = 1;
        private const string VendorUploadType = "Medical";
        public const int VendorUploadMaxRows = 1000;

        /// <summary>
        /// Validates the uploaded vendor rows (SaveData = false) or validates and saves them (SaveData = true).
        /// The branches (request.MappingBranch) are chosen once for all rows. The location comes only from the pincode.
        /// Saving is all-or-nothing: nothing is saved while any row has an error, and all rows go in one transaction
        /// through the same IU_VendorMaster used by the single-vendor form.
        /// </summary>
        public ServiceResult<VendorExcelUploadResponse> VendorExcelUpload(VendorExcelUploadRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var rows = request.Rows ?? new List<VendorUploadRow>();
                _log.Info($"VendorExcelUpload called. Rows={rows.Count}, SaveData={request.SaveData}, Branches={request.MappingBranch}");

                var (results, branchIds) = ValidateVendorUploadRows(rows, globalValues.hospId);

                // branches for all rows: required, every id must be an active branch of this hospital
                var chosen = (request.MappingBranch ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out int id) ? id : -1)
                    .Distinct()
                    .ToList();
                string? branchError = chosen.Count == 0 ? "Select at least one branch"
                    : chosen.Any(id => !branchIds.Contains(id)) ? "One or more selected branches are not active branches"
                    : null;
                string mappingBranch = string.Join(",", chosen);
                if (branchError == null && mappingBranch.Length > 100) branchError = "Too many branches selected";

                var response = new VendorExcelUploadResponse
                {
                    Rows = results,
                    TotalRows = results.Count,
                    ValidRows = results.Count(r => r.IsValid),
                    ErrorRows = results.Count(r => !r.IsValid)
                };

                if (!request.SaveData)
                {
                    string msg = response.ErrorRows == 0
                        ? $"All {response.TotalRows} row(s) are valid"
                        : $"{response.ErrorRows} of {response.TotalRows} row(s) have errors";
                    return ServiceResult<VendorExcelUploadResponse>.Success(response, "Info", msg, 200);
                }

                if (branchError != null || response.ErrorRows > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                    return new ServiceResult<VendorExcelUploadResponse>
                    {
                        Result = false,
                        Data = response,
                        MessageType = alert.Type,
                        Message = branchError != null
                            ? $"Nothing saved: {branchError}"
                            : $"Nothing saved: fix the {response.ErrorRows} row(s) with errors first",
                        StatusCode = 200
                    };
                }

                using (var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    con.Open();
                    using (SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con))
                    {
                        foreach (var r in results)
                        {
                            object raw = _sqlHelper.DML(tnx, "IU_VendorMaster", CommandType.StoredProcedure, new
                            {
                                hospId = globalValues.hospId,
                                typeId = VendorUploadTypeId,
                                type = VendorUploadType,
                                vendorId = 0,
                                vendorName = r.VendorName,
                                contactNo = r.ContactNo,
                                email = string.IsNullOrEmpty(r.Email) ? null : r.Email,
                                dlNo = r.DLNO,
                                gstinNo = r.GSTINNo,
                                address = string.IsNullOrEmpty(r.Address) ? null : r.Address,
                                countryId = r.CountryId,
                                stateId = r.StateId,
                                districtId = r.DistrictId,
                                cityId = r.CityId,
                                isActive = 1,
                                mappingBranch,
                                userId = globalValues.userId,
                                Pincode = string.IsNullOrEmpty(r.Pincode) ? (int?)null : int.Parse(r.Pincode),
                                IpAddress = globalValues.ipAddress,
                                creditDays = string.IsNullOrEmpty(r.CreditDays) ? (int?)null : int.Parse(r.CreditDays)
                            }, new { Result = 0 });

                            int id = raw == null || raw == DBNull.Value ? 0 : Convert.ToInt32(raw);
                            if (id <= 0)
                            {
                                // a vendor with this name was added after the rows were validated
                                tnx.Rollback();
                                r.IsValid = false;
                                r.Errors.Add(id == -1 ? "Vendor name already exists" : "Vendor could not be saved");
                                response.ValidRows--; response.ErrorRows++;
                                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                                return new ServiceResult<VendorExcelUploadResponse>
                                {
                                    Result = false,
                                    Data = response,
                                    MessageType = alert.Type,
                                    Message = $"Nothing saved: row {r.RowNo} ({r.VendorName}) - {r.Errors.Last()}",
                                    StatusCode = 200
                                };
                            }
                            r.VendorId = id;
                        }
                        tnx.Commit();
                    }
                }

                ClearVendorCache();
                response.Saved = true;
                response.SavedRows = results.Count;
                _log.Info($"VendorExcelUpload saved {response.SavedRows} vendor(s)");

                var savedAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<VendorExcelUploadResponse>.Success(
                    response, savedAlert.Type, $"{response.SavedRows} vendor(s) saved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<VendorExcelUploadResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        /// <summary>
        /// Same field rules as the single-vendor form (vendorMasterSchema + StoreController checks).
        /// Returns the checked rows and the ids of the hospital's active branches.
        /// </summary>
        private (List<VendorUploadRowResult> Rows, HashSet<int> BranchIds) ValidateVendorUploadRows(List<VendorUploadRow> rows, int hospId)
        {
            static string Clean(string? v) => System.Text.RegularExpressions.Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");

            var results = rows.Select((row, i) => new VendorUploadRowResult
            {
                RowNo = row.RowNo > 0 ? row.RowNo : i + 1,
                VendorName = Clean(row.VendorName),
                ContactNo = Clean(row.ContactNo).Replace(" ", ""),
                Email = Clean(row.Email),
                DLNO = Clean(row.DLNO),
                GSTINNo = Clean(row.GSTINNo).ToUpperInvariant(),
                CreditDays = Clean(row.CreditDays),
                Address = Clean(row.Address),
                Pincode = Clean(row.Pincode).Replace(" ", "")
            }).ToList();

            // ---- pincodes / existing vendors resolved in the DB in one call (location names are not uploaded) ----
            string json = System.Text.Json.JsonSerializer.Serialize(results.Select(r => new { r.RowNo, r.VendorName, r.Pincode }));
            DataSet ds = _sqlHelper.GetDataSet("S_ValidateVendorUpload", CommandType.StoredProcedure, new { hospId, rowsJson = json });
            var lookup = new Dictionary<int, DataRow>();
            if (ds != null && ds.Tables.Count > 0)
                foreach (DataRow dr in ds.Tables[0].Rows)
                    lookup[Convert.ToInt32(dr["RowNo"])] = dr;

            var branchIds = new HashSet<int>();
            if (ds != null && ds.Tables.Count > 1)
                foreach (DataRow dr in ds.Tables[1].Rows)
                    branchIds.Add(Convert.ToInt32(dr["BranchId"]));

            static int? IntOf(DataRow? dr, string col) =>
                dr == null || dr[col] == DBNull.Value ? null : Convert.ToInt32(dr[col]);

            // vendor names repeated inside the file
            var dupNames = results.Where(r => r.VendorName != "")
                .GroupBy(r => r.VendorName!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.RowNo)), StringComparer.OrdinalIgnoreCase);

            foreach (var r in results)
            {
                lookup.TryGetValue(r.RowNo, out DataRow? dr);
                var e = r.Errors;

                // vendor name
                if (r.VendorName == "") e.Add("Vendor Name is required");
                else if (r.VendorName!.Length > 256) e.Add("Vendor Name cannot exceed 256 characters");
                else if (dupNames.TryGetValue(r.VendorName, out string? dupRows)) e.Add($"Vendor Name repeated in rows {dupRows}");
                else if (IntOf(dr, "ExistingVendorId") is int existing) e.Add($"Vendor Name already exists (Vendor Id {existing})");

                // contact / email / licence / GSTIN
                if (r.ContactNo == "") e.Add("Contact No. is required");
                else if (!System.Text.RegularExpressions.Regex.IsMatch(r.ContactNo!, @"^\d{10}$")) e.Add("Contact No. must be 10 digits");

                if (r.Email != "")
                {
                    if (r.Email!.Length > 100) e.Add("Email cannot exceed 100 characters");
                    else if (!System.Text.RegularExpressions.Regex.IsMatch(r.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) e.Add("Email is not valid");
                }

                if (r.DLNO == "") e.Add("Drug Licence No. is required");
                else if (r.DLNO!.Length > 100) e.Add("Drug Licence No. cannot exceed 100 characters");

                if (r.GSTINNo == "") e.Add("GSTIN is required");
                else if (r.GSTINNo!.Length > 100) e.Add("GSTIN cannot exceed 100 characters");

                if (r.Address!.Length > 1000) e.Add("Address cannot exceed 1000 characters");

                // credit days 0-365 (optional)
                if (r.CreditDays != "" && !(int.TryParse(r.CreditDays, out int cd) && cd >= 0 && cd <= 365))
                    e.Add("Credit Days must be a whole number 0-365");

                // pincode (optional): 6 digits; Country/State/District/City come from Pincode Master
                if (r.Pincode != "")
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(r.Pincode!, @"^\d{6}$")) e.Add("Pincode must be 6 digits");
                    else if (IntOf(dr, "PinCityId") == null) e.Add($"Pincode {r.Pincode} not found - correct it or leave it blank");
                    else
                    {
                        r.CountryId = IntOf(dr, "PinCountryId");
                        r.StateId = IntOf(dr, "PinStateId");
                        r.DistrictId = IntOf(dr, "PinDistrictId");
                        r.CityId = IntOf(dr, "PinCityId");
                        r.Location = string.Join(", ", new[] { "PinCityName", "PinDistrictName", "PinStateName", "PinCountryName" }
                            .Select(c => dr![c]?.ToString()?.Trim()).Where(s => !string.IsNullOrEmpty(s)));
                    }
                }

                r.IsValid = e.Count == 0;
            }

            return (results, branchIds);
        }

        #endregion

        public ServiceResult<CreateUpdateManufacturerMasterResponse> CreateUpdateManufacturerMaster(
            CreateUpdateManufacturerMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateManufacturerMaster called. ManufacturerId={request.ManufacturerId}, ManufacturerName={request.ManufacturerName}");

                var result = _sqlHelper.DML(
                    "IU_ManufacturerMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        manufacturerId = request.ManufacturerId,
                        manufacturerName = request.ManufacturerName?.Trim(),
                        shortCode = request.ShortCode?.Trim(),
                        cityCountry = request.CityCountry?.Trim(),
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

                _distributedCache.Remove(ManufacturerCacheKey);
                _log.Info($"Cleared ManufacturerMaster cache. Key={ManufacturerCacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate manufacturer name: {request.ManufacturerName}");
                    return ServiceResult<CreateUpdateManufacturerMasterResponse>.Failure(
                        alert.Type,
                        "Manufacturer name already exists",
                        409
                    );
                }

                if (resultValue == 0 && request.ManufacturerId > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Manufacturer not found for update. ManufacturerId={request.ManufacturerId}");
                    return ServiceResult<CreateUpdateManufacturerMasterResponse>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                if (resultValue > 0)
                {
                    bool isNew = request.ManufacturerId == 0;
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"Manufacturer {(isNew ? "created" : "updated")} successfully. ManufacturerId={resultValue}");

                    return ServiceResult<CreateUpdateManufacturerMasterResponse>.Success(
                        new CreateUpdateManufacturerMasterResponse { ManufacturerId = resultValue },
                        alert.Type,
                        alert.Message,
                        isNew ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Manufacturer operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateManufacturerMasterResponse>.Failure(
                    failAlert.Type,
                    failAlert.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateManufacturerMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<ManufacturerMasterModel>> GetManufacturerList(
            int? manufacturerId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetManufacturerList called. ManufacturerId={manufacturerId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(ManufacturerCacheKey);
                List<ManufacturerMasterModel> allManufacturers;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"ManufacturerMaster data retrieved from cache. Key={ManufacturerCacheKey}");
                    allManufacturers = System.Text.Json.JsonSerializer.Deserialize<List<ManufacturerMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"ManufacturerMaster cache miss. Fetching all data from database. Key={ManufacturerCacheKey}");

                    var dataTable = _sqlHelper.GetDataTable("S_GetManufacturerList", CommandType.StoredProcedure);

                    allManufacturers = dataTable?.AsEnumerable().Select(row => new ManufacturerMasterModel
                    {
                        ManufacturerId = row.Field<int>("ManufacturerId"),
                        ManufacturerName = row.Field<string>("ManufacturerName") ?? string.Empty,
                        ShortCode = row.Field<string>("ShortCode") ?? string.Empty,
                        CityCountry = row.Field<string>("CityCountry") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        MedicineCount = row.Field<int>("MedicineCount"),
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                    }).ToList() ?? new List<ManufacturerMasterModel>();

                    if (allManufacturers.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allManufacturers);
                        _distributedCache.SetString(ManufacturerCacheKey, serialized, new DistributedCacheEntryOptions());
                        _log.Info($"All ManufacturerMaster data cached. Key={ManufacturerCacheKey}, Count={allManufacturers.Count}");
                    }
                }

                IEnumerable<ManufacturerMasterModel> filtered = allManufacturers;

                if (manufacturerId.HasValue)
                    filtered = filtered.Where(m => m.ManufacturerId == manufacturerId.Value);

                if (isActive.HasValue)
                    filtered = filtered.Where(m => m.IsActive == isActive.Value);

                var list = filtered.ToList();

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<ManufacturerMasterModel>>.Failure(
                        alert.Type,
                        "No manufacturers found",
                        404
                    );
                }

                return ServiceResult<IEnumerable<ManufacturerMasterModel>>.Success(
                    list,
                    "Info",
                    $"{list.Count} manufacturer(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<ManufacturerMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        private const string ManufacturerCacheKey = "_ManufacturerMaster_All";

        private const string TaxPercentageCacheKey = "_TaxPercentage_All";

        private const string DrugCategoryCacheKey = "_DrugCategoryMaster_All";

        private const string RackCacheKey = "_RackMaster_All";

        private const string PurchaseUnitCacheKey = "_PurchaseUnitMaster_All";
        private const string SaleUnitCacheKey = "_SaleUnitMaster_All";
        private const string DosageFormCacheKey = "_DosageFormMaster_All";

        // ---------------- shared helpers for the small lookup masters ----------------

        /// <summary>Runs an IU_ lookup procedure (-1 duplicate, 0 not found, >0 id) and clears its cache.</summary>
        private ServiceResult<LookupSaveResponse> SaveLookup(string procName, object parameters, int requestId,
            string cacheKey, string duplicateMessage, string entity)
        {
            try
            {
                var result = _sqlHelper.DML(procName, CommandType.StoredProcedure, parameters, new { Result = 0 });
                int resultValue = Convert.ToInt32(result);

                _distributedCache.Remove(cacheKey);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"{entity}: duplicate name");
                    return ServiceResult<LookupSaveResponse>.Failure(alert.Type, duplicateMessage, 409);
                }

                if (resultValue == 0 && requestId > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<LookupSaveResponse>.Failure(alert.Type, alert.Message, 404);
                }

                if (resultValue > 0)
                {
                    bool isNew = requestId == 0;
                    var alert = _messageService.GetMessageAndTypeByAlertCode(isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"{entity} {(isNew ? "created" : "updated")}. Id={resultValue}");
                    return ServiceResult<LookupSaveResponse>.Success(
                        new LookupSaveResponse { Id = resultValue }, alert.Type, alert.Message, isNew ? 201 : 200);
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<LookupSaveResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.SaveLookup({procName})");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<LookupSaveResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        /// <summary>Reads a parameter-less list procedure through the Redis cache and filters by IsActive in memory.</summary>
        private ServiceResult<IEnumerable<T>> GetCachedLookup<T>(string procName, string cacheKey, Func<DataRow, T> map,
            Func<T, int> isActiveOf, int? isActive, string entityPlural)
        {
            try
            {
                List<T> all;
                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    all = System.Text.Json.JsonSerializer.Deserialize<List<T>>(cachedData) ?? new List<T>();
                }
                else
                {
                    var dataTable = _sqlHelper.GetDataTable(procName, CommandType.StoredProcedure);
                    all = dataTable?.AsEnumerable().Select(map).ToList() ?? new List<T>();

                    if (all.Any())
                        _distributedCache.SetString(cacheKey, System.Text.Json.JsonSerializer.Serialize(all), new DistributedCacheEntryOptions());
                }

                var list = isActive.HasValue ? all.Where(x => isActiveOf(x) == isActive.Value).ToList() : all;

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<T>>.Failure(alert.Type, $"No {entityPlural} found", 404);
                }

                return ServiceResult<IEnumerable<T>>.Success(list, "Info", $"{list.Count} {entityPlural} retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.GetCachedLookup({procName})");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<T>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ---------------- Purchase Unit / Sale Unit / Dosage Form ----------------

        public ServiceResult<LookupSaveResponse> CreateUpdatePurchaseUnit(CreateUpdatePurchaseUnitRequest request, AllGlobalValues globalValues) =>
            SaveLookup("IU_PurchaseUnitMaster", new
            {
                hospId = globalValues.hospId,
                purchaseUnitId = request.PurchaseUnitId,
                purchaseUnitName = request.PurchaseUnitName?.Trim(),
                isActive = request.IsActive,
                userId = globalValues.userId,
                IpAddress = globalValues.ipAddress
            }, request.PurchaseUnitId, PurchaseUnitCacheKey, "Purchase unit name already exists", "PurchaseUnit");

        public ServiceResult<IEnumerable<PurchaseUnitModel>> GetPurchaseUnitList(int? isActive = null) =>
            GetCachedLookup("S_GetPurchaseUnitList", PurchaseUnitCacheKey, row => new PurchaseUnitModel
            {
                PurchaseUnitId = row.Field<int>("PurchaseUnitId"),
                PurchaseUnitName = row.Field<string>("PurchaseUnitName") ?? string.Empty,
                IsActive = row.Field<int>("IsActive"),
                ItemCount = row.Field<int>("ItemCount"),
                CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
            }, x => x.IsActive, isActive, "purchase units");

        public ServiceResult<LookupSaveResponse> CreateUpdateSaleUnit(CreateUpdateSaleUnitRequest request, AllGlobalValues globalValues) =>
            SaveLookup("IU_SaleUnitMaster", new
            {
                hospId = globalValues.hospId,
                saleUnitId = request.SaleUnitId,
                saleUnitName = request.SaleUnitName?.Trim(),
                isActive = request.IsActive,
                userId = globalValues.userId,
                IpAddress = globalValues.ipAddress
            }, request.SaleUnitId, SaleUnitCacheKey, "Sale unit name already exists", "SaleUnit");

        public ServiceResult<IEnumerable<SaleUnitModel>> GetSaleUnitList(int? isActive = null) =>
            GetCachedLookup("S_GetSaleUnitList", SaleUnitCacheKey, row => new SaleUnitModel
            {
                SaleUnitId = row.Field<int>("SaleUnitId"),
                SaleUnitName = row.Field<string>("SaleUnitName") ?? string.Empty,
                IsActive = row.Field<int>("IsActive"),
                ItemCount = row.Field<int>("ItemCount"),
                CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
            }, x => x.IsActive, isActive, "sale units");

        public ServiceResult<LookupSaveResponse> CreateUpdateDosageForm(CreateUpdateDosageFormRequest request, AllGlobalValues globalValues) =>
            SaveLookup("IU_DosageFormMaster", new
            {
                hospId = globalValues.hospId,
                dosageFormId = request.DosageFormId,
                dosageFormName = request.DosageFormName?.Trim(),
                description = request.Description?.Trim(),
                isActive = request.IsActive,
                userId = globalValues.userId,
                IpAddress = globalValues.ipAddress
            }, request.DosageFormId, DosageFormCacheKey, "Dosage form already exists", "DosageForm");

        public ServiceResult<IEnumerable<DosageFormModel>> GetDosageFormList(int? isActive = null) =>
            GetCachedLookup("S_GetDosageFormList", DosageFormCacheKey, row => new DosageFormModel
            {
                DosageFormId = row.Field<int>("DosageFormId"),
                DosageFormName = row.Field<string>("DosageFormName") ?? string.Empty,
                Description = row.Field<string>("Description") ?? string.Empty,
                IsActive = row.Field<int>("IsActive"),
                CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
            }, x => x.IsActive, isActive, "dosage forms");

        // ---------------- Store Item Master ----------------

        public ServiceResult<CreateUpdateStoreItemMasterResponse> CreateUpdateStoreItemMaster(
            CreateUpdateStoreItemMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateStoreItemMaster called. ServiceItemId={request.ServiceItemId}, Name={request.Name}, CategoryId={request.CategoryId}");

                var result = _sqlHelper.DML(
                    "IU_StoreItemMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        serviceItemId = request.ServiceItemId,
                        categoryId = request.CategoryId,
                        subCategoryId = request.SubCategoryId,
                        subSubCategoryId = request.SubSubCategoryId,
                        name = request.Name?.Trim(),
                        code = request.Code?.Trim(),
                        descriptionId = request.DescriptionId,
                        strength = request.Strength?.Trim(),
                        snomedCode = request.SnomedCode?.Trim(),
                        manufacturerId = request.ManufacturerId,
                        dosageFormId = request.DosageFormId,
                        drugCategoryId = request.DrugCategoryId,
                        barcode = request.Barcode?.Trim(),
                        packSize = request.PackSize?.Trim(),
                        purchaseUnitId = request.PurchaseUnitId,
                        saleUnitId = request.SaleUnitId,
                        packing = request.Packing,
                        hsnCode = request.HsnCode?.Trim(),
                        commodityCode = request.CommodityCode?.Trim(),
                        gstPer = request.GstPer,
                        drugSchedule = request.DrugSchedule?.Trim(),
                        rackId = request.RackId,
                        isColdChain = request.IsColdChain,
                        expirable = request.Expirable,
                        vedType = request.VedType?.Trim(),
                        minLevel = request.MinLevel,
                        reorderLevel = request.ReorderLevel,
                        maxLevel = request.MaxLevel,
                        doctorId = request.DoctorId,
                        isKitTypeItem = request.IsKitTypeItem,
                        isActive = request.IsActive,
                        defaultStoreRoleId = request.DefaultStoreRoleId,
                        expiryAlertDays = request.ExpiryAlertDays,
                        isFEFOIssue = request.IsFEFOIssue,
                        isBatchMandatory = request.IsBatchMandatory,
                        allowExpiredIssue = request.AllowExpiredIssue,
                        itemClass = request.ItemClass?.Trim(),
                        storageCondition = request.StorageCondition?.Trim(),
                        isSterile = request.IsSterile,
                        isSingleUse = request.IsSingleUse,
                        isRecallItem = request.IsRecallItem,
                        isRestrictedItem = request.IsRestrictedItem,
                        isTrackBatch = request.IsTrackBatch,
                        isBarcodeApplicable = request.IsBarcodeApplicable,
                        isStockable = request.IsStockable,
                        isCapitalItem = request.IsCapitalItem,
                        isHighRisk = request.IsHighRisk,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new { Result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                // item counts shown in the lookup masters change with every item save
                foreach (var key in new[] { ManufacturerCacheKey, "_SaltNameMaster_AllRows", TaxPercentageCacheKey, DrugCategoryCacheKey,
                                            PurchaseUnitCacheKey, SaleUnitCacheKey })
                    _distributedCache.Remove(key);

                if (resultValue < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode(resultValue == -4 ? "INVALID_PARAMETER" : "RECORD_ALREADY_EXISTS");
                    string message = resultValue switch
                    {
                        -1 => "Store Item Name already exists in this Sub-Sub Group",
                        -2 => "Store Item Code already exists",
                        -3 => "Salt / Generic, Dosage Form, HSN Code and GST Slab are required for Medical items",
                        -4 => "You do not have the right to change Store Type, Sub Group or Sub-Sub Group of a saved item",
                        -5 => "Store Type must be Medical, General or Blood Bank Inventory",
                        -6 => "Sub Group / Sub-Sub Group does not belong to the selected Store Type",
                        _ => alert.Message
                    };
                    int status = resultValue switch { -1 or -2 => 409, -4 => 403, _ => 400 };
                    _log.Warn($"Store item save rejected ({resultValue}): {message}");
                    return ServiceResult<CreateUpdateStoreItemMasterResponse>.Failure(alert.Type, message, status);
                }

                if (resultValue == 0 && request.ServiceItemId > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<CreateUpdateStoreItemMasterResponse>.Failure(alert.Type, alert.Message, 404);
                }

                if (resultValue > 0)
                {
                    bool isNew = request.ServiceItemId == 0;
                    var alert = _messageService.GetMessageAndTypeByAlertCode(isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Store item {(isNew ? "created" : "updated")}. ServiceItemId={resultValue}");
                    return ServiceResult<CreateUpdateStoreItemMasterResponse>.Success(
                        new CreateUpdateStoreItemMasterResponse { ServiceItemId = resultValue },
                        alert.Type,
                        isNew ? "Store Item Saved Successfully" : "Store Item Updated Successfully",
                        isNew ? 201 : 200);
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateStoreItemMasterResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateStoreItemMasterResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<StoreItemListModel>> GetStoreItemList(string? search = null, int? categoryId = null, int? isActive = null)
        {
            try
            {
                _log.Info($"GetStoreItemList called. Search={search}, CategoryId={categoryId}, IsActive={isActive}");

                var dataTable = _sqlHelper.GetDataTable("S_GetStoreItemList", CommandType.StoredProcedure, new
                {
                    search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    categoryId,
                    isActive,
                    top = 500
                });

                var list = dataTable?.AsEnumerable().Select(row => new StoreItemListModel
                {
                    ServiceItemId = row.Field<int>("ServiceItemId"),
                    CategoryId = row.Field<int>("CategoryId"),
                    StoreType = row.Field<string>("StoreType") ?? string.Empty,
                    SubGroup = row.Field<string>("SubGroup") ?? string.Empty,
                    SubSubGroup = row.Field<string>("SubSubGroup") ?? string.Empty,
                    Code = row.Field<string>("Code") ?? string.Empty,
                    Name = row.Field<string>("Name") ?? string.Empty,
                    SaltName = (row.Field<string>("SaltName") ?? string.Empty).Trim(),
                    Strength = row.Field<string>("Strength") ?? string.Empty,
                    SnomedCode = row.Field<string>("SnomedCode") ?? string.Empty,
                    ManufacturerName = row.Field<string>("ManufacturerName") ?? string.Empty,
                    DosageFormName = row.Field<string>("DosageFormName") ?? string.Empty,
                    PackSize = row.Field<string>("PackSize") ?? string.Empty,
                    SaleUnitName = row.Field<string>("SaleUnitName") ?? string.Empty,
                    Packing = row.Field<int>("Packing"),
                    HsnCode = row.Field<string>("HsnCode") ?? string.Empty,
                    GstPer = row.Field<decimal?>("GstPer"),
                    DrugSchedule = row.Field<string>("DrugSchedule") ?? string.Empty,
                    RackCode = row.Field<string>("RackCode") ?? string.Empty,
                    ReorderLevel = row.Field<string>("ReorderLevel") ?? string.Empty,
                    IsColdChain = row.Field<int>("IsColdChain"),
                    IsActive = row.Field<int>("IsActive")
                }).ToList() ?? new List<StoreItemListModel>();

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<StoreItemListModel>>.Failure(alert.Type, "No store items found", 404);
                }

                return ServiceResult<IEnumerable<StoreItemListModel>>.Success(list, "Info", $"{list.Count} store item(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<StoreItemListModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<StoreItemDetailsModel> GetStoreItemDetails(int serviceItemId)
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable("S_GetStoreItemDetails", CommandType.StoredProcedure, new { serviceItemId });
                var row = dataTable?.AsEnumerable().FirstOrDefault();

                if (row == null)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<StoreItemDetailsModel>.Failure(alert.Type, "Store item not found", 404);
                }

                var item = new StoreItemDetailsModel
                {
                    ServiceItemId = row.Field<int>("ServiceItemId"),
                    CategoryId = row.Field<int>("CategoryId"),
                    SubCategoryId = row.Field<int>("SubCategoryId"),
                    SubSubCategoryId = row.Field<int>("SubSubCategoryId"),
                    Name = row.Field<string>("Name") ?? string.Empty,
                    Code = row.Field<string>("Code") ?? string.Empty,
                    DescriptionId = row.Field<int>("DescriptionId"),
                    SaltName = (row.Field<string>("SaltName") ?? string.Empty).Trim(),
                    Strength = row.Field<string>("Strength") ?? string.Empty,
                    SnomedCode = row.Field<string>("SnomedCode") ?? string.Empty,
                    ManufacturerId = row.Field<int>("ManufacturerId"),
                    DosageFormId = row.Field<int>("DosageFormId"),
                    DrugCategoryId = row.Field<int>("DrugCategoryId"),
                    Barcode = row.Field<string>("Barcode") ?? string.Empty,
                    PackSize = row.Field<string>("PackSize") ?? string.Empty,
                    PurchaseUnitId = row.Field<int>("PurchaseUnitId"),
                    SaleUnitId = row.Field<int>("SaleUnitId"),
                    Packing = row.Field<int>("Packing"),
                    HsnCode = row.Field<string>("HsnCode") ?? string.Empty,
                    CommodityCode = row.Field<string>("CommodityCode") ?? string.Empty,
                    GstPer = row.Field<decimal?>("GstPer"),
                    DrugSchedule = row.Field<string>("DrugSchedule") ?? string.Empty,
                    RackId = row.Field<int>("RackId"),
                    IsColdChain = row.Field<int>("IsColdChain"),
                    Expirable = row.Field<int>("Expirable"),
                    VedType = row.Field<string>("VedType") ?? string.Empty,
                    MinLevel = row.Field<int?>("MinLevel"),
                    ReorderLevel = row.Field<int?>("ReorderLevel"),
                    MaxLevel = row.Field<int?>("MaxLevel"),
                    DoctorId = row.Field<int>("DoctorId"),
                    IsKitTypeItem = row.Field<int>("IsKitTypeItem"),
                    IsBagTypeItem = row.Field<int>("IsBagTypeItem"),
                    IsActive = row.Field<int>("IsActive"),
                    DefaultStoreRoleId = row.Field<int>("DefaultStoreRoleId"),
                    ExpiryAlertDays = row.Field<int>("ExpiryAlertDays"),
                    IsFEFOIssue = row.Field<int>("IsFEFOIssue"),
                    IsBatchMandatory = row.Field<int>("IsBatchMandatory"),
                    AllowExpiredIssue = row.Field<int>("AllowExpiredIssue"),
                    ItemClass = row.Field<string>("ItemClass") ?? string.Empty,
                    StorageCondition = row.Field<string>("StorageCondition") ?? string.Empty,
                    IsSterile = row.Field<int>("IsSterile"),
                    IsSingleUse = row.Field<int>("IsSingleUse"),
                    IsRecallItem = row.Field<int>("IsRecallItem"),
                    IsRestrictedItem = row.Field<int>("IsRestrictedItem"),
                    IsTrackBatch = row.Field<int>("IsTrackBatch"),
                    IsBarcodeApplicable = row.Field<int>("IsBarcodeApplicable"),
                    IsStockable = row.Field<int>("IsStockable"),
                    IsCapitalItem = row.Field<int>("IsCapitalItem"),
                    IsHighRisk = row.Field<int>("IsHighRisk"),
                    CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                    CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                    LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                    LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                };

                return ServiceResult<StoreItemDetailsModel>.Success(item, "Info", "Store item retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<StoreItemDetailsModel>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<CreateUpdateRackMasterResponse> CreateUpdateRackMaster(
            CreateUpdateRackMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateRackMaster called. RackId={request.RackId}, RackCode={request.RackCode}");

                var result = _sqlHelper.DML(
                    "IU_RackMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        rackId = request.RackId,
                        rackCode = request.RackCode?.Trim(),
                        location = request.Location?.Trim(),
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

                _distributedCache.Remove(RackCacheKey);
                _log.Info($"Cleared RackMaster cache. Key={RackCacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate rack code: {request.RackCode}");
                    return ServiceResult<CreateUpdateRackMasterResponse>.Failure(alert.Type, "Rack code already exists", 409);
                }

                if (resultValue == 0 && request.RackId > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Rack not found for update. RackId={request.RackId}");
                    return ServiceResult<CreateUpdateRackMasterResponse>.Failure(alert.Type, alert.Message, 404);
                }

                if (resultValue > 0)
                {
                    bool isNew = request.RackId == 0;
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"Rack {(isNew ? "created" : "updated")} successfully. RackId={resultValue}");

                    return ServiceResult<CreateUpdateRackMasterResponse>.Success(
                        new CreateUpdateRackMasterResponse { RackId = resultValue },
                        alert.Type,
                        alert.Message,
                        isNew ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Rack operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateRackMasterResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateRackMasterResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<RackMasterModel>> GetRackList(
            int? rackId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetRackList called. RackId={rackId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(RackCacheKey);
                List<RackMasterModel> allRacks;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    allRacks = System.Text.Json.JsonSerializer.Deserialize<List<RackMasterModel>>(cachedData);
                }
                else
                {
                    var dataTable = _sqlHelper.GetDataTable("S_GetRackList", CommandType.StoredProcedure);

                    allRacks = dataTable?.AsEnumerable().Select(row => new RackMasterModel
                    {
                        RackId = row.Field<int>("RackId"),
                        RackCode = row.Field<string>("RackCode") ?? string.Empty,
                        Location = row.Field<string>("Location") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                    }).ToList() ?? new List<RackMasterModel>();

                    if (allRacks.Any())
                    {
                        _distributedCache.SetString(RackCacheKey,
                            System.Text.Json.JsonSerializer.Serialize(allRacks), new DistributedCacheEntryOptions());
                        _log.Info($"All RackMaster data cached. Key={RackCacheKey}, Count={allRacks.Count}");
                    }
                }

                IEnumerable<RackMasterModel> filtered = allRacks;
                if (rackId.HasValue) filtered = filtered.Where(r => r.RackId == rackId.Value);
                if (isActive.HasValue) filtered = filtered.Where(r => r.IsActive == isActive.Value);
                var list = filtered.ToList();

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<RackMasterModel>>.Failure(alert.Type, "No racks found", 404);
                }

                return ServiceResult<IEnumerable<RackMasterModel>>.Success(
                    list, "Info", $"{list.Count} rack(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<RackMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<CreateUpdateDrugCategoryResponse> CreateUpdateDrugCategory(
            CreateUpdateDrugCategoryRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDrugCategory called. DrugId={request.DrugId}, DrugName={request.DrugName}");

                var result = _sqlHelper.DML(
                    "IU_DrugCategoryMasters",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        drugId = request.DrugId,
                        drugName = request.DrugName?.Trim(),
                        isNarcoticDrug = request.IsNarcoticDrug,
                        colorPicker = request.ColorPicker?.Trim(),
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

                _distributedCache.Remove(DrugCategoryCacheKey);
                _log.Info($"Cleared DrugCategory cache. Key={DrugCategoryCacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate drug category name: {request.DrugName}");
                    return ServiceResult<CreateUpdateDrugCategoryResponse>.Failure(alert.Type, "Drug category name already exists", 409);
                }

                if (resultValue == 0 && request.DrugId > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Drug category not found for update. DrugId={request.DrugId}");
                    return ServiceResult<CreateUpdateDrugCategoryResponse>.Failure(alert.Type, alert.Message, 404);
                }

                if (resultValue > 0)
                {
                    bool isNew = request.DrugId == 0;
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"Drug category {(isNew ? "created" : "updated")} successfully. DrugId={resultValue}");

                    return ServiceResult<CreateUpdateDrugCategoryResponse>.Success(
                        new CreateUpdateDrugCategoryResponse { DrugId = resultValue },
                        alert.Type,
                        alert.Message,
                        isNew ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Drug category operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateDrugCategoryResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateDrugCategoryResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<DrugCategoryModel>> GetDrugCategoryList(
            int? drugId = null,
            int? isActive = null,
            int? isNarcoticDrug = null)
        {
            try
            {
                _log.Info($"GetDrugCategoryList called. DrugId={drugId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}, IsNarcoticDrug={isNarcoticDrug?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(DrugCategoryCacheKey);
                List<DrugCategoryModel> allCategories;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    allCategories = System.Text.Json.JsonSerializer.Deserialize<List<DrugCategoryModel>>(cachedData);
                }
                else
                {
                    var dataTable = _sqlHelper.GetDataTable("S_GetAllDrugCategoryMasters", CommandType.StoredProcedure);

                    allCategories = dataTable?.AsEnumerable().Select(row => new DrugCategoryModel
                    {
                        DrugId = row.Field<int>("DrugId"),
                        DrugName = row.Field<string>("DrugName") ?? string.Empty,
                        IsNarcoticDrug = row.Field<int>("IsNarcoticDrug"),
                        ColorPicker = row.Field<string>("ColorPicker") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        MedicineCount = row.Field<int>("MedicineCount"),
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                    }).ToList() ?? new List<DrugCategoryModel>();

                    if (allCategories.Any())
                    {
                        _distributedCache.SetString(DrugCategoryCacheKey,
                            System.Text.Json.JsonSerializer.Serialize(allCategories), new DistributedCacheEntryOptions());
                        _log.Info($"All DrugCategory data cached. Key={DrugCategoryCacheKey}, Count={allCategories.Count}");
                    }
                }

                IEnumerable<DrugCategoryModel> filtered = allCategories;
                if (drugId.HasValue) filtered = filtered.Where(d => d.DrugId == drugId.Value);
                if (isActive.HasValue) filtered = filtered.Where(d => d.IsActive == isActive.Value);
                if (isNarcoticDrug.HasValue) filtered = filtered.Where(d => d.IsNarcoticDrug == isNarcoticDrug.Value);
                var list = filtered.ToList();

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<DrugCategoryModel>>.Failure(alert.Type, "No drug categories found", 404);
                }

                return ServiceResult<IEnumerable<DrugCategoryModel>>.Success(
                    list, "Info", $"{list.Count} drug categor(ies) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DrugCategoryModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<CreateUpdateTaxPercentageResponse> CreateUpdateTaxPercentage(
            CreateUpdateTaxPercentageRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateTaxPercentage called. TaxId={request.TaxId}, Description={request.Description}, TaxValue={request.TaxValue}");

                var result = _sqlHelper.DML(
                    "IU_TaxPercentage",
                    CommandType.StoredProcedure,
                    new
                    {
                        taxId = request.TaxId,
                        description = request.Description?.Trim(),
                        taxValue = Math.Round(request.TaxValue, 2),
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

                _distributedCache.Remove(TaxPercentageCacheKey);
                _log.Info($"Cleared TaxPercentage cache. Key={TaxPercentageCacheKey}");

                if (resultValue < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    string message = resultValue switch
                    {
                        -1 => "GST rate already exists",
                        -2 => "GST slab name already exists",
                        _ => alert.Message
                    };
                    _log.Warn($"TaxPercentage save rejected ({resultValue}): {message}");
                    return ServiceResult<CreateUpdateTaxPercentageResponse>.Failure(alert.Type, message, 409);
                }

                if (resultValue == 0 && request.TaxId > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"GST slab not found for update. TaxId={request.TaxId}");
                    return ServiceResult<CreateUpdateTaxPercentageResponse>.Failure(alert.Type, alert.Message, 404);
                }

                if (resultValue > 0)
                {
                    bool isNew = request.TaxId == 0;
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"GST slab {(isNew ? "created" : "updated")} successfully. TaxId={resultValue}");

                    return ServiceResult<CreateUpdateTaxPercentageResponse>.Success(
                        new CreateUpdateTaxPercentageResponse { TaxId = resultValue },
                        alert.Type,
                        alert.Message,
                        isNew ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"GST slab operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateTaxPercentageResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateTaxPercentageResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<TaxPercentageModel>> GetTaxPercentageList(
            int? taxId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetTaxPercentageList called. TaxId={taxId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(TaxPercentageCacheKey);
                List<TaxPercentageModel> allSlabs;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    allSlabs = System.Text.Json.JsonSerializer.Deserialize<List<TaxPercentageModel>>(cachedData);
                }
                else
                {
                    var dataTable = _sqlHelper.GetDataTable("S_GetTaxPercentageList", CommandType.StoredProcedure);

                    allSlabs = dataTable?.AsEnumerable().Select(row => new TaxPercentageModel
                    {
                        TaxId = row.Field<int>("TaxID"),
                        Description = row.Field<string>("Description") ?? string.Empty,
                        TaxValue = row.Field<decimal>("TaxValue"),
                        CGST = row.Field<decimal>("CGST"),
                        SGST = row.Field<decimal>("SGST"),
                        IsActive = row.Field<int>("IsActive"),
                        MedicineCount = row.Field<int>("MedicineCount"),
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                    }).ToList() ?? new List<TaxPercentageModel>();

                    if (allSlabs.Any())
                    {
                        _distributedCache.SetString(TaxPercentageCacheKey,
                            System.Text.Json.JsonSerializer.Serialize(allSlabs), new DistributedCacheEntryOptions());
                        _log.Info($"All TaxPercentage data cached. Key={TaxPercentageCacheKey}, Count={allSlabs.Count}");
                    }
                }

                IEnumerable<TaxPercentageModel> filtered = allSlabs;
                if (taxId.HasValue) filtered = filtered.Where(t => t.TaxId == taxId.Value);
                if (isActive.HasValue) filtered = filtered.Where(t => t.IsActive == isActive.Value);
                var list = filtered.ToList();

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<TaxPercentageModel>>.Failure(alert.Type, "No GST slabs found", 404);
                }

                return ServiceResult<IEnumerable<TaxPercentageModel>>.Success(
                    list, "Info", $"{list.Count} GST slab(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<TaxPercentageModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // all rows (this master); active-only list used by EMR/getSaltNameMasterList lives under "_SaltNameMaster_All"
        private const string SaltNameCacheKey = "_SaltNameMaster_AllRows";
        private const string EmrSaltNameCacheKey = "_SaltNameMaster_All";

        public ServiceResult<CreateUpdateSaltNameMasterResponse> CreateUpdateSaltNameMaster(
            CreateUpdateSaltNameMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSaltNameMaster called. SaltNameId={request.SaltNameId}, SaltName={request.SaltName}");

                var result = _sqlHelper.DML(
                    "IU_SaltNameMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        saltNameId = request.SaltNameId,
                        saltName = request.SaltName?.Trim(),
                        therapeuticClass = request.TherapeuticClass?.Trim(),
                        snomedCode = request.SNOMEDCode?.Trim(),
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

                _distributedCache.Remove(SaltNameCacheKey);
                _distributedCache.Remove(EmrSaltNameCacheKey);
                _log.Info($"Cleared SaltNameMaster caches. Keys={SaltNameCacheKey},{EmrSaltNameCacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate salt name: {request.SaltName}");
                    return ServiceResult<CreateUpdateSaltNameMasterResponse>.Failure(
                        alert.Type,
                        "Salt/Generic Name Already Exists.",
                        409
                    );
                }

                if (resultValue == 0 && request.SaltNameId > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Salt not found for update. SaltNameId={request.SaltNameId}");
                    return ServiceResult<CreateUpdateSaltNameMasterResponse>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                if (resultValue > 0)
                {
                    bool isNew = request.SaltNameId == 0;
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    _log.Info($"Salt {(isNew ? "created" : "updated")} successfully. SaltNameId={resultValue}");

                    return ServiceResult<CreateUpdateSaltNameMasterResponse>.Success(
                        new CreateUpdateSaltNameMasterResponse { SaltNameId = resultValue },
                        alert.Type,
                        isNew ? "Salt/Generic Name Saved Successfully" : "Salt/Generic Name Updated Successfully",
                        isNew ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Salt operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateSaltNameMasterResponse>.Failure(
                    failAlert.Type,
                    failAlert.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSaltNameMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<SaltNameMasterModel>> GetSaltNameList(
            int? saltNameId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetSaltNameList called. SaltNameId={saltNameId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(SaltNameCacheKey);
                List<SaltNameMasterModel> allSalts;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SaltNameMaster data retrieved from cache. Key={SaltNameCacheKey}");
                    allSalts = System.Text.Json.JsonSerializer.Deserialize<List<SaltNameMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"SaltNameMaster cache miss. Fetching all data from database. Key={SaltNameCacheKey}");

                    var dataTable = _sqlHelper.GetDataTable("S_GetAllSaltNameMaster", CommandType.StoredProcedure);

                    allSalts = dataTable?.AsEnumerable().Select(row => new SaltNameMasterModel
                    {
                        SaltNameId = row.Field<int>("SaltNameId"),
                        SaltName = row.Field<string>("SaltName") ?? string.Empty,
                        TherapeuticClass = row.Field<string>("TherapeuticClass") ?? string.Empty,
                        SNOMEDCode = row.Field<string>("SNOMEDCode") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        MedicineCount = row.Field<int>("MedicineCount"),
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                    }).ToList() ?? new List<SaltNameMasterModel>();

                    if (allSalts.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allSalts);
                        _distributedCache.SetString(SaltNameCacheKey, serialized, new DistributedCacheEntryOptions());
                        _log.Info($"All SaltNameMaster data cached. Key={SaltNameCacheKey}, Count={allSalts.Count}");
                    }
                }

                IEnumerable<SaltNameMasterModel> filtered = allSalts;

                if (saltNameId.HasValue)
                    filtered = filtered.Where(s => s.SaltNameId == saltNameId.Value);

                if (isActive.HasValue)
                    filtered = filtered.Where(s => s.IsActive == isActive.Value);

                var list = filtered.ToList();

                if (!list.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<SaltNameMasterModel>>.Failure(
                        alert.Type,
                        "No salts found",
                        404
                    );
                }

                return ServiceResult<IEnumerable<SaltNameMasterModel>>.Success(
                    list,
                    "Info",
                    $"{list.Count} salt(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SaltNameMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        /// <summary>
        /// Clear all vendor-related cache entries
        /// </summary>
        private void ClearVendorCache()
        {
            try
            {
                _distributedCache.Remove("_VendorMaster_All");
                _log.Info("Cleared VendorMaster cache");
            }
            catch (Exception ex)
            {
                _log.Error($"Error clearing VendorMaster cache: {ex.Message}");
            }
        }
    }
}
