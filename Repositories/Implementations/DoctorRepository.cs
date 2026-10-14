using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using log4net;
using Microsoft.Extensions.Caching.Distributed;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Services;
using HISWEBAPI.Utilities;
using Microsoft.Data.SqlClient;
using System.Configuration;
using HISWEBAPI.Configuration;
using System.Text.Json;

namespace HISWEBAPI.Repositories.Implementations
{
    public class DoctorRepository : Interfaces.IDoctorRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly IConfiguration _configuration;

        public DoctorRepository(
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

        #region Doctor Department Methods

        public ServiceResult<CreateUpdateDoctorDepartmentResponse> CreateUpdateDoctorDepartment(
            CreateUpdateDoctorDepartmentRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDoctorDepartment called. DepartmentId={request.DepartmentId}, Department={request.Department}");

                var result = _sqlHelper.DML(
                    "IU_DoctorDepartmentMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        DepartmentId = request.DepartmentId,
                        Department = request.Department,
                        DepartmentTypeId = request.DepartmentTypeId,
                        DepartmentType = request.DepartmentType,
                        IsActive = request.IsActive,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );


                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_DoctorDepartment_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared DoctorDepartment cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate department name: {request.Department}");
                    return ServiceResult<CreateUpdateDoctorDepartmentResponse>.Failure(
                        alert.Type,
                        "Department name already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateDoctorDepartmentResponse { DepartmentId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.DepartmentId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Doctor Department {(request.DepartmentId == 0 ? "created" : "updated")} successfully. DepartmentId={resultValue}");

                    return ServiceResult<CreateUpdateDoctorDepartmentResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.DepartmentId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Doctor Department operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateDoctorDepartmentResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateDoctorDepartmentResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<DoctorDepartmentModel>> GetDoctorDepartmentList(
            int? departmentId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetDoctorDepartmentList called. DepartmentId={departmentId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL departments
                string cacheKey = "_DoctorDepartment_All";

                // Try to get all departments from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DoctorDepartmentModel> allDepartments;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorDepartment data retrieved from cache. Key={cacheKey}");
                    allDepartments = System.Text.Json.JsonSerializer.Deserialize<List<DoctorDepartmentModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorDepartment cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL departments from database (no parameters)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_DoctorDepartmentMaster",
                        CommandType.StoredProcedure
                    );

                    allDepartments = dataTable?.AsEnumerable().Select(row => new DoctorDepartmentModel
                    {
                        DepartmentId = row.Field<int>("DepartmentId"),
                        Department = row.Field<string>("Department") ?? string.Empty,
                        DepartmentTypeId = row.Field<int>("DepartmentTypeId"),
                        DepartmentType = row.Field<string>("DepartmentType") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<DoctorDepartmentModel>();

                    // Store ALL departments in cache (permanent until manually cleared)
                    if (allDepartments.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allDepartments);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All DoctorDepartment data cached permanently. Key={cacheKey}, Count={allDepartments.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<DoctorDepartmentModel> filteredDepartments = allDepartments;

                if (departmentId.HasValue)
                {
                    _log.Info($"Filtering cached data by DepartmentId: {departmentId.Value}");
                    filteredDepartments = filteredDepartments.Where(d => d.DepartmentId == departmentId.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredDepartments = filteredDepartments.Where(d => d.IsActive == isActive.Value).ToList();
                }

                if (!filteredDepartments.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No departments found for DepartmentId={departmentId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<DoctorDepartmentModel>>.Failure(
                        alert.Type,
                        "No departments found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredDepartments.Count} department(s) from cache");

                return ServiceResult<IEnumerable<DoctorDepartmentModel>>.Success(
                    filteredDepartments,
                    "Info",
                    $"{filteredDepartments.Count} department(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorDepartmentModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        #endregion

        #region Doctor Specialization Methods

        public ServiceResult<CreateUpdateDoctorSpecializationResponse> CreateUpdateDoctorSpecialization(
            CreateUpdateDoctorSpecializationRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDoctorSpecialization called. SpecializationId={request.SpecializationId}, Specialization={request.Specialization}");

                var result = _sqlHelper.DML(
                    "IU_DoctorSpecializationMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        SpecializationId = request.SpecializationId,
                        Specialization = request.Specialization,
                        IsActive = request.IsActive,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_DoctorSpecialization_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared DoctorSpecialization cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate specialization name: {request.Specialization}");
                    return ServiceResult<CreateUpdateDoctorSpecializationResponse>.Failure(
                        alert.Type,
                        "Specialization name already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateDoctorSpecializationResponse { SpecializationId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.SpecializationId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Doctor Specialization {(request.SpecializationId == 0 ? "created" : "updated")} successfully. SpecializationId={resultValue}");

                    return ServiceResult<CreateUpdateDoctorSpecializationResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.SpecializationId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Doctor Specialization operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateDoctorSpecializationResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateDoctorSpecializationResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<DoctorSpecializationModel>> GetDoctorSpecializationList(
            int? specializationId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetDoctorSpecializationList called. SpecializationId={specializationId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL specializations
                string cacheKey = "_DoctorSpecialization_All";

                // Try to get all specializations from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DoctorSpecializationModel> allSpecializations;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorSpecialization data retrieved from cache. Key={cacheKey}");
                    allSpecializations = System.Text.Json.JsonSerializer.Deserialize<List<DoctorSpecializationModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorSpecialization cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL specializations from database (no parameters)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_DoctorSpecializationMaster",
                        CommandType.StoredProcedure
                    );

                    allSpecializations = dataTable?.AsEnumerable().Select(row => new DoctorSpecializationModel
                    {
                        SpecializationId = row.Field<int>("SpecializationId"),
                        Specialization = row.Field<string>("Specialization") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<DoctorSpecializationModel>();

                    // Store ALL specializations in cache (permanent until manually cleared)
                    if (allSpecializations.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allSpecializations);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All DoctorSpecialization data cached permanently. Key={cacheKey}, Count={allSpecializations.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<DoctorSpecializationModel> filteredSpecializations = allSpecializations;

                if (specializationId.HasValue)
                {
                    _log.Info($"Filtering cached data by SpecializationId: {specializationId.Value}");
                    filteredSpecializations = filteredSpecializations.Where(s => s.SpecializationId == specializationId.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredSpecializations = filteredSpecializations.Where(s => s.IsActive == isActive.Value).ToList();
                }

                if (!filteredSpecializations.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No specializations found for SpecializationId={specializationId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<DoctorSpecializationModel>>.Failure(
                        alert.Type,
                        "No specializations found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredSpecializations.Count} specialization(s) from cache");

                return ServiceResult<IEnumerable<DoctorSpecializationModel>>.Success(
                    filteredSpecializations,
                    "Info",
                    $"{filteredSpecializations.Count} specialization(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorSpecializationModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        #endregion



        public ServiceResult<CreateUpdateDoctorMasterResponse> CreateUpdateDoctorMaster(
  CreateUpdateDoctorMasterRequest request,
  AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;

            try
            {
                _log.Info($"CreateUpdateDoctorMaster called. DoctorId={request.DoctorId}, Name={request.Name}");
               

                // Handle doctor photo file upload
                string doctorPhotoFilePath = null;
                if (request.DoctorPhotoFile != null && request.DoctorPhotoFile.Length > 0)
                {
                    _log.Info($"Processing doctor photo file: {request.DoctorPhotoFile.FileName}, Size: {request.DoctorPhotoFile.Length} bytes");

                    var fileUploadHelper = new FileUploadHelper(_configuration);

                    // Upload file to DMS
                    var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                        request.DoctorPhotoFile,
                        "DoctorPhoto"
                    );

                    if (!uploadSuccess)
                    {
                        _log.Error($"Doctor photo file upload failed: {uploadError}");
                        var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                        return ServiceResult<CreateUpdateDoctorMasterResponse>.Failure(
                            alert.Type,
                            $"Doctor photo file upload failed: {uploadError}",
                            500
                        );
                    }

                    doctorPhotoFilePath = filePath;
                    _log.Info($"Doctor photo file uploaded successfully: {doctorPhotoFilePath}");
                }

                // Get connection and start transaction
                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();

                tnx = CustomSqlHelper.getSqlTransaction(con);

                long result = 0;

                // Step 1: Create user login if IsLogin = 1
                if (request.IsLogin == 1 && request.DoctorId==0)
                {
                    _log.Info("Creating user login for doctor");
                    _distributedCache.Remove("_UserMaster_All");

                    var userResult = _sqlHelper.DML(tnx, "I_NewUserSignUp", CommandType.StoredProcedure, new
                    {
                        @Address = request.Address,
                        @Contact = request.ContactNo,
                        @DOB = request.Dob,
                        @Email = request.EmailId,
                        @FirstName = request.Name,
                        @MidelName = "",
                        @LastName = "",
                        @Password = PasswordHasher.HashPassword(request.Password),
                        @UserName = request.UserName,
                        @Gender = request.Gender
                    },
                    new
                    {
                        result = 0
                    });

                    if (Convert.ToInt32(userResult) == -1)
                    {
                        tnx.Rollback();
                        var alert = _messageService.GetMessageAndTypeByAlertCode("USERNAME_EXISTS");
                        _log.Warn($"Username already exists: {request.UserName}");
                        return ServiceResult<CreateUpdateDoctorMasterResponse>.Failure(
                            alert.Type,
                            alert.Message,
                            409
                        );
                    }

                    _log.Info($"User created successfully. UserId={userResult}");
                }

                GlobalFunctions.ClearCacheByPattern(_configuration, "_DoctorMaster_Branch*");
                _distributedCache.Remove("_DoctorMaster_All");

                result = _sqlHelper.DML(tnx, "IU_DoctorMaster", CommandType.StoredProcedure, new
                {
                    @DoctorId = request.DoctorId,
                    @Title = request.Title,
                    @Name = request.Name,
                    @Gender = request.Gender,
                    @Dob = request.Dob.ToString("yyyy-MM-dd"),
                    @ContactNo = request.ContactNo,
                    @EmailId = request.EmailId ?? "",
                    @Address = request.Address ?? "",
                    @SpecializationId = request.SpecializationId,
                    @Specialization = request.Specialization,
                    @DepartmentId = request.DepartmentId,
                    @Department = request.Department,
                    @ProfileSummery = request.ProfileSummery,
                    @RegistrationNo = request.RegistrationNo??"",
                    @IsActive = request.IsActive,
                    @BranchId = request.BranchList,
                    @RoomNo = request.RoomNo??"",
                    @CanApproveLabReport = request.CanApproveLabReport,
                    @CanApproveDischargeSummary = request.CanApproveDischargeSummary,
                    @DoctorPhotoFilePath = doctorPhotoFilePath ?? "",
                    @IsDoctorUnit = 0,
                    @DoctorRole= request.DoctorRole??"",
                    @DoctorRoleId= request.DoctorRoleId,
                    @UserId = globalValues.userId,
                    @HospId = globalValues.hospId,
                    @IPAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                if (result <= 0)
                {
                    tnx.Rollback();
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Error("Failed to insert/update doctor master");
                    return ServiceResult<CreateUpdateDoctorMasterResponse>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                _log.Info($"Doctor master saved successfully. DoctorId={result}");

              

               
                // Commit transaction
                tnx.Commit();
                _log.Info("Transaction committed successfully");

                var responseData = new CreateUpdateDoctorMasterResponse
                {
                    DoctorId = result,
                    DoctorPhotoFilePath = doctorPhotoFilePath
                };

                var alert1 = _messageService.GetMessageAndTypeByAlertCode(
                    request.DoctorId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                );

                return ServiceResult<CreateUpdateDoctorMasterResponse>.Success(
                    responseData,
                    alert1.Type,
                    alert1.Message,
                    request.DoctorId == 0 ? 201 : 200
                );
            }
            catch (Exception ex)
            {
                if (tnx != null)
                {
                    try
                    {
                        tnx.Rollback();
                        _log.Error("Transaction rolled back due to error");
                    }
                    catch (Exception rollbackEx)
                    {
                        _log.Error($"Error during rollback: {rollbackEx.Message}");
                    }
                }

                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateDoctorMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                if (tnx != null)
                {
                    tnx.Dispose();
                }
                if (con != null)
                {
                    if (con.State == ConnectionState.Open)
                        con.Close();
                    con.Dispose();
                }
            }
        }


        public ServiceResult<string> UpdateDoctorMasterStatus(int doctorId, int isActive, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UpdateDoctorMasterStatus", CommandType.StoredProcedure, new
                {
                    @DoctorId = doctorId,
                    @UserId = globalValues.userId,
                    @IsActive = isActive,
                    @IpAddress = globalValues.ipAddress
                });

                // Clear doctor master cache after successful update
                _distributedCache.Remove("_DoctorMaster_All");
                _log.Info($"Cleared DoctorMaster cache after status update");

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Doctor status updated successfully. DoctorId={doctorId}, IsActive={isActive}");
                    return ServiceResult<string>.Success(
                        "Doctor status updated successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Doctor not found for DoctorId={doctorId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Doctor not found",
                        404
                    );
                }
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
        public ServiceResult<IEnumerable<DoctorMasterModelAll>> GetDoctorMaster(int? doctorId = null, int? isDoctorUnit = null, int? doctorDepartmentId = null, int? isActive = null)
        {
            try
            {
                _log.Info($"GetDoctorMaster called. DoctorId={doctorId?.ToString() ?? "All"}, IsDoctorUnit={isDoctorUnit?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                // Always use the same cache key for all doctors
                string cacheKey = "_DoctorMaster_All";

                // Try to get all doctors from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DoctorMasterModelAll> allDoctors;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorMaster data retrieved from cache. Key={cacheKey}");
                    allDoctors = System.Text.Json.JsonSerializer.Deserialize<List<DoctorMasterModelAll>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL doctors from database (NO parameters passed - SP returns everything)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getDoctorMaster",
                        CommandType.StoredProcedure
                    // No parameters - SP always returns all doctors
                    );

                    allDoctors = dataTable?.AsEnumerable().Select(row => new DoctorMasterModelAll
                    {
                        DoctorId = row.Field<int>("DoctorId"),
                        Title = row.Field<string>("Title") ?? string.Empty,
                        Name = row.Field<string>("Name") ?? string.Empty,
                        Dob = row.Field<string>("Dob") ?? string.Empty,
                        Gender = row.Field<string>("Gender") ?? string.Empty,
                        CompleteName = row.Field<string>("CompleteName") ?? string.Empty,
                        ContactNo = row.Field<string>("ContactNo") ?? string.Empty,
                        EmailId = row.Field<string>("EmailId") ?? string.Empty,
                        Address = row.Field<string>("Address") ?? string.Empty,
                        SpecializationId = row.Field<int>("SpecializationId"),
                        Specialization = row.Field<string>("Specialization") ?? string.Empty,
                        UserName = row.Field<string>("UserName") ?? string.Empty,
                        Password = row.Field<string>("Password") ?? string.Empty,
                        DepartmentId = row.Field<int>("DepartmentId"),
                        Department = row.Field<string>("Department") ?? string.Empty,
                        ProfileSummery = row.Field<string>("ProfileSummery") ?? string.Empty,
                        RegistrationNo = row.Field<string>("RegistrationNo") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        UserId = row.Field<int?>("UserId"),
                        HospId = row.Field<int>("HospId"),
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        IpAddress = row.Field<string>("IpAddress") ?? string.Empty,
                        BranchId = row.Field<string>("BranchId") ?? string.Empty,
                        CanApproveLabReport = row.Field<int>("CanApproveLabReport"),
                        CanApproveDischargeSummary = row.Field<int>("CanApproveDischargeSummary"),
                        DoctorPhotoFilePath = row.Field<string>("DoctorPhotoFilePath") ?? string.Empty,
                        IsDoctorUnit = row.Field<byte>("IsDoctorUnit"),
                        RoomNo = row.Field<string>("RoomNo") ?? string.Empty,
                        DoctorRoleId = row.Field<int?>("DoctorRoleId"),
                        DoctorRole = row.Field<string>("DoctorRole")
                    }).ToList() ?? new List<DoctorMasterModelAll>();

                    // Store ALL doctors in cache (no expiration)
                    if (allDoctors.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allDoctors);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All DoctorMaster data cached permanently. Key={cacheKey}, Count={allDoctors.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<DoctorMasterModelAll> filteredDoctors = allDoctors;

                if (doctorId.HasValue)
                {
                    _log.Info($"Filtering cached data by DoctorId: {doctorId.Value}");
                    filteredDoctors = filteredDoctors.Where(d => d.DoctorId == doctorId.Value).ToList();
                }

                if (doctorDepartmentId.HasValue)
                {
                    _log.Info($"Filtering cached data by DoctorId: {doctorDepartmentId.Value}");
                    filteredDoctors = filteredDoctors.Where(d => d.DepartmentId == doctorDepartmentId.Value).ToList();
                }


                if (isDoctorUnit.HasValue)
                {
                    _log.Info($"Filtering cached data by IsDoctorUnit: {isDoctorUnit.Value}");
                    filteredDoctors = filteredDoctors.Where(d => d.IsDoctorUnit == isDoctorUnit.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredDoctors = filteredDoctors.Where(d => d.IsActive == isActive.Value).ToList();
                }

                if (!filteredDoctors.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No doctors found for DoctorId={doctorId?.ToString() ?? "All"}, IsDoctorUnit={isDoctorUnit?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<DoctorMasterModelAll>>.Failure(
                        alert.Type,
                        "No doctors found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredDoctors.Count} doctor(s) from cache");

                return ServiceResult<IEnumerable<DoctorMasterModelAll>>.Success(
                    filteredDoctors,
                    "Info",
                    $"{filteredDoctors.Count} doctor(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorMasterModelAll>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<DoctorUnitMappingResponse> CreateUpdateDoctorUnitMaster(
            CreateUpdateDoctorUnitMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDoctorUnitMapping called. DoctorId={request.DoctorId}, Name={request.Name}");

                // Step 1: Create/Update Doctor Unit Master
                SqlParameter[] doctorUnitParameters = new SqlParameter[]
                {
            new SqlParameter("@DoctorId", request.DoctorId),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@SpecializationId", request.SpecializationId),
            new SqlParameter("@Specialization", request.Specialization),
            new SqlParameter("@DepartmentId", request.DepartmentId),
            new SqlParameter("@Department", request.Department),
            new SqlParameter("@IsActive", request.IsActive),
            new SqlParameter("@BranchId", request.BranchId),
            new SqlParameter("@HospId", globalValues.hospId),
            new SqlParameter("@UserId", globalValues.userId),
            new SqlParameter("@IPAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                _sqlHelper.RunProcedure("IU_DoctorUnitMaster", doctorUnitParameters);

                int unitId = Convert.ToInt32(doctorUnitParameters.Last().Value);

                if (unitId == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate doctor unit name: {request.Name}");
                    return ServiceResult<DoctorUnitMappingResponse>.Failure(
                        alert.Type,
                        $"Doctor unit '{request.Name}' already exists",
                        409
                    );
                }
                _distributedCache.Remove("_DoctorMaster_All");

                if (unitId <= 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    _log.Error($"Failed to create/update doctor unit. Result={unitId}");
                    return ServiceResult<DoctorUnitMappingResponse>.Failure(
                        alert.Type,
                        "Failed to create/update doctor unit",
                        500
                    );
                }

              

                var responseData = new DoctorUnitMappingResponse { UnitId = unitId };
                var alert1 = _messageService.GetMessageAndTypeByAlertCode(
                    request.DoctorId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                );

                _log.Info($"Doctor unit mapping {(request.DoctorId == 0 ? "created" : "updated")} successfully. UnitId={unitId}");

                return ServiceResult<DoctorUnitMappingResponse>.Success(
                    responseData,
                    alert1.Type,
                    alert1.Message,
                    request.DoctorId == 0 ? 201 : 200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<DoctorUnitMappingResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }



        public ServiceResult<DoctorUnitMappingResponse> CreateUpdateDoctorUnitMapping(
          CreateUpdateDoctorUnitMappingRequest request,
          AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"Deleting existing doctor unit mappings for UnitId={request.UnitId}");

                var deleteResult = _sqlHelper.DML("D_DoctorUnitMapping", CommandType.StoredProcedure, new
                {
                    @unitId = request.UnitId,
                    @userId = globalValues.userId
                });

                _log.Info($"Deleted existing mappings for UnitId={request.UnitId}");

                // Step 3: Insert new mappings
                if (request.DoctorMappings != null && request.DoctorMappings.Any())
                {
                    int insertedCount = 0;
                    foreach (var mapping in request.DoctorMappings)
                    {
                        if (mapping.DoctorId <= 0)
                        {
                            _log.Warn($"Skipping invalid DoctorId={mapping.DoctorId}");
                            continue;
                        }

                        var insertResult = _sqlHelper.DML("I_DoctorUnitMapping", CommandType.StoredProcedure, new
                        {
                            @unitId = request.UnitId,
                            @doctorId = mapping.DoctorId,
                            @userId = globalValues.userId
                        });

                        if (insertResult > 0)
                        {
                            insertedCount++;
                        }
                    }

                    _log.Info($"Inserted {insertedCount} doctor unit mappings for UnitId={request.UnitId}");
                }
                else
                {
                    _log.Info($"No doctor mappings to insert for UnitId={request.UnitId}");
                }
                var responseData = new DoctorUnitMappingResponse { UnitId = request.UnitId };

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");

                _distributedCache.Remove($"_DoctorUnitMapping_Unit{request.UnitId}");
                return ServiceResult<DoctorUnitMappingResponse>.Success(
                    responseData,
                    alert1.Type,
                    alert1.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<DoctorUnitMappingResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


    
        public ServiceResult<IEnumerable<DoctorUnitMappingModel>> GetDoctorUnitMapping(int unitId)
        {
            try
            {
                _log.Info($"GetDoctorUnitMapping called. UnitId={unitId}");

                // Validate unitId
                if (unitId <= 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn($"Invalid UnitId provided: {unitId}");
                    return ServiceResult<IEnumerable<DoctorUnitMappingModel>>.Failure(
                        alert.Type,
                        "UnitId must be greater than 0",
                        400
                    );
                }

                // Generate dynamic cache key based on unitId
                string cacheKey = $"_DoctorUnitMapping_Unit{unitId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DoctorUnitMappingModel> doctors;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorUnitMapping data retrieved from cache. Key={cacheKey}");
                    doctors = System.Text.Json.JsonSerializer.Deserialize<List<DoctorUnitMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorUnitMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_DoctorUnitMapping",
                        CommandType.StoredProcedure,
                        new { unitId = unitId }
                    );

                    doctors = dataTable?.AsEnumerable().Select(row => new DoctorUnitMappingModel
                    {
                        DoctorId = row.Field<int>("DoctorId"),
                        CompleteName = row.Field<string>("CompleteName") ?? string.Empty
                    }).ToList() ?? new List<DoctorUnitMappingModel>();

                    // Store data in cache (permanent until manually cleared)
                    if (doctors.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(doctors);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"DoctorUnitMapping data cached permanently. Key={cacheKey}, Count={doctors.Count}");
                    }
                }

                if (!doctors.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No doctors found for UnitId={unitId}");
                    return ServiceResult<IEnumerable<DoctorUnitMappingModel>>.Failure(
                        alert.Type,
                        $"No doctors found for UnitId: {unitId}",
                        404
                    );
                }

                _log.Info($"Retrieved {doctors.Count} doctor(s) from cache for UnitId={unitId}");

                return ServiceResult<IEnumerable<DoctorUnitMappingModel>>.Success(
                    doctors,
                    "Info",
                    $"{doctors.Count} doctor(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorUnitMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CreateUpdateDoctorTimingDetailsResponse> CreateUpdateDoctorTimingDetails(
 CreateUpdateDoctorTimingDetailsRequest request,
 AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDoctorTimingDetails called. Timings Count={request.DoctorTimings.Count}");

                // Validate that StartTiming is before EndTiming for each entry
                foreach (var timing in request.DoctorTimings)
                {
                    if (TimeSpan.TryParse(timing.StartTiming, out TimeSpan startTime) &&
                        TimeSpan.TryParse(timing.EndTiming, out TimeSpan endTime))
                    {
                        if (startTime >= endTime)
                        {
                            _log.Warn($"Invalid timing: StartTiming ({timing.StartTiming}) must be before EndTiming ({timing.EndTiming}) for {timing.Day}");
                            var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                            return ServiceResult<CreateUpdateDoctorTimingDetailsResponse>.Failure(
                                alert.Type,
                                $"StartTiming must be before EndTiming for {timing.Day}",
                                400
                            );
                        }
                    }
                }

                // Check for duplicate days in the request
                var duplicateDays = request.DoctorTimings
                    .GroupBy(x => x.Day)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateDays.Any())
                {
                    _log.Warn($"Duplicate days found in request: {string.Join(", ", duplicateDays)}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<CreateUpdateDoctorTimingDetailsResponse>.Failure(
                        alert.Type,
                        $"Duplicate days found: {string.Join(", ", duplicateDays)}. Each day can only appear once.",
                        400
                    );
                }

                // First, delete existing doctor timing details
                var deleteResult = _sqlHelper.ExecuteScalar(
                    "IU_DeleteDoctorTimingDetails",
                    CommandType.StoredProcedure,
                    new { @DoctorId = request.DoctorId }
                );

                _log.Info($"Deleted existing doctor timings for DoctorId={request.DoctorId}");

                // Clear cache after delete
                string cacheKey = $"_DoctorTimingDetails_Doctor{request.DoctorId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                // Insert new doctor timing details
                List<int> doctorTimingIds = new List<int>();
                int successCount = 0;

                foreach (var timing in request.DoctorTimings)
                {
                    var result = _sqlHelper.DML(
                        "IU_DoctorTimingDetails",
                        CommandType.StoredProcedure,
                        new
                        {
                            DoctorTimingId = 0,
                            BranchId = timing.BranchId,
                            DoctorId = request.DoctorId,
                            Day = timing.Day,
                            StartTiming = timing.StartTiming,
                            EndTiming = timing.EndTiming,
                            UserId = globalValues.userId
                        },
                new
                {
                    result = 0
                });


                    int timingId = Convert.ToInt32(result);

                    if (timingId > 0)
                    {
                        doctorTimingIds.Add(timingId);
                        successCount++;
                        _log.Info($"Doctor timing created successfully. DoctorTimingId={timingId}, Day={timing.Day}");
                    }
                    else
                    {
                        _log.Warn($"Failed to create doctor timing for Day={timing.Day}");
                    }
                }

                if (successCount > 0)
                {
                    var responseData = new CreateUpdateDoctorTimingDetailsResponse
                    {
                        DoctorId = request.DoctorId,
                        TotalTimingsCreated = successCount
                    };

                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Doctor timing details saved successfully. DoctorId={request.DoctorId}, Total={successCount}");

                    return ServiceResult<CreateUpdateDoctorTimingDetailsResponse>.Success(
                        responseData,
                        alert.Type,
                        $"{successCount} doctor timing(s) created successfully",
                        request.DoctorId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Failed to create any doctor timing details for DoctorId={request.DoctorId}");
                return ServiceResult<CreateUpdateDoctorTimingDetailsResponse>.Failure(
                    alert1.Type,
                    "Failed to create doctor timing details",
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateDoctorTimingDetailsResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<DoctorTimingDetailsModel>> GetDoctorTimingDetails(int doctorId)
        {
            try
            {
                _log.Info($"GetDoctorTimingDetails called. DoctorId={doctorId}");

                // Generate cache key based on doctorId
                string cacheKey = $"_DoctorTimingDetails_Doctor{doctorId}";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DoctorTimingDetailsModel> doctorTimings;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorTimingDetails data retrieved from cache. Key={cacheKey}");
                    doctorTimings = System.Text.Json.JsonSerializer.Deserialize<List<DoctorTimingDetailsModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorTimingDetails cache miss. Fetching data from database. Key={cacheKey}");

                    // Fetch data from database
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetDoctorTimingDetails",
                        CommandType.StoredProcedure,
                        new { DoctorId = doctorId }
                    );

                    doctorTimings = dataTable?.AsEnumerable().Select(row => new DoctorTimingDetailsModel
                    {
                        BranchId = row.Field<int>("BranchId"),
                        Day = row.Field<string>("Day") ?? string.Empty,
                        StartTiming = row.Field<string>("StartTiming") ?? string.Empty,
                        EndTiming = row.Field<string>("EndTiming") ?? string.Empty,
                        BranchName = row.Field<string>("branchName") ?? string.Empty
                    }).ToList() ?? new List<DoctorTimingDetailsModel>();

                    // Store data in Redis cache (no expiration - cache persists until manually cleared)
                    if (doctorTimings.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(doctorTimings);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"DoctorTimingDetails data cached permanently. Key={cacheKey}, Count={doctorTimings.Count}");
                    }
                }

                if (!doctorTimings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No doctor timing details found for DoctorId={doctorId}");
                    return ServiceResult<IEnumerable<DoctorTimingDetailsModel>>.Failure(
                        alert.Type,
                        $"No timing details found for DoctorId: {doctorId}",
                        404
                    );
                }

                _log.Info($"Retrieved {doctorTimings.Count} doctor timing detail(s) from cache");

                return ServiceResult<IEnumerable<DoctorTimingDetailsModel>>.Success(
                    doctorTimings,
                    "Info",
                    $"{doctorTimings.Count} timing detail(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorTimingDetailsModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<CreateUpdateProMasterResponse> CreateUpdateProMaster(
      CreateUpdateProMasterRequest request,
      AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateProMaster called. ProId={request.ProId}, ProName={request.ProName}");

                var result = _sqlHelper.DML(
                    "IU_ProMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        proId = request.ProId,
                        proName = request.ProName,
                        contactNo = request.ContactNo,
                        isActive = request.IsActive,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        result = 0
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_ProMaster_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared ProMaster cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate PRO name: {request.ProName}");
                    return ServiceResult<CreateUpdateProMasterResponse>.Failure(
                        alert.Type,
                        "PRO Name Already Exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateProMasterResponse { ProId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ProId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"PRO Master {(request.ProId == 0 ? "created" : "updated")} successfully. ProId={resultValue}");

                    return ServiceResult<CreateUpdateProMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        request.ProId == 0 ? "PRO Saved Successfully" : "PRO Updated Successfully",
                        request.ProId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"PRO Master operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateProMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateProMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<ProMasterModel>> GetProList(
            int? proId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetProList called. ProId={proId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL PRO records
                string cacheKey = "_ProMaster_All";

                // Try to get all PRO records from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<ProMasterModel> allProRecords;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"ProMaster data retrieved from cache. Key={cacheKey}");
                    allProRecords = JsonSerializer.Deserialize<List<ProMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"ProMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL PRO records from database (no parameters)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetProList",
                        CommandType.StoredProcedure
                    );

                    allProRecords = dataTable?.AsEnumerable().Select(row => new ProMasterModel
                    {
                        ProId = row.Field<int>("ProId"),
                        Name = row.Field<string>("Name") ?? string.Empty,
                        ContactNo = row.Field<string>("ContactNo") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<ProMasterModel>();

                    // Store ALL PRO records in cache (permanent until manually cleared)
                    if (allProRecords.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allProRecords);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All ProMaster data cached permanently. Key={cacheKey}, Count={allProRecords.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<ProMasterModel> filteredProRecords = allProRecords;

                if (proId.HasValue)
                {
                    _log.Info($"Filtering cached data by ProId: {proId.Value}");
                    filteredProRecords = filteredProRecords.Where(p => p.ProId == proId.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredProRecords = filteredProRecords.Where(p => p.IsActive == isActive.Value).ToList();
                }

                if (!filteredProRecords.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No PRO records found for ProId={proId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<ProMasterModel>>.Failure(
                        alert.Type,
                        "No PRO records found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredProRecords.Count} PRO record(s) from cache");

                return ServiceResult<IEnumerable<ProMasterModel>>.Success(
                    filteredProRecords,
                    "Info",
                    $"{filteredProRecords.Count} PRO record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<ProMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<CreateUpdateReferDoctorResponse> CreateUpdateReferDoctor(
            CreateUpdateReferDoctorRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateReferDoctor called. ReferDoctorId={request.ReferDoctorId}, Name={request.Name}");

                var result = _sqlHelper.DML(
                    "IU_ReferDoctorMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        referDoctorId = request.ReferDoctorId,
                        title = request.Title,
                        name = request.Name,
                        doctorContacNo = request.DoctorContacNo,
                        clinicName = request.ClinicName,
                        address = request.Address,
                        proId = request.ProId,
                        active = request.Active,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        result = 0
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache after successful operation
                string cacheKey = "_ReferDoctorMaster_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared ReferDoctorMaster cache. Key={cacheKey}");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate Refer Doctor name: {request.Name}");
                    return ServiceResult<CreateUpdateReferDoctorResponse>.Failure(
                        alert.Type,
                        "Refer Doctor Name Already Exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateReferDoctorResponse { ReferDoctorId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ReferDoctorId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Refer Doctor {(request.ReferDoctorId == 0 ? "created" : "updated")} successfully. ReferDoctorId={resultValue}");

                    return ServiceResult<CreateUpdateReferDoctorResponse>.Success(
                        responseData,
                        alert.Type,
                        request.ReferDoctorId == 0 ? "Refer Doctor Saved Successfully" : "Refer Doctor Updated Successfully",
                        request.ReferDoctorId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Refer Doctor operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateReferDoctorResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateReferDoctorResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<ReferDoctorModel>> GetReferDoctorList(
            int? referDoctorId = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetReferDoctorList called. ReferDoctorId={referDoctorId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                // Cache key for ALL Refer Doctor records
                string cacheKey = "_ReferDoctorMaster_All";

                // Try to get all Refer Doctor records from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<ReferDoctorModel> allReferDoctors;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"ReferDoctorMaster data retrieved from cache. Key={cacheKey}");
                    allReferDoctors = JsonSerializer.Deserialize<List<ReferDoctorModel>>(cachedData);
                }
                else
                {
                    _log.Info($"ReferDoctorMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL Refer Doctor records from database (no parameters)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetReferDoctorList",
                        CommandType.StoredProcedure
                    );

                    allReferDoctors = dataTable?.AsEnumerable().Select(row => new ReferDoctorModel
                    {
                        ReferDoctorId = row.Field<int>("ReferDoctorId"),
                        Title = row.Field<string>("Title") ?? string.Empty,
                        Name = row.Field<string>("Name") ?? string.Empty,
                        DoctorName = row.Field<string>("DoctorName") ?? string.Empty,
                        ContactNo = row.Field<string>("ContactNo") ?? string.Empty,
                        ClinicName = row.Field<string>("ClinicName") ?? string.Empty,
                        Address = row.Field<string>("Address") ?? string.Empty,
                        PROName = row.Field<string>("PROName") ?? string.Empty,
                        ProId = row.Field<int>("ProId"),
                        IsActive = row.Field<int>("IsActive"),
                          CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn"),
                    }).ToList() ?? new List<ReferDoctorModel>();

                    // Store ALL Refer Doctor records in cache (permanent until manually cleared)
                    if (allReferDoctors.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allReferDoctors);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All ReferDoctorMaster data cached permanently. Key={cacheKey}, Count={allReferDoctors.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<ReferDoctorModel> filteredReferDoctors = allReferDoctors;

                if (referDoctorId.HasValue)
                {
                    _log.Info($"Filtering cached data by ReferDoctorId: {referDoctorId.Value}");
                    filteredReferDoctors = filteredReferDoctors.Where(r => r.ReferDoctorId == referDoctorId.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredReferDoctors = filteredReferDoctors.Where(r => r.IsActive == isActive.Value).ToList();
                }

                if (!filteredReferDoctors.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No Refer Doctor records found for ReferDoctorId={referDoctorId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<ReferDoctorModel>>.Failure(
                        alert.Type,
                        "No Refer Doctor records found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredReferDoctors.Count} Refer Doctor record(s) from cache");

                return ServiceResult<IEnumerable<ReferDoctorModel>>.Success(
                    filteredReferDoctors,
                    "Info",
                    $"{filteredReferDoctors.Count} Refer Doctor record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<ReferDoctorModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> UpdateReferDoctorMasterStatus(int referDoctorId, int isActive, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UpdateReferDoctorMasterStatus", CommandType.StoredProcedure, new
                {
                    @ReferDoctorId = referDoctorId,
                    @UserId = globalValues.userId,
                    @IsActive = isActive,
                    @IpAddress = globalValues.ipAddress
                });

                // Clear Refer doctor master cache after successful update
                _distributedCache.Remove("_ReferDoctorMaster_All");
                _log.Info($"Cleared ReferDoctorMaster cache after status update");

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"ReferDoctor status updated successfully. ReferDoctorId={referDoctorId}, IsActive={isActive}");
                    return ServiceResult<string>.Success(
                        "Refer Doctor status updated successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Refer Doctor not found for ReferDoctorId={referDoctorId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Doctor not found",
                        404
                    );
                }
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

    }
}