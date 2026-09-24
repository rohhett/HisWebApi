using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Domain;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using HISWEBAPI.Utilities;
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static iTextSharp.text.pdf.AcroFields;

namespace HISWEBAPI.Repositories.Implementations
{
    public class PatientRepository : IPatientRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public PatientRepository(
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

        private const string CACHE_KEY_PatientMaster_All = "_PatientMaster_All";
        private const string CACHE_KEY_SearchPatientMaster_All = "_SearchPatientMaster_All";

        public ServiceResult<CreateUpdatePatientMasterResponse> CreateUpdatePatientMaster(
            CreateUpdatePatientMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdatePatientMaster called. PatientId={request.PatientId}, FirstName={request.FirstName}");

                // Handle patient image file upload if provided
                string patientImagePath = null;
                if (request.PatientImageFile != null && request.PatientImageFile.Length > 0)
                {
                    _log.Info($"Processing patient image file: {request.PatientImageFile.FileName}, Size: {request.PatientImageFile.Length} bytes");

                    var fileUploadHelper = new FileUploadHelper(_configuration);
                    var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                        request.PatientImageFile,
                        "PatientImages"
                    );

                    if (!uploadSuccess)
                    {
                        _log.Error($"Patient image upload failed: {uploadError}");
                        var alertUpload = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                        return ServiceResult<CreateUpdatePatientMasterResponse>.Failure(
                            alertUpload.Type,
                            $"Patient image upload failed: {uploadError}",
                            500
                        );
                    }

                    patientImagePath = filePath;
                    _log.Info($"Patient image uploaded successfully: {patientImagePath}");
                }

                // Parse DOB to DateTime — SQL Server date column requires a proper DateTime, not a string
                DateTime dobParsed;
                bool dobParsedOk = false;

                // Try common formats: dd-MM-yyyy, yyyy-MM-dd, dd/MM/yyyy, MM/dd/yyyy
                string[] dobFormats = { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd" };
                dobParsedOk = DateTime.TryParseExact(
                    request.Dob?.Trim(),
                    dobFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out dobParsed);

                if (!dobParsedOk)
                {
                    // Fallback: try general parse
                    dobParsedOk = DateTime.TryParse(request.Dob?.Trim(), out dobParsed);
                }

                if (!dobParsedOk)
                {
                    _log.Warn($"Invalid DOB format received: {request.Dob}");
                    var alertDob = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<CreateUpdatePatientMasterResponse>.Failure(
                        alertDob.Type,
                        $"Invalid date of birth format: '{request.Dob}'. Expected formats: dd-MM-yyyy or yyyy-MM-dd",
                        400
                    );
                }

                var result = _sqlHelper.ExecuteScalar(
                    "IU_PatientMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @hospId = globalValues.hospId,
                        @branchId = request.BranchId,
                        @roleId = request.RoleId,
                        @patientId = request.PatientId,
                        @title = request.Title,
                        @firstName = request.FirstName,
                        @middleName = request.MiddleName ?? (object)DBNull.Value,
                        @lastName = request.LastName ?? (object)DBNull.Value,
                        @ageYears = request.AgeYears,
                        @ageMonths = request.AgeMonths,
                        @ageDays = request.AgeDays,
                        @dob = dobParsed,
                        @gender = request.Gender,
                        @maritalStatus = request.MaritalStatus ?? (object)DBNull.Value,
                        @relation = request.Relation ?? (object)DBNull.Value,
                        @relativeName = request.RelativeName ?? (object)DBNull.Value,
                        @idProofName = request.IdProofName ?? (object)DBNull.Value,
                        @idProofNumber = request.IdProofNumber ?? (object)DBNull.Value,
                        @selfContactNumber = request.SelfContactNumber,
                        @emergencyContactNumber = request.EmergencyContactNumber ?? (object)DBNull.Value,
                        @email = request.Email ?? (object)DBNull.Value,
                        @privilegedCardNumber = request.PrivilegedCardNumber ?? (object)DBNull.Value,
                        @address = request.Address ?? (object)DBNull.Value,
                        @countryId = request.CountryId,
                        @country = request.Country ?? (object)DBNull.Value,
                        @stateId = request.StateId,
                        @state = request.State ?? (object)DBNull.Value,
                        @districtId = request.DistrictId,
                        @district = request.District ?? (object)DBNull.Value,
                        @cityId = request.CityId,
                        @city = request.City ?? (object)DBNull.Value,
                        @insuranceCompanyId = request.InsuranceCompanyId,
                        @corporateId = request.CorporateId,
                        @cardNo = request.CardNo ?? (object)DBNull.Value,
                        @patientImagePath = patientImagePath ?? (object)DBNull.Value,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress,
                        @IsVaccination = request.IsVaccination,
                        @vipPatient = request.VipPatient ?? (object)DBNull.Value,
                        @PolicyNo = request.PolicyNo ?? (object)DBNull.Value,
                        @PolicyCardNo = request.PolicyCardNo ?? (object)DBNull.Value,
                        @ExpiryDate = request.ExpiryDate ?? (object)DBNull.Value,
                        @CardHolder = request.CardHolder ?? (object)DBNull.Value,
                        @ReferalNo = request.ReferalNo ?? (object)DBNull.Value,
                        @ReferalDate = request.ReferalDate ?? (object)DBNull.Value,
                        @healthId = request.HealthId ?? (object)DBNull.Value,
                        @healthIdNumber = request.HealthIdNumber ?? (object)DBNull.Value,
                        @landlineNo = request.LandlineNo ?? (object)DBNull.Value,
                        @birthPlace = request.BirthPlace ?? (object)DBNull.Value,
                        @religion = request.Religion ?? (object)DBNull.Value,
                        @relationPhone = request.RelationPhone ?? (object)DBNull.Value,
                        @relationAge = request.RelationAge ?? (object)DBNull.Value,
                        @relationGender = request.RelationGender ?? (object)DBNull.Value,
                        @eMG_FirstName = request.EMG_FirstName ?? (object)DBNull.Value,
                        @eMG_LastName = request.EMG_LastName ?? (object)DBNull.Value,
                        @eMG_Relation = request.EMG_Relation ?? (object)DBNull.Value,
                        @eMG_MobileNo = request.EMG_MobileNo ?? (object)DBNull.Value,
                        @eMG_ResidentNo = request.EMG_ResidentNo ?? (object)DBNull.Value,
                        @eMG_Address = request.EMG_Address ?? (object)DBNull.Value,
                        @isInternational = request.IsInternational,
                        @locality = request.Locality ?? (object)DBNull.Value,
                        @passportNumber = request.PassportNumber ?? (object)DBNull.Value,
                        @internationalNo = request.InternationalNo ?? (object)DBNull.Value,
                        @membershipNo = request.MembershipNo ?? (object)DBNull.Value,
                        @patientType = request.PatientType ?? (object)DBNull.Value,
                        @identityMark = request.IdentityMark ?? (object)DBNull.Value,
                        @identityMark2 = request.IdentityMark2 ?? (object)DBNull.Value,
                        @referenceType = request.ReferenceType ?? (object)DBNull.Value,
                        @remarks = request.Remarks ?? (object)DBNull.Value,

                    }
                );

                // Clear patient cache after successful operation
                _distributedCache.Remove(CACHE_KEY_PatientMaster_All);
                _distributedCache.Remove(CACHE_KEY_SearchPatientMaster_All);
                _log.Info($"Cleared PatientMaster cache. Key={CACHE_KEY_PatientMaster_All}");

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Patient already exists: {request.FirstName} {request.LastName}, Contact={request.SelfContactNumber}");
                    return ServiceResult<CreateUpdatePatientMasterResponse>.Failure(
                        alert.Type,
                        "Patient already exists with the same name and contact number",
                        409
                    );
                }

                if (resultValue > 0)
                {


                    var responseData = new CreateUpdatePatientMasterResponse
                    {
                        PatientId = resultValue,
                        PatientImagePath = patientImagePath
                    };

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.PatientId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Patient {(request.PatientId == 0 ? "created" : "updated")} successfully. PatientId={resultValue}");

                    return ServiceResult<CreateUpdatePatientMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.PatientId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Patient operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdatePatientMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdatePatientMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }



        public ServiceResult<string> UploadPatientDocument(
    UploadPatientDocumentRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UploadPatientDocument called. PatientId={request.PatientId}, DocumentId={request.DocumentId}");

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
                    "PatientDocuments"
                );

                if (!uploadSuccess)
                {
                    _log.Error($"Document file upload failed: {uploadError}");
                    var alertUpload = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    return ServiceResult<string>.Failure(
                        alertUpload.Type,
                        $"Document file upload failed: {uploadError}",
                        500
                    );
                }

                _log.Info($"Document file uploaded successfully: {filePath}");

                // Save to database
                _sqlHelper.DML(
                    "IU_PatientDocumentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @hospId = globalValues.hospId,
                        @documentId = request.DocumentId,
                        @patientId = request.PatientId,
                        @documentPath = filePath,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                // Clear cache for this patient's documents
                _distributedCache.Remove($"_PatientDocumentMapping_{request.PatientId}");
                _log.Info($"Cleared PatientDocumentMapping cache for PatientId={request.PatientId}");

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

        public ServiceResult<IEnumerable<PatientDocumentMappingResponse>> GetPatientDocumentMapping(int patientId)
        {
            try
            {
                _log.Info($"GetPatientDocumentMapping called. PatientId={patientId}");

                string cacheKey = $"_PatientDocumentMapping_{patientId}";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<PatientDocumentMappingResponse> documents;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PatientDocumentMapping retrieved from cache. Key={cacheKey}");
                    documents = JsonSerializer.Deserialize<List<PatientDocumentMappingResponse>>(cachedData);
                }
                else
                {
                    _log.Info($"PatientDocumentMapping cache miss. Fetching from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_PatientDocumentMapping",
                        CommandType.StoredProcedure,
                        new { @patientId = patientId }
                    );

                    documents = dataTable?.AsEnumerable().Select(row => new PatientDocumentMappingResponse
                    {
                        DocumentId = row.Field<int>("DocumentId"),
                        DocumentName = row.Field<string>("DocumentName") ?? string.Empty,
                        DocumentCode = row.Field<string>("DocumentCode") ?? string.Empty,
                        DocumentPath = row.Field<string>("DocumentPath") ?? string.Empty,
                        IsMandatory = row.Field<int>("IsMandatory"),

                    }).ToList() ?? new List<PatientDocumentMappingResponse>();

                    if (documents.Any())
                    {
                        var serialized = JsonSerializer.Serialize(documents);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"PatientDocumentMapping cached. Key={cacheKey}, Count={documents.Count}");
                    }
                }

                if (!documents.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No documents found for PatientId={patientId}");
                    return ServiceResult<IEnumerable<PatientDocumentMappingResponse>>.Failure(
                        alert.Type,
                        "No documents found for this patient",
                        404
                    );
                }

                return ServiceResult<IEnumerable<PatientDocumentMappingResponse>>.Success(
                    documents,
                    "Info",
                    $"{documents.Count} document(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<PatientDocumentMappingResponse>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
        public ServiceResult<IEnumerable<PatientMasterModel>> GetPatientMaster(
      int? patientId = null,
      string? uhid = null,
      string? contactNumber = null,
      int? branchId = null)
        {
            try
            {
                _log.Info($"GetPatientMaster called. PatientId={patientId?.ToString() ?? "All"}, Uhid={uhid ?? "All"}, ContactNumber={contactNumber ?? "All"}, BranchId={branchId?.ToString() ?? "All"}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        PatientId = patientId,
                        UHID = uhid,
                        ContactNumber = contactNumber,
                        BranchId = branchId
                    }
                );

                var patients = dataTable?.AsEnumerable().Select(row => new PatientMasterModel
                {
                    PatientId = row.Field<int>("PatientId"),
                    BranchId = row.Field<int>("BranchId"),
                    Uhid = row.Field<string>("UHID") ?? string.Empty,
                    Title = row.Field<string>("Title") ?? string.Empty,
                    FirstName = row.Field<string>("FirstName") ?? string.Empty,
                    MiddleName = row.Field<string>("MiddleName"),
                    LastName = row.Field<string>("LastName"),
                    PatientName = row.Field<string>("PatientName") ?? string.Empty,
                    AgeYears = row.Field<int?>("AgeYears"),
                    AgeMonths = row.Field<int?>("AgeMonths"),
                    AgeDays = row.Field<int?>("AgeDays"),
                    Age = row.Field<string>("Age"),
                    Dob = row.Field<string>("DOB"),
                    Gender = row.Field<string>("Gender"),
                    MaritalStatus = row.Field<string>("MaritalStatus"),
                    Relation = row.Field<string>("Relation"),
                    RelativeName = row.Field<string>("RelativeName"),
                    IdProofName = row.Field<string>("IdProofName"),
                    IdProofNumber = row.Field<string>("IdProofNumber"),
                    ContactNumber = row.Field<string>("ContactNumber"),
                    EmergencyContactNumber = row.Field<string>("EmergencyContactNumber"),
                    Email = row.Field<string>("Email"),
                    PrivilegedCardNumber = row.Field<string>("PrivilegedCardNumber"),
                    Address = row.Field<string>("Address"),
                    CountryId = row.Field<int?>("CountryId"),
                    Country = row.Field<string>("Country"),
                    StateId = row.Field<int?>("StateId"),
                    State = row.Field<string>("State"),
                    DistrictId = row.Field<int?>("DistrictId"),
                    District = row.Field<string>("District"),
                    CityId = row.Field<int?>("CityId"),
                    City = row.Field<string>("City"),
                    InsuranceCompanyId = row.Field<int?>("InsuranceCompanyId"),
                    CorporateId = row.Field<int?>("CorporateId"),
                    CardNo = row.Field<string>("CardNo"),
                    IsVaccination = row.Field<int?>("IsVaccination"),
                    VIPPatient = row.Field<int?>("VIPPatient"),
                    PatientImagePath = row.Field<string>("PatientImagePath"),
                    PolicyNo = row.Field<string>("PolicyNo"),
                    PolicyCardNo = row.Field<string>("PolicyCardNo"),
                    ExpiryDate = row.Field<string>("ExpiryDate"),
                    CardHolder = row.Field<string>("CardHolder"),
                    ReferalNo = row.Field<string>("ReferalNo"),
                    ReferalDate = row.Field<string>("ReferalDate"),
                    LandlineNo = row.Field<string>("LandlineNo"),
                    BirthPlace = row.Field<string>("BirthPlace"),
                    Religion = row.Field<string>("Religion"),
                    RelationPhone = row.Field<string>("RelationPhone"),
                    RelationAge = row.Field<int?>("RelationAge"),
                    RelationGender = row.Field<string>("RelationGender"),
                    EMG_FirstName = row.Field<string>("EMG_FirstName"),
                    EMG_LastName = row.Field<string>("EMG_LastName"),
                    EMG_Relation = row.Field<string>("EMG_Relation"),
                    EMG_MobileNo = row.Field<string>("EMG_MobileNo"),
                    EMG_ResidentNo = row.Field<string>("EMG_ResidentNo"),
                    EMG_Address = row.Field<string>("EMG_Address"),
                    IsInternational = row.Field<int?>("IsInternational"),
                    Locality = row.Field<string>("Locality"),
                    PassportNumber = row.Field<string>("PassportNumber"),
                    InternationalNo = row.Field<string>("InternationalNo"),
                    MembershipNo = row.Field<string>("MembershipNo"),
                    PatientType = row.Field<string>("PatientType"),
                    IdentityMark = row.Field<string>("IdentityMark"),
                    IdentityMark2 = row.Field<string>("IdentityMark2"),
                    ReferenceType = row.Field<string>("ReferenceType"),
                    Remarks = row.Field<string>("Remarks"),
                    DoctorId = row.IsNull("DoctorId") ? 0 : row.Field<int>("DoctorId"),
                    IPDNo = row.Field<string>("IPDNo"),
                    DayCareNo = row.Field<string>("DayCareNo"),
                    DialysisNo = row.Field<string>("DialysisNo"),
                    EmergencyNo = row.Field<string>("EmergencyNo"),
                    IsRegistrationValid = row.Field<int>("IsRegistrationValid"),
                }).ToList() ?? new List<PatientMasterModel>();

                if (!patients.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No patients found for the given filters");
                    return ServiceResult<IEnumerable<PatientMasterModel>>.Failure(
                        alert.Type,
                        "No patients found",
                        404
                    );
                }

                _log.Info($"Retrieved {patients.Count} patient(s) from database");

                return ServiceResult<IEnumerable<PatientMasterModel>>.Success(
                    patients,
                    "Info",
                    $"{patients.Count} patient(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<PatientMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<IEnumerable<Dictionary<string, object>>> SearchPatientMaster(
    int? patientId = null,
    string? uhid = null,
    string? firstName = null,
    string? middleName = null,
    string? lastName = null,
    string? relativeName = null,
    string? dob = null,
    string? contactNumber = null,
    string? emergencyContactNumber = null,
    string? address = null,
    string? registrationDate = null,
    string? ipdNo = null,
    int? branchId = null)
    {
        try
        {
            _log.Info($"SearchPatientMaster called. PatientId={patientId?.ToString() ?? "All"}, Uhid={uhid ?? "All"}, BranchId={branchId?.ToString() ?? "All"}");

            var dataTable = _sqlHelper.GetDataTable(
                "S_SearchPatientMaster",
                CommandType.StoredProcedure,
                new
                {
                    PatientId = patientId,
                    UHID = uhid,
                    FirstName = firstName,
                    MiddleName = middleName,
                    LastName = lastName,
                    RelativeName = relativeName,
                    DOB = dob,
                    ContactNumber = contactNumber,
                    EmergencyContactNumber = emergencyContactNumber,
                    Address = address,
                    RegistrationDate = registrationDate,
                    IPDNo = ipdNo,
                    BranchId = branchId
                }
            );

            var patients = dataTable.ToRawList();

            if (!patients.Any())
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                _log.Info("No patients found for the given filters");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                    alert.Type,
                    "No patients found",
                    404
                );
            }

            _log.Info($"Retrieved {patients.Count} patient(s) from database");

            return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                patients,
                "Info",
                $"{patients.Count} patient(s) retrieved successfully",
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
    public ServiceResult<ServiceBillingDetailsModel> GetServiceAllDetailsForOPDBilling(
          int branchId,
          int corporateId,
          int doctorId,
          int serviceItemId,
          int categoryId,
          int subCategoryId,
          int subSubCategoryId,
          int bedTypeId)
        {
            try
            {
                _log.Info($"GetServiceAllDetailsForOPDBilling called. CorporateId={corporateId}, DoctorId={doctorId}, ServiceItemId={serviceItemId}, CategoryId={categoryId}, SubCategoryId={subCategoryId}, SubSubCategoryId={subSubCategoryId}, BedTypeId={bedTypeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetServiceAllDetailsForOPDBilling",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @corporateId = corporateId,
                        @doctorId = doctorId,
                        @serviceItemId = serviceItemId,
                        @categoryId = categoryId,
                        @subCategoryId = subCategoryId,
                        @subSubCategoryId = subSubCategoryId,
                        @bedTypeId = bedTypeId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"No billing details found for ServiceItemId={serviceItemId}, CorporateId={corporateId}");
                    return ServiceResult<ServiceBillingDetailsModel>.Failure(
                        alert.Type,
                        "No billing details found for the given service and corporate",
                        404
                    );
                }

                var row = dataTable.Rows[0];
                var result = new ServiceBillingDetailsModel
                {
                    Rate = row["Rate"] != DBNull.Value ? Convert.ToDecimal(row["Rate"]) : 0,
                    RateListId = row["RateListId"] != DBNull.Value ? Convert.ToInt32(row["RateListId"]) : 0,
                    IsRateEditable = row["IsRateEditable"] != DBNull.Value ? Convert.ToInt32(row["IsRateEditable"]) : 1,
                    ServiceName = row["ServiceName"]?.ToString() ?? string.Empty,
                    Code = row["Code"]?.ToString() ?? string.Empty,
                    CorporateAlias = row["CorporateAlias"]?.ToString() ?? string.Empty,
                    CorporateCode = row["CorporateCode"]?.ToString() ?? string.Empty,
                    ValidityDays = row["ValidityDays"] != DBNull.Value ? Convert.ToInt32(row["ValidityDays"]) : 0,
                    DiscountPer = row["DiscountPer"] != DBNull.Value ? Convert.ToDecimal(row["DiscountPer"]) : 0,
                    DiscountReason = row["DiscountReason"]?.ToString() ?? string.Empty,
                    IsNonPayable = row["IsNonPayable"] != DBNull.Value ? Convert.ToInt32(row["IsNonPayable"]) : 0,
                    ServiceItemId = row["ServiceItemId"] != DBNull.Value ? Convert.ToInt32(row["ServiceItemId"]) : serviceItemId,
                    CorporateId = row["CorporateId"] != DBNull.Value ? Convert.ToInt32(row["CorporateId"]) : corporateId,
                    CategoryTypeId = row["CategoryTypeId"] != DBNull.Value ? Convert.ToInt32(row["CategoryTypeId"]) : 0,
                    CategoryId = row["CategoryId"] != DBNull.Value ? Convert.ToInt32(row["CategoryId"]) : categoryId,
                    SubCategoryId = row["SubCategoryId"] != DBNull.Value ? Convert.ToInt32(row["SubCategoryId"]) : subCategoryId,
                    SubSubCategoryId = row["SubSubCategoryId"] != DBNull.Value ? Convert.ToInt32(row["SubSubCategoryId"]) : subSubCategoryId,
                    IsCorporateDiscount = row["IsCorporateDiscount"] != DBNull.Value ? Convert.ToInt32(row["IsCorporateDiscount"]) : 0,
                    IsPrivilegedCardDiscount = row["IsPrivilegedCardDiscount"] != DBNull.Value ? Convert.ToInt32(row["IsPrivilegedCardDiscount"]) : 0,
                    GSTPer = row["GSTPer"] != DBNull.Value ? Convert.ToDecimal(row["GSTPer"]) : 0,
                    SampleTypeId = row["SampleTypeId"] != DBNull.Value ? Convert.ToInt32(row["SampleTypeId"]) : 0,
                    ReportTypeId = row["ReportTypeId"] != DBNull.Value ? Convert.ToInt32(row["ReportTypeId"]) : 0,
                    IsRequiredSeparatePerformingDoctor = row["IsRequiredSeparatePerformingDoctor"] != DBNull.Value ? Convert.ToInt32(row["IsRequiredSeparatePerformingDoctor"]) : 0,
                    DoctorDepartmentIds = row["DoctorDepartmentIds"]?.ToString() ?? string.Empty,
                    LabTypeId = row["LabTypeId"] != DBNull.Value ? Convert.ToInt32(row["LabTypeId"]) : 0,
                    TatTimeInMin = row["TatTimeInMin"] != DBNull.Value ? Convert.ToInt32(row["TatTimeInMin"]) : 0,


                };

                _log.Info($"Service billing details retrieved. ServiceName={result.ServiceName}, Rate={result.Rate}, RateListId={result.RateListId}");

                return ServiceResult<ServiceBillingDetailsModel>.Success(
                    result,
                    "Info",
                    "Service billing details retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<ServiceBillingDetailsModel>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<SaveOPDBillingResponse> SaveOPDBilling(
           SaveOPDBillingRequest request,
           AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SaveOPDBilling called. PatientId={request.VisitDetails.PatientId}, BranchId={request.VisitDetails.BranchId}");

                var v = request.VisitDetails;
                decimal totalPaidAmount = 0;
                decimal totalAmountSettledWithPatientAdvance = 0;
                if (request.PaymentDetails?.Count > 0)
                {
                    totalPaidAmount = request.PaymentDetails.Sum(p => p.Amount);
                    foreach (var adv in request.PaymentDetails)
                    {
                        if (adv.IsPatientAdvanceAmount == 1)
                        {
                            totalAmountSettledWithPatientAdvance = adv.Amount;
                            break;
                        }
                    }
                }

                // ── 1. PatientVisitDetails ───────────────────────────────────────────
                var pvd = new PatientVisitDetails
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    PatientId = v.PatientId,
                    Uhid = v.Uhid,
                    visitType = VisitType.OPD,
                    CurrentAge = v.CurrentAge,
                    DoctorId = 0,
                    CorporateId = v.CorporateId,
                    InsuranceCompanyId = v.InsuranceCompanyId,
                    ReferDoctorId = v.ReferDoctorId > 0 ? v.ReferDoctorId : (int?)null,
                    TotalBillAmount = v.GrossBillAmount,
                    TotalDiscountPerOnBill = v.TotalDiscPerOnBill,
                    TotalDiscountAmountOnBill = v.TotalDiscAmtOnBill,
                    DiscountApprovedById = v.DiscApprovedById > 0 ? v.DiscApprovedById : (int?)null,
                    DiscountReason = v.DiscountReason,
                    RoundOff = v.RoundOff,
                    TotalPatientPayableAmount = v.NetAmount,
                    TotalPaidAmount = totalPaidAmount,
                    TotalBalanceAmount = v.NetAmount - totalPaidAmount,
                    TotalAmountSettledWithPatientAdvance = totalAmountSettledWithPatientAdvance,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress,
                    UniqueId = v.UniqueId,
                    Mlc = v.Mlc,
                    Pi = v.Pi,
                    Remark = v.Remark,
                    PolicyNo = v.PolicyNo,
                    PolicyCardNo = v.PolicyCardNo,
                    ExpiryDate = v.ExpiryDate,
                    CardHolder = v.CardHolder,
                    ReferalNo = v.ReferalNo,
                    ReferalDate = v.ReferalDate,
                    ProId = v.ProId,
                    ProName = v.ProName,
                    IsSendMRD = v.IsSendMRD
                };

                int visitId = Convert.ToInt32(pvd.Create(_sqlHelper, tnx));
                _log.Info($"PatientVisitDetails created. VisitId={visitId}");


                // ── 2. PatientBillDetails ────────────────────────────────────────────────
                var pbd = new PatientBillDetails
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    RoleId = v.RoleId,
                    PatientId = v.PatientId,
                    VisitId = visitId,
                    TypeId = 1,                                // 1 = OPD
                    TotalBillAmount = v.GrossBillAmount,
                    TotalDiscountPerOnBill = v.TotalDiscPerOnBill,
                    TotalDiscountAmountOnBill = v.TotalDiscAmtOnBill,
                    DiscountApprovedById = v.DiscApprovedById > 0 ? v.DiscApprovedById : (int?)null,
                    DiscountReason = v.DiscountReason,
                    RoundOff = v.RoundOff,
                    TotalPayableAmount = v.NetAmount,
                    TotalPaidAmount = totalPaidAmount,
                    TotalBalanceAmount = v.NetAmount - totalPaidAmount,
                    TotalAmountSettledWithPatientAdvance = totalAmountSettledWithPatientAdvance,
                    TotalPatientPayableAmount = v.NetAmount,
                    TotalCorporatePayableAmount = 0,
                    TotalPatientPaidAmount = totalPaidAmount,
                    TotalCorporatePaidAmount = 0,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                };

                int billId = Convert.ToInt32(pbd.Create(_sqlHelper, tnx));
                _log.Info($"PatientBillDetails created. BillId={billId}");


                // ── 2. FinancialTransactions ─────────────────────────────────────────
                var ft = new FinancialTransactions
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    VisitId = visitId,
                    BillId = billId,
                    PatientId = v.PatientId,
                    tnxType = TnxType.OPDBilling,
                    GrossAmount = v.GrossBillAmount,
                    DiscountPercentage = v.TotalDiscPerOnBill,
                    DiscountAmount = v.TotalDiscAmtOnBill,
                    RoundOff = v.RoundOff,
                    NetAmount = v.NetAmount,
                    Remarks = v.Remarks,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress,
                    UniqueId = v.UniqueId
                };

                int ftid = Convert.ToInt32(ft.Create(_sqlHelper, tnx));
                _log.Info($"FinancialTransactions created. FTID={ftid}");


                // ── Fetch Registration Charge ServiceItemId (once) ───────────────────
                int registrationChargeServiceItemId = 0;
                int registrationChargeValidityDays = 0;

                var regChargeDt = _sqlHelper.GetDataTable(
                    tnx,
                    "S_GetRegistrationChargeServiceItemId",
                    CommandType.StoredProcedure
                );

                if (regChargeDt != null && regChargeDt.Rows.Count > 0)
                {
                    registrationChargeServiceItemId = Convert.ToInt32(regChargeDt.Rows[0]["ServiceItemId"]);
                    registrationChargeValidityDays = Convert.ToInt32(regChargeDt.Rows[0]["RegistrationChargeValidityDays"]);
                }


                // ── 3. Process billing items ─────────────────────────────────────────
                bool isReceipt = false;
                bool isDoctorAppointment = false;
                bool isLabInvestigations = false;

                int labNo = 0;
                var sampleTypeBarcodeMap = new Dictionary<int, int>();
                int pathologyTokenNo = 0;
                int radiologyTokenNo = 0;
                int cardiologyTokenNo = 0;

                foreach (var item in request.BillingItems)
                {
                    // ── 3a. FinancialTransactionDetails ──────────────────────────────
                    decimal itemDiscPer, itemDiscAmt, itemNetAmt;
                    string itemDiscReason;

                    if (request.IsBillDiscount == 1)
                    {
                        itemDiscPer = v.TotalDiscPerOnBill;
                        itemDiscAmt = (item.GrossAmt * v.TotalDiscPerOnBill) / 100;
                        itemNetAmt = item.GrossAmt - itemDiscAmt;
                        itemDiscReason = v.DiscountReason;
                    }
                    else
                    {
                        itemDiscPer = item.DiscPer;
                        itemDiscAmt = item.DiscAmt;
                        itemNetAmt = item.NetAmt;
                        itemDiscReason = item.DiscountReason;
                    }

                    var ftd = new FinancialTransactionDetails
                    {
                        HospId = globalValues.hospId,
                        BranchId = v.BranchId,
                        FTID = ftid,
                        VisitId = visitId,
                        BillId = billId,
                        PatientId = v.PatientId,
                        ServiceItemId = item.ServiceItemId,
                        SubSubCategoryId = item.SubSubCategoryId,
                        ServiceName = item.ServiceName,
                        ServiceCode = item.Code,
                        Remarks = item.Remarks,
                        CorporateAlias = item.CorporateAlias,
                        CorporateCode = item.CorporateCode,
                        DoctorId = item.DoctorId > 0 ? item.DoctorId : (int?)null,
                        PerformingDoctorId = item.PerformingDoctorId > 0 ? item.PerformingDoctorId : (int?)null,
                        CorporateId = v.CorporateId > 0 ? v.CorporateId : (int?)null,
                        Rate = item.Rate,
                        Qty = item.Qty,
                        GrossAmt = item.GrossAmt,
                        DiscPer = itemDiscPer,
                        DiscAmt = itemDiscAmt,
                        NetAmt = itemNetAmt,
                        IsCorporateNonPayable = item.IsNonPayable,
                        IsUnderPackage = item.IsUnderPackage,
                        DiscountReason = itemDiscReason,
                        PackageId = item.PackageId,
                        RateListId = item.RateListId,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    };

                    int ftdId = Convert.ToInt32(ftd.Create(_sqlHelper, tnx));
                    _log.Info($"FinancialTransactionDetails created. FTDId={ftdId}, ServiceItemId={item.ServiceItemId}");


                    // ── Update Patient Registration Charge Validity if matched ───────────
                    if (registrationChargeServiceItemId > 0 && registrationChargeValidityDays > 0 && item.ServiceItemId == registrationChargeServiceItemId)
                    {
                        _sqlHelper.DML(
                            tnx,
                            "U_PatientMasterRegistrationChargeValidity",
                            CommandType.StoredProcedure,
                            new
                            {
                                @PatientId = v.PatientId,
                                @RegistrationChargeValidityDays = registrationChargeValidityDays
                            }
                        );

                        _log.Info($"PatientMaster RegistrationChargeExpiryDate updated. PatientId={v.PatientId}, ValidityDays={registrationChargeValidityDays}");
                    }

                    // ── 3b. Consultation → DoctorAppointments (CategoryTypeId == 1) ──────
                    if (item.CategoryTypeId == 1)
                    {
                        var appt = new DoctorAppointments
                        {
                            HospId = globalValues.hospId,
                            BranchId = v.BranchId,
                            VisitId = visitId,
                            DoctorId = item.DoctorId,
                            PatientId = v.PatientId,
                            FTDID = ftdId,
                            AppDateTime = DateTime.Now,
                            AppointmentType = "DirectAppointment",
                            ValidUpToDate = DateTime.Now.AddDays(item.ValidityDays),
                            ValidityDays = item.ValidityDays,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };

                        appt.Create(_sqlHelper, tnx);
                        isDoctorAppointment = true;
                        _log.Info($"DoctorAppointments created for DoctorId={item.DoctorId}");
                    }

                    // ── 3c. Investigation (CategoryTypeId == 3) ──────────────────────────
                    else if (item.CategoryTypeId == 3)
                    {
                        // Barcode – one per unique SampleTypeId (Pathology only)
                        int barCode = 0;
                        if (item.LabTypeId == 1 && item.SampleTypeId > 0)
                        {
                            if (!sampleTypeBarcodeMap.ContainsKey(item.SampleTypeId))
                            {
                                int newBarcode = Convert.ToInt32(_sqlHelper.ExecuteScalar(
                                    tnx,
                                    "S_GetNextGlobalBarcode",
                                    CommandType.StoredProcedure,
                                    new { @branchId = v.BranchId }));
                                sampleTypeBarcodeMap[item.SampleTypeId] = newBarcode;
                            }
                            barCode = sampleTypeBarcodeMap[item.SampleTypeId];
                        }

                        // Lab number – shared for all investigations in this visit
                        if (labNo == 0)
                        {
                            labNo = Convert.ToInt32(_sqlHelper.ExecuteScalar(
                                tnx,
                                "getLabNo",
                                CommandType.StoredProcedure,
                                new { @branchId = v.BranchId },
                                new { result = 0 }));
                        }

                        // Token number per sub-category
                        int tokenNo = 0;
                        if (item.LabTypeId == 1 || item.LabTypeId == 2 || item.LabTypeId == 3)
                        {
                            tokenNo = Convert.ToInt32(_sqlHelper.ExecuteScalar(
                                tnx,
                                "S_GetLabTokenNo",
                                CommandType.StoredProcedure,
                                new { @branchId = v.BranchId, @SubCategoryId = item.LabTypeId },
                                new { result = 0 }));
                        }

                        if (pathologyTokenNo == 0 && item.LabTypeId == 1) pathologyTokenNo = tokenNo;
                        if (radiologyTokenNo == 0 && item.LabTypeId == 2) radiologyTokenNo = tokenNo;
                        if (cardiologyTokenNo == 0 && item.LabTypeId == 3) cardiologyTokenNo = tokenNo;

                        var pid = new PatientInvestigationDetails
                        {
                            HospId = globalValues.hospId,
                            BranchId = v.BranchId,
                            VisitId = visitId,
                            FTDID = ftdId,
                            InvestigationId = item.ServiceItemId,
                            DoctorId = item.DoctorId,
                            PatientId = v.PatientId,
                            LabNo = labNo,
                            TokenNo = item.LabTypeId == 1 ? pathologyTokenNo
                                    : item.LabTypeId == 2 ? radiologyTokenNo
                                    : cardiologyTokenNo,
                            IsUrgent = item.IsUrgent,
                            BarCode = barCode,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };

                        pid.Create(_sqlHelper, tnx);
                        isLabInvestigations = true;
                        _log.Info($"PatientInvestigationDetails created for InvestigationId={item.ServiceItemId}");
                    }
                }

                // ── 4. Receipt ───────────────────────────────────────────────────────
                int receiptId = 0;
                if (totalPaidAmount > 0)
                {
                    var receipt = new Receipts
                    {
                        HospId = globalValues.hospId,
                        BranchId = v.BranchId,
                        RoleId = v.RoleId,
                        BillId = billId,
                        VisitId = visitId,
                        PatientId = v.PatientId,
                        Amount = totalPaidAmount - totalAmountSettledWithPatientAdvance,
                        IsCopaymentReceipt = request.PaymentDetails[0].IsCopaymentReceipt,
                        PlutusTransactionReferenceID = request.PaymentDetails[0].PlutusTransactionReferenceID,
                        TransactionLogId = request.PaymentDetails[0].TransactionLogId,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress,
                        UniqueId = v.UniqueId
                    };

                    receiptId = Convert.ToInt32(receipt.Create(_sqlHelper, tnx));
                    _log.Info($"Receipt created. ReceiptId={receiptId}");

                    foreach (var p in request.PaymentDetails)
                    {
                        // PaymentModeTypeId 4 = Credit → skip
                        if (p.PaymentModeTypeId == 4)
                            continue;

                        //IsPatientAdvanceAmount==1 -> skip for Advance Amt
                        if (p.IsPatientAdvanceAmount == 1)
                            continue;

                        var rpmd = new ReceiptsPaymentModeDetails
                        {
                            HospId = globalValues.hospId,
                            BranchId = v.BranchId,
                            ReceiptID = receiptId,
                            Amount = p.Amount,
                            PaymentModeId = p.PaymentModeId,
                            BankId = p.BankId > 0 ? p.BankId : (int?)null,
                            ReferenceNo = p.RefNo,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };

                        rpmd.Create(_sqlHelper, tnx);
                    }

                    isReceipt = true;
                }

                if (totalAmountSettledWithPatientAdvance > 0)
                {

                    // ── PatientLedgerBill (insert or update running balance) ─────────
                    var ledgerBill = new PatientLedgerBill
                    {
                        PatientId = v.PatientId,
                        LedgerId = 0,
                        TransactionType = LedgerBillTransactionType.Debit,
                        Amount = totalAmountSettledWithPatientAdvance,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    };

                    int ledgerId = Convert.ToInt32(ledgerBill.Create(_sqlHelper, tnx));
                    _log.Info($"PatientLedgerBill upserted. LedgerId={ledgerId}");

                    // ── PatientLedgerDetails (transaction history row) ────────────────
                    var ledgerDetails = new PatientLedgerDetails
                    {
                        PatientId = v.PatientId,
                        LedgerId = ledgerId,
                        TransactionType = LedgerTransactionType.Debit,
                        Amount = totalAmountSettledWithPatientAdvance,
                        VisitId = visitId,
                        BillId = billId,
                        ReceiptId = 0,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    };

                    ledgerDetails.Create(_sqlHelper, tnx);
                }


                tnx.Commit();
                _log.Info($"SaveOPDBilling committed. VisitId={visitId}, FTID={ftid}, ReceiptId={receiptId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveOPDBillingResponse>.Success(
                    new SaveOPDBillingResponse
                    {
                        VisitId = visitId,
                        FTID = ftid,
                        ReceiptId = receiptId,
                        IsReceipt = isReceipt,
                        IsDoctorAppointment = isDoctorAppointment,
                        IsLabInvestigations = isLabInvestigations
                    },
                    alert.Type,
                    "OPD Billing saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveOPDBillingResponse>.Failure(
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

        public ServiceResult<IEnumerable<PackageAllDetailsModel>> GetPackageAllDetails(int packageId)
        {
            try
            {
                _log.Info($"GetPackageAllDetails called. PackageId={packageId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPackageAllDetails",
                    CommandType.StoredProcedure,
                    new { packageId = packageId }
                );

                var packageDetails = dataTable?.AsEnumerable().Select(row => new PackageAllDetailsModel
                {
                    PackageId = row.Field<int>("PackageId"),
                    PackageName = row.Field<string>("PackageName") ?? string.Empty,
                    PackageCode = row.Field<string>("PackageCode") ?? string.Empty,
                    IsActive = row.Field<int>("IsActive"),
                    SubSubCategoryId = row.Field<int?>("SubSubCategoryId") ?? 0,
                    SubCategoryId = row.Field<int?>("SubCategoryId") ?? 0,
                    CategoryId = row.Field<int?>("CategoryId") ?? 0,
                    StartsFrom = row.Field<string>("StartsFrom") ?? string.Empty,
                    ExpiresOn = row.Field<string>("ExpiresOn") ?? string.Empty,
                    PackageServiceNameCode = row.Field<string>("PackageServiceNameCode") ?? string.Empty,
                    PackageServiceName = row.Field<string>("PackageServiceName") ?? string.Empty,
                    PackageServiceCode = row.Field<string>("PackageServiceCode") ?? string.Empty,
                    PackageServiceId = row.Field<int>("PackageServiceId"),
                    QTY = row.Field<int>("QTY"),
                    PackageServiceCategoryTypeId = row.Field<int?>("PackageServiceCategoryTypeId") ?? 0,
                    PackageServiceCategory = row.Field<string>("PackageServiceCategory") ?? string.Empty,
                    PackageServiceCategoryId = row.Field<int?>("PackageServiceCategoryId") ?? 0,
                    PackageServiceSubCategoryId = row.Field<int?>("PackageServiceSubCategoryId") ?? 0,
                    PackageServiceSubSubCategoryId = row.Field<int?>("PackageServiceSubSubCategoryId") ?? 0,
                    IsMultipleVisitAllow = row.Field<int?>("IsMultipleVisitAllow") ?? 0,
                    VisitDuration = row.Field<int?>("VisitDuration") ?? 0,
                    VisitDurationType = row.Field<string>("VisitDurationType") ?? string.Empty,


                }).ToList() ?? new List<PackageAllDetailsModel>();

                if (!packageDetails.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No package details found for PackageId={packageId}");
                    return ServiceResult<IEnumerable<PackageAllDetailsModel>>.Failure(
                        alert.Type,
                        $"No details found for PackageId: {packageId}",
                        404
                    );
                }

                _log.Info($"Retrieved {packageDetails.Count} service item(s) for PackageId={packageId}");

                return ServiceResult<IEnumerable<PackageAllDetailsModel>>.Success(
                    packageDetails,
                    "Info",
                    $"{packageDetails.Count} service item(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<PackageAllDetailsModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetReceiptDetailsByFTID(int ftid, int isReceipt, int receiptId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetReceiptDetailsByFTID called. FTID={ftid}, IsReceipt={isReceipt}, ReceiptId={receiptId}, PrintUserId={globalValues.userId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetReceiptDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @FTID = ftid,
                        @isReceipt = isReceipt,
                        @receiptId = receiptId,
                        @printUserId = globalValues.userId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No receipt details found for FTID={ftid}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No receipt details found",
                        404
                    );
                }

                // Return raw DataTable as list of dictionaries without model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Receipt details retrieved successfully for FTID={ftid}. Rows={result.Count}");

                return ServiceResult<object>.Success(
                    result,
                    "Info",
                    $"Receipt details retrieved successfully",
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

        public ServiceResult<object> GetOPDReceiptList(string visitNo)
        {
            try
            {
                _log.Info($"GetOPDReceiptList called. VisitNo={visitNo}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetReceiptAllDetailsForOPDPatient",
                    CommandType.StoredProcedure,
                    new
                    {
                        @VisitNo = visitNo
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No receipt list found for VisitNo={visitNo}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No receipts found for the given visit",
                        404
                    );
                }

                // Return raw DataTable as list of dictionaries without model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"OPD receipt list retrieved successfully for VisitNo={visitNo}. Rows={result.Count}");

                return ServiceResult<object>.Success(
                    result,
                    "Info",
                    $"Receipt list retrieved successfully",
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

        public ServiceResult<object> GetOPDCardDetails(long ftid)
        {
            try
            {
                _log.Info($"GetOPDCardDetails called. FTID={ftid}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetOPDCardDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @FTID = ftid
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No OPD card details found for FTID={ftid}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No OPD card details found",
                        404
                    );
                }

                // Return raw DataTable as list of dictionaries without model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"OPD card details retrieved successfully for FTID={ftid}. Rows={result.Count}");

                return ServiceResult<object>.Success(
                    result,
                    "Info",
                    $"OPD card details retrieved successfully",
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

        public ServiceResult<DataTable> FindDuplicateService(int serviceItemId, int patientId)
        {
            try
            {
                _log.Info($"FindDuplicateService called. ServiceItemId={serviceItemId}, PatientId={patientId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetDublicateServiceName",
                    CommandType.StoredProcedure,
                    new
                    {
                        @ServiceItemId = serviceItemId,
                        @PatientId = patientId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No duplicate service found for ServiceItemId={serviceItemId}, PatientId={patientId}");
                    return ServiceResult<DataTable>.Failure(
                        alert.Type,
                        "No duplicate service found for today",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                _log.Info($"Found {dataTable.Rows.Count} duplicate service record(s)");

                return ServiceResult<DataTable>.Success(
                    dataTable,
                    alert1.Type,
                    $"{dataTable.Rows.Count} duplicate service record(s) found",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<DataTable>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetInvestigationObservationMappingDetails(int investigationId, int ageInDays, string gender)
        {
            try
            {
                _log.Info($"GetInvestigationObservationMappingDetails called. InvestigationId={investigationId}, AgeInDays={ageInDays}, Gender={gender}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_getInvestigationObservationMappingDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @investigationId = investigationId,
                        @ageInDays = ageInDays,
                        @gender = gender
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No observation mapping details found for InvestigationId={investigationId}, AgeInDays={ageInDays}, Gender={gender}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var rawData = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();

                _log.Info($"GetInvestigationObservationMappingDetails retrieved {rawData.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
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

        public ServiceResult<object> GetUserDiscountRights(int userId)
        {
            try
            {
                _log.Info($"GetUserDiscountRights called. UserId={userId}");

                string cacheKey = $"_UserDiscountRights_User{userId}";

                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserDiscountRights data retrieved from cache. Key={cacheKey}");
                    return ServiceResult<object>.Success(
                        System.Text.Json.JsonSerializer.Deserialize<object>(cachedData),
                        "Info",
                        "Data retrieved successfully",
                        200
                    );
                }

                _log.Info($"UserDiscountRights cache miss. Fetching from database. Key={cacheKey}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetUserDiscountRights",
                    CommandType.StoredProcedure,
                    new { @userId = userId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No discount rights found for UserId={userId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                // Convert DataTable to raw list of dictionaries
                var rawData = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();

                // Cache the raw data
                var serialized = System.Text.Json.JsonSerializer.Serialize(rawData);
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = null,
                    SlidingExpiration = null
                };
                _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                _log.Info($"UserDiscountRights data cached permanently. Key={cacheKey}");

                return ServiceResult<object>.Success(
                    rawData,
                    "Info",
                    "Discount rights retrieved successfully",
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

        public ServiceResult<object> GetPatientPreviousDues(int branchId, int patientId)
        {
            try
            {
                _log.Info($"GetPatientPreviousDues called. BranchId={branchId}, PatientId={patientId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientPreviousDues",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @patientId = patientId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No previous dues found for BranchId={branchId}, PatientId={patientId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var rawData = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();

                _log.Info($"GetPatientPreviousDues retrieved {rawData.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
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

        public ServiceResult<object> GetPatientLastConsultationDetail(int patientId)
        {
            try
            {
                _log.Info($"GetPatientLastConsultationDetail called. PatientId={patientId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetLastVisit",
                    CommandType.StoredProcedure,
                    new
                    {
                        @patientId = patientId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No last consultation detail found for PatientId={patientId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var rawData = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();

                _log.Info($"GetPatientLastConsultationDetail retrieved {rawData.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
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

        public ServiceResult<object> GetServiceItemDetailsByVisitId(int visitId)
        {
            try
            {
                _log.Info($"GetServiceItemDetailsByVisitId called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_getServiceDetailsByVisitId",
                    CommandType.StoredProcedure,
                    new
                    {
                        @VisitId = visitId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No service item details found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var rawData = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();

                _log.Info($"GetServiceItemDetailsByVisitId retrieved {rawData.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
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

        public ServiceResult<object> GetPatientBalanceAmountOPD(string uhid)
        {
            try
            {
                _log.Info($"GetPatientBalanceAmountOPD called. UHID={uhid}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientDueAmountOPD",
                    CommandType.StoredProcedure,
                    new { @uhid = uhid }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No OPD balance amount found for UHID={uhid}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var rawData = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();

                _log.Info($"GetPatientBalanceAmountOPD retrieved {rawData.Count} record(s) for UHID={uhid}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
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

        public ServiceResult<object> GetPatientBalanceAmountIPD(string uhid)
        {
            try
            {
                _log.Info($"GetPatientBalanceAmountIPD called. UHID={uhid}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientDueAmountIPD",
                    CommandType.StoredProcedure,
                    new { @uhid = uhid }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No IPD balance amount found for UHID={uhid}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var rawData = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();

                _log.Info($"GetPatientBalanceAmountIPD retrieved {rawData.Count} record(s) for UHID={uhid}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
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

        public ServiceResult<object> GetPatientBalanceAmountPharmacy(string uhid)
        {
            try
            {
                _log.Info($"GetPatientBalanceAmountPharmacy called. UHID={uhid}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientDueAmountPharmacy",
                    CommandType.StoredProcedure,
                    new { @uhid = uhid }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No Pharmacy balance amount found for UHID={uhid}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var rawData = dataTable.Rows
                    .Cast<DataRow>()
                    .Select(row => dataTable.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();

                _log.Info($"GetPatientBalanceAmountPharmacy retrieved {rawData.Count} record(s) for UHID={uhid}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} record(s) retrieved successfully",
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

        public ServiceResult<IEnumerable<Dictionary<string, object>>> SearchPatientForConsultation(
           SearchPatientForConsultationRequest request)
        {
            try
            {
                _log.Info($"SearchPatientForConsultation called. BranchId={request.BranchId}, TypeId={request.TypeId}, " +
                          $"FromDate={request.FromDate}, ToDate={request.ToDate}");

                // Parse dates
                if (!DateTime.TryParse(request.FromDate, out DateTime fromDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alertDate.Type, "Invalid FromDate format", 400);
                }

                if (!DateTime.TryParse(request.ToDate, out DateTime toDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alertDate.Type, "Invalid ToDate format", 400);
                }

                var dataTable = _sqlHelper.GetDataTable(
                    "S_SearchPatientForConsultation",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = request.BranchId,
                        @uhid = request.Uhid ?? string.Empty,
                        @appNo = request.AppNo,
                        @doctorId = request.DoctorId,
                        @typeId = request.TypeId,
                        @bedTypeId = request.BedTypeId,
                        @fromDate = fromDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        @toDate = toDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        @statusId = request.StatusId,
                        @dateTypeId = request.DateTypeId,
                        @doctorDepartmentId = request.DoctorDepartmentId,
                        @isTempratureRoomOut = request.isTempratureRoomOut
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("SearchPatientForConsultation: no records found");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type, "No patients found", 404);
                }

                // Convert every row to a plain dictionary so the response is
                // column-name driven, not model driven.
                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns
                        .Cast<System.Data.DataColumn>()
                        .ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                ).ToList();

                _log.Info($"SearchPatientForConsultation: returned {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    rows,
                    alert1.Type,
                    $"{rows.Count} patient(s) found",
                    200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                    alert.Type, alert.Message, 500);
            }
        }

     

       

        public ServiceResult<object> GetPatientObservationResultsTrend(int patientId, int pageNumber, int pageSize)
        {
            try
            {
                _log.Info($"GetPatientObservationResultsTrend called. PatientId={patientId}, PageNumber={pageNumber}, PageSize={pageSize}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientObservationResultsTrend",
                    CommandType.StoredProcedure,
                    new
                    {
                        @PatientId = patientId,
                        @PageNumber = pageNumber,
                        @PageSize = pageSize
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No observation trend data found for PatientId={patientId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var data = dataTable.AsEnumerable().Select(row =>
                      dataTable.Columns.Cast<DataColumn>().ToDictionary(
                          col => col.ColumnName,
                          col => row[col] == DBNull.Value ? null : row[col]
                      )
                  ).ToList<object>();

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                _log.Info($"Retrieved {data.Count} observation trend record(s) for PatientId={patientId}");

                return ServiceResult<object>.Success(
                    data,
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<SaveIPDAdmissionResponse> SaveIPDAdmission(
    SaveIPDAdmissionRequest request,
    AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SaveIPDAdmission called. PatientId={request.PatientId}, BranchId={request.BranchId}");

                // ── 1. PatientVisitDetails ───────────────────────────────────────────
                var pvd = new PatientVisitDetails
                {
                    HospId = globalValues.hospId,
                    BranchId = request.BranchId,
                    PatientId = request.PatientId,
                    Uhid = request.Uhid,
                    visitType = VisitType.IPD,
                    CurrentAge = request.CurrentAge,
                    DoctorId = request.PrimaryDoctorId,
                    CorporateId = request.CorporateId,
                    InsuranceCompanyId = request.InsuranceCompanyId,
                    ReferDoctorId = request.ReferDoctorId > 0 ? request.ReferDoctorId : (int?)null,
                    ProId = request.ProId,
                    ProName = request.ProName,
                    AdmissionType = request.AdmissionType,
                    BillingTypeId = request.BillingTypeId,
                    RoomTypeId = request.RoomTypeId,
                    BedId = request.BedId,
                    AdmissionDate = request.AdmissionDate,
                    AdmissionTime = request.AdmissionTime,
                    StatusId = 1,
                    Status = "IN",
                    AttendantRelation = request.AttendantRelation,
                    AttendantName = request.AttendantName,
                    AttendantContactNumber = request.AttendantContactNumber,
                    HandleWithCare = request.HandleWithCare,
                    NameMasking = request.NameMasking,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                };

                int visitId = Convert.ToInt32(pvd.Create(_sqlHelper, tnx));
                _log.Info($"PatientVisitDetails created. VisitId={visitId}");


                // ── 2. PatientBillDetails ────────────────────────────────────────────────
                var pbd = new PatientBillDetails
                {
                    HospId = globalValues.hospId,
                    BranchId = request.BranchId,
                    PatientId = request.PatientId,
                    VisitId = visitId,
                    TypeId = 2,                                // 1 = IPD
                    TotalBillAmount = 0,
                    TotalDiscountPerOnBill = 0,
                    TotalDiscountAmountOnBill = 0,
                    DiscountApprovedById = null,
                    DiscountReason = null,
                    RoundOff = 0,
                    TotalPayableAmount = 0,
                    TotalPaidAmount = 0,
                    TotalBalanceAmount = 0,
                    TotalPatientPayableAmount = 0,
                    TotalCorporatePayableAmount = 0,
                    TotalPatientPaidAmount = 0,
                    TotalCorporatePaidAmount = 0,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                };

                long billId = pbd.Create(_sqlHelper, tnx);
                _log.Info($"PatientBillDetails created. BillId={billId}");



                // ── 2. MLC (only when AdmissionType == "MLC") ───────────────────────
                if (request.AdmissionType == "MLC")
                {
                    _sqlHelper.DML(tnx, "I_MLC", CommandType.StoredProcedure, new
                    {
                        @VisitId = visitId,
                        @MLCNo = request.MlcNo,
                        @MLCTypeId = request.MlcTypeId,
                        @MLCType = request.MlcType,
                        @InjuryTypeId = request.InjuryTypeId,
                        @InjuryType = request.InjuryType,
                        @BroughtBy = request.BroughtBy,
                        @TransportId = request.TransportId,
                        @Transport = request.Transport,
                        @PlaceOfAccident = request.PlaceOfAccident,
                        @PoliceStation = request.PoliceStation,
                        @OfficerName = request.OfficerName,
                        @OfficerPhone = request.OfficerPhone,
                        @ComplaintNo = request.ComplaintNo,
                        @BuckleNoOfPolice = request.BuckleNoOfPolice,
                        @DateOfInjury = request.DateOfInjury,
                        @DateOfInitiation = request.DateOfInitiation,
                        @CauseOfAccident = request.CauseOfAccident,
                        @IdentificationMarks = request.IdentificationMarks,
                        @Remarks = request.Remarks,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });
                    _log.Info($"MLC record created for VisitId={visitId}");
                }

                // ── 3. Secondary doctor mappings ────────────────────────────────────
                foreach (var doctorId in request.SecondaryDoctorIds)
                {
                    _sqlHelper.DML(tnx, "I_IPDVisitDoctorMapping", CommandType.StoredProcedure, new
                    {
                        @visitId = visitId,
                        @doctorId = doctorId,
                        @isPrimaryDoctor = 0,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    });
                }

                // ── 4. Primary doctor mapping ────────────────────────────────────────
                _sqlHelper.DML(tnx, "I_IPDVisitDoctorMapping", CommandType.StoredProcedure, new
                {
                    @visitId = visitId,
                    @doctorId = request.PrimaryDoctorId,
                    @isPrimaryDoctor = 1,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });
                _log.Info($"Doctor mappings created. PrimaryDoctorId={request.PrimaryDoctorId}");

                // ── 5. Bed mapping ───────────────────────────────────────────────────
                _sqlHelper.DML(tnx, "IU_IPDVisitBedMapping", CommandType.StoredProcedure, new
                {
                    @visitId = visitId,
                    @bedId = request.BedId,
                    @isTransfer = 0,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });

                // ── 6. Update bed status to occupied ────────────────────────────────
                _sqlHelper.DML(tnx, "U_UpdateBedStatus", CommandType.StoredProcedure, new
                {
                    @bedId = request.BedId,
                    @currentStatus = 1   // PatientAdmitted
                });
                _log.Info($"Bed {request.BedId} marked as occupied");

                // ── 7. Corporate mapping ─────────────────────────────────────────────
                _sqlHelper.DML(tnx, "IU_IPDVisitCorporateMapping", CommandType.StoredProcedure, new
                {
                    @visitId = visitId,
                    @insuranceCompanyId = request.InsuranceCompanyId,
                    @corporateId = request.CorporateId,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });

                // ── 8. Doctor IPD sequence number ────────────────────────────────────
                _sqlHelper.DML(tnx, "I_IPDVisitDoctorSequence", CommandType.StoredProcedure, new
                {
                    @branchId = request.BranchId,
                    @doctorId = request.PrimaryDoctorId,
                    @visitId = visitId
                });

                tnx.Commit();
                _log.Info($"SaveIPDAdmission committed. VisitId={visitId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveIPDAdmissionResponse>.Success(
                    new SaveIPDAdmissionResponse { VisitId = visitId },
                    alert.Type,
                    "IPD Admission saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveIPDAdmissionResponse>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        public ServiceResult<object> SearchIPDPatient(SearchIPDPatientRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SearchIPDPatient called. BranchId={request.BranchId}, SearchBy={request.SearchBy}, StatusId={request.StatusId}");

                string filter = null;
                if (!string.IsNullOrWhiteSpace(request.SearchBy) && !string.IsNullOrWhiteSpace(request.SearchValue))
                {
                    if (request.SearchBy == "PVD.VisitNo")
                        filter = request.SearchBy + " = '" + request.SearchValue + "'";
                    else if (request.SearchBy == "AdmissionDate" || request.SearchBy == "DischargeDate")
                    {
                        if (!DateTime.TryParse(request.SearchValue, out DateTime parsedDate))
                        {
                            var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                            return ServiceResult<object>.Failure(alertDate.Type, $"Invalid date format for {request.SearchBy}", 400);
                        }
                        filter = request.SearchBy + " = '" + parsedDate.ToString("yyyy-MM-dd") + "'";
                    }
                    else
                        filter = request.SearchBy + " LIKE '%" + request.SearchValue + "%'";
                }

                var dataTable = _sqlHelper.GetDataTable(
                    "S_SearchIPDPatient",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = request.BranchId,
                        @statusId = request.StatusId == 0 ? "0" : request.StatusId.ToString(),
                        @UserId = globalValues.userId.ToString(),
                        @filter = filter
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("SearchIPDPatient: no records found");
                    return ServiceResult<object>.Failure(alert.Type, "No IPD patients found", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"SearchIPDPatient: returned {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} patient(s) found", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UploadVisitWisePatientDocument(
    UploadVisitWisePatientDocumentRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UploadVisitWisePatientDocument called. PatientId={request.PatientId}, VisitId={request.VisitId}, DocumentId={request.DocumentId}, DocumentCategoryId={request.DocumentCategoryId}");

                if (request.DocumentFile == null || request.DocumentFile.Length == 0)
                {
                    var alertFile = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<string>.Failure(alertFile.Type, "Document file is required", 400);
                }

                var fileUploadHelper = new FileUploadHelper(_configuration);
                var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                    request.DocumentFile,
                    "PatientDocuments"
                );

                if (!uploadSuccess)
                {
                    _log.Error($"Document file upload failed: {uploadError}");
                    var alertUpload = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    return ServiceResult<string>.Failure(alertUpload.Type, $"Document file upload failed: {uploadError}", 500);
                }

                _log.Info($"Document file uploaded successfully: {filePath}");

                _sqlHelper.DML(
                    "IU_VisitWisePatientDocumentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @documentId = request.DocumentId,
                        @patientId = request.PatientId,
                        @visitId = request.VisitId,
                        @documentCategoryId = request.DocumentCategoryId,
                        @documentPath = filePath,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                // Invalidate cache for this combination
                _distributedCache.Remove($"_VisitWisePatientDocumentMapping_{request.DocumentCategoryId}_{request.VisitId}_{request.PatientId}");
                _log.Info($"Cleared VisitWisePatientDocumentMapping cache. VisitId={request.VisitId}, PatientId={request.PatientId}, DocumentCategoryId={request.DocumentCategoryId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(filePath, alert.Type, alert.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetVisitWisePatientDocumentMapping(
            int documentCategoryId,
            int visitId,
            int patientId)
        {
            try
            {
                _log.Info($"GetVisitWisePatientDocumentMapping called. DocumentCategoryId={documentCategoryId}, VisitId={visitId}, PatientId={patientId}");

                string cacheKey = $"_VisitWisePatientDocumentMapping_{documentCategoryId}_{visitId}_{patientId}";

                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"VisitWisePatientDocumentMapping retrieved from cache. Key={cacheKey}");
                    return ServiceResult<object>.Success(
                        JsonSerializer.Deserialize<object>(cachedData),
                        "Info",
                        "Documents retrieved successfully",
                        200
                    );
                }

                _log.Info($"VisitWisePatientDocumentMapping cache miss. Fetching from database. Key={cacheKey}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_VisitWisePatientDocumentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @documentCategoryId = documentCategoryId,
                        @visitId = visitId,
                        @patientId = patientId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No documents found for DocumentCategoryId={documentCategoryId}, VisitId={visitId}, PatientId={patientId}");
                    return ServiceResult<object>.Failure(alert.Type, "No documents found", 404);
                }

                var rawData = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                var serialized = JsonSerializer.Serialize(rawData);
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = null,
                    SlidingExpiration = null
                };
                _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                _log.Info($"VisitWisePatientDocumentMapping cached. Key={cacheKey}, Count={rawData.Count}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    rawData,
                    alert1.Type,
                    $"{rawData.Count} document(s) retrieved successfully",
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




        public ServiceResult<SaveOPDBookingResponse> SaveOPDBooking(
    SaveOPDBookingRequest request,
    AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SaveOPDBooking called. PatientId={request.VisitDetails.PatientId}, BranchId={request.VisitDetails.BranchId}");

                var v = request.VisitDetails;

                int bookingId = Convert.ToInt32(_sqlHelper.RunProcedureInsert(
      "I_OPDBookingDetails",
      new IDataParameter[]
      {
        new SqlParameter("@BranchId", v.BranchId),
        new SqlParameter("@PatientId", v.PatientId),
        new SqlParameter("@RoleId", v.RoleId),
        new SqlParameter("@CorporateId", v.CorporateId),
        new SqlParameter("@InsuranceCompanyId", v.InsuranceCompanyId),
        new SqlParameter("@ReferDoctorId", v.ReferDoctorId > 0 ? (object)v.ReferDoctorId : DBNull.Value),
        new SqlParameter("@TotalBillAmount", v.GrossBillAmount),
        new SqlParameter("@TotalDiscountPerOnBill", v.TotalDiscPerOnBill),
        new SqlParameter("@TotalDiscountAmountOnBill", v.TotalDiscAmtOnBill),
        new SqlParameter("@RoundOff", v.RoundOff),
        new SqlParameter("@TotalPatientPayableAmount", v.NetAmount),
        new SqlParameter("@PolicyNo", string.IsNullOrEmpty(v.PolicyNo) ? (object)DBNull.Value : v.PolicyNo),
        new SqlParameter("@PolicyCardNo", string.IsNullOrEmpty(v.PolicyCardNo) ? (object)DBNull.Value : v.PolicyCardNo),
        new SqlParameter("@ExpiryDate", string.IsNullOrEmpty(v.ExpiryDate) ? (object)DBNull.Value : v.ExpiryDate),
        new SqlParameter("@CardHolder", string.IsNullOrEmpty(v.CardHolder) ? (object)DBNull.Value : v.CardHolder),
        new SqlParameter("@ReferalNo", string.IsNullOrEmpty(v.ReferalNo) ? (object)DBNull.Value : v.ReferalNo),
        new SqlParameter("@ReferalDate", string.IsNullOrEmpty(v.ReferalDate) ? (object)DBNull.Value : v.ReferalDate),
        new SqlParameter("@isDiscountApprovalRequired", v.IsDiscountApprovalRequired),
        new SqlParameter("@DiscountApprovedID", v.DiscountApprovedID),
        new SqlParameter("@DiscountApprovedName", string.IsNullOrEmpty(v.DiscountApprovedName) ? (object)DBNull.Value : v.DiscountApprovedName),
        new SqlParameter("@DiscountReason", string.IsNullOrEmpty(v.DiscountReason) ? (object)DBNull.Value : v.DiscountReason),
        new SqlParameter("@Remark", string.IsNullOrEmpty(v.Remark) ? (object)DBNull.Value : v.Remark),

        new SqlParameter("@UserId", globalValues.userId),
        new SqlParameter("@IpAddress", globalValues.ipAddress ?? (object)DBNull.Value),
        new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
      }
  ));

                _log.Info($"OPDBookingDetails created. BookingId={bookingId}");

                // ── 2. Insert line items ────────────────────────────────────────────────
                foreach (var item in request.BillingItems)
                {
                    _sqlHelper.DML(tnx, "I_OPDBookingItemDetails", CommandType.StoredProcedure, new
                    {
                        @BranchId = v.BranchId,
                        @BookingId = bookingId,
                        @PatientId = v.PatientId,
                        @ServiceItemId = item.ServiceItemId,
                        @SubSubCategoryId = item.SubSubCategoryId,
                        @ServiceName = item.ServiceName,
                        @ServiceCode = item.Code,
                        @Remarks = item.Remarks,
                        @DoctorId = item.DoctorId > 0 ? item.DoctorId : (int?)null,
                        @PerformingDoctorId = item.PerformingDoctorId > 0 ? item.PerformingDoctorId : (int?)null,
                        @Rate = item.Rate,
                        @Qty = item.Qty,
                        @GrossAmt = item.GrossAmt,
                        @DiscPer = item.DiscPer,
                        @DiscAmt = item.DiscAmt,
                        @NetAmt = item.NetAmt,
                        @RateListId = item.RateListId,
                        @IsUrgent = item.IsUrgent,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });
                }

                tnx.Commit();
                _log.Info($"SaveOPDBooking committed. BookingId={bookingId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveOPDBookingResponse>.Success(
                    new SaveOPDBookingResponse { BookingId = bookingId },
                    alert.Type,
                    "OPD Booking saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveOPDBookingResponse>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }


        public ServiceResult<object> GetOPDBookingDetailsForPaymentCollection(int branchId, int corporateId, string fromDate, string toDate)
        {
            try
            {
                _log.Info($"GetOPDBookingDetailsForPaymentCollection called. FromDate={fromDate}, ToDate={toDate}");

                // Parse dates
                if (!DateTime.TryParse(fromDate, out DateTime parsedFromDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid FromDate format", 400);
                }
                if (!DateTime.TryParse(toDate, out DateTime parsedToDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid ToDate format", 400);
                }

                var dataTable = _sqlHelper.GetDataTable(
                    "S_OPDBookingDetailsForPaymentCollection",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = parsedFromDate.ToString("yyyy-MM-dd"),
                        @toDate = parsedToDate.ToString("yyyy-MM-dd"),
                        @branchId = branchId,
                        @corporateId = corporateId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No OPD booking details found for payment collection. FromDate={fromDate}, ToDate={toDate}");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"OPD booking details for payment collection retrieved successfully. Count={result.Count}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, alert1.Type, $"{result.Count} record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetOPDBookingDetailsForDiscountApproval(int branchId, int corporateId, string fromDate, string toDate, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetOPDBookingDetailsForDiscountApproval called. FromDate={fromDate}, ToDate={toDate}");

                if (!DateTime.TryParse(fromDate, out DateTime parsedFromDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid FromDate format", 400);
                }
                if (!DateTime.TryParse(toDate, out DateTime parsedToDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid ToDate format", 400);
                }

                var dataTable = _sqlHelper.GetDataTable(
                    "S_OPDBookingDetailsForDiscountApproval",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = parsedFromDate.ToString("yyyy-MM-dd"),
                        @toDate = parsedToDate.ToString("yyyy-MM-dd"),
                        @branchId = branchId,
                        @corporateId = corporateId,
                        @userId = globalValues.userId

                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No OPD booking details found for discount approval. FromDate={fromDate}, ToDate={toDate}");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"OPD booking details for discount approval retrieved successfully. Count={result.Count}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, alert1.Type, $"{result.Count} record(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetOPDBookingDetailsByBookingId(int bookingId)
        {
            try
            {
                _log.Info($"GetOPDBookingDetailsByBookingId called. BookingId={bookingId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_OPDBookingDetailsByBookingId",
                    CommandType.StoredProcedure,
                    new { @bookingId = bookingId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No OPD booking details found for BookingId={bookingId}");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 404);
                }

                // OBD header columns — these are the same across all rows for a single bookingId
                var obdColumns = new HashSet<string>
        {
            "BookingId", "TokenNo", "BranchId", "PatientId", "UHID",
            "CorporateId", "InsuranceCompanyId", "ReferDoctorId",
            "TotalBillAmount", "TotalDiscountPerOnBill", "TotalDiscountAmountOnBill",
            "RoundOff", "TotalPatientPayableAmount",
            "PolicyNo", "PolicyCardNo", "ExpiryDate", "CardHolder",
            "ReferalNo", "ReferalDate",
            "IsPaymentCollected", "IsDiscountApprovalRequired", "IsDiscountApproved","TotalApprovedDiscountPerOnBill",
            "IsLevel1Approve", "Level1ApproveId", "Level1ApproveOn",
            "IsLevel2Approve", "Level2ApproveId", "Level2ApproveOn",
            "IsLevel3Approve", "Level3ApproveId", "Level3ApproveOn",
            "IsLevel4Approve", "Level4ApproveId", "Level4ApproveOn",
            "IsCancel", "CancelBy", "CancelOn", "CancelReason",
            "CreatedBy", "CreatedOn", "LastModifiedBy", "LastModifiedOn",
            "DiscountApprovedID", "DiscountApprovedName", "DiscountReason", "Remark"
        };

                // OBID item columns
                var obidColumns = new HashSet<string>
        {
            "ServiceItemId", "CategoryId", "SubCategoryId", "SubSubCategoryId",
            "ServiceName", "ServiceCode", "DoctorId","DoctorName","PerformingDoctorId","Remarks",
            "Rate", "Qty", "GrossAmt", "DiscPer", "DiscAmt", "NetAmt",
            "RateListId", "IsUrgent"
        };

                // Build header from first row (OBD data is same across all rows)
                var firstRow = dataTable.Rows[0];
                var allColumns = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToHashSet();

                var header = allColumns
                    .Where(col => obdColumns.Contains(col))
                    .ToDictionary(
                        col => col,
                        col => firstRow[col] == DBNull.Value ? null : firstRow[col]
                    );

                // Build items array (OBID data — one per row)
                var items = dataTable.AsEnumerable().Select(row =>
                    allColumns
                        .Where(col => obidColumns.Contains(col))
                        .ToDictionary(
                            col => col,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                ).ToList();

                var response = new Dictionary<string, object>(header)
                {
                    ["bookingItems"] = items
                };

                _log.Info($"OPD booking details retrieved for BookingId={bookingId}. Items={items.Count}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(response, alert1.Type, $"Booking details retrieved with {items.Count} item(s)", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> CancelOPDBooking(CancelOPDBookingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CancelOPDBooking called. BookingId={request.BookingId}");

                _sqlHelper.DML(
                    "U_CancelOPDBooking",
                    CommandType.StoredProcedure,
                    new
                    {
                        @BookingId = request.BookingId,
                        @CancelReason = request.CancelReason ?? (object)DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"OPD booking cancelled successfully. BookingId={request.BookingId}");

                return ServiceResult<string>.Success(
                    "OPD booking cancelled successfully",
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

        public ServiceResult<string> PaymentCollectedForOPDBooking(int bookingId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"PaymentCollectedForOPDBooking called. BookingId={bookingId}");

                _sqlHelper.DML(
                    "U_PaymentCollectedForOPDBooking",
                    CommandType.StoredProcedure,
                    new
                    {
                        @BookingId = bookingId,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"Payment collected marked successfully. BookingId={bookingId}");

                return ServiceResult<string>.Success(
                    "Payment collected marked successfully",
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

        public ServiceResult<string> ApproveOPDBookingDiscount(ApproveOPDBookingDiscountRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ApproveOPDBookingDiscount called. BookingId={request.BookingId}, Flag={request.Flag}, ApprovedPer={request.ApprovedPer}");



                _sqlHelper.DML(
                    "U_ApproveOPDBookingDiscount",
                    CommandType.StoredProcedure,
                    new
                    {
                        @BookingId = request.BookingId,
                        @flag = request.Flag,
                        @approvedPer = request.ApprovedPer,
                        @ApprovalRemarks = request.ApprovalRemarks,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"OPD booking discount approval processed. BookingId={request.BookingId}, Flag={request.Flag}");

                return ServiceResult<string>.Success(
                    "OPD booking discount approval processed successfully",
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

        public ServiceResult<object> GetOPDBookingApprovalDetails(long bookingId)
        {
            try
            {
                _log.Info($"GetOPDBookingApprovalDetails called. BookingId={bookingId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetOPDBookingApprovalDetails",
                    CommandType.StoredProcedure,
                    new { @BookingId = bookingId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No approval details found for BookingId={bookingId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No approval details found for the given BookingId",
                        404
                    );
                }

                // Raw DataTable → list of dictionaries, no model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetOPDBookingApprovalDetails retrieved successfully for BookingId={bookingId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    "Approval details retrieved successfully",
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
        public ServiceResult<SavePatientAdvanceResponse> SavePatientAdvance(
            SavePatientAdvanceRequest request,
            AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SavePatientAdvance called. PatientId={request.PatientId}, PatientLedgerId={request.PatientLedgerId}");

                decimal totalPaidAmount = request.PaymentDetails.Sum(p => p.Amount);
               

                // ── 1. Receipts (IsAdvanceReceipt = 1) ───────────────────────────────
                var receipt = new Receipts
                {
                    HospId = globalValues.hospId,
                    BranchId = request.BranchId,
                    RoleId = request.RoleId,
                    BillId = 0,
                    VisitId = 0,
                    PatientId = request.PatientId,
                    Amount = request.IsRefund == 0 ? totalPaidAmount : -totalPaidAmount,
                    IsAdvanceReceipt = 1,
                    PlutusTransactionReferenceID = request.PaymentDetails[0].PlutusTransactionReferenceID,
                    TransactionLogId = request.PaymentDetails[0].TransactionLogId,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress,
                    UniqueId = Guid.NewGuid().ToString()
                };

                int receiptId = Convert.ToInt32(receipt.Create(_sqlHelper, tnx));
                _log.Info($"Receipt created for Patient Advance. ReceiptId={receiptId}");

                // ── 2. Receipt Payment Mode Details ──────────────────────────────────
                foreach (var p in request.PaymentDetails)
                {
                    // PaymentModeTypeId 4 = Credit → skip
                    if (p.PaymentModeTypeId == 4)
                        continue;

                    var rpmd = new ReceiptsPaymentModeDetails
                    {
                        HospId = globalValues.hospId,
                        BranchId = request.BranchId,
                        ReceiptID = receiptId,
                        Amount = p.Amount,
                        PaymentModeId = p.PaymentModeId,
                        BankId = p.BankId > 0 ? p.BankId : (int?)null,
                        ReferenceNo = p.RefNo,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    };

                    rpmd.Create(_sqlHelper, tnx);
                }

                // ── 3. PatientLedgerBill (insert or update running balance) ─────────
                var ledgerBill = new PatientLedgerBill
                {
                    PatientId = request.PatientId,
                    TransactionType = request.IsRefund == 0 ? LedgerBillTransactionType.Credit : LedgerBillTransactionType.Refund,
                    LedgerId = request.PatientLedgerId,
                    Amount = totalPaidAmount,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                };

                int ledgerId = Convert.ToInt32(ledgerBill.Create(_sqlHelper, tnx));
                _log.Info($"PatientLedgerBill upserted. LedgerId={ledgerId}");

                // ── 4. PatientLedgerDetails (transaction history row) ────────────────
                var ledgerDetails = new PatientLedgerDetails
                {
                    PatientId = request.PatientId,
                    LedgerId = ledgerId,
                    TransactionType = request.IsRefund == 0 ? LedgerTransactionType.Credit : LedgerTransactionType.Refund,
                    Amount = totalPaidAmount,
                    VisitId = 0,
                    BillId = 0,
                    ReceiptId = receiptId,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                };




                ledgerDetails.Create(_sqlHelper, tnx);

                tnx.Commit();
                _log.Info($"SavePatientAdvance committed. PatientId={request.PatientId}, LedgerId={ledgerId}, ReceiptId={receiptId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SavePatientAdvanceResponse>.Success(
                    new SavePatientAdvanceResponse
                    {
                        PatientId = request.PatientId,
                        LedgerId = ledgerId,
                        ReceiptId = receiptId
                    },
                    alert.Type,
                    "Patient advance saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SavePatientAdvanceResponse>.Failure(
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

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetPatientLedgerReceiptDetails(
    int receiptId,
    int patientId,
    int ledgerId)
        {
            try
            {
                _log.Info($"GetPatientLedgerReceiptDetails called. ReceiptId={receiptId}, PatientId={patientId}, LedgerId={ledgerId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientLedgerReceiptDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @receiptId = receiptId,
                        @patientId = patientId,
                        @ledgerId = ledgerId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No ledger receipt details found for ReceiptId={receiptId}, PatientId={patientId}, LedgerId={ledgerId}");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        "No ledger receipt details found",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetPatientLedgerReceiptDetails retrieved {result.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
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
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetPatientAdvanceReceiptList(int patientId, int receiptId)
        {
            try
            {
                _log.Info($"GetPatientAdvanceReceiptList called. PatientId={patientId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientAdvanceReceiptList",
                    CommandType.StoredProcedure,
                    new
                    {
                        @patientId = patientId,
                        @receiptId = receiptId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No advance receipt list found for PatientId={patientId}");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        "No advance receipt list found for this patient",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetPatientAdvanceReceiptList retrieved {result.Count} record(s) for PatientId={patientId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} advance receipt record(s) retrieved successfully",
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

        public ServiceResult<object> GetBillToRefund(string receiptNo, string billNo, string uhid, string patientName)
        {
            try
            {
                _log.Info($"GetBillToRefund called. ReceiptNo={receiptNo}, BillNo={billNo}, UHID={uhid}, PatientName={patientName}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetOPDBillForRefund",
                    CommandType.StoredProcedure,
                    new
                    {
                        @receiptNo = string.IsNullOrWhiteSpace(receiptNo) ? (object)DBNull.Value : receiptNo,
                        @billNo = string.IsNullOrWhiteSpace(billNo) ? (object)DBNull.Value : billNo,
                        @uhid = string.IsNullOrWhiteSpace(uhid) ? (object)DBNull.Value : uhid,
                        @patientName = string.IsNullOrWhiteSpace(patientName) ? (object)DBNull.Value : patientName
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No bills found for refund with the given filters");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No bills found for refund",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetBillToRefund retrieved {result.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} bill(s) retrieved successfully",
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
        public ServiceResult<object> GetBillDetailsToRefund(int visitId)
        {
            try
            {
                _log.Info($"GetBillDetailsToRefund called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetOPDBillDetailsForRefund",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No bill details found for refund. VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No bill details found for refund",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetBillDetailsToRefund retrieved {result.Count} record(s) for VisitId={visitId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} bill detail(s) retrieved successfully",
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

        public ServiceResult<object> GetOPDPackageServicesForRefund(int visitId, int packageId)
        {
            try
            {
                _log.Info($"GetOPDPackageServicesForRefund called. VisitId={visitId}, PackageId={packageId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetOPDPackageServicesforRefund",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId, @packageId = packageId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No package services found for refund. VisitId={visitId}, PackageId={packageId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No package services found for refund",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetOPDPackageServicesForRefund retrieved {result.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} package service(s) retrieved successfully",
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

        public ServiceResult<SaveOPDRefundBillingResponse> SaveOPDRefundBilling(
    SaveOPDRefundBillingRequest request,
    AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SaveOPDRefundBilling called. PatientId={request.VisitDetails.PatientId}, BranchId={request.VisitDetails.BranchId}");

                var v = request.VisitDetails;
                decimal totalPaidAmount = 0;
                if (request.PaymentDetails?.Count > 0)
                    totalPaidAmount = request.PaymentDetails.Sum(p => p.Amount);

                // ── 1. PatientVisitDetails ───────────────────────────────────────────
                var pvd = new PatientVisitDetails
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    PatientId = v.PatientId,
                    Uhid = v.Uhid,
                    visitType = VisitType.OPD,
                    CurrentAge = v.CurrentAge,
                    DoctorId = 0,
                    CorporateId = v.CorporateId,
                    InsuranceCompanyId = v.InsuranceCompanyId,
                    ReferDoctorId = v.ReferDoctorId > 0 ? v.ReferDoctorId : (int?)null,
                    TotalBillAmount = v.GrossBillAmount,
                    TotalDiscountPerOnBill = v.TotalDiscPerOnBill,
                    TotalDiscountAmountOnBill = v.TotalDiscAmtOnBill,
                    DiscountApprovedById = v.DiscApprovedById > 0 ? v.DiscApprovedById : (int?)null,
                    DiscountReason = v.DiscountReason,
                    RoundOff = v.RoundOff,
                    TotalPatientPayableAmount = v.NetAmount,
                    TotalPaidAmount = totalPaidAmount,
                    TotalBalanceAmount = v.NetAmount - totalPaidAmount,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress,
                    UniqueId = v.UniqueId,
                    Mlc = v.Mlc,
                    Pi = v.Pi,
                    Remark = v.Remark,
                    PolicyNo = v.PolicyNo,
                    PolicyCardNo = v.PolicyCardNo,
                    ExpiryDate = v.ExpiryDate,
                    CardHolder = v.CardHolder,
                    ReferalNo = v.ReferalNo,
                    ReferalDate = v.ReferalDate,
                    ProId = v.ProId,
                    ProName = v.ProName,
                    IsSendMRD = v.IsSendMRD
                };

                int visitId = Convert.ToInt32(pvd.Create(_sqlHelper, tnx));
                _log.Info($"PatientVisitDetails created for refund. VisitId={visitId}");


                var pbd = new PatientBillDetails
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    RoleId = v.RoleId,
                    PatientId = v.PatientId,
                    VisitId = visitId,
                    TypeId = 1,
                    TotalBillAmount = v.GrossBillAmount,
                    TotalDiscountPerOnBill = v.TotalDiscPerOnBill,
                    TotalDiscountAmountOnBill = v.TotalDiscAmtOnBill,
                    DiscountApprovedById = v.DiscApprovedById > 0 ? v.DiscApprovedById : (int?)null,
                    DiscountReason = v.DiscountReason,
                    RoundOff = v.RoundOff,
                    TotalPayableAmount = v.NetAmount,
                    TotalPaidAmount = totalPaidAmount,
                    TotalBalanceAmount = v.NetAmount - totalPaidAmount,
                    TotalPatientPayableAmount = v.NetAmount,
                    TotalCorporatePayableAmount = 0,
                    TotalPatientPaidAmount = totalPaidAmount,
                    TotalCorporatePaidAmount = 0,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                };

                int billId = Convert.ToInt32(pbd.Create(_sqlHelper, tnx));
                _log.Info($"PatientBillDetails created. BillId={billId}");

                // ── 2. FinancialTransactions ─────────────────────────────────────────
                var ft = new FinancialTransactions
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    VisitId = visitId,
                    BillId = billId,
                    PatientId = v.PatientId,
                    tnxType = TnxType.OPDRefund,
                    GrossAmount = v.GrossBillAmount,
                    DiscountPercentage = v.TotalDiscPerOnBill,
                    DiscountAmount = v.TotalDiscAmtOnBill,
                    RoundOff = v.RoundOff,
                    NetAmount = v.NetAmount,
                    Remarks = v.Remarks,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress,
                    UniqueId = v.UniqueId
                };

                int ftid = Convert.ToInt32(ft.Create(_sqlHelper, tnx));
                _log.Info($"FinancialTransactions created for refund. FTID={ftid}");

                // ── 3. Process refund items ──────────────────────────────────────────
                foreach (var item in request.RefundItems)
                {
                    var ftd = new FinancialTransactionDetails
                    {
                        HospId = globalValues.hospId,
                        BranchId = v.BranchId,
                        FTID = ftid,
                        VisitId = visitId,
                        BillId = billId,
                        PatientId = v.PatientId,
                        ServiceItemId = item.ServiceItemId,
                        SubSubCategoryId = item.SubSubCategoryId,
                        ServiceName = item.ServiceName,
                        ServiceCode = item.Code,
                        CorporateAlias = item.CorporateAlias,
                        CorporateCode = item.CorporateCode,
                        DoctorId = item.DoctorId > 0 ? item.DoctorId : (int?)null,
                        CorporateId = v.CorporateId > 0 ? v.CorporateId : (int?)null,
                        Rate = item.Rate,
                        Qty = (-1) * item.Qty,                 // negative qty for refund
                        GrossAmt = item.GrossAmt,
                        DiscPer = item.DiscPer,
                        DiscAmt = item.DiscAmt,
                        NetAmt = item.NetAmt,
                        IsCorporateNonPayable = item.IsNonPayable,
                        IsUnderPackage = item.IsUnderPackage,
                        RateListId = item.RateListId,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress,
                        FromFTDID = item.FTDId
                    };

                    int newFtdId = Convert.ToInt32(ftd.Create(_sqlHelper, tnx));
                    _log.Info($"FinancialTransactionDetails (refund) created. NewFTDId={newFtdId}, OriginalFTDId={item.FTDId}");

                    // ── 3a. Consultation refund validation/cancel ────────────────────
                    if (item.CategoryId == 1)
                    {
                        if (ValidateDoctorVisitForRefund(tnx, item.FTDId))
                        {
                            _sqlHelper.DML(tnx, "U_CancelDoctorAppointment", CommandType.StoredProcedure, new
                            {
                                @UserId = globalValues.userId,
                                @FTDID = item.FTDId
                            });
                        }
                        else
                        {
                            tnx.Rollback();
                            var alertBusy = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                            return ServiceResult<SaveOPDRefundBillingResponse>.Failure(
                                alertBusy.Type,
                                $"Patient is either out from Temperature Room or Doctor Appointment is done for '{item.ServiceName}'. Cannot refund now.",
                                409
                            );
                        }
                    }
                    // ── 3b. Investigation refund validation/cancel ───────────────────
                    else if (item.CategoryId == 3)
                    {
                        if (ValidatePatientInvestigationForRefund(tnx, item.FTDId))
                        {
                            _sqlHelper.DML(tnx, "U_CancelPatientInvestigationDetails", CommandType.StoredProcedure, new
                            {
                                @UserId = globalValues.userId,
                                @FTDID = item.FTDId
                            });
                        }
                        else
                        {
                            tnx.Rollback();
                            var alertBusy = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                            return ServiceResult<SaveOPDRefundBillingResponse>.Failure(
                                alertBusy.Type,
                                $"Lab processing already started for '{item.ServiceName}'. Cannot refund now.",
                                409
                            );
                        }
                    }

                    // ── 3c. Update refunded quantity on the original FTD row ─────────
                    _sqlHelper.DML(tnx, "U_UpdateFTDRefundQTY", CommandType.StoredProcedure, new
                    {
                        @FTDId = item.FTDId,
                        @refundQty = Convert.ToInt32(item.Qty),
                        @userId = globalValues.userId
                    });
                }

                // ── 4. Receipt (negative amount for refund) ─────────────────────────
                int receiptId = 0;
                if (totalPaidAmount > 0)
                {
                    var receipt = new Receipts
                    {
                        HospId = globalValues.hospId,
                        BranchId = v.BranchId,
                        BillId = billId,
                        VisitId = visitId,
                        PatientId = v.PatientId,
                        Amount = (-1) * totalPaidAmount,
                        PlutusTransactionReferenceID = request.PaymentDetails[0].PlutusTransactionReferenceID,
                        TransactionLogId = request.PaymentDetails[0].TransactionLogId,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress,
                        UniqueId = v.UniqueId
                    };

                    receiptId = Convert.ToInt32(receipt.Create(_sqlHelper, tnx));
                    _log.Info($"Refund Receipt created. ReceiptId={receiptId}");

                    foreach (var p in request.PaymentDetails)
                    {
                        // PaymentModeTypeId 4 = Credit → skip
                        if (p.PaymentModeTypeId == 4)
                            continue;

                        var rpmd = new ReceiptsPaymentModeDetails
                        {
                            HospId = globalValues.hospId,
                            BranchId = v.BranchId,
                            ReceiptID = receiptId,
                            Amount = p.Amount,
                            PaymentModeId = p.PaymentModeId,
                            BankId = p.BankId > 0 ? p.BankId : (int?)null,
                            ReferenceNo = p.RefNo,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };

                        rpmd.Create(_sqlHelper, tnx);
                    }
                }

                tnx.Commit();
                _log.Info($"SaveOPDRefundBilling committed. VisitId={visitId}, FTID={ftid}, ReceiptId={receiptId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveOPDRefundBillingResponse>.Success(
                    new SaveOPDRefundBillingResponse
                    {
                        VisitId = visitId,
                        FTID = ftid,
                        ReceiptId = receiptId
                    },
                    alert.Type,
                    "OPD Refund saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveOPDRefundBillingResponse>.Failure(
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

        /// <summary>
        /// Returns true if the doctor appointment (FTDId) is still eligible for refund
        /// (patient not yet out of temperature room AND consultation not done)
        /// </summary>
        private bool ValidateDoctorVisitForRefund(SqlTransaction tnx, int ftdId)
        {
            object result = _sqlHelper.ExecuteScalar(
                tnx,
                "S_ValidateDoctorVisitforRefund",
                CommandType.StoredProcedure,
                new { @FTDId = ftdId }
            );

            int resultValue = (result == null || Convert.IsDBNull(result)) ? 0 : Convert.ToInt32(result);
            return resultValue <= 0;
        }

        /// <summary>
        /// Returns true if the patient investigation (FTDId) is still eligible for refund
        /// (no sample collected/segregated/received/result/approval yet)
        /// </summary>
        private bool ValidatePatientInvestigationForRefund(SqlTransaction tnx, int ftdId)
        {
            object result = _sqlHelper.ExecuteScalar(
                tnx,
                "S_ValidatePatientInvestigationforRefund",
                CommandType.StoredProcedure,
                new { @FTDId = ftdId }
            );

            int resultValue = (result == null || Convert.IsDBNull(result)) ? 0 : Convert.ToInt32(result);
            return resultValue <= 0;
        }

        public ServiceResult<SaveOPDRefundRequestApprovalResponse> SaveOPDRefundRequestApproval(
            SaveOPDRefundRequestApprovalRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveOPDRefundRequestApproval called. PatientId={request.VisitDetails.PatientId}, BranchId={request.VisitDetails.BranchId}");

                var v = request.VisitDetails;

                // I_OPDRefundRequestDetails uses a true OUTPUT parameter (no trailing SELECT @Result;),
                // so RunProcedureInsert is required here (reads the OUTPUT param value directly).
                long refundIdResult = _sqlHelper.RunProcedureInsert(
                    "I_OPDRefundRequestDetails",
                    new IDataParameter[]
                    {
                new SqlParameter("@BranchId", v.BranchId),
                new SqlParameter("@RoleId", v.RoleId),
                new SqlParameter("@PatientId", v.PatientId),
                new SqlParameter("@VisitId", v.VisitId),
                new SqlParameter("@TotalBillAmount", v.GrossBillAmount),
                new SqlParameter("@TotalDiscountPerOnBill", v.TotalDiscPerOnBill),
                new SqlParameter("@TotalDiscountAmountOnBill", v.TotalDiscAmtOnBill),
                new SqlParameter("@RoundOff", v.RoundOff),
                new SqlParameter("@TotalRefundAmount", v.NetAmount),
                new SqlParameter("@RefundApprovedID", v.RefundApprovedID),
                new SqlParameter("@RefundApprovedName", (object)v.RefundApprovedName ?? DBNull.Value),
                new SqlParameter("@RefundReason", (object)v.RefundReason ?? DBNull.Value),
                new SqlParameter("@RefundRemark", (object)v.RefundRemark ?? DBNull.Value),
                new SqlParameter("@UserId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int refundId = Convert.ToInt32(refundIdResult);
                _log.Info($"OPDRefundRequestDetails created. RefundId={refundId}");

                // ── Insert refund item rows ──────────────────────────────────────────────
                foreach (var item in request.BillingItems)
                {
                    _sqlHelper.DML(
                        "I_OPDRefundRequestItemDetails",
                        CommandType.StoredProcedure,
                        new
                        {
                            @RefundId = refundId,
                            @FTDId = item.FTDId,
                            @RefundQty = item.RefundQty,
                            @UserId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        });
                }

                _log.Info($"SaveOPDRefundRequestApproval completed. RefundId={refundId}, ItemCount={request.BillingItems.Count}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveOPDRefundRequestApprovalResponse>.Success(
                    new SaveOPDRefundRequestApprovalResponse { RefundId = refundId },
                    alert.Type,
                    "OPD Refund request saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveOPDRefundRequestApprovalResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> ApproveOPDRefundRequest(ApproveOPDRefundRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ApproveOPDRefundRequest called. RefundId={request.RefundId}, Flag={request.Flag}");

                _sqlHelper.DML(
                    "U_ApproveOPDRefundRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @RefundId = request.RefundId,
                        @flag = request.Flag,
                        @ApprovalRemarks = (object)request.ApprovalRemarks ?? DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"ApproveOPDRefundRequest completed. RefundId={request.RefundId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "OPD Refund request approval updated successfully",
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

        public ServiceResult<string> CancelOPDRefundRequest(CancelOPDRefundRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CancelOPDRefundRequest called. RefundId={request.RefundId}");

                _sqlHelper.DML(
                    "U_CancelOPDRefundRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @RefundId = request.RefundId,
                        @CancelReason = (object)request.CancelReason ?? DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"CancelOPDRefundRequest completed. RefundId={request.RefundId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "OPD Refund request cancelled successfully",
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

        public ServiceResult<string> paymentOPDRefundRequest(paymentOPDRefundRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"paymentOPDRefundRequest called. RefundId={request.RefundId}");

                _sqlHelper.DML(
                    "U_PaymentOPDRefundRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @RefundId = request.RefundId,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"paymentOPDRefundRequest completed. RefundId={request.RefundId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "OPD Refund marked as collected successfully",
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

        public ServiceResult<object> GetOPDRefundRequestListForApproval(string fromDate, string toDate, int branchId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetOPDRefundRequestListForApproval called. FromDate={fromDate}, ToDate={toDate}, BranchId={branchId}");

                if (!DateTime.TryParse(fromDate, out DateTime parsedFromDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid FromDate format", 400);
                }

                if (!DateTime.TryParse(toDate, out DateTime parsedToDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid ToDate format", 400);
                }

                var dataTable = _sqlHelper.GetDataTable(
                    "S_OPDRefundRequestDetailsForDiscountApproval",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = parsedFromDate.ToString("yyyy-MM-dd"),
                        @toDate = parsedToDate.ToString("yyyy-MM-dd"),
                        @branchId = branchId,
                        @userId = globalValues.userId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("GetOPDRefundRequestListForApproval: no records found");
                    return ServiceResult<object>.Failure(alert.Type, "No refund requests found", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetOPDRefundRequestListForApproval retrieved {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} refund request(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetOPDRefundRequestDetailsByRefundId(int refundId)
        {
            try
            {
                _log.Info($"GetOPDRefundRequestDetailsByRefundId called. RefundId={refundId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_OPDRefundRequestDetailsByRefundId",
                    CommandType.StoredProcedure,
                    new { @RefundId = refundId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No refund details found for RefundId={refundId}");
                    return ServiceResult<object>.Failure(alert.Type, "No refund details found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Refund details retrieved successfully for RefundId={refundId}. Rows={result.Count}");

                return ServiceResult<object>.Success(result, "Info", "Refund details retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetOPDRefundRequestApprovalDetails(int refundId)
        {
            try
            {
                _log.Info($"GetOPDRefundRequestApprovalDetails called. RefundId={refundId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetOPDRefundRequestApprovalDetails",
                    CommandType.StoredProcedure,
                    new { @RefundId = refundId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No approval details found for RefundId={refundId}");
                    return ServiceResult<object>.Failure(alert.Type, "No approval details found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Approval details retrieved successfully for RefundId={refundId}");

                return ServiceResult<object>.Success(result, "Info", "Approval details retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetBillReceiptReprintDetails(
    string branchId, string uhid, string name, int type,
    string billNo, string receiptNo, string fromDate, string toDate)
        {
            try
            {
                _log.Info($"GetBillReceiptReprintDetails called. BranchId={branchId}, UHID={uhid}, Name={name}, Type={type}, BillNo={billNo}, ReceiptNo={receiptNo}, FromDate={fromDate}, ToDate={toDate}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetBillReceiptReprintDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @UHID = uhid ?? string.Empty,
                        @Name = name ?? string.Empty,
                        @Type = type,
                        @BillNo = billNo ?? string.Empty,
                        @ReceiptNo = receiptNo ?? string.Empty,
                        @FromDate = fromDate,
                        @Todate = toDate
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No bill/receipt reprint details found for BranchId={branchId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No bill/receipt reprint details found",
                        404
                    );
                }

                // Return raw DataTable as list of dictionaries without model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Bill/receipt reprint details retrieved successfully. Rows={result.Count}");

                return ServiceResult<object>.Success(
                    result,
                    "Info",
                    $"Bill/receipt reprint details retrieved successfully",
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

        public ServiceResult<object> GetBillForCreditNote(
    string fromDate,
    string toDate,
    string billNo,
    string uhid,
    string patientName,
    int typeId)
        {
            try
            {
                _log.Info($"GetBillForCreditNote called. FromDate={fromDate}, ToDate={toDate}, BillNo={billNo}, Uhid={uhid}, PatientName={patientName}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetBillForCreditNote",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = string.IsNullOrWhiteSpace(fromDate) ? (object)DBNull.Value : fromDate,
                        @toDate = string.IsNullOrWhiteSpace(toDate) ? (object)DBNull.Value : toDate,
                        @billNo = string.IsNullOrWhiteSpace(billNo) ? (object)DBNull.Value : billNo,
                        @uhid = string.IsNullOrWhiteSpace(uhid) ? (object)DBNull.Value : uhid,
                        @patientName = string.IsNullOrWhiteSpace(patientName) ? (object)DBNull.Value : patientName,
                        @typeId = typeId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("GetBillForCreditNote: no records found");
                    return ServiceResult<object>.Failure(alert.Type, "No bills found for credit note", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetBillForCreditNote retrieved {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} bill(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetBillDetailsForCreditNote(int visitId)
        {
            try
            {
                _log.Info($"GetBillDetailsForCreditNote called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetBillDetailsForCreditNote",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No bill details found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(alert.Type, "No bill details found for credit note", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetBillDetailsForCreditNote retrieved {rows.Count} record(s) for VisitId={visitId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} item(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }


        public ServiceResult<SaveCreditNoteResponse> SaveCreditNote(
 SaveCreditNoteRequest request,
 AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveCreditNote called. PatientId={request.VisitDetails.PatientId}, BranchId={request.VisitDetails.BranchId}, BillId={request.VisitDetails.BillId}");

                var v = request.VisitDetails;

                // I_CreditNoteDetails uses a true OUTPUT parameter (no trailing SELECT @Result;),
                // so RunProcedureInsert is required here.
                long creditNoteIdResult = _sqlHelper.RunProcedureInsert(
                    "I_CreditNoteDetails",
                    new IDataParameter[]
                    {
                new SqlParameter("@BranchId", v.BranchId),
                new SqlParameter("@RoleId", v.RoleId),
                new SqlParameter("@PatientId", v.PatientId),
                new SqlParameter("@VisitId", v.VisitId),
                new SqlParameter("@BillId", v.BillId),
                new SqlParameter("@TotalCreditNoteAmount", v.TotalCreditNoteAmount),
                new SqlParameter("@CreditNoteApprovedID", v.CreditNoteApprovedID),
                new SqlParameter("@CreditNoteApprovedName", (object)v.CreditNoteApprovedName ?? DBNull.Value),
                new SqlParameter("@CreditNoteReason", (object)v.CreditNoteReason ?? DBNull.Value),
                new SqlParameter("@CreditNoteRemark", (object)v.CreditNoteRemark ?? DBNull.Value),
                new SqlParameter("@UserId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int creditNoteId = Convert.ToInt32(creditNoteIdResult);
                _log.Info($"CreditNoteDetails created. CreditNoteId={creditNoteId}");

                _sqlHelper.DML(
                "U_CreditNoteAmount",
                CommandType.StoredProcedure,
                new
                {
                    @VisitId = v.VisitId,
                    @BillId = v.BillId,
                    @TotalCreditNoteAmount = v.TotalCreditNoteAmount,
                    @UserId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                });


                // ── Insert credit note item rows ─────────────────────────────────────────
                foreach (var item in request.BillingItems)
                {
                    _sqlHelper.DML(
                        "I_CreditNoteItemDetails",
                        CommandType.StoredProcedure,
                        new
                        {
                            @CreditNoteId = creditNoteId,
                            @FTDId = item.FTDId,
                            @CreditNotePer = item.CreditNotePer,
                            @CreditNoteAmt = item.CreditNoteAmt,
                            @UserId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        });

                    _sqlHelper.DML(
                "U_CreditNoteAmountItemWise",
                CommandType.StoredProcedure,
                new
                {
                    @ftdId = item.FTDId,
                    @CreditNoteAmt = item.CreditNoteAmt,
                    @UserId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                });

                }

                _log.Info($"SaveCreditNote completed. CreditNoteId={creditNoteId}, ItemCount={request.BillingItems.Count}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveCreditNoteResponse>.Success(
                    new SaveCreditNoteResponse { CreditNoteId = creditNoteId },
                    alert.Type,
                    "Credit note saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveCreditNoteResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }
        public ServiceResult<SaveCreditNoteRequestApprovalResponse> SaveCreditNoteRequestApproval(
    SaveCreditNoteRequestApprovalRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveCreditNoteRequestApproval called. PatientId={request.VisitDetails.PatientId}, BranchId={request.VisitDetails.BranchId}, BillId={request.VisitDetails.BillId}");

                var v = request.VisitDetails;

                // I_CreditNoteRequestDetails uses a true OUTPUT parameter (no trailing SELECT @Result;),
                // so RunProcedureInsert is required here (reads the OUTPUT param value directly).
                long creditNoteIdResult = _sqlHelper.RunProcedureInsert(
                    "I_CreditNoteRequestDetails",
                    new IDataParameter[]
                    {
                new SqlParameter("@BranchId", v.BranchId),
                new SqlParameter("@RoleId", v.RoleId),
                new SqlParameter("@PatientId", v.PatientId),
                new SqlParameter("@VisitId", v.VisitId),
                new SqlParameter("@BillId", v.BillId),
                new SqlParameter("@TotalCreditNoteAmount", v.TotalCreditNoteAmount),
                new SqlParameter("@CreditNoteApprovedID", v.CreditNoteApprovedID),
                new SqlParameter("@CreditNoteApprovedName", (object)v.CreditNoteApprovedName ?? DBNull.Value),
                new SqlParameter("@CreditNoteReason", (object)v.CreditNoteReason ?? DBNull.Value),
                new SqlParameter("@CreditNoteRemark", (object)v.CreditNoteRemark ?? DBNull.Value),
                new SqlParameter("@UserId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int creditNoteId = Convert.ToInt32(creditNoteIdResult);
                _log.Info($"CreditNoteRequestDetails created. CreditNoteId={creditNoteId}");

                // ── Insert credit note item rows ─────────────────────────────────────────
                foreach (var item in request.BillingItems)
                {
                    _sqlHelper.DML(
                        "I_CreditNoteRequestItemDetails",
                        CommandType.StoredProcedure,
                        new
                        {
                            @CreditNoteId = creditNoteId,
                            @FTDId = item.FTDId,
                            @CreditNotePer = item.CreditNotePer,
                            @CreditNoteAmt = item.CreditNoteAmt,
                            @UserId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        });
                }

                _log.Info($"SaveCreditNoteRequestApproval completed. CreditNoteId={creditNoteId}, ItemCount={request.BillingItems.Count}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveCreditNoteRequestApprovalResponse>.Success(
                    new SaveCreditNoteRequestApprovalResponse { CreditNoteId = creditNoteId },
                    alert.Type,
                    "Credit note request saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveCreditNoteRequestApprovalResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> ApproveCreditNoteRequest(ApproveCreditNoteRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ApproveCreditNoteRequest called. CreditNoteId={request.CreditNoteId}, Flag={request.Flag}");

                _sqlHelper.DML(
                    "U_ApproveCreditNoteRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @CreditNoteId = request.CreditNoteId,
                        @flag = request.Flag,
                        @ApprovalRemarks = (object)request.ApprovalRemarks ?? DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"ApproveCreditNoteRequest completed. CreditNoteId={request.CreditNoteId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Credit note request approval updated successfully",
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

        public ServiceResult<string> CancelCreditNoteRequest(CancelCreditNoteRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CancelCreditNoteRequest called. CreditNoteId={request.CreditNoteId}");

                _sqlHelper.DML(
                    "U_CancelCreditNoteRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @CreditNoteId = request.CreditNoteId,
                        @CancelReason = (object)request.CancelReason ?? DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"CancelCreditNoteRequest completed. CreditNoteId={request.CreditNoteId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Credit note request cancelled successfully",
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

        public ServiceResult<string> CollectCreditNoteRequest(CollectCreditNoteRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CollectCreditNoteRequest called. CreditNoteId={request.CreditNoteId}");

                _sqlHelper.DML(
                    "U_CollectCreditNoteRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @CreditNoteId = request.CreditNoteId,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"CollectCreditNoteRequest completed. CreditNoteId={request.CreditNoteId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Credit note marked as created successfully",
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

        public ServiceResult<object> GetCreditNoteRequestListForApproval(string fromDate, string toDate, int branchId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetCreditNoteRequestListForApproval called. FromDate={fromDate}, ToDate={toDate}, BranchId={branchId}");

                if (!DateTime.TryParse(fromDate, out DateTime parsedFromDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid FromDate format", 400);
                }

                if (!DateTime.TryParse(toDate, out DateTime parsedToDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid ToDate format", 400);
                }

                var dataTable = _sqlHelper.GetDataTable(
                    "S_CreditNoteRequestDetailsForDiscountApproval",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = parsedFromDate.ToString("yyyy-MM-dd"),
                        @toDate = parsedToDate.ToString("yyyy-MM-dd"),
                        @branchId = branchId,
                        @userId = globalValues.userId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("GetCreditNoteRequestListForApproval: no records found");
                    return ServiceResult<object>.Failure(alert.Type, "No credit note requests found", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetCreditNoteRequestListForApproval retrieved {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} credit note request(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCreditNoteRequestDetailsByCreditNoteId(int creditNoteId)
        {
            try
            {
                _log.Info($"GetCreditNoteRequestDetailsByCreditNoteId called. CreditNoteId={creditNoteId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_CreditNoteRequestDetailsByCreditNoteId",
                    CommandType.StoredProcedure,
                    new { @CreditNoteId = creditNoteId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No credit note details found for CreditNoteId={creditNoteId}");
                    return ServiceResult<object>.Failure(alert.Type, "No credit note details found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Credit note details retrieved successfully for CreditNoteId={creditNoteId}. Rows={result.Count}");

                return ServiceResult<object>.Success(result, "Info", "Credit note details retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCreditNoteRequestApprovalDetails(int creditNoteId)
        {
            try
            {
                _log.Info($"GetCreditNoteRequestApprovalDetails called. CreditNoteId={creditNoteId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetCreditNoteRequestApprovalDetails",
                    CommandType.StoredProcedure,
                    new { @CreditNoteId = creditNoteId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No approval details found for CreditNoteId={creditNoteId}");
                    return ServiceResult<object>.Failure(alert.Type, "No approval details found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Approval details retrieved successfully for CreditNoteId={creditNoteId}");

                return ServiceResult<object>.Success(result, "Info", "Approval details retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }







        public ServiceResult<object> GetBillForWriteOff(
      string fromDate,
      string toDate,
      string billNo,
      string uhid,
      string patientName,
      int typeId)
        {
            try
            {
                _log.Info($"GetBillForWriteOff called. FromDate={fromDate}, ToDate={toDate}, BillNo={billNo}, Uhid={uhid}, PatientName={patientName}, TypeId={typeId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetBillForWriteOff",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = string.IsNullOrWhiteSpace(fromDate) ? (object)DBNull.Value : fromDate,
                        @toDate = string.IsNullOrWhiteSpace(toDate) ? (object)DBNull.Value : toDate,
                        @billNo = string.IsNullOrWhiteSpace(billNo) ? (object)DBNull.Value : billNo,
                        @uhid = string.IsNullOrWhiteSpace(uhid) ? (object)DBNull.Value : uhid,
                        @patientName = string.IsNullOrWhiteSpace(patientName) ? (object)DBNull.Value : patientName,
                        @typeId = typeId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("GetBillForWriteOff: no records found");
                    return ServiceResult<object>.Failure(alert.Type, "No bills found for writeoff", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetBillForWriteOff retrieved {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} bill(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetBillDetailsForWriteOff(int visitId)
        {
            try
            {
                _log.Info($"GetBillDetailsForWriteOff called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetBillDetailsForWriteOff",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No bill details found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(alert.Type, "No bill details found for writeoff", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetBillDetailsForWriteOff retrieved {rows.Count} record(s) for VisitId={visitId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} item(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<SaveWriteOffResponse> SaveWriteOff(
         SaveWriteOffRequest request,
         AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveWriteOff called. PatientId={request.PatientId}, BranchId={request.BranchId}, BillId={request.BillId}");

                // I_WriteOffDetails uses a true OUTPUT parameter (no trailing SELECT @Result;),
                // so RunProcedureInsert is required here. No item table for WriteOff — header only.
                long writeOffIdResult = _sqlHelper.RunProcedureInsert(
                    "I_WriteOffDetails",
                    new IDataParameter[]
                    {
                new SqlParameter("@BranchId", request.BranchId),
                new SqlParameter("@RoleId", request.RoleId),
                new SqlParameter("@PatientId", request.PatientId),
                new SqlParameter("@VisitId", request.VisitId),
                new SqlParameter("@BillId", request.BillId),
                new SqlParameter("@TotalWriteOffAmount", request.TotalWriteOffAmount),
                new SqlParameter("@WriteOffApprovedID", request.WriteOffApprovedID),
                new SqlParameter("@WriteOffApprovedName", (object)request.WriteOffApprovedName ?? DBNull.Value),
                new SqlParameter("@WriteOffReason", (object)request.WriteOffReason ?? DBNull.Value),
                new SqlParameter("@WriteOffRemark", (object)request.WriteOffRemark ?? DBNull.Value),
                new SqlParameter("@UserId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int writeOffId = Convert.ToInt32(writeOffIdResult);
                _log.Info($"SaveWriteOff completed. WriteOffId={writeOffId}");

                _sqlHelper.DML(
                  "U_WriteOffAmount",
                  CommandType.StoredProcedure,
                  new
                  {
                      @VisitId = request.VisitId,
                      @BillId = request.BillId,
                      @TotalWriteOffAmount = request.TotalWriteOffAmount,
                      @UserId = globalValues.userId,
                      @IpAddress = globalValues.ipAddress
                  });


                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveWriteOffResponse>.Success(
                    new SaveWriteOffResponse { WriteOffId = writeOffId },
                    alert.Type,
                    "Write-off saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveWriteOffResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<SaveWriteOffRequestApprovalResponse> SaveWriteOffRequestApproval(
    SaveWriteOffRequestApprovalRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveWriteOffRequestApproval called. PatientId={request.PatientId}, BranchId={request.BranchId}, BillId={request.BillId}");

                // I_WriteOffRequestDetails uses a true OUTPUT parameter (no trailing SELECT @Result;),
                // so RunProcedureInsert is required here. No item table exists for WriteOff — header only.
                long writeOffIdResult = _sqlHelper.RunProcedureInsert(
                    "I_WriteOffRequestDetails",
                    new IDataParameter[]
                    {
                new SqlParameter("@BranchId", request.BranchId),
                new SqlParameter("@RoleId", request.RoleId),
                new SqlParameter("@PatientId", request.PatientId),
                new SqlParameter("@VisitId", request.VisitId),
                new SqlParameter("@BillId", request.BillId),
                new SqlParameter("@TotalWriteOffAmount", request.TotalWriteOffAmount),
                new SqlParameter("@WriteOffApprovedID", request.WriteOffApprovedID),
                new SqlParameter("@WriteOffApprovedName", (object)request.WriteOffApprovedName ?? DBNull.Value),
                new SqlParameter("@WriteOffReason", (object)request.WriteOffReason ?? DBNull.Value),
                new SqlParameter("@WriteOffRemark", (object)request.WriteOffRemark ?? DBNull.Value),
                new SqlParameter("@UserId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int writeOffId = Convert.ToInt32(writeOffIdResult);
                _log.Info($"SaveWriteOffRequestApproval completed. WriteOffId={writeOffId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveWriteOffRequestApprovalResponse>.Success(
                    new SaveWriteOffRequestApprovalResponse { WriteOffId = writeOffId },
                    alert.Type,
                    "WriteOff request saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveWriteOffRequestApprovalResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> ApproveWriteOffRequest(ApproveWriteOffRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ApproveWriteOffRequest called. WriteOffId={request.WriteOffId}, Flag={request.Flag}");

                _sqlHelper.DML(
                    "U_ApproveWriteOffRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @WriteOffId = request.WriteOffId,
                        @flag = request.Flag,
                        @ApprovalRemarks = (object)request.ApprovalRemarks ?? DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"ApproveWriteOffRequest completed. WriteOffId={request.WriteOffId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "WriteOff request approval updated successfully",
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

        public ServiceResult<string> CancelWriteOffRequest(CancelWriteOffRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CancelWriteOffRequest called. WriteOffId={request.WriteOffId}");

                _sqlHelper.DML(
                    "U_CancelWriteOffRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @WriteOffId = request.WriteOffId,
                        @CancelReason = (object)request.CancelReason ?? DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"CancelWriteOffRequest completed. WriteOffId={request.WriteOffId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "WriteOff request cancelled successfully",
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

        public ServiceResult<string> CollectWriteOffRequest(CollectWriteOffRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CollectWriteOffRequest called. WriteOffId={request.WriteOffId}");

                _sqlHelper.DML(
                    "U_CollectWriteOffRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @WriteOffId = request.WriteOffId,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"CollectWriteOffRequest completed. WriteOffId={request.WriteOffId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "WriteOff marked as created successfully",
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

        public ServiceResult<object> GetWriteOffRequestListForApproval(string fromDate, string toDate, int branchId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetWriteOffRequestListForApproval called. FromDate={fromDate}, ToDate={toDate}, BranchId={branchId}");

                if (!DateTime.TryParse(fromDate, out DateTime parsedFromDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid FromDate format", 400);
                }

                if (!DateTime.TryParse(toDate, out DateTime parsedToDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid ToDate format", 400);
                }

                var dataTable = _sqlHelper.GetDataTable(
                    "S_WriteOffRequestDetailsForDiscountApproval",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = parsedFromDate.ToString("yyyy-MM-dd"),
                        @toDate = parsedToDate.ToString("yyyy-MM-dd"),
                        @branchId = branchId,
                        @userId = globalValues.userId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("GetWriteOffRequestListForApproval: no records found");
                    return ServiceResult<object>.Failure(alert.Type, "No writeoff requests found", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetWriteOffRequestListForApproval retrieved {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} writeoff request(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetWriteOffRequestDetailsByWriteOffId(int writeOffId)
        {
            try
            {
                _log.Info($"GetWriteOffRequestDetailsByWriteOffId called. WriteOffId={writeOffId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_WriteOffRequestDetailsByWriteOffId",
                    CommandType.StoredProcedure,
                    new { @WriteOffId = writeOffId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No writeoff details found for WriteOffId={writeOffId}");
                    return ServiceResult<object>.Failure(alert.Type, "No writeoff details found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"WriteOff details retrieved successfully for WriteOffId={writeOffId}. Rows={result.Count}");

                return ServiceResult<object>.Success(result, "Info", "WriteOff details retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetWriteOffRequestApprovalDetails(int writeOffId)
        {
            try
            {
                _log.Info($"GetWriteOffRequestApprovalDetails called. WriteOffId={writeOffId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetWriteOffRequestApprovalDetails",
                    CommandType.StoredProcedure,
                    new { @WriteOffId = writeOffId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No approval details found for WriteOffId={writeOffId}");
                    return ServiceResult<object>.Failure(alert.Type, "No approval details found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Approval details retrieved successfully for WriteOffId={writeOffId}");

                return ServiceResult<object>.Success(result, "Info", "Approval details retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }


        public ServiceResult<SaveOPDAppointmentResponse> SaveOPDAppointment(
    SaveOPDAppointmentRequest request,
    AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SaveOPDAppointment called. PatientId={request.PatientDetails.PatientId}, BranchId={request.VisitDetails.BranchId}, DoctorId={request.VisitDetails.DoctorId}");

                int receiptId = 0;
                int ledgerId = 0;
                int patientId = request.PatientDetails.PatientId;
                int appId = 0;

                // ── Parse DOB once — needed for both IU_PatientMaster and I_DoctorAppointmentPreBooking ──
                DateTime dobParsed;
                string[] dobFormats = { "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy/MM/dd" };

                bool dobParsedOk = DateTime.TryParseExact(
                    request.PatientDetails.Dob?.Trim(),
                    dobFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out dobParsed);

                if (!dobParsedOk)
                    dobParsedOk = DateTime.TryParse(request.PatientDetails.Dob?.Trim(), out dobParsed);

                if (!dobParsedOk)
                {
                    tnx.Rollback();
                    _log.Warn($"Invalid DOB format received: {request.PatientDetails.Dob}");
                    var alertDob = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<SaveOPDAppointmentResponse>.Failure(
                        alertDob.Type,
                        $"Invalid date of birth format: '{request.PatientDetails.Dob}'. Expected formats: dd-MM-yyyy or yyyy-MM-dd",
                        400
                    );
                }

                // ── Check if payment details exist and have a valid amount ────────────
                bool hasPayment = request.PaymentDetails != null
                                  && request.PaymentDetails.Any()
                                  && request.PaymentDetails.Sum(p => p.Amount) > 0;

                if (hasPayment)
                {
                    decimal totalPaidAmount = request.PaymentDetails.Sum(p => p.Amount);

                    // ── 1. IU_PatientMaster — only when PatientId = 0 (new patient) ──
                    if (request.PatientDetails.PatientId == 0)
                    {
                        var patientResult = _sqlHelper.ExecuteScalar(
                            "IU_PatientMaster",
                            CommandType.StoredProcedure,
                            new
                            {
                                @hospId = globalValues.hospId,
                                @branchId = request.PatientDetails.BranchId,
                                @patientId = 0,
                                @title = request.PatientDetails.Title,
                                @firstName = request.PatientDetails.FirstName,
                                @middleName = request.PatientDetails.MiddleName ?? (object)DBNull.Value,
                                @lastName = request.PatientDetails.LastName ?? (object)DBNull.Value,
                                @ageYears = request.PatientDetails.AgeYears,
                                @ageMonths = request.PatientDetails.AgeMonths,
                                @ageDays = request.PatientDetails.AgeDays,
                                @dob = dobParsed,
                                @gender = request.PatientDetails.Gender,
                                @selfContactNumber = request.PatientDetails.SelfContactNumber,
                                @address = request.PatientDetails.Address ?? (object)DBNull.Value,
                                @countryId = request.PatientDetails.CountryId,
                                @country = request.PatientDetails.Country ?? (object)DBNull.Value,
                                @stateId = request.PatientDetails.StateId,
                                @state = request.PatientDetails.State ?? (object)DBNull.Value,
                                @districtId = request.PatientDetails.DistrictId,
                                @district = request.PatientDetails.District ?? (object)DBNull.Value,
                                @cityId = request.PatientDetails.CityId,
                                @city = request.PatientDetails.City ?? (object)DBNull.Value,
                                @insuranceCompanyId = request.PatientDetails.InsuranceCompanyId,
                                @corporateId = request.PatientDetails.CorporateId,
                                @maritalStatus = (object)DBNull.Value,
                                @relation = (object)DBNull.Value,
                                @relativeName = (object)DBNull.Value,
                                @idProofName = (object)DBNull.Value,
                                @idProofNumber = (object)DBNull.Value,
                                @emergencyContactNumber = (object)DBNull.Value,
                                @email = (object)DBNull.Value,
                                @privilegedCardNumber = (object)DBNull.Value,
                                @cardNo = (object)DBNull.Value,
                                @patientImagePath = (object)DBNull.Value,
                                @IsVaccination = 0,
                                @vipPatient = (object)DBNull.Value,
                                @PolicyNo = (object)DBNull.Value,
                                @PolicyCardNo = (object)DBNull.Value,
                                @ExpiryDate = (object)DBNull.Value,
                                @CardHolder = (object)DBNull.Value,
                                @ReferalNo = (object)DBNull.Value,
                                @ReferalDate = (object)DBNull.Value,
                                @OnlinePtId = 0,
                                @healthId = (object)DBNull.Value,
                                @healthIdNumber = (object)DBNull.Value,
                                @landlineNo = (object)DBNull.Value,
                                @birthPlace = (object)DBNull.Value,
                                @religion = (object)DBNull.Value,
                                @relationPhone = (object)DBNull.Value,
                                @relationAge = (object)DBNull.Value,
                                @relationGender = (object)DBNull.Value,
                                @eMG_FirstName = (object)DBNull.Value,
                                @eMG_LastName = (object)DBNull.Value,
                                @eMG_Relation = (object)DBNull.Value,
                                @eMG_MobileNo = (object)DBNull.Value,
                                @eMG_ResidentNo = (object)DBNull.Value,
                                @eMG_Address = (object)DBNull.Value,
                                @isInternational = 0,
                                @locality = (object)DBNull.Value,
                                @passportNumber = (object)DBNull.Value,
                                @internationalNo = (object)DBNull.Value,
                                @membershipNo = (object)DBNull.Value,
                                @patientType = (object)DBNull.Value,
                                @identityMark = (object)DBNull.Value,
                                @identityMark2 = (object)DBNull.Value,
                                @referenceType = (object)DBNull.Value,
                                @remarks = (object)DBNull.Value,
                                @userId = globalValues.userId,
                                @IpAddress = globalValues.ipAddress
                            }
                        );

                        object rawResult = patientResult;
                        if (Convert.IsDBNull(rawResult) || rawResult == null)
                        {
                            tnx.Rollback();
                            var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                            return ServiceResult<SaveOPDAppointmentResponse>.Failure(
                                failAlert.Type, "Failed to create patient record", 500);
                        }

                        int patientResultValue = Convert.ToInt32(rawResult);

                        if (patientResultValue == -1)
                        {
                            tnx.Rollback();
                            var alertDuplicate = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                            _log.Warn($"Patient already exists: {request.PatientDetails.FirstName} {request.PatientDetails.LastName}, Contact={request.PatientDetails.SelfContactNumber}");
                            return ServiceResult<SaveOPDAppointmentResponse>.Failure(
                                alertDuplicate.Type,
                                "Patient already exists with the same name and contact number",
                                409
                            );
                        }

                        patientId = patientResultValue;
                        _log.Info($"New patient created via IU_PatientMaster. PatientId={patientId}");

                        // Invalidate patient cache after new patient created
                        _distributedCache.Remove(CACHE_KEY_PatientMaster_All);
                        _distributedCache.Remove(CACHE_KEY_SearchPatientMaster_All);
                    }

                    // ── 2. Receipts (IsAdvanceReceipt = 1) ───────────────────────────
                    var receipt = new Receipts
                    {
                        HospId = globalValues.hospId,
                        BranchId = request.VisitDetails.BranchId,
                        BillId = 0,
                        VisitId = 0,
                        PatientId = patientId,
                        Amount = totalPaidAmount,
                        IsAdvanceReceipt = 1,
                        PlutusTransactionReferenceID = string.Empty,
                        TransactionLogId = string.Empty,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress,
                        UniqueId = Guid.NewGuid().ToString()
                    };

                    receiptId = Convert.ToInt32(receipt.Create(_sqlHelper, tnx));
                    _log.Info($"Advance receipt created. ReceiptId={receiptId}");

                    // ── 3. Receipt Payment Mode Details ──────────────────────────────
                    foreach (var p in request.PaymentDetails)
                    {
                        // PaymentModeTypeId 4 = Credit → skip
                        if (p.PaymentModeTypeId == 4)
                            continue;

                        var rpmd = new ReceiptsPaymentModeDetails
                        {
                            HospId = globalValues.hospId,
                            BranchId = request.VisitDetails.BranchId,
                            ReceiptID = receiptId,
                            Amount = p.Amount,
                            PaymentModeId = p.PaymentModeId,
                            BankId = p.BankId > 0 ? p.BankId : (int?)null,
                            ReferenceNo = p.RefNo,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };

                        rpmd.Create(_sqlHelper, tnx);
                    }
                    _log.Info($"Receipt payment mode details created for ReceiptId={receiptId}");

                    // ── 4. PatientLedgerBill (upsert running balance) ─────────────────
                    var ledgerBill = new PatientLedgerBill
                    {
                        PatientId = patientId,
                        TransactionType = LedgerBillTransactionType.Credit,
                        LedgerId = 0,
                        Amount = totalPaidAmount,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    };

                    ledgerId = Convert.ToInt32(ledgerBill.Create(_sqlHelper, tnx));
                    _log.Info($"PatientLedgerBill upserted. LedgerId={ledgerId}");

                    // ── 5. PatientLedgerDetails (transaction history row) ─────────────
                    var ledgerDetails = new PatientLedgerDetails
                    {
                        PatientId = patientId,
                        LedgerId = ledgerId,
                        TransactionType = LedgerTransactionType.Credit,
                        Amount = totalPaidAmount,
                        VisitId = 0,
                        BillId = 0,
                        ReceiptId = receiptId,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    };

                    ledgerDetails.Create(_sqlHelper, tnx);
                    _log.Info($"PatientLedgerDetails inserted. LedgerId={ledgerId}, ReceiptId={receiptId}");
                }
                else
                {
                    _log.Info("No payment details provided. Skipping receipt and ledger steps.");
                }

                // ── 6. I_DoctorAppointmentPreBooking (always runs) ───────────────────
                var appointmentResult = _sqlHelper.DML(tnx, "I_DoctorAppointmentPreBooking",
                    CommandType.StoredProcedure,
                    new
                    {
                        @BranchId = request.VisitDetails.BranchId,
                        @RoleId = request.VisitDetails.RoleId,
                        @PatientId = patientId,
                        @title = request.PatientDetails.Title,
                        @firstName = request.PatientDetails.FirstName,
                        @middleName = request.PatientDetails.MiddleName ?? (object)DBNull.Value,
                        @lastName = request.PatientDetails.LastName ?? (object)DBNull.Value,
                        @ageYears = request.PatientDetails.AgeYears,
                        @ageMonths = request.PatientDetails.AgeMonths,
                        @ageDays = request.PatientDetails.AgeDays,
                        @dob = dobParsed,
                        @gender = request.PatientDetails.Gender,
                        @selfContactNumber = request.PatientDetails.SelfContactNumber,
                        @address = request.PatientDetails.Address ?? (object)DBNull.Value,
                        @countryId = request.PatientDetails.CountryId,
                        @country = request.PatientDetails.Country ?? (object)DBNull.Value,
                        @stateId = request.PatientDetails.StateId,
                        @state = request.PatientDetails.State ?? (object)DBNull.Value,
                        @districtId = request.PatientDetails.DistrictId,
                        @district = request.PatientDetails.District ?? (object)DBNull.Value,
                        @cityId = request.PatientDetails.CityId,
                        @city = request.PatientDetails.City ?? (object)DBNull.Value,
                        @InsuranceCompanyId = request.VisitDetails.InsuranceCompanyId,
                        @CorporateId = request.VisitDetails.CorporateId,
                        @DoctorId = request.VisitDetails.DoctorId,
                        @ServiceItemId = request.VisitDetails.ServiceItemId.HasValue
                                                ? request.VisitDetails.ServiceItemId.Value
                                                : (object)DBNull.Value,
                        @ServiceName = request.VisitDetails.ServiceName ?? (object)DBNull.Value,
                        @Amount = request.VisitDetails.Amount,
                        @ReceiptId = receiptId,
                        @AppDateTime = request.VisitDetails.AppDateTime,
                        @SlotId = request.VisitDetails.SlotId.HasValue
                                                ? request.VisitDetails.SlotId.Value
                                                : (object)DBNull.Value,
                        @SourceType = request.VisitDetails.SourceType ?? (object)DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                appId = Convert.ToInt32(appointmentResult);
                _log.Info($"DoctorAppointmentPreBooking inserted. AppId={appId}, PatientId={patientId}, DoctorId={request.VisitDetails.DoctorId}");

                tnx.Commit();
                _log.Info($"SaveOPDAppointment committed. AppId={appId}, PatientId={patientId}, ReceiptId={receiptId}, LedgerId={ledgerId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveOPDAppointmentResponse>.Success(
                    new SaveOPDAppointmentResponse
                    {
                        AppId = appId,
                        PatientId = patientId,
                        ReceiptId = receiptId,
                        LedgerId = ledgerId
                    },
                    alert.Type,
                    "OPD appointment saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveOPDAppointmentResponse>.Failure(
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

        public ServiceResult<object> GetDoctorAppointmentPreBookingDetails(
    DateTime fromDate,
    DateTime toDate,
    int dateTypeId,
    int branchId,
    int doctorId,
    string sourceType,
    int id,
    string tokenNo)
        {
            try
            {
                _log.Info($"GetDoctorAppointmentPreBookingDetails called. FromDate={fromDate:yyyy-MM-dd}, ToDate={toDate:yyyy-MM-dd}, DateTypeId={dateTypeId}, BranchId={branchId}, DoctorId={doctorId}, TokenNo={tokenNo ?? "All"}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetDoctorAppointmentPreBookingDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = fromDate,
                        @toDate = toDate,
                        @dateTypeId = dateTypeId,
                        @branchId = branchId,
                        @doctorId = doctorId,
                        @sourceType= sourceType,
                        @id = id,
                        @tokenNo = (object)tokenNo ?? DBNull.Value
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No credit note request details found for the given filters");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No credit note request details found",
                        404
                    );
                }

                // Raw SP output, no model mapping — new columns surface automatically
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetDoctorAppointmentPreBookingDetails retrieved {result.Count} record(s)");

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
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        // Slot length in minutes — change here to affect slot generation globally
        private const int SlotDurationMinutes = 30;
        private const int TotalDaysToShow = 30;

        public ServiceResult<object> GetDoctorAppointmentSlots(int branchId, int doctorId, DateTime appointmentDate)
        {
            try
            {
                _log.Info($"GetDoctorAppointmentSlots called. BranchId={branchId}, DoctorId={doctorId}, AppointmentDate={appointmentDate:yyyy-MM-dd}");

                DateTime fromDate = appointmentDate.Date;
                DateTime toDate = fromDate.AddDays(TotalDaysToShow - 1);

                // 1. Doctor's weekly timing windows (a day can have multiple windows, per your screenshot)
                var timingTable = _sqlHelper.GetDataTable(
                    "S_GetDoctorTimingDetailsForSlots",
                    CommandType.StoredProcedure,
                    new { @branchId = branchId, @doctorId = doctorId }
                );

                if (timingTable == null || timingTable.Rows.Count == 0)
                {
                    var alertNoTiming = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No active timing configured for DoctorId={doctorId}, BranchId={branchId}");
                    return ServiceResult<object>.Failure(
                        alertNoTiming.Type,
                        "No timing configured for this doctor",
                        404
                    );
                }

                // Group windows by Day name ("Wednesday", "Thursday" ...)
                var timingByDay = timingTable.AsEnumerable()
                    .GroupBy(r => r.Field<string>("Day"), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(r => new
                        {
                            StartTiming = r.Field<string>("StartTiming"),
                            EndTiming = r.Field<string>("EndTiming")
                        }).ToList(),
                        StringComparer.OrdinalIgnoreCase);

                // 2. Existing non-cancelled bookings within the range
                var bookedTable = _sqlHelper.GetDataTable(
                    "S_GetDoctorBookedSlotsForRange",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @doctorId = doctorId,
                        @fromDate = fromDate,
                        @toDate = toDate
                    }
                );

                var bookedByDateTime = new Dictionary<DateTime, (int PatientId, string TokenNo)>();
                if (bookedTable != null)
                {
                    foreach (DataRow row in bookedTable.Rows)
                    {
                        DateTime appDt = Convert.ToDateTime(row["AppDateTime"]);
                        int patientId = row["PatientId"] == DBNull.Value ? 0 : Convert.ToInt32(row["PatientId"]);
                        string tokenNo = row["TokenNo"] == DBNull.Value ? null : row["TokenNo"].ToString();

                        if (!bookedByDateTime.ContainsKey(appDt))
                            bookedByDateTime.Add(appDt, (patientId, tokenNo));
                    }
                }

                DateTime now = DateTime.Now;
                var result = new List<Dictionary<string, object>>();

                // 3. Generate slots for the next N days
                for (int d = 0; d < TotalDaysToShow; d++)
                {
                    DateTime currentDate = fromDate.AddDays(d);
                    string dayName = currentDate.DayOfWeek.ToString();

                    if (!timingByDay.TryGetValue(dayName, out var windows))
                        continue; // no timing configured for this day => no slots shown, matches original UI behavior

                    int slotIndex = 0;

                    foreach (var window in windows)
                    {
                        if (!DateTime.TryParse(window.StartTiming, out DateTime startTime) ||
                            !DateTime.TryParse(window.EndTiming, out DateTime endTime))
                        {
                            _log.Warn($"Invalid timing format. Day={dayName}, Start={window.StartTiming}, End={window.EndTiming}");
                            continue;
                        }

                        DateTime slotStart = currentDate.Date.Add(startTime.TimeOfDay);
                        DateTime windowEnd = currentDate.Date.Add(endTime.TimeOfDay);

                        while (slotStart.AddMinutes(SlotDurationMinutes) <= windowEnd)
                        {
                            DateTime slotEnd = slotStart.AddMinutes(SlotDurationMinutes);

                            // Deterministic & reproducible across calls
                            string slotTimingId = $"{currentDate:yyMMdd}{doctorId}{slotIndex}";

                            bool isBooked = bookedByDateTime.TryGetValue(slotStart, out var bookedInfo);
                            bool isExpired = slotStart < now;

                            result.Add(new Dictionary<string, object>
                    {
                        { "SlotTimingId", slotTimingId },
                        { "AppointmentDate", currentDate.ToString("dd-MM-yyyy") },
                        { "Day", dayName },
                        { "SlotStartTime", slotStart.ToString("hh:mm tt") },
                        { "SlotEndTime", slotEnd.ToString("hh:mm tt") },
                        { "SlotStartDateTime", slotStart },
                        { "SlotEndDateTime", slotEnd },
                        { "IsBooked", isBooked ? 1 : 0 },
                        { "IsExpired", isExpired ? 1 : 0 },
                        { "PatientId", isBooked ? bookedInfo.PatientId : (object)null },
                        { "TokenNo", isBooked ? bookedInfo.TokenNo : null }
                    });

                            slotIndex++;
                            slotStart = slotEnd;
                        }
                    }
                }

                if (!result.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No slots generated for DoctorId={doctorId} in the given range");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No slots available for this doctor in the selected date range",
                        404
                    );
                }

                _log.Info($"GetDoctorAppointmentSlots generated {result.Count} slot(s) for DoctorId={doctorId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} slot(s) retrieved successfully",
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

        public ServiceResult<string> CancelDoctorAppointmentPreBooking(int id, string cancelReason, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CancelDoctorAppointmentPreBooking called. Id={id}");

                var result = _sqlHelper.DML("U_CancelDoctorAppointmentPreBooking", CommandType.StoredProcedure, new
                {
                    @Id = id,
                    @userId = globalValues.userId,
                    @cancelReason = cancelReason,
                    @ipAddress = globalValues.ipAddress
                });

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"Doctor appointment pre-booking cancelled successfully. Id={id}");
                return ServiceResult<string>.Success(
                    "Appointment pre-booking cancelled successfully",
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

        public ServiceResult<string> ConfirmDoctorAppointmentPreBooking(int id, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ConfirmDoctorAppointmentPreBooking called. Id={id}");

                var result = _sqlHelper.DML("U_ConfirmDoctorAppointmentPreBooking", CommandType.StoredProcedure, new
                {
                    @Id = id,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });
                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"Doctor appointment pre-booking confirmed successfully. Id={id}");
                return ServiceResult<string>.Success(
                    "Appointment pre-booking confirmed successfully",
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

        public ServiceResult<string> RescheduleDoctorAppointmentPreBooking(int id, int slotId, DateTime appDateTime, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"RescheduleDoctorAppointmentPreBooking called. Id={id}, SlotId={slotId}, AppDateTime={appDateTime}");

                var result = _sqlHelper.DML("U_RescheduleDoctorAppointmentPreBooking", CommandType.StoredProcedure, new
                {
                    @Id = id,
                    @userId = globalValues.userId,
                    @slotId = slotId,
                    @AppDateTime = appDateTime,
                    @ipAddress = globalValues.ipAddress
                });

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"Doctor appointment pre-booking rescheduled successfully. Id={id}");
                return ServiceResult<string>.Success(
                    "Appointment pre-booking rescheduled successfully",
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

        public ServiceResult<object> GetServiceDetailsForCorporateRateComparison(int visitId, int corporateId)
        {
            try
            {
                _log.Info($"GetServiceDetailsForCorporateRateComparison called. VisitId={visitId}, CorporateId={corporateId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetServiceDetailsForCorporateRateComparison",
                    CommandType.StoredProcedure,
                    new
                    {
                        @visitId = visitId,
                        @corporateId = corporateId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No service details found for VisitId={visitId}, CorporateId={corporateId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No service details found for the given visit and corporate",
                        404
                    );
                }

                // Raw DataTable -> List<Dictionary<string,object>> (no model mapping,
                // so any new columns added to the SP surface automatically)
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetServiceDetailsForCorporateRateComparison retrieved {result.Count} record(s) for VisitId={visitId}, CorporateId={corporateId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} service detail(s) retrieved successfully",
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