using HISWEBAPI.Configuration;
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
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HISWEBAPI.Repositories.Implementations
{
    public class AdminRepository : IAdminRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly IConfiguration _configuration;

        private const string CACHE_KEY_DOCTOR_HEADER_ALL = "_DoctorHeaderMaster_All";
        private const string CACHE_KEY_TabGroupType_All = "_TabGroupTypeMaster_All";
        private const string CACHE_KEY_IPDTab_All = "_IPDTabMaster_All";
        private const string CACHE_KEY_PREFIX_ApprovalAuthority = "_ApprovalAuthorityMaster_TypeId";


        public AdminRepository(
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

        public ServiceResult<string> CreateUpdateRoleMaster(RoleMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {

                var result = _sqlHelper.DML("IU_RoleMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @roleId = request.RoleId,
                    @roleName = request.RoleName,
                    @isActive = request.IsActive,
                    @faIconId = request.FaIconId,
                    @imagePath = request.ImagePath,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });
                _distributedCache.Remove("_RoleMaster_All");
                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        409 // Conflict
                    );
                }

                if (request.RoleId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "Role created successfully",
                        alert.Type,
                        alert.Message,
                        201 // Created
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "Role updated successfully",
                        alert.Type,
                        alert.Message,
                        200 // OK
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

        public ServiceResult<string> UpdateRoleMasterStatus(int roleId, int isActive, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UpdateRoleMasterStatus", CommandType.StoredProcedure, new
                {
                    @roleId = roleId,
                    @userId = globalValues.userId,
                    @isActive = isActive
                });
                _distributedCache.Remove("_RoleMaster_All");
                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Role status updated successfully. RoleId={roleId}, IsActive={isActive}");
                    return ServiceResult<string>.Success(
                        "Role status updated successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Role not found for RoleId={roleId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Role not found",
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


        public ServiceResult<IEnumerable<RoleMasterModel>> RoleMasterList(int? roleId = null)
        {
            try
            {
                _log.Info($"RoleMasterList called. RoleId={roleId?.ToString() ?? "All"}");

                // Always use the same cache key for all roles
                string cacheKey = "_RoleMaster_All";

                // Try to get all roles from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<RoleMasterModel> allRoles;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"RoleMaster data retrieved from cache. Key={cacheKey}");
                    allRoles = System.Text.Json.JsonSerializer.Deserialize<List<RoleMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"RoleMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL roles from database (NO parameters passed - SP returns everything)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetRoleList",
                        CommandType.StoredProcedure
                    // No parameters - SP always returns all roles
                    );

                    allRoles = dataTable?.AsEnumerable().Select(row => new RoleMasterModel
                    {
                        RoleId = row.Field<int>("RoleId"),
                        RoleName = row.Field<string>("RoleName"),
                        FaIconId = row.Field<int>("FaIconId"),
                        IsActive = row.Field<int>("IsActive"),
                        IconName = row.Field<string>("IconName"),
                        IconClass = row.Field<string>("IconClass"),
                        ImagePath = row.Field<string>("ImagePath"),
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn"),
                    }).ToList() ?? new List<RoleMasterModel>();

                    // Store ALL roles in cache (no expiration)
                    if (allRoles.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allRoles);
                        var cacheOptions = new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All RoleMaster data cached permanently. Key={cacheKey}, Count={allRoles.Count}");
                    }
                }

                // Filter in memory based on roleId parameter (always from cache)
                List<RoleMasterModel> filteredRoles;
                if (roleId.HasValue)
                {
                    _log.Info($"Filtering cached data by RoleId: {roleId.Value}");
                    filteredRoles = allRoles.Where(r => r.RoleId == roleId.Value).ToList();
                }
                else
                {
                    _log.Info("Returning all cached roles");
                    filteredRoles = allRoles;
                }

                if (!filteredRoles.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No roles found for RoleId: {roleId?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<RoleMasterModel>>.Failure(
                        alert.Type,
                        roleId.HasValue
                            ? $"Role not found for RoleId: {roleId.Value}"
                            : "No roles found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredRoles.Count} role(s) from cache");

                return ServiceResult<IEnumerable<RoleMasterModel>>.Success(
                    filteredRoles,
                    "Info",
                    $"{filteredRoles.Count} role(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<RoleMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<FaIconModel>> getFaIconMaster()
        {
            try
            {
                _log.Info("getFaIconMaster called.");

                // Cache key for FaIcon
                string cacheKey = "_FaIconMaster_All";

                // Try to get cached data
                var cachedData = _distributedCache.GetString(cacheKey);
                List<FaIconModel> faIcons;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"FaIconMaster data retrieved from cache. Key={cacheKey}");

                    faIcons = System.Text.Json.JsonSerializer.Deserialize<List<FaIconModel>>(cachedData);
                }
                else
                {
                    _log.Info($"FaIconMaster cache miss. Fetching from database. Key={cacheKey}");

                    // Fetch from DB
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getFaIconMaster",
                        CommandType.StoredProcedure
                    );

                    faIcons = dataTable?.AsEnumerable().Select(row => new FaIconModel
                    {
                        Id = row.Field<int>("Id"),
                        IconName = row.Field<string>("IconName"),
                        IconClass = row.Field<string>("IconClass")
                    }).ToList() ?? new List<FaIconModel>();

                    // Store in Redis permanently
                    if (faIcons.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(faIcons);

                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };

                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);

                        _log.Info($"FaIconMaster cached permanently. Key={cacheKey}, Count={faIcons.Count}");
                    }
                }

                // Check if empty
                if (!faIcons.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");

                    _log.Info("No FaIcon data found");
                    return ServiceResult<IEnumerable<FaIconModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var alertSuccess = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");

                _log.Info($"Retrieved {faIcons.Count} fa icon(s) from cache");

                return ServiceResult<IEnumerable<FaIconModel>>.Success(
                    faIcons,
                    alertSuccess.Type,
                    $"{faIcons.Count} fa icon(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");

                return ServiceResult<IEnumerable<FaIconModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<UserMasterResponse> CreateUpdateUserMaster(UserMasterRequest request)
        {
            try
            {
                SqlParameter[] parameters = new SqlParameter[]
                {
                    new SqlParameter("@HospId",1),
                    new SqlParameter("@Address", request.Address ?? (object)DBNull.Value),
                    new SqlParameter("@Contact", request.Contact ?? (object)DBNull.Value),
                    new SqlParameter("@DOB", request.DOB != default(DateTime) ? (object)request.DOB : DBNull.Value),
                    new SqlParameter("@Email", request.Email ?? (object)DBNull.Value),
                    new SqlParameter("@FirstName", request.FirstName),
                    new SqlParameter("@MidelName", request.MiddleName ?? (object)DBNull.Value),
                    new SqlParameter("@LastName", request.LastName ?? (object)DBNull.Value),
                    new SqlParameter("@Password", PasswordHasher.HashPassword(request.Password)),
                    new SqlParameter("@UserName", request.UserName),
                    new SqlParameter("@Gender", request.Gender ?? (object)DBNull.Value),
                    new SqlParameter("@UserId",request.userId),
                    new SqlParameter("@IsActive",request.IsActive),
                    new SqlParameter("@EmployeeID",request.EmployeeID),
                    new SqlParameter("@UserDepartmentId",request.UserDepartmentId),
                    new SqlParameter("@ReportToUserId",request.ReportToUserId),
                    new SqlParameter("@Result", SqlDbType.BigInt) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_UserMaster", parameters);
                _distributedCache.Remove("_UserMaster_All");

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("USERNAME_EXISTS");
                    _log.Warn($"Duplicate username attempted: {request.UserName}");
                    return ServiceResult<UserMasterResponse>.Failure(
                        alert.Type,
                        alert.Message,
                        409
                    );
                }



                if (request.userId == 0)
                {
                    var responseData = new UserMasterResponse { userId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("USER_CREATED");
                    _log.Info($"User inserted successfully. UserId={result}");
                    return ServiceResult<UserMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                if (request.userId > 0)
                {
                    var responseData = new UserMasterResponse { userId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"User Updated successfully. UserId={result}");
                    return ServiceResult<UserMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("USER_SAVE_FAILED");
                _log.Error("Failed to insert user. Result=0");
                return ServiceResult<UserMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SIGNUP_ERROR");
                return ServiceResult<UserMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> UpdateUserMasterStatus(int userId, int isActive, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UpdateUserMasterStatus", CommandType.StoredProcedure, new
                {
                    @userId = userId,
                    @loginUserId = globalValues.userId,
                    @isActive = isActive
                });
                _distributedCache.Remove("_UserMaster_All");

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"User status updated successfully. UserId={userId}, IsActive={isActive}");
                    return ServiceResult<string>.Success(
                        "User status updated successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"User not found for UserId={userId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "User not found",
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

        public ServiceResult<IEnumerable<UserMasterModel>> UserMasterList(int? userId = null)
        {
            try
            {
                _log.Info($"UserMasterList called. UserId={userId?.ToString() ?? "All"}");

                string cacheKey = "_UserMaster_All";

                // Try to get all roles from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<UserMasterModel> allUsers;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserMaster data retrieved from cache. Key={cacheKey}");
                    allUsers = System.Text.Json.JsonSerializer.Deserialize<List<UserMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"UserMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL users from database (NO parameters - SP returns everything)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetUserMasterList",
                        CommandType.StoredProcedure
                    // No parameters - SP always returns all users
                    );

                    allUsers = dataTable?.AsEnumerable().Select(row => new UserMasterModel
                    {
                        Id = row.Field<int>("Id"),
                        FirstName = row.Field<string>("FirstName"),
                        MidelName = row.Field<string>("MidelName"),
                        LastName = row.Field<string>("LastName"),
                        DOB = row.Field<string>("DOB"),
                        Gender = row.Field<string>("Gender"),
                        UserName = row.Field<string>("UserName") ?? string.Empty,
                        Password = row.Field<string>("Password"),
                        Address = row.Field<string>("Address"),
                        Contact = row.Field<string>("Contact"),
                        Email = row.Field<string>("Email"),
                        IsActive = row.Field<int>("IsActive"),
                        EmployeeID = row.Field<string>("EmployeeID"),
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn"),
                        ReportToUserId = row.Field<int?>("ReportToUserId"),
                        UserDepartmentId = row.Field<int?>("UserDepartmentId")
                    }).ToList() ?? new List<UserMasterModel>();

                    // Store ALL users in cache (no expiration)
                    if (allUsers.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allUsers);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All UserMaster data cached permanently. Key={cacheKey}, Count={allUsers.Count}");
                    }
                }

                // Filter in memory based on userId parameter (always from cache)
                List<UserMasterModel> filteredUsers;
                if (userId.HasValue)
                {
                    _log.Info($"Filtering cached data by UserId: {userId.Value}");
                    filteredUsers = allUsers.Where(u => u.Id == userId.Value).ToList();
                }
                else
                {
                    _log.Info("Returning all cached users");
                    filteredUsers = allUsers;
                }

                if (!filteredUsers.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No users found for UserId: {userId?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<UserMasterModel>>.Failure(
                        alert.Type,
                        userId.HasValue
                            ? $"User not found for UserId: {userId.Value}"
                            : "No users found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredUsers.Count} user(s) from cache");

                return ServiceResult<IEnumerable<UserMasterModel>>.Success(
                    filteredUsers,
                    "Info",
                    $"{filteredUsers.Count} user(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> CreateUpdateUserDepartment(UserDepartmentRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("IU_UserDepartmentMaster", CommandType.StoredProcedure, new
                {
                    @Id = request.Id,
                    @DepartmentName = request.DepartmentName,
                    @IsActive = request.IsActive,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });
                // Clear cache after successful operation
                _distributedCache.Remove("_UserDepartment_All");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        409 // Conflict
                    );
                }

                if (request.Id == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "Department created successfully",
                        alert.Type,
                        alert.Message,
                        201 // Created
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "Department updated successfully",
                        alert.Type,
                        alert.Message,
                        200 // OK
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


        public ServiceResult<string> UpdateUserDepartmentStatus(int id, int isActive, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UserDepartmentStatus", CommandType.StoredProcedure, new
                {
                    @Id = id,
                    @IsActive = isActive,
                    @UserId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                });

                // Clear cache after successful update
                _distributedCache.Remove("_UserDepartment_All");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"User department status updated successfully. Id={id}, IsActive={isActive}");
                return ServiceResult<string>.Success(
                    "Department status updated successfully",
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

        public ServiceResult<IEnumerable<UserDepartmentMasterModel>> UserDepartmentList(int? id = null)
        {
            try
            {
                _log.Info($"UserDepartmentList called. Id={id?.ToString() ?? "All"}");

                string cacheKey = "_UserDepartment_All";

                // Try to get all departments from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<UserDepartmentMasterModel> allDepartments;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserDepartment data retrieved from cache. Key={cacheKey}");
                    allDepartments = System.Text.Json.JsonSerializer.Deserialize<List<UserDepartmentMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"UserDepartment cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL departments from database (NO parameters - SP returns everything)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetUserDepartmentList",
                        CommandType.StoredProcedure
                    // No parameters - SP always returns all departments
                    );

                    allDepartments = dataTable?.AsEnumerable().Select(row => new UserDepartmentMasterModel
                    {
                        Id = row.Field<int>("Id"),
                        DepartmentName = row.Field<string>("DepartmentName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn"),
                        IPAddress = row.Field<string>("IPAddress")
                    }).ToList() ?? new List<UserDepartmentMasterModel>();

                    // Store ALL departments in cache (no expiration)
                    if (allDepartments.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allDepartments);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All UserDepartment data cached permanently. Key={cacheKey}, Count={allDepartments.Count}");
                    }
                }

                // Filter in memory based on id parameter (always from cache)
                List<UserDepartmentMasterModel> filteredDepartments;
                if (id.HasValue)
                {
                    _log.Info($"Filtering cached data by Id: {id.Value}");
                    filteredDepartments = allDepartments.Where(d => d.Id == id.Value).ToList();
                }
                else
                {
                    _log.Info("Returning all cached departments");
                    filteredDepartments = allDepartments;
                }

                if (!filteredDepartments.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No departments found for Id: {id?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<UserDepartmentMasterModel>>.Failure(
                        alert.Type,
                        id.HasValue
                            ? $"Department not found for Id: {id.Value}"
                            : "No departments found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredDepartments.Count} department(s) from cache");

                return ServiceResult<IEnumerable<UserDepartmentMasterModel>>.Success(
                    filteredDepartments,
                    "Info",
                    $"{filteredDepartments.Count} department(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserDepartmentMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<string> CreateUpdateUserGroupMaster(UserGroupRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("IU_UserGroupMaster", CommandType.StoredProcedure, new
                {
                    @Id = request.Id,
                    @GroupName = request.GroupName,
                    @IsActive = request.IsActive,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });
                _distributedCache.Remove("_UserGroupMaster_All");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        409 // Conflict
                    );
                }

                if (request.Id == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "Group created successfully",
                        alert.Type,
                        alert.Message,
                        201 // Created
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "Group updated successfully",
                        alert.Type,
                        alert.Message,
                        200 // OK
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


        public ServiceResult<string> UpdateUserGroupStatus(int id, int isActive, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UpdateUserGroupStatus", CommandType.StoredProcedure, new
                {
                    @Id = id,
                    @IsActive = isActive,
                    @UserId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                });

                _distributedCache.Remove("_UserGroupMaster_All");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Group status updated successfully",
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

        public ServiceResult<IEnumerable<UserGroupMasterModel>> UserGroupList(int? id = null)
        {
            try
            {
                _log.Info($"UserGroupList called. Id={id?.ToString() ?? "All"}");

                string cacheKey = "_UserGroupMaster_All";

                // Try from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<UserGroupMasterModel> allGroups;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("UserGroupMaster data retrieved from cache");
                    allGroups = System.Text.Json.JsonSerializer.Deserialize<List<UserGroupMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info("UserGroupMaster cache miss. Fetching from DB");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetUserGroupList",
                        CommandType.StoredProcedure
                    );

                    allGroups = dataTable?.AsEnumerable().Select(row => new UserGroupMasterModel
                    {
                        Id = row.Field<int>("Id"),
                        GroupName = row.Field<string>("GroupName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn"),
                        IPAddress = row.Field<string>("IPAddress")
                    }).ToList() ?? new List<UserGroupMasterModel>();

                    if (allGroups.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allGroups);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info("UserGroupMaster cached permanently");
                    }
                }

                // Filter
                List<UserGroupMasterModel> filteredGroups;
                if (id.HasValue)
                    filteredGroups = allGroups.Where(x => x.Id == id.Value).ToList();
                else
                    filteredGroups = allGroups;

                if (!filteredGroups.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<UserGroupMasterModel>>.Failure(
                        alert.Type,
                        id.HasValue ? "Group not found" : "No groups found",
                        404
                    );
                }

                return ServiceResult<IEnumerable<UserGroupMasterModel>>.Success(
                    filteredGroups,
                    "Info",
                    $"{filteredGroups.Count} record(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserGroupMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> CreateUpdateUserGroupMembers(UserGroupMembersRequest request, AllGlobalValues globalValues)
        {
            try
            {
                string userIdsJson = System.Text.Json.JsonSerializer.Serialize(request.UserIds);

                var result = _sqlHelper.ExecuteScalar(
                    "IU_UserGroupMembers",
                    CommandType.StoredProcedure,
                    new
                    {
                        @GroupId = request.GroupId,
                        @UserIds = userIdsJson,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                int rowCount = Convert.ToInt32(result);

                if (rowCount < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                var alert2 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{rowCount} group member(s) saved successfully",
                    alert2.Type,
                    alert2.Message,
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
        public ServiceResult<IEnumerable<UserGroupMembersModel>> UserGroupMembersList(int? groupId)
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetUserGroupMembersList",
                    CommandType.StoredProcedure,
                    new { @GroupId = groupId }
                );

                var members = dataTable?.AsEnumerable().Select(row => new UserGroupMembersModel
                {
                    isGranted = row.Field<int>("isGranted"),
                    GroupId = row.Field<int>("GroupId"),
                    UserId = row.Field<int>("UserId"),
                    GroupName = row.Field<string>("GroupName"),
                    UserName = row.Field<string>("UserName")
                }).ToList() ?? new List<UserGroupMembersModel>();

                if (!members.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<UserGroupMembersModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404 // Not Found
                    );
                }

                return ServiceResult<IEnumerable<UserGroupMembersModel>>.Success(
                    members,
                    "Info",
                    $"{members.Count} group member(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserGroupMembersModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }






        public ServiceResult<string> SaveUpdateRoleMapping(int userId, int branchId, int typeId, List<UserRoleMappingRequest> request, AllGlobalValues globalValues)
        {
            try
            {
                // Delete existing user role mappings
                var deleteResult = _sqlHelper.DML("D_DeleteUserRoleMapping", CommandType.StoredProcedure, new
                {
                    @UserId = userId,
                    @TypeId = typeId,
                    @BranchId = branchId
                },
                new
                {
                    result = 0
                });

                _log.Info($"Deleted existing role mappings for UserId={userId}, BranchId={branchId}, TypeId={typeId}");

                // Generate cache key for this specific role mapping
                string cacheKey = $"_UserRoleMapping_{branchId}_{typeId}_{userId}";

                // If request list is empty or null, only delete operation is performed
                if (request == null || !request.Any())
                {
                    // Clear cache after delete
                    _distributedCache.Remove(cacheKey);
                    _log.Info($"Cleared cache for key: {cacheKey}");

                    _log.Info($"No new roles to assign. All roles removed for UserId={userId}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "All user roles removed successfully",
                        alert.Type,
                        alert.Message ?? "All roles removed successfully",
                        200
                    );
                }

                // Insert new role mappings
                int successCount = 0;
                foreach (var item in request)
                {
                    // Skip if roleId is 0
                    if (item.roleId == 0)
                    {
                        _log.Warn($"Skipping role assignment with RoleId=0 for UserId={item.userId}");
                        continue;
                    }

                    var result = _sqlHelper.DML("IU_UserRoleMapping", CommandType.StoredProcedure, new
                    {
                        @hospId = globalValues.hospId,
                        @TypeId = item.typeId,
                        @UserId = item.userId,
                        @BranchId = item.branchId,
                        @RoleId = item.roleId,
                        @IpAddress = globalValues.ipAddress,
                        @CreatedBy = globalValues.userId
                    },
                    new
                    {
                        result = 0
                    });

                    if (result < 0)
                    {
                        _log.Error($"Failed to insert role mapping for RoleId={item.roleId}");
                    }
                    else
                    {
                        successCount++;
                    }
                }

                // Clear cache after successful operation
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                if (successCount > 0)
                {
                    _log.Info($"Successfully inserted {successCount} role mapping(s) for UserId={userId}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        $"{successCount} user role(s) assigned successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else if (request.Count > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVE_FAILED");
                    _log.Error($"Failed to insert any role mappings for UserId={userId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Failed to assign any roles",
                        500
                    );
                }

                // This case shouldn't happen, but handle it anyway
                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "User roles updated successfully",
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<IEnumerable<UserRoleMappingModel>> GetAssignRoleForUserAuthorization(int branchId, int typeId, int userId)
        {
            try
            {
                _log.Info($"GetAssignRoleForUserAuthorization called. BranchId={branchId}, TypeId={typeId}, UserId={userId}");

                // Generate dynamic cache key based on parameters
                string cacheKey = $"_UserRoleMapping_{branchId}_{typeId}_{userId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<UserRoleMappingModel> roles;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserRoleMapping data retrieved from cache. Key={cacheKey}");
                    roles = System.Text.Json.JsonSerializer.Deserialize<List<UserRoleMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"UserRoleMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetAssignRoleForUserAuthorization",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = branchId,
                            @TypeId = typeId,
                            @UserId = userId
                        }
                    );

                    roles = dataTable?.AsEnumerable().Select(row => new UserRoleMappingModel
                    {
                        isGranted = row.Field<int>("isGranted"),
                        RoleName = row.Field<string>("RoleName") ?? string.Empty,
                        RoleId = row.Field<int>("RoleId")
                    }).ToList() ?? new List<UserRoleMappingModel>();

                    // Store data in cache with no expiration (permanent until manually cleared)
                    if (roles.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(roles);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"UserRoleMapping data cached permanently. Key={cacheKey}, Count={roles.Count}");
                    }
                }

                if (!roles.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No role authorization data found for UserId={userId}, BranchId={branchId}, TypeId={typeId}");
                    return ServiceResult<IEnumerable<UserRoleMappingModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {roles.Count} role authorization records from cache for UserId={userId}");

                return ServiceResult<IEnumerable<UserRoleMappingModel>>.Success(
                    roles,
                    "Info",
                    $"{roles.Count} role(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserRoleMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<string> SaveUpdateUserRightMapping(SaveUserRightMappingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                // First, delete existing user right mappings for this user/branch/role/type combination
                var deleteResult = _sqlHelper.DML("D_DeleteUserRightMapping", CommandType.StoredProcedure, new
                {
                    @TypeId = request.TypeId,
                    @UserId = request.UserId,
                    @BranchId = request.BranchId,
                    @RoleId = request.RoleId
                },
                new
                {
                    result = 0
                });

                _log.Info($"Deleted existing user rights for UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}, TypeId={request.TypeId}");

                // Generate cache key for this specific user right mapping
                string cacheKey = $"_UserRightMapping_{request.BranchId}_{request.TypeId}_{request.UserId}_{request.RoleId}";

                // Clear cache after delete
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                // If UserRights list is empty or null, only delete operation was needed
                if (request.UserRights == null || !request.UserRights.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info("User rights deleted successfully. No new rights to insert.");

                    return ServiceResult<string>.Success(
                        "User rights deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // If UserRights list has items with UserRightId = 0, it means no rights to assign
                var validUserRights = request.UserRights.Where(ur => ur.UserRightId != 0).ToList();

                if (!validUserRights.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info("User rights deleted successfully. No valid rights to insert.");

                    return ServiceResult<string>.Success(
                        "User rights deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Insert new user right mappings
                int insertedCount = 0;
                foreach (var userRight in validUserRights)
                {
                    var result = _sqlHelper.DML("IU_UserRightMapping", CommandType.StoredProcedure, new
                    {
                        @hospId = globalValues.hospId,
                        @TypeId = userRight.TypeId,
                        @UserId = userRight.UserId,
                        @BranchId = userRight.BranchId,
                        @RoleId = userRight.RoleId,
                        @UserRightId = userRight.UserRightId,
                        @IpAddress = globalValues.ipAddress,
                        @CreatedBy = globalValues.userId
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }

                _log.Info($"Inserted {insertedCount} user rights for UserId={request.UserId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"User rights updated successfully. {insertedCount} right(s) assigned.",
                    alert1.Type,
                    alert1.Message,
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


        public ServiceResult<IEnumerable<UserRightMappingModel>> GetAssignUserRightMapping(
            int branchId,
            int typeId,
            int userId,
            int roleId)
        {
            try
            {
                _log.Info($"GetAssignUserRightMapping called. BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

                // Generate dynamic cache key based on branchId, typeId, userId, and roleId
                string cacheKey = $"_UserRightMapping_{branchId}_{typeId}_{userId}_{roleId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<UserRightMappingModel> userRights;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserRightMapping data retrieved from cache. Key={cacheKey}");
                    userRights = System.Text.Json.JsonSerializer.Deserialize<List<UserRightMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"UserRightMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getAssignUserRightMapping",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = branchId,
                            @typeId = typeId,
                            @UserId = userId,
                            @RoleId = roleId
                        }
                    );

                    userRights = dataTable?.AsEnumerable().Select(row => new UserRightMappingModel
                    {
                        IsGranted = row.Field<int>("isGranted"),
                        UserRightName = row.Field<string>("UserRightName") ?? string.Empty,
                        Description = row.Field<string>("Description") ?? string.Empty,
                        UserRightId = row.Field<int>("UserRightId")
                    }).ToList() ?? new List<UserRightMappingModel>();

                    // Store data in cache with no expiration
                    if (userRights.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(userRights);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"UserRightMapping data cached permanently. Key={cacheKey}, Count={userRights.Count}");
                    }
                }

                if (!userRights.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No user rights found for BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

                    return ServiceResult<IEnumerable<UserRightMappingModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {userRights.Count} user rights mapping records from cache");

                return ServiceResult<IEnumerable<UserRightMappingModel>>.Success(
                    userRights,
                    "Info",
                    $"{userRights.Count} user right(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserRightMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }



        public ServiceResult<string> SaveUpdateDashBoardUserRightMapping(SaveDashboardUserRightMappingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                // First, delete existing dashboard user right mappings for this user/branch/role/type combination
                var deleteResult = _sqlHelper.DML("D_DeleteDashBoardUserRightMapping", CommandType.StoredProcedure, new
                {
                    @TypeId = request.TypeId,
                    @UserId = request.UserId,
                    @BranchId = request.BranchId,
                    @RoleId = request.RoleId
                },
                new
                {
                    result = 0
                });

                _log.Info($"Deleted existing dashboard user rights for UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}, TypeId={request.TypeId}");

                // Generate cache key for this specific dashboard user right mapping
                string cacheKey = $"_DashboardUserRightMapping_{request.BranchId}_{request.TypeId}_{request.UserId}_{request.RoleId}";

                // Clear cache after delete
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                // If DashboardUserRights list is empty or null, only delete operation was needed
                if (request.DashboardUserRights == null || !request.DashboardUserRights.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info("Dashboard user rights deleted successfully. No new rights to insert.");

                    return ServiceResult<string>.Success(
                        "Dashboard user rights deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // If DashboardUserRights list has items with UserRightId = 0, it means no rights to assign
                var validDashboardRights = request.DashboardUserRights.Where(ur => ur.UserRightId != 0).ToList();

                if (!validDashboardRights.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info("Dashboard user rights deleted successfully. No valid rights to insert.");

                    return ServiceResult<string>.Success(
                        "Dashboard user rights deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Insert new dashboard user right mappings
                int insertedCount = 0;
                foreach (var dashboardRight in validDashboardRights)
                {
                    var result = _sqlHelper.DML("IU_DashBoardUserRightMapping", CommandType.StoredProcedure, new
                    {
                        @hospId = globalValues.hospId,
                        @TypeId = dashboardRight.TypeId,
                        @UserId = dashboardRight.UserId,
                        @BranchId = dashboardRight.BranchId,
                        @RoleId = dashboardRight.RoleId,
                        @UserRightId = dashboardRight.UserRightId,
                        @IpAddress = globalValues.ipAddress,
                        @CreatedBy = globalValues.userId
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }

                _log.Info($"Inserted {insertedCount} dashboard user rights for UserId={request.UserId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"Dashboard user rights updated successfully. {insertedCount} dashboard right(s) assigned.",
                    alert1.Type,
                    alert1.Message,
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



        public ServiceResult<IEnumerable<DashboardUserRightMappingModel>> GetAssignDashBoardUserRight(
            int branchId,
            int typeId,
            int userId,
            int roleId)
        {
            try
            {
                _log.Info($"GetAssignDashBoardUserRight called. BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

                // Generate dynamic cache key based on branchId, typeId, userId, and roleId
                string cacheKey = $"_DashboardUserRightMapping_{branchId}_{typeId}_{userId}_{roleId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DashboardUserRightMappingModel> dashboardRights;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DashboardUserRightMapping data retrieved from cache. Key={cacheKey}");
                    dashboardRights = System.Text.Json.JsonSerializer.Deserialize<List<DashboardUserRightMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DashboardUserRightMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getAssignDashBoardUserRight",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = branchId,
                            @TypeId = typeId,
                            @UserId = userId,
                            @RoleId = roleId
                        }
                    );

                    dashboardRights = dataTable?.AsEnumerable().Select(row => new DashboardUserRightMappingModel
                    {
                        IsGranted = row.Field<int>("isGranted"),
                        UserRightName = row.Field<string>("UserRightName") ?? string.Empty,
                        Details = row.Field<string>("Details") ?? string.Empty,
                        UserRightId = row.Field<int>("UserRightId")
                    }).ToList() ?? new List<DashboardUserRightMappingModel>();

                    // Store data in cache with no expiration
                    if (dashboardRights.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(dashboardRights);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"DashboardUserRightMapping data cached permanently. Key={cacheKey}, Count={dashboardRights.Count}");
                    }
                }

                if (!dashboardRights.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No dashboard user rights found for BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

                    return ServiceResult<IEnumerable<DashboardUserRightMappingModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {dashboardRights.Count} dashboard user rights mapping records from cache");

                return ServiceResult<IEnumerable<DashboardUserRightMappingModel>>.Success(
                    dashboardRights,
                    "Info",
                    $"{dashboardRights.Count} dashboard user right(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DashboardUserRightMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }



        public ServiceResult<NavigationTabMasterResponse> CreateUpdateNavigationTabMaster(NavigationTabMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@TabId", request.TabId),
            new SqlParameter("@HospId", globalValues.hospId),
            new SqlParameter("@TabName", request.TabName),
            new SqlParameter("@FaIconId", request.FaIconId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@CreatedOn", globalValues.userId),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };
                int result = (int)_sqlHelper.RunProcedureInsert("IU_NavigationTabMaster", parameters);

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate navigation tab name attempted: {request.TabName}");
                    return ServiceResult<NavigationTabMasterResponse>.Failure(
                        alert.Type,
                        alert.Message,
                        409
                    );
                }

                if (request.TabId == 0)
                {
                    var responseData = new NavigationTabMasterResponse { TabId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Navigation tab created successfully. TabId={result}");
                    return ServiceResult<NavigationTabMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var responseData = new NavigationTabMasterResponse { TabId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Navigation tab updated successfully. TabId={result}");
                    return ServiceResult<NavigationTabMasterResponse>.Success(
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
                return ServiceResult<NavigationTabMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<IEnumerable<NavigationTabMasterModel>> GetNavigationTabMaster()
        {
            try
            {
                _log.Info("GetNavigationTabMaster called.");

                // Fetch data from database using stored procedure
                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetNavigationTabMaster",
                    CommandType.StoredProcedure
                );

                var navigationTabs = dataTable?.AsEnumerable().Select(row => new NavigationTabMasterModel
                {
                    TabId = row.Field<int>("TabId"),
                    TabName = row.Field<string>("TabName") ?? string.Empty,
                    FaIconId = row.Field<int>("FaIconId"),
                    IsActive = row.Field<int>("IsActive")
                }).ToList() ?? new List<NavigationTabMasterModel>();

                if (!navigationTabs.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No navigation tabs found");
                    return ServiceResult<IEnumerable<NavigationTabMasterModel>>.Failure(
                        alert.Type,
                        "No navigation tabs found",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                _log.Info($"Retrieved {navigationTabs.Count} navigation tab(s) from database");

                return ServiceResult<IEnumerable<NavigationTabMasterModel>>.Success(
                    navigationTabs,
                    alert1.Type,
                    $"{navigationTabs.Count} navigation tab(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<NavigationTabMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<NavigationSubMenuMasterResponse> CreateUpdateNavigationSubMenuMaster(
            NavigationSubMenuMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@SubMenuId", request.SubMenuId),
            new SqlParameter("@TabId", request.TabId),
            new SqlParameter("@SubMenuName", request.SubMenuName),
            new SqlParameter("@HospId", globalValues.hospId),
            new SqlParameter("@URL", request.URL ?? (object)DBNull.Value),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@CreatedOn", globalValues.userId),
            new SqlParameter("@IsActive", request.IsActive ? 1 : 0),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                int result = (int)_sqlHelper.RunProcedureInsert("IU_NavigationSubMenuMaster", parameters);

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate navigation sub menu name attempted: {request.SubMenuName}");
                    return ServiceResult<NavigationSubMenuMasterResponse>.Failure(
                        alert.Type,
                        $"Sub menu '{request.SubMenuName}' already exists",
                        409
                    );
                }

                if (request.SubMenuId == 0)
                {
                    var responseData = new NavigationSubMenuMasterResponse { SubMenuId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Navigation sub menu created successfully. SubMenuId={result}");
                    return ServiceResult<NavigationSubMenuMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var responseData = new NavigationSubMenuMasterResponse { SubMenuId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Navigation sub menu updated successfully. SubMenuId={result}");
                    return ServiceResult<NavigationSubMenuMasterResponse>.Success(
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
                return ServiceResult<NavigationSubMenuMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<NavigationSubMenuMasterModel>> GetNavigationSubMenuMaster()
        {
            try
            {
                _log.Info($"GetNavigationSubMenuMaster called");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetNavigationSubMenuMaster",
                    CommandType.StoredProcedure,
                    new
                    {

                    }
                );

                var subMenus = dataTable?.AsEnumerable().Select(row => new NavigationSubMenuMasterModel
                {
                    SubMenuId = row.Field<int>("SubMenuId"),
                    TabId = row.Field<int>("TabId"),
                    TabName = row.Field<string>("TabName") ?? string.Empty,
                    SubMenuName = row.Field<string>("SubMenuName") ?? string.Empty,
                    URL = row.Field<string>("URL") ?? string.Empty,
                    IsActive = row.Field<int>("IsActive"),
                    CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                    CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                    LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                    LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty,
                    IpAddress = row.Field<string>("IpAddress") ?? string.Empty
                }).ToList() ?? new List<NavigationSubMenuMasterModel>();

                if (!subMenus.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No navigation sub menus found");

                    return ServiceResult<IEnumerable<NavigationSubMenuMasterModel>>.Failure(
                        alert.Type,
                        "No navigation sub menus found",
                        404
                    );
                }

                _log.Info($"Retrieved {subMenus.Count} navigation sub menu(s)");

                return ServiceResult<IEnumerable<NavigationSubMenuMasterModel>>.Success(
                    subMenus,
                    "Info",
                    $"{subMenus.Count} navigation sub menu(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<NavigationSubMenuMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }





        public ServiceResult<string> SaveUpdateRoleWiseMenuMapping(
            SaveRoleWiseMenuMappingRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                // Delete existing role-wise menu mappings only if IsFirst = 1
                if (request.IsFirst == 1)
                {
                    var deleteResult = _sqlHelper.DML("D_DeleteRoleWiseMenuMappingMaster", CommandType.StoredProcedure, new
                    {
                        @BranchId = 1,
                        @RoleId = request.RoleId
                    },
                    new
                    {
                        result = 0
                    });

                    _log.Info($"Deleted existing role-wise menu mappings for BranchId={request.BranchId}, RoleId={request.RoleId}");
                }

                // Generate cache key for this specific mapping
                string cacheKey = $"_RoleWiseMenuMapping_{1}_{request.RoleId}";

                _distributedCache.Remove(cacheKey);

                GlobalFunctions.ClearCacheByPattern(_configuration, "_RoleWiseMenuMapping_*");
                GlobalFunctions.ClearCacheByPattern(_configuration, "_UserWiseMenuMapping_*");

                _log.Info($"Cleared cache for key: {cacheKey}");

                // If MenuMappings list is empty or null, only delete operation was needed
                if (request.MenuMappings == null || !request.MenuMappings.Any())
                {


                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );
                    _log.Info("Role-wise menu mapping operation completed. No new mappings to insert.");

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "Role-wise menu mappings deleted successfully" : "No menu mappings to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Filter out items with SubMenuId = 0
                var validMenuMappings = request.MenuMappings.Where(mm => mm.SubMenuId != 0).ToList();

                if (!validMenuMappings.Any())
                {


                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );
                    _log.Info("Role-wise menu mapping operation completed. No valid mappings to insert.");

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "Role-wise menu mappings deleted successfully" : "No valid menu mappings to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Validate consistency of all items with parent request
                bool isConsistent = validMenuMappings.All(x =>
                    x.BranchId == request.BranchId &&
                    x.RoleId == request.RoleId);

                if (!isConsistent)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("Inconsistent BranchId or RoleId in menu mapping list.");

                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "All menu mapping items must have the same BranchId and RoleId as the request",
                        400
                    );
                }

                // Insert new role-wise menu mappings
                int insertedCount = 0;
                foreach (var menuMapping in validMenuMappings)
                {
                    var result = _sqlHelper.DML("IU_RoleWiseMenuMappingMaster", CommandType.StoredProcedure, new
                    {
                        @RoleId = menuMapping.RoleId,
                        @BranchId = 1,
                        @SubMenuId = menuMapping.SubMenuId,
                        @HospId = globalValues.hospId,
                        @CreatedBy = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }



                _log.Info($"Inserted {insertedCount} role-wise menu mappings for RoleId={request.RoleId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"Role-wise menu mappings updated successfully. {insertedCount} mapping(s) assigned.",
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<IEnumerable<RoleWiseMenuMappingModel>> GetRoleWiseMenuMapping(
            int branchId,
            int roleId)
        {
            try
            {
                _log.Info($"GetRoleWiseMenuMapping called. BranchId={1}, RoleId={roleId}");

                // Generate dynamic cache key based on branchId and roleId
                string cacheKey = $"_RoleWiseMenuMapping_{1}_{roleId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<RoleWiseMenuMappingModel> menuMappings;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"RoleWiseMenuMapping data retrieved from cache. Key={cacheKey}");
                    menuMappings = System.Text.Json.JsonSerializer.Deserialize<List<RoleWiseMenuMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"RoleWiseMenuMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_RoleWiseMenuMappingMaster",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = 1,
                            @RoleId = roleId
                        }
                    );

                    menuMappings = dataTable?.AsEnumerable().Select(row => new RoleWiseMenuMappingModel
                    {
                        IsGranted = row.Field<int>("isGranted"),
                        SubMenuId = row.Field<int>("SubMenuId"),
                        TabId = row.Field<int>("TabId"),
                        SubMenuName = row.Field<string>("SubMenuName") ?? string.Empty,
                        TabName = row.Field<string>("TabName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<RoleWiseMenuMappingModel>();

                    // Store data in cache with no expiration
                    if (menuMappings.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(menuMappings);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"RoleWiseMenuMapping data cached permanently. Key={cacheKey}, Count={menuMappings.Count}");
                    }
                }

                if (!menuMappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No role-wise menu mapping found for BranchId={branchId}, RoleId={roleId}");

                    return ServiceResult<IEnumerable<RoleWiseMenuMappingModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {menuMappings.Count} role-wise menu mapping records from cache");

                return ServiceResult<IEnumerable<RoleWiseMenuMappingModel>>.Success(
                    menuMappings,
                    "Info",
                    $"{menuMappings.Count} menu mapping(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<RoleWiseMenuMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> SaveUpdateUserMenuMaster(
            SaveUserMenuMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                // Delete existing user menu mappings if IsFirst = 1
                if (request.IsFirst == 1)
                {
                    var deleteResult = _sqlHelper.DML("D_DeleteUserMenuMaster", CommandType.StoredProcedure, new
                    {
                        @TypeId = request.TypeId,
                        @UserId = request.UserId,
                        @BranchId = request.BranchId,
                        @RoleId = request.RoleId
                    },
                    new
                    {
                        result = 0
                    });

                    _log.Info($"Deleted existing user menu for TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}");
                }

                // Generate cache key for this specific user menu mapping
                string cacheKey = $"_UserWiseMenuMapping_{request.BranchId}_{request.TypeId}_{request.UserId}_{request.RoleId}";
                string cacheKey2 = $"_UserTabMenu_{request.BranchId}_{request.RoleId}_{request.UserId}";
                // Clear cache after delete
                _distributedCache.Remove(cacheKey);
                _distributedCache.Remove(cacheKey2);
                _log.Info($"Cleared cache for key: {cacheKey},{cacheKey2}");

                // If UserMenus list is empty or null, only delete operation was needed
                if (request.UserMenus == null || !request.UserMenus.Any())
                {


                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );
                    _log.Info("User menu operation completed. No new menus to insert.");

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "User menus deleted successfully" : "No user menus to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Filter out items with SubMenuId = 0
                var validUserMenus = request.UserMenus.Where(um => um.SubMenuId != 0).ToList();

                if (!validUserMenus.Any())
                {


                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );
                    _log.Info("User menu operation completed. No valid menus to insert.");

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "User menus deleted successfully" : "No valid user menus to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Validate consistency of all items with parent request
                bool isConsistent = validUserMenus.All(x =>
                    x.TypeId == request.TypeId &&
                    x.UserId == request.UserId &&
                    x.BranchId == request.BranchId &&
                    x.RoleId == request.RoleId);

                if (!isConsistent)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("Inconsistent TypeId, UserId, BranchId, or RoleId in user menu list.");

                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "All user menu items must have the same TypeId, UserId, BranchId, and RoleId as the request",
                        400
                    );
                }

                // Insert new user menu mappings
                int insertedCount = 0;
                foreach (var userMenu in validUserMenus)
                {
                    var result = _sqlHelper.DML("IU_UserMenuMaster", CommandType.StoredProcedure, new
                    {
                        @TypeId = userMenu.TypeId,
                        @UserId = userMenu.UserId,
                        @RoleId = userMenu.RoleId,
                        @BranchId = userMenu.BranchId,
                        @SubMenuId = userMenu.SubMenuId,
                        @HospId = globalValues.hospId,
                        @CreatedBy = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }



                _log.Info($"Inserted {insertedCount} user menu records for UserId={request.UserId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"User menu updated successfully. {insertedCount} menu(s) assigned.",
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<IEnumerable<UserWiseMenuMasterModel>> GetUserWiseMenuMaster(
            int branchId,
            int typeId,
            int userId,
            int roleId)
        {
            try
            {
                _log.Info($"GetUserWiseMenuMaster called. BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

                // Generate dynamic cache key based on branchId, typeId, userId, and roleId
                string cacheKey = $"_UserWiseMenuMapping_{branchId}_{typeId}_{userId}_{roleId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<UserWiseMenuMasterModel> userMenus;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserWiseMenuMaster data retrieved from cache. Key={cacheKey}");
                    userMenus = System.Text.Json.JsonSerializer.Deserialize<List<UserWiseMenuMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"UserWiseMenuMaster cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_UserWiseMenuMaster",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = branchId,
                            @TypeId = typeId,
                            @UserId = userId,
                            @RoleId = roleId
                        }
                    );

                    userMenus = dataTable?.AsEnumerable().Select(row => new UserWiseMenuMasterModel
                    {
                        IsGranted = row.Field<int>("isGranted"),
                        SubMenuId = row.Field<int>("SubMenuId"),
                        TabId = row.Field<int>("TabId"),
                        SubMenuName = row.Field<string>("SubMenuName") ?? string.Empty,
                        TabName = row.Field<string>("TabName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<UserWiseMenuMasterModel>();

                    // Store data in cache with no expiration
                    if (userMenus.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(userMenus);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"UserWiseMenuMaster data cached permanently. Key={cacheKey}, Count={userMenus.Count}");
                    }
                }

                if (!userMenus.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No user-wise menu found for BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

                    return ServiceResult<IEnumerable<UserWiseMenuMasterModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {userMenus.Count} user-wise menu records (Granted + Remaining) from cache");

                return ServiceResult<IEnumerable<UserWiseMenuMasterModel>>.Success(
                    userMenus,
                    "Info",
                    $"{userMenus.Count} user-wise menu(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserWiseMenuMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> SaveUpdateUserCorporateMapping(
     SaveUserCorporateMappingRequest request,
     AllGlobalValues globalValues)
        {
            try
            {
                // Delete existing user corporate mappings if IsFirst = 1
                if (request.IsFirst == 1)
                {
                    var deleteResult = _sqlHelper.DML("D_DeleteUserCorporateMapping", CommandType.StoredProcedure, new
                    {
                        @TypeId = request.TypeId,
                        @UserId = request.UserId,
                        @BranchId = request.BranchId
                    },
                    new
                    {
                        result = 0
                    });

                    _log.Info($"Deleted existing user corporate mapping for TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}");
                }

                // If UserCorporates list is empty or null, only delete operation was needed
                if (request.UserCorporates == null || !request.UserCorporates.Any())
                {
                    // Clear cache after delete
                    string cacheKey = $"_UserCorporateMapping_{request.BranchId}_{request.TypeId}_{request.UserId}";
                    _distributedCache.Remove(cacheKey);
                    _log.Info($"Cleared cache for key: {cacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );
                    _log.Info("User corporate mapping operation completed. No new mappings to insert.");

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "User corporate mappings deleted successfully" : "No corporate mappings to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Filter out items with CorporateId = 0
                var validUserCorporates = request.UserCorporates.Where(uc => uc.CorporateId != 0).ToList();

                if (!validUserCorporates.Any())
                {
                    // Clear cache
                    string cacheKey = $"_UserCorporateMapping_{request.BranchId}_{request.TypeId}_{request.UserId}";
                    _distributedCache.Remove(cacheKey);
                    _log.Info($"Cleared cache for key: {cacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );
                    _log.Info("User corporate mapping operation completed. No valid mappings to insert.");

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "User corporate mappings deleted successfully" : "No valid corporate mappings to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Validate consistency of all items with parent request
                bool isConsistent = validUserCorporates.All(x =>
                    x.TypeId == request.TypeId &&
                    x.UserId == request.UserId &&
                    x.BranchId == request.BranchId);

                if (!isConsistent)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("Inconsistent TypeId, UserId, or BranchId in user corporate mapping list.");

                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "All user corporate mapping items must have the same TypeId, UserId, and BranchId as the request",
                        400
                    );
                }

                // Insert new user corporate mappings
                int insertedCount = 0;
                foreach (var userCorporate in validUserCorporates)
                {
                    var result = _sqlHelper.DML("IU_UserCorporateMapping", CommandType.StoredProcedure, new
                    {
                        @hospId = globalValues.hospId,
                        @TypeId = userCorporate.TypeId,
                        @UserId = userCorporate.UserId,
                        @BranchId = userCorporate.BranchId,
                        @CorporateId = userCorporate.CorporateId,
                        @CreatedBy = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }

                // Clear cache after successful operation
                string clearCacheKey = $"_UserCorporateMapping_{request.BranchId}_{request.TypeId}_{request.UserId}";
                _distributedCache.Remove(clearCacheKey);
                _log.Info($"Cleared cache for key: {clearCacheKey}");

                _log.Info($"Inserted {insertedCount} user corporate mapping records for UserId={request.UserId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"User corporate mapping updated successfully. {insertedCount} corporate(s) assigned.",
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<IEnumerable<UserWiseCorporateMappingModel>> GetUserWiseCorporateMapping(
            int branchId,
            int typeId,
            int userId)
        {
            try
            {
                _log.Info($"GetUserWiseCorporateMapping called. BranchId={branchId}, TypeId={typeId}, UserId={userId}");

                // Generate dynamic cache key based on branchId, typeId, and userId
                string cacheKey = $"_UserCorporateMapping_{branchId}_{typeId}_{userId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<UserWiseCorporateMappingModel> userCorporates;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserCorporateMapping data retrieved from cache. Key={cacheKey}");
                    userCorporates = System.Text.Json.JsonSerializer.Deserialize<List<UserWiseCorporateMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"UserCorporateMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetRemainingAssignCorporateForUserAuthorization",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = branchId,
                            @TypeId = typeId,
                            @UserId = userId
                        }
                    );

                    userCorporates = dataTable?.AsEnumerable().Select(row => new UserWiseCorporateMappingModel
                    {
                        IsGranted = row.Field<int>("isGranted"),
                        CorporateId = row.Field<int>("CorporateId"),
                        CorporateName = row.Field<string>("CorporateName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<UserWiseCorporateMappingModel>();

                    // Store data in cache with no expiration
                    if (userCorporates.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(userCorporates);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"UserCorporateMapping data cached permanently. Key={cacheKey}, Count={userCorporates.Count}");
                    }
                }

                if (!userCorporates.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No user corporate mapping found for BranchId={branchId}, TypeId={typeId}, UserId={userId}");

                    return ServiceResult<IEnumerable<UserWiseCorporateMappingModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {userCorporates.Count} user corporate mapping records (Granted + Remaining) from cache");

                return ServiceResult<IEnumerable<UserWiseCorporateMappingModel>>.Success(
                    userCorporates,
                    "Info",
                    $"{userCorporates.Count} user corporate mapping(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserWiseCorporateMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<string> SaveUpdateUserBedMapping(
    SaveUserBedMappingRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                // Delete existing user bed mappings if IsFirst = 1
                if (request.IsFirst == 1)
                {
                    var deleteResult = _sqlHelper.DML("D_DeleteUserBedMapping", CommandType.StoredProcedure, new
                    {
                        @TypeId = request.TypeId,
                        @UserId = request.UserId,
                        @BranchId = request.BranchId
                    },
                    new
                    {
                        result = 0
                    });

                    _log.Info($"Deleted existing user bed mapping for TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}");
                }

                // Generate cache key for this specific bed mapping
                string cacheKey = $"_UserBedMapping_{request.BranchId}_{request.TypeId}_{request.UserId}";

                // If UserBeds list is empty or null, only delete operation was needed
                if (request.UserBeds == null || !request.UserBeds.Any())
                {
                    // Clear cache after delete
                    _distributedCache.Remove(cacheKey);
                    _log.Info($"Cleared cache for key: {cacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );
                    _log.Info("User bed mapping operation completed. No new mappings to insert.");

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "User bed mappings deleted successfully" : "No bed mappings to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Filter out items with ServiceItemId = 0
                var validUserBeds = request.UserBeds.Where(ub => ub.ServiceItemId != 0).ToList();

                if (!validUserBeds.Any())
                {
                    // Clear cache
                    _distributedCache.Remove(cacheKey);
                    _log.Info($"Cleared cache for key: {cacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );
                    _log.Info("User bed mapping operation completed. No valid mappings to insert.");

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "User bed mappings deleted successfully" : "No valid bed mappings to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Validate consistency of all items with parent request
                bool isConsistent = validUserBeds.All(x =>
                    x.TypeId == request.TypeId &&
                    x.UserId == request.UserId &&
                    x.BranchId == request.BranchId);

                if (!isConsistent)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("Inconsistent TypeId, UserId, or BranchId in user bed mapping list.");

                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "All user bed mapping items must have the same TypeId, UserId, and BranchId as the request",
                        400
                    );
                }

                // Insert new user bed mappings
                int insertedCount = 0;
                foreach (var userBed in validUserBeds)
                {
                    var result = _sqlHelper.DML("IU_UserBedMapping", CommandType.StoredProcedure, new
                    {
                        @hospId = globalValues.hospId,
                        @TypeId = userBed.TypeId,
                        @UserId = userBed.UserId,
                        @BranchId = userBed.BranchId,
                        @ServiceItemId = userBed.ServiceItemId,
                        @CreatedBy = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }

                // Clear cache after successful operation
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                _log.Info($"Inserted {insertedCount} user bed mapping records for UserId={request.UserId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"User bed mapping updated successfully. {insertedCount} bed(s) assigned.",
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<IEnumerable<UserWiseBedMappingModel>> GetUserWiseBedMapping(
            int branchId,
            int typeId,
            int userId)
        {
            try
            {
                _log.Info($"GetUserWiseBedMapping called. BranchId={branchId}, TypeId={typeId}, UserId={userId}");

                // Generate dynamic cache key based on branchId, typeId, and userId
                string cacheKey = $"_UserBedMapping_{branchId}_{typeId}_{userId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<UserWiseBedMappingModel> userBeds;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserBedMapping data retrieved from cache. Key={cacheKey}");
                    userBeds = System.Text.Json.JsonSerializer.Deserialize<List<UserWiseBedMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"UserBedMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetRemainingAssignBedForUserAuthorization",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = branchId,
                            @TypeId = typeId,
                            @UserId = userId
                        }
                    );

                    userBeds = dataTable?.AsEnumerable().Select(row => new UserWiseBedMappingModel
                    {
                        IsGranted = row.Field<int>("isGranted"),
                        ServiceItemId = row.Field<int>("ServiceItemId"),
                        Name = row.Field<string>("Name") ?? string.Empty,
                    }).ToList() ?? new List<UserWiseBedMappingModel>();

                    // Store data in cache with no expiration
                    if (userBeds.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(userBeds);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"UserBedMapping data cached permanently. Key={cacheKey}, Count={userBeds.Count}");
                    }
                }

                if (!userBeds.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No user bed mapping found for BranchId={branchId}, TypeId={typeId}, UserId={userId}");

                    return ServiceResult<IEnumerable<UserWiseBedMappingModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {userBeds.Count} user bed mapping records (Granted + Remaining) from cache");

                return ServiceResult<IEnumerable<UserWiseBedMappingModel>>.Success(
                    userBeds,
                    "Info",
                    $"{userBeds.Count} user bed mapping(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<UserWiseBedMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<BranchMasterResponse> CreateUpdateBranchMaster(BranchMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@hospId", globalValues.hospId),
            new SqlParameter("@branchId", request.BranchId),
            new SqlParameter("@branchName", request.BranchName),
            new SqlParameter("@branchCode", request.BranchCode),
            new SqlParameter("@email", request.Email ?? (object)DBNull.Value),
            new SqlParameter("@contactNo1", request.ContactNo1),
            new SqlParameter("@contactNo2", request.ContactNo2 ?? (object)DBNull.Value),
            new SqlParameter("@address", request.Address ?? (object)DBNull.Value),
            new SqlParameter("@isActive", request.IsActive),
            new SqlParameter("@fYStartFrom", request.FYStartFrom),
            new SqlParameter("@userId", globalValues.userId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_BranchMaster", parameters);

                // Clear cache after successful operation
                _distributedCache.Remove("_BranchMaster_All");

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate branch name or code attempted: {request.BranchName}");
                    return ServiceResult<BranchMasterResponse>.Failure(
                        alert.Type,
                        "Branch Name or Branch Code already exists",
                        409
                    );
                }

                if (request.BranchId == 0)
                {
                    var responseData = new BranchMasterResponse { BranchId = (int)result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Branch created successfully. BranchId={result}");
                    return ServiceResult<BranchMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var responseData = new BranchMasterResponse { BranchId = (int)result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Branch updated successfully. BranchId={result}");
                    return ServiceResult<BranchMasterResponse>.Success(
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
                return ServiceResult<BranchMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<BranchMasterModel>> GetBranchDetails(int? branchId = null)
        {
            try
            {
                _log.Info($"GetBranchDetails called. BranchId={branchId?.ToString() ?? "All"}");

                string cacheKey = "_BranchMaster_All";

                // Try to get all branches from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<BranchMasterModel> allBranches;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"BranchMaster data retrieved from cache. Key={cacheKey}");
                    allBranches = System.Text.Json.JsonSerializer.Deserialize<List<BranchMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"BranchMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL branches from database (NO parameters - SP returns everything)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetBranchDetails",
                        CommandType.StoredProcedure
                    );

                    allBranches = dataTable?.AsEnumerable().Select(row => new BranchMasterModel
                    {
                        BranchId = row.Field<int>("BranchId"),
                        BranchName = row.Field<string>("BranchName") ?? string.Empty,
                        BranchCode = row.Field<string>("BranchCode") ?? string.Empty,
                        Email = row.Field<string>("Email") ?? string.Empty,
                        ContactNo1 = row.Field<string>("ContactNo1") ?? string.Empty,
                        ContactNo2 = row.Field<string>("ContactNo2") ?? string.Empty,
                        Address = row.Field<string>("Address") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        FYStartMonth = row.Field<string>("FYStartMonth") ?? string.Empty,
                        DefaultCountryId = row.Field<int>("DefaultCountryId"),
                        DefaultStateId = row.Field<int>("DefaultStateId"),
                        DefaultDistrictId = row.Field<int>("DefaultDistrictId"),
                        DefaultCityId = row.Field<int>("DefaultCityId"),
                        DefaultInsuranceCompanyId = row.Field<int>("DefaultInsuranceCompanyId"),
                        DefaultCorporateId = row.Field<int>("DefaultCorporateId")
                    }).ToList() ?? new List<BranchMasterModel>();

                    // Store ALL branches in cache (no expiration)
                    if (allBranches.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allBranches);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All BranchMaster data cached permanently. Key={cacheKey}, Count={allBranches.Count}");
                    }
                }

                // Filter in memory based on branchId parameter (always from cache)
                List<BranchMasterModel> filteredBranches;
                if (branchId.HasValue)
                {
                    _log.Info($"Filtering cached data by BranchId: {branchId.Value}");
                    filteredBranches = allBranches.Where(b => b.BranchId == branchId.Value).ToList();
                }
                else
                {
                    _log.Info("Returning all cached branches");
                    filteredBranches = allBranches;
                }

                if (!filteredBranches.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No branches found for BranchId: {branchId?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<BranchMasterModel>>.Failure(
                        alert.Type,
                        branchId.HasValue
                            ? $"Branch not found for BranchId: {branchId.Value}"
                            : "No branches found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredBranches.Count} branch(es) from cache");

                return ServiceResult<IEnumerable<BranchMasterModel>>.Success(
                    filteredBranches,
                    "Info",
                    $"{filteredBranches.Count} branch(es) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<BranchMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }



        public ServiceResult<int> CreateUpdateStateMaster(CreateUpdateStateMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateStateMaster called. StateId={request.StateId}, CountryId={request.CountryId}, StateName={request.StateName}");

                var dataTable = _sqlHelper.GetDataTable(
                    "IU_StateMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        StateId = request.StateId,
                        CountryId = request.CountryId,
                        StateName = request.StateName,
                        IsActive = request.IsActive,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error("No result returned from stored procedure");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                int result = Convert.ToInt32(dataTable.Rows[0]["Result"]);

                // Clear all state-related cache keys
                ClearStateMasterCache(request.CountryId);

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate state name: {request.StateName} for CountryId={request.CountryId}");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        $"State '{request.StateName}' already exists for this country",
                        409
                    );
                }

                if (result == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"StateId not found: {request.StateId}");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        "State record not found",
                        404
                    );
                }

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.StateId <= 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"State {(request.StateId <= 0 ? "created" : "updated")} successfully. StateId={result}. Cache cleared.");

                    return ServiceResult<int>.Success(
                        result,
                        alert.Type,
                        alert.Message,
                        request.StateId <= 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Operation failed with result: {result}");
                return ServiceResult<int>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<int>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<int> CreateUpdateDistrictMaster(CreateUpdateDistrictMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDistrictMaster called. DistrictId={request.DistrictId}, StateId={request.StateId}, DistrictName={request.DistrictName}");

                var dataTable = _sqlHelper.GetDataTable(
                    "IU_DistrictMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        DistrictId = request.DistrictId,
                        StateId = request.StateId,
                        CountryId = request.CountryId,
                        DistrictName = request.DistrictName,
                        IsActive = request.IsActive,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error("No result returned from stored procedure");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                int result = Convert.ToInt32(dataTable.Rows[0]["Result"]);

                // Clear all district-related cache keys
                ClearDistrictMasterCache(request.StateId);

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate district name: {request.DistrictName} for StateId={request.StateId}");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        $"District '{request.DistrictName}' already exists for this state",
                        409
                    );
                }

                if (result == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"DistrictId not found: {request.DistrictId}");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        "District record not found",
                        404
                    );
                }

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.DistrictId <= 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"District {(request.DistrictId <= 0 ? "created" : "updated")} successfully. DistrictId={result}. Cache cleared.");

                    return ServiceResult<int>.Success(
                        result,
                        alert.Type,
                        alert.Message,
                        request.DistrictId <= 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Operation failed with result: {result}");
                return ServiceResult<int>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<int>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<int> CreateUpdateCityMaster(CreateUpdateCityMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateCityMaster called. CityId={request.CityId}, DistrictId={request.DistrictId}, CityName={request.CityName}");

                var dataTable = _sqlHelper.GetDataTable(
                    "IU_CityMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        CityId = request.CityId,
                        DistrictId = request.DistrictId,
                        StateId = request.StateId,
                        CountryId = request.CountryId,
                        CityName = request.CityName,
                        IsActive = request.IsActive,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error("No result returned from stored procedure");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                int result = Convert.ToInt32(dataTable.Rows[0]["Result"]);

                // Clear all city-related cache keys
                ClearCityMasterCache(request.DistrictId);

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate city name: {request.CityName} for DistrictId={request.DistrictId}");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        $"City '{request.CityName}' already exists for this district",
                        409
                    );
                }

                if (result == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"CityId not found: {request.CityId}");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        "City record not found",
                        404
                    );
                }



                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.CityId <= 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"City {(request.CityId <= 0 ? "created" : "updated")} successfully. CityId={result}. Cache cleared.");

                    return ServiceResult<int>.Success(
                        result,
                        alert.Type,
                        alert.Message,
                        request.CityId <= 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Operation failed with result: {result}");
                return ServiceResult<int>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<int>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }



        public ServiceResult<int> CreateUpdatePincodeMaster(CreateUpdatePincodeMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdatePincodeMaster called. PincodeId={request.PincodeId}, CityId={request.CityId}, Pincode={request.Pincode}");

                var dataTable = _sqlHelper.GetDataTable(
                    "IU_PincodeMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        PincodeId = request.PincodeId,
                        CityId = request.CityId,
                        Pincode = request.Pincode,
                        IsActive = request.IsActive,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error("No result returned from stored procedure");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                int result = Convert.ToInt32(dataTable.Rows[0]["Result"]);

                // Clear all pincode-related cache keys
                ClearPincodeMasterCache(request.CityId);

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate pincode: {request.Pincode} for CityId={request.CityId}");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        $"Pincode '{request.Pincode}' already exists for this city",
                        409
                    );
                }

                if (result == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"PincodeId not found: {request.PincodeId}");
                    return ServiceResult<int>.Failure(
                        alert.Type,
                        "Pincode record not found",
                        404
                    );
                }

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.PincodeId <= 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Pincode {(request.PincodeId <= 0 ? "created" : "updated")} successfully. PincodeId={result}. Cache cleared.");

                    return ServiceResult<int>.Success(
                        result,
                        alert.Type,
                        alert.Message,
                        request.PincodeId <= 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Operation failed with result: {result}");
                return ServiceResult<int>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<int>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        // Helper method to clear cache
        private void ClearPincodeMasterCache(int cityId)
        {
            try
            {
                // Clear all possible cache keys for this city's pincodes
                _distributedCache.Remove($"_PincodeMaster_City{cityId}_All");
                _distributedCache.Remove($"_PincodeMaster_City{cityId}_1");
                _distributedCache.Remove($"_PincodeMaster_City{cityId}_0");
                _log.Info($"Cleared PincodeMaster cache for CityId={cityId}");
            }
            catch (Exception ex)
            {
                _log.Error($"Error clearing PincodeMaster cache: {ex.Message}");
            }
        }

        private void ClearStateMasterCache(int countryId)
        {
            try
            {
                // Clear all possible cache keys for this country's states
                _distributedCache.Remove($"_StateMaster_Country{countryId}_All");
                _distributedCache.Remove($"_StateMaster_Country{countryId}_1");
                _distributedCache.Remove($"_StateMaster_Country{countryId}_0");
                _log.Info($"Cleared StateMaster cache for CountryId={countryId}");
            }
            catch (Exception ex)
            {
                _log.Error($"Error clearing StateMaster cache: {ex.Message}");
            }
        }

        private void ClearDistrictMasterCache(int stateId)
        {
            try
            {
                // Clear all possible cache keys for this state's districts
                _distributedCache.Remove($"_DistrictMaster_State{stateId}_All");
                _distributedCache.Remove($"_DistrictMaster_State{stateId}_1");
                _distributedCache.Remove($"_DistrictMaster_State{stateId}_0");
                _log.Info($"Cleared DistrictMaster cache for StateId={stateId}");
            }
            catch (Exception ex)
            {
                _log.Error($"Error clearing DistrictMaster cache: {ex.Message}");
            }
        }

        private void ClearCityMasterCache(int districtId)
        {
            try
            {
                // Clear all possible cache keys for this district's cities
                _distributedCache.Remove($"_CityMaster_District{districtId}_All");
                _distributedCache.Remove($"_CityMaster_District{districtId}_1");
                _distributedCache.Remove($"_CityMaster_District{districtId}_0");
                _log.Info($"Cleared CityMaster cache for DistrictId={districtId}");
            }
            catch (Exception ex)
            {
                _log.Error($"Error clearing CityMaster cache: {ex.Message}");
            }
        }

        public ServiceResult<HeaderMasterResponse> CreateUpdateHeaderMaster(HeaderMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateHeaderMaster called. HeaderId={request.HeaderId}, RoleId={request.RoleId}, BranchId={request.BranchId}");

                var result = _sqlHelper.DML("IU_HeaderMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @headerId = request.HeaderId,
                    @roleId = request.RoleId,
                    @branchId = request.BranchId,
                    @type = request.Type,
                    @typeId = request.TypeId,
                    @isHeader = request.IsHeader,
                    @headerBody = request.HeaderBody ?? (object)DBNull.Value,
                    @isActive = request.IsActive,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                // Clear cache after successful operation
                string cacheKey = $"_HeaderMaster_{request.BranchId}_{request.RoleId}_{request.TypeId}_{request.HeaderId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared HeaderMaster cache for key: {cacheKey}");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    _log.Error($"HeaderMaster operation failed. Result={result}");
                    return ServiceResult<HeaderMasterResponse>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                var responseData = new HeaderMasterResponse { HeaderId = result };

                if (request.HeaderId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Header created successfully. HeaderId={result}");
                    return ServiceResult<HeaderMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        "Header saved successfully",
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Header updated successfully. HeaderId={result}");
                    return ServiceResult<HeaderMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        "Header updated successfully",
                        200
                    );
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<HeaderMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<HeaderMasterModel>> GetHeaderMaster(int branchId, int roleId, int typeId, int isHeader)
        {
            try
            {
                _log.Info($"GetHeaderMaster called. BranchId={branchId}, RoleId={roleId}, TypeId={typeId}, IsHeader={isHeader}");

                // Generate dynamic cache key based on parameters
                string cacheKey = $"_HeaderMaster_{branchId}_{roleId}_{typeId}_{isHeader}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<HeaderMasterModel> headers;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"HeaderMaster data retrieved from cache. Key={cacheKey}");
                    headers = System.Text.Json.JsonSerializer.Deserialize<List<HeaderMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"HeaderMaster cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetHeaderMaster",
                        CommandType.StoredProcedure,
                        new
                        {
                            @branchId = branchId,
                            @roleId = roleId,
                            @typeId = typeId,
                            @isHeader = isHeader
                        }
                    );

                    headers = dataTable?.AsEnumerable().Select(row => new HeaderMasterModel
                    {
                        HeaderId = row.Field<int>("HeaderId"),
                        HeaderBody = row.Field<string>("HeaderBody") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<HeaderMasterModel>();

                    // Store data in cache with no expiration
                    if (headers.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(headers);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"HeaderMaster data cached permanently. Key={cacheKey}, Count={headers.Count}");
                    }
                }

                if (!headers.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No headers found for BranchId={branchId}, RoleId={roleId}, TypeId={typeId}, IsHeader={isHeader}");
                    return ServiceResult<IEnumerable<HeaderMasterModel>>.Failure(
                        alert.Type,
                        "No headers found",
                        404
                    );
                }

                _log.Info($"Retrieved {headers.Count} header(s) from cache");

                return ServiceResult<IEnumerable<HeaderMasterModel>>.Success(
                    headers,
                    "Info",
                    $"{headers.Count} header(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<HeaderMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        // Repositories/Implementations/AdminRepository.cs
        // Add these methods to the existing AdminRepository class

        public ServiceResult<IEnumerable<SequenceTypeMasterModel>> GetSequenceTypeList()
        {
            try
            {
                _log.Info("GetSequenceTypeList called.");

                // Define cache key
                string cacheKey = "_SequenceTypeMaster_All";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<SequenceTypeMasterModel> sequenceTypes;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SequenceTypeMaster data retrieved from cache. Key={cacheKey}");
                    sequenceTypes = System.Text.Json.JsonSerializer.Deserialize<List<SequenceTypeMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"SequenceTypeMaster cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetSequenceTypeList",
                        CommandType.StoredProcedure
                    );

                    sequenceTypes = dataTable?.AsEnumerable().Select(row => new SequenceTypeMasterModel
                    {
                        TypeId = row.Field<int>("TypeId"),
                        TypeName = row.Field<string>("TypeName") ?? string.Empty
                    }).ToList() ?? new List<SequenceTypeMasterModel>();

                    // Store data in Redis cache (permanent until manually cleared)
                    if (sequenceTypes.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(sequenceTypes);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"SequenceTypeMaster data cached permanently. Key={cacheKey}, Count={sequenceTypes.Count}");
                    }
                }

                if (!sequenceTypes.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No sequence types found");

                    return ServiceResult<IEnumerable<SequenceTypeMasterModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {sequenceTypes.Count} sequence type(s) from cache");

                return ServiceResult<IEnumerable<SequenceTypeMasterModel>>.Success(
                    sequenceTypes,
                    "Info",
                    $"{sequenceTypes.Count} sequence type(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SequenceTypeMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CreateUpdateSequenceMasterResponse> CreateUpdateSequenceMaster(
     CreateUpdateSequenceMasterRequest request,
     AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSequenceMaster called. SequenceId={request.SequenceId}, Name={request.Name}");

                var result = _sqlHelper.ExecuteScalar(
                    "IU_SequenceMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        hospId = globalValues.hospId,
                        sequenceId = request.SequenceId,
                        name = request.Name,
                        typeId = request.TypeId,
                        typeName = request.TypeName,
                        prefix = request.Prefix ?? string.Empty,
                        firstSeprator = request.FirstSeprator ?? string.Empty,
                        fYFormatId = request.FYFormatId,
                        fYFormat = request.FYFormat ?? string.Empty,
                        secondSeprator = request.SecondSeprator ?? string.Empty,
                        length = request.Length,
                        preview = request.Preview,
                        userId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );

                int resultValue = Convert.ToInt32(result);

                // Clear cache for this sequence type
                string cacheKey = $"_SequenceMaster_Type{request.TypeId}";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared SequenceMaster cache for SequenceTypeId={request.TypeId}");
                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Sequence Name already exists: {request.Name}");
                    return ServiceResult<CreateUpdateSequenceMasterResponse>.Failure(
                        alert.Type,
                        "Sequence Name Already Exists",
                        409
                    );
                }

                if (resultValue == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Sequence Format already exists: {request.Preview}");
                    return ServiceResult<CreateUpdateSequenceMasterResponse>.Failure(
                        alert.Type,
                        "Sequence Format Already Exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new CreateUpdateSequenceMasterResponse { SequenceId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.SequenceId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Sequence Master {(request.SequenceId == 0 ? "created" : "updated")} successfully. SequenceId={resultValue}");

                    return ServiceResult<CreateUpdateSequenceMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.SequenceId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Sequence Master operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateSequenceMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSequenceMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
        public ServiceResult<IEnumerable<SequenceMasterModel>> GetSequenceMaster(int sequenceTypeId)
        {
            try
            {
                _log.Info($"GetSequenceMaster called. SequenceTypeId={sequenceTypeId}");

                // Validate sequenceTypeId
                if (sequenceTypeId <= 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn($"Invalid SequenceTypeId: {sequenceTypeId}");
                    return ServiceResult<IEnumerable<SequenceMasterModel>>.Failure(
                        alert.Type,
                        "SequenceTypeId must be greater than 0",
                        400
                    );
                }

                // Generate dynamic cache key based on sequenceTypeId
                string cacheKey = $"_SequenceMaster_Type{sequenceTypeId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<SequenceMasterModel> sequences;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SequenceMaster data retrieved from cache. Key={cacheKey}");
                    sequences = System.Text.Json.JsonSerializer.Deserialize<List<SequenceMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"SequenceMaster cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetSequenceMaster",
                        CommandType.StoredProcedure,
                        new { sequenceTypeId = sequenceTypeId }
                    );

                    sequences = dataTable?.AsEnumerable().Select(row => new SequenceMasterModel
                    {
                        SequenceId = row.Field<int>("SequenceId"),
                        Name = row.Field<string>("Name") ?? string.Empty,
                        TypeId = row.Field<int>("TypeId"),
                        TypeName = row.Field<string>("TypeName") ?? string.Empty,
                        Prefix = row.Field<string>("Prefix") ?? string.Empty,
                        FirstSeprator = row.Field<string>("FirstSeprator") ?? string.Empty,
                        FYFormatId = row.Field<int>("FYFormatId"),
                        FYFormat = row.Field<string>("FYFormat") ?? string.Empty,
                        SecondSeprator = row.Field<string>("SecondSeprator") ?? string.Empty,
                        Length = row.Field<int>("Length"),
                        Preview = row.Field<string>("Preview") ?? string.Empty
                    }).ToList() ?? new List<SequenceMasterModel>();

                    // Store data in cache (permanent until manually cleared)
                    if (sequences.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(sequences);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"SequenceMaster data cached permanently. Key={cacheKey}, Count={sequences.Count}");
                    }
                }

                if (!sequences.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No sequences found for SequenceTypeId={sequenceTypeId}");
                    return ServiceResult<IEnumerable<SequenceMasterModel>>.Failure(
                        alert.Type,
                        $"No sequences found for SequenceTypeId: {sequenceTypeId}",
                        404
                    );
                }

                _log.Info($"Retrieved {sequences.Count} sequence(s) from cache");

                return ServiceResult<IEnumerable<SequenceMasterModel>>.Success(
                    sequences,
                    "Info",
                    $"{sequences.Count} sequence(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SequenceMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }



        public ServiceResult<CreateUpdateBranchSequenceMappingResponse> CreateUpdateBranchSequenceMapping(
            CreateUpdateBranchSequenceMappingRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateBranchSequenceMapping called. MappingId={request.MappingId}, BranchId={request.BranchId}, RoleId={request.RoleId}, TypeId={request.TypeId}, SequenceId={request.SequenceId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "IU_BranchSequenceMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        mappingId = request.MappingId,
                        branchId = request.BranchId,
                        roleId = request.RoleId,
                        typeId = request.TypeId,
                        sequenceId = request.SequenceId,
                        userId = globalValues.userId,
                        ipAddress = globalValues.ipAddress
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error("No result returned from stored procedure");
                    return ServiceResult<CreateUpdateBranchSequenceMappingResponse>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                int result = Convert.ToInt32(dataTable.Rows[0]["Result"]);

                // Clear cache after successful operation
                string cacheKey = "_BranchSequenceMapping_All";
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared BranchSequenceMapping cache. Key={cacheKey}");

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Branch sequence mapping already exists for BranchId={request.BranchId}, RoleId={request.RoleId}, TypeId={request.TypeId}");
                    return ServiceResult<CreateUpdateBranchSequenceMappingResponse>.Failure(
                        alert.Type,
                        "Mapping already exists for this Branch, Role, and Type combination",
                        409
                    );
                }

                if (result > 0)
                {
                    var responseData = new CreateUpdateBranchSequenceMappingResponse { MappingId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.MappingId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Branch sequence mapping {(request.MappingId == 0 ? "created" : "updated")} successfully. MappingId={result}");

                    return ServiceResult<CreateUpdateBranchSequenceMappingResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.MappingId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Branch sequence mapping operation failed with result: {result}");
                return ServiceResult<CreateUpdateBranchSequenceMappingResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateBranchSequenceMappingResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<BranchSequenceMappingModel>> GetBranchSequenceMapping()
        {
            try
            {
                _log.Info("GetBranchSequenceMapping called.");

                // Define cache key
                string cacheKey = "_BranchSequenceMapping_All";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<BranchSequenceMappingModel> mappings;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"BranchSequenceMapping data retrieved from cache. Key={cacheKey}");
                    mappings = System.Text.Json.JsonSerializer.Deserialize<List<BranchSequenceMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"BranchSequenceMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetBranchSequenceMapping",
                        CommandType.StoredProcedure
                    );

                    mappings = dataTable?.AsEnumerable().Select(row => new BranchSequenceMappingModel
                    {
                        MappingId = row.Field<int>("MappingId"),
                        BranchId = row.Field<int>("BranchId"),
                        BranchName = row.Field<string>("BranchName") ?? string.Empty,
                        RoleId = row.Field<int>("RoleId"),
                        RoleName = row.Field<string>("RoleName") ?? string.Empty,
                        TypeId = row.Field<int>("TypeId"),
                        TypeName = row.Field<string>("TypeName") ?? string.Empty,
                        SequenceId = row.Field<int>("SequenceId"),
                        SequencePreview = row.Field<string>("SequencePreview") ?? string.Empty,
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                    }).ToList() ?? new List<BranchSequenceMappingModel>();

                    // Store data in Redis cache (permanent until manually cleared)
                    if (mappings.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(mappings);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"BranchSequenceMapping data cached permanently. Key={cacheKey}, Count={mappings.Count}");
                    }
                }

                if (!mappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No branch sequence mappings found");

                    return ServiceResult<IEnumerable<BranchSequenceMappingModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {mappings.Count} branch sequence mapping(s) from cache");

                return ServiceResult<IEnumerable<BranchSequenceMappingModel>>.Success(
                    mappings,
                    "Info",
                    $"{mappings.Count} branch sequence mapping(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<BranchSequenceMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<LabReportLetterHeadResponse> CreateUpdateLabReportLetterHead(
     LabReportLetterHeadRequest request,
     AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateLabReportLetterHead called. Id={request.Id}, BranchId={request.BranchId}, TypeId={request.TypeId}");

                string letterHeadFilePath = null;
                string signatureFilePath = null;

                var fileUploadHelper = new Utilities.FileUploadHelper(_configuration);

                // Handle letter head file upload if provided
                if (request.LetterHeadFile != null && request.LetterHeadFile.Length > 0)
                {
                    _log.Info($"Processing letter head file: {request.LetterHeadFile.FileName}, Size: {request.LetterHeadFile.Length} bytes");

                    // Upload file to DMS
                    var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                        request.LetterHeadFile,
                        "LetterHeadImages"
                    );

                    if (!uploadSuccess)
                    {
                        _log.Error($"Letter head file upload failed: {uploadError}");
                        var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                        return ServiceResult<LabReportLetterHeadResponse>.Failure(
                            alert.Type,
                            $"Letter head file upload failed: {uploadError}",
                            500
                        );
                    }

                    letterHeadFilePath = filePath;
                    _log.Info($"Letter head file uploaded successfully: {letterHeadFilePath}");
                }



                // Execute stored procedure
                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@Id", request.Id),
            new SqlParameter("@branchId", request.BranchId),
            new SqlParameter("@TypeId", request.TypeId),
            new SqlParameter("@TypeName", request.TypeName ?? (object)DBNull.Value),
            new SqlParameter("@paddingLeft", request.PaddingLeft),
            new SqlParameter("@paddingRight", request.PaddingRight),
            new SqlParameter("@paddingTop", request.PaddingTop),
            new SqlParameter("@paddingBottom", request.PaddingBottom),
            new SqlParameter("@letterHeadFilePath", letterHeadFilePath ?? (object)DBNull.Value),
            new SqlParameter("@userId", globalValues.userId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_LabReportLetterHeadMaster", parameters);

                // Clear cache for this branch and type
                string allCacheKey = "_LabReportLetterHead_All";
                _distributedCache.Remove(allCacheKey);
                _log.Info($"Cleared cache for keys: {allCacheKey}");

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Lab Report Letter Head already exists for BranchId={request.BranchId}, TypeId={request.TypeId}");
                    return ServiceResult<LabReportLetterHeadResponse>.Failure(
                        alert.Type,
                        "Letter Head configuration already exists for this branch and type",
                        409
                    );
                }

                if (result > 0)
                {
                    var responseData = new LabReportLetterHeadResponse
                    {
                        Id = (int)result,
                        LetterHeadFilePath = letterHeadFilePath
                    };

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.Id == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Lab Report Letter Head {(request.Id == 0 ? "created" : "updated")} successfully. Id={result}");

                    return ServiceResult<LabReportLetterHeadResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.Id == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Lab Report Letter Head operation failed. Result={result}");
                return ServiceResult<LabReportLetterHeadResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<LabReportLetterHeadResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<LabReportLetterHeadMaster>> GetLabReportLetterHeadList()
        {
            try
            {
                _log.Info("GetLabReportLetterHeadList called.");

                string cacheKey = "_LabReportLetterHead_All";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<LabReportLetterHeadMaster> letterHeads;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"LabReportLetterHead data retrieved from cache. Key={cacheKey}");
                    letterHeads = System.Text.Json.JsonSerializer.Deserialize<List<LabReportLetterHeadMaster>>(cachedData);
                }
                else
                {
                    _log.Info($"LabReportLetterHead cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getLabReportLetterHeadMasterList",
                        CommandType.StoredProcedure
                    );

                    letterHeads = dataTable?.AsEnumerable().Select(row => new LabReportLetterHeadMaster
                    {
                        Id = row.Field<int>("Id"),
                        BranchId = row.Field<int>("BranchId"),
                        BranchName = row.Field<string>("BranchName") ?? string.Empty,
                        TypeId = row.Field<int>("TypeId"),
                        TypeName = row.Field<string>("TypeName") ?? string.Empty,
                        PaddingLeft = row.Field<int>("PaddingLeft"),
                        PaddingRight = row.Field<int>("PaddingRight"),
                        PaddingTop = row.Field<int>("PaddingTop"),
                        PaddingBottom = row.Field<int>("PaddingBottom"),
                        LetterHeadFilePath = row.Field<string>("LetterHeadFilePath") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<LabReportLetterHeadMaster>();

                    // Store in cache permanently
                    if (letterHeads.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(letterHeads);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"LabReportLetterHead data cached permanently. Key={cacheKey}, Count={letterHeads.Count}");
                    }
                }

                if (!letterHeads.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No lab report letter heads found");
                    return ServiceResult<IEnumerable<LabReportLetterHeadMaster>>.Failure(
                        alert.Type,
                        "No letter head configurations found",
                        404
                    );
                }

                _log.Info($"Retrieved {letterHeads.Count} lab report letter head(s) from cache");

                return ServiceResult<IEnumerable<LabReportLetterHeadMaster>>.Success(
                    letterHeads,
                    "Info",
                    $"{letterHeads.Count} letter head(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<LabReportLetterHeadMaster>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> DeleteLetterHeadMaster(int id, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeleteLetterHeadMaster called. Id={id}");

                if (id <= 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("Invalid Id provided for letter head deletion.");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Id must be greater than 0",
                        400
                    );
                }

                var result = _sqlHelper.DML(
                    "D_DeleteLetterHeadMasterById",
                    CommandType.StoredProcedure,
                    new { @id = id }
                );

                // Clear cache for this branch and type
                string allCacheKey = "_LabReportLetterHead_All";
                _distributedCache.Remove(allCacheKey);

                _log.Info($"Cleared cache for LetterHeadMaster after deletion");

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info($"Letter head deleted successfully. Id={id}");
                    return ServiceResult<string>.Success(
                        "Letter head deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Letter head not found for Id={id}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Letter head not found or already deleted",
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


        public ServiceResult<DoctorSignatureMasterResponse> CreateUpdateDoctorSignatureMaster(
    DoctorSignatureMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDoctorSignatureMaster called. Id={request.Id}, BranchId={request.BranchId}, DoctorId={request.DoctorId}");

                string docSignFilePath = null;

                var fileUploadHelper = new Utilities.FileUploadHelper(_configuration);

                // Handle doctor signature file upload if provided
                if (request.DocSignFile != null && request.DocSignFile.Length > 0)
                {
                    _log.Info($"Processing doctor signature file: {request.DocSignFile.FileName}, Size: {request.DocSignFile.Length} bytes");

                    // Upload file to DMS
                    var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                        request.DocSignFile,
                        "DoctorSignatures"
                    );

                    if (!uploadSuccess)
                    {
                        _log.Error($"Doctor signature file upload failed: {uploadError}");
                        var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                        return ServiceResult<DoctorSignatureMasterResponse>.Failure(
                            alert.Type,
                            $"Doctor signature file upload failed: {uploadError}",
                            500
                        );
                    }

                    docSignFilePath = filePath;
                    _log.Info($"Doctor signature file uploaded successfully: {docSignFilePath}");
                }

                // Execute stored procedure
                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@Id", request.Id),
            new SqlParameter("@hospId", globalValues.hospId),
            new SqlParameter("@branchId", request.BranchId),
            new SqlParameter("@DoctorId", request.DoctorId),
            new SqlParameter("@XSign", request.XSign),
            new SqlParameter("@YSign", request.YSign),
            new SqlParameter("@DocSignPath", docSignFilePath ?? (object)DBNull.Value),
            new SqlParameter("@userId", globalValues.userId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_DoctorSignatureMaster", parameters);

                // Clear cache for all doctor signatures
                string allCacheKey = "_DoctorSignature_All";
                _distributedCache.Remove(allCacheKey);
                _log.Info($"Cleared cache for keys: {allCacheKey}");

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Doctor Signature already exists for BranchId={request.BranchId}, DoctorId={request.DoctorId}");
                    return ServiceResult<DoctorSignatureMasterResponse>.Failure(
                        alert.Type,
                        "Signature configuration already exists for this branch and doctor",
                        409
                    );
                }

                if (result > 0)
                {
                    var responseData = new DoctorSignatureMasterResponse
                    {
                        Id = (int)result,
                        DocSignPath = docSignFilePath
                    };

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.Id == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Doctor Signature {(request.Id == 0 ? "created" : "updated")} successfully. Id={result}");

                    return ServiceResult<DoctorSignatureMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.Id == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"Doctor Signature operation failed. Result={result}");
                return ServiceResult<DoctorSignatureMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<DoctorSignatureMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<DoctorSignatureMaster>> GetDoctorSignatureMasterList()
        {
            try
            {
                _log.Info("GetDoctorSignatureMasterList called.");

                string cacheKey = "_DoctorSignature_All";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DoctorSignatureMaster> doctorSignatures;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorSignature data retrieved from cache. Key={cacheKey}");
                    doctorSignatures = System.Text.Json.JsonSerializer.Deserialize<List<DoctorSignatureMaster>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorSignature cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getDoctorSignatureMasterList",
                        CommandType.StoredProcedure
                    );

                    doctorSignatures = dataTable?.AsEnumerable().Select(row => new DoctorSignatureMaster
                    {
                        Id = row.Field<int>("Id"),
                        BranchId = row.Field<int>("BranchId"),
                        BranchName = row.Field<string>("BranchName") ?? string.Empty,
                        DoctorId = row.Field<int>("DoctorId"),
                        DoctorName = row.Field<string>("DoctorName") ?? string.Empty,
                        XSign = row.Field<int>("XSign"),
                        YSign = row.Field<int>("YSign"),
                        DocSignPath = row.Field<string>("DocSignPath") ?? string.Empty
                    }).ToList() ?? new List<DoctorSignatureMaster>();

                    // Store in cache permanently
                    if (doctorSignatures.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(doctorSignatures);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"DoctorSignature data cached permanently. Key={cacheKey}, Count={doctorSignatures.Count}");
                    }
                }

                if (!doctorSignatures.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No doctor signatures found");
                    return ServiceResult<IEnumerable<DoctorSignatureMaster>>.Failure(
                        alert.Type,
                        "No signature configurations found",
                        404
                    );
                }

                _log.Info($"Retrieved {doctorSignatures.Count} doctor signature(s) from cache");

                return ServiceResult<IEnumerable<DoctorSignatureMaster>>.Success(
                    doctorSignatures,
                    "Info",
                    $"{doctorSignatures.Count} signature(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorSignatureMaster>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> DeleteDoctorSignatureMaster(int id, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeleteDoctorSignatureMaster called. Id={id}");

                if (id <= 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("Invalid Id provided for doctor signature deletion.");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Id must be greater than 0",
                        400
                    );
                }

                var result = _sqlHelper.DML(
                    "D_DeleteDoctorSignatureMasterById",
                    CommandType.StoredProcedure,
                    new { @id = id }
                );

                // Clear cache for all doctor signatures
                string allCacheKey = "_DoctorSignature_All";
                _distributedCache.Remove(allCacheKey);

                _log.Info($"Cleared cache for DoctorSignatureMaster after deletion");

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info($"Doctor signature deleted successfully. Id={id}");
                    return ServiceResult<string>.Success(
                        "Doctor signature deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Doctor signature not found for Id={id}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Doctor signature not found or already deleted",
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

        public ServiceResult<BankMasterResponse> CreateUpdateBankMaster(BankMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateBankMaster called. BankId={request.BankId}, BankName={request.BankName}");

                var result = _sqlHelper.DML("IU_BankMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @bankId = request.BankId,
                    @bankName = request.BankName,
                    @isActive = request.IsActive,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                // Clear cache after successful operation
                _distributedCache.Remove("_BankMaster_All");
                _log.Info("Cleared BankMaster cache");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate bank name: {request.BankName}");
                    return ServiceResult<BankMasterResponse>.Failure(
                        alert.Type,
                        "Bank Name Already Exists",
                        409
                    );
                }

                var responseData = new BankMasterResponse { BankId = result };

                if (request.BankId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Bank created successfully. BankId={result}");
                    return ServiceResult<BankMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Bank updated successfully. BankId={result}");
                    return ServiceResult<BankMasterResponse>.Success(
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
                return ServiceResult<BankMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<BankMasterModel>> GetBankList(int? bankId = null, int? isActive = null)
        {
            try
            {
                _log.Info($"GetBankList called. BankId={bankId?.ToString() ?? "All"}");

                string cacheKey = "_BankMaster_All";

                // Try to get all banks from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<BankMasterModel> allBanks;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"BankMaster data retrieved from cache. Key={cacheKey}");
                    allBanks = System.Text.Json.JsonSerializer.Deserialize<List<BankMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"BankMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL banks from database (NO parameters - SP returns everything)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetBankList",
                        CommandType.StoredProcedure
                    );

                    allBanks = dataTable?.AsEnumerable().Select(row => new BankMasterModel
                    {
                        BankId = row.Field<int>("BankId"),
                        BankName = row.Field<string>("BankName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn")
                    }).ToList() ?? new List<BankMasterModel>();

                    // Store ALL banks in cache (no expiration)
                    if (allBanks.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allBanks);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All BankMaster data cached permanently. Key={cacheKey}, Count={allBanks.Count}");
                    }
                }

                // Filter in memory based on bankId parameter (always from cache)
                List<BankMasterModel> filteredBanks = allBanks;
                if (bankId.HasValue)
                {
                    _log.Info($"Filtering cached data by BankId: {bankId.Value}");
                    filteredBanks = filteredBanks.Where(b => b.BankId == bankId.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredBanks = filteredBanks.Where(b => b.IsActive == isActive.Value).ToList();
                }


                if (!filteredBanks.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No banks found for BankId: {bankId?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<BankMasterModel>>.Failure(
                        alert.Type,
                        bankId.HasValue
                            ? $"Bank not found for BankId: {bankId.Value}"
                            : "No banks found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredBanks.Count} bank(s) from cache");

                return ServiceResult<IEnumerable<BankMasterModel>>.Success(
                    filteredBanks,
                    "Info",
                    $"{filteredBanks.Count} bank(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<BankMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<BankDetailMasterResponse> CreateUpdateBankDetailMaster(
    BankDetailMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateBankDetailMaster called. BankId={request.BankId}, BankName={request.BankName}");

                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@hospId", globalValues.hospId),
            new SqlParameter("@BankId", request.BankId),
            new SqlParameter("@PayeeName", request.PayeeName),
            new SqlParameter("@PANNumber", request.PANNumber),
            new SqlParameter("@BankName", request.BankName),
            new SqlParameter("@BankAccountNumber", request.BankAccountNumber),
            new SqlParameter("@BankAddress", request.BankAddress ?? (object)DBNull.Value),
            new SqlParameter("@IFSCCode", request.IFSCCode),
            new SqlParameter("@PINCode", request.PINCode ?? (object)DBNull.Value),
            new SqlParameter("@TINNumber", request.TINNumber ?? (object)DBNull.Value),
            new SqlParameter("@isActive", request.IsActive),
            new SqlParameter("@userId", globalValues.userId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_BankDetailMaster", parameters);

                // Clear cache after successful operation
                _distributedCache.Remove("_BankDetailMaster_All");
                _log.Info("Cleared BankDetailMaster cache");

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate bank name attempted: {request.BankName}");
                    return ServiceResult<BankDetailMasterResponse>.Failure(
                        alert.Type,
                        "Bank name already exists",
                        409
                    );
                }

                if (request.BankId == 0)
                {
                    var responseData = new BankDetailMasterResponse { BankId = (int)result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Bank detail created successfully. BankId={result}");
                    return ServiceResult<BankDetailMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var responseData = new BankDetailMasterResponse { BankId = (int)result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Bank detail updated successfully. BankId={result}");
                    return ServiceResult<BankDetailMasterResponse>.Success(
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
                return ServiceResult<BankDetailMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<BankDetailMasterModel>> GetBankDetailList(int? bankId = null, int? isActive = null)
        {
            try
            {
                _log.Info($"GetBankDetailList called. BankId={bankId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                // Single cache key for all bank details
                string cacheKey = "_BankDetailMaster_All";

                // Try to get all bank details from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<BankDetailMasterModel> allBankDetails;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"BankDetailMaster data retrieved from cache. Key={cacheKey}");
                    allBankDetails = System.Text.Json.JsonSerializer.Deserialize<List<BankDetailMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"BankDetailMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL bank details from database (NO parameters - SP returns everything)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetBankDetailList",
                        CommandType.StoredProcedure
                    );

                    allBankDetails = dataTable?.AsEnumerable().Select(row => new BankDetailMasterModel
                    {
                        Id = row.Field<int>("ID"),
                        PayeeName = row.Field<string>("PayeeName") ?? string.Empty,
                        PANNumber = row.Field<string>("PANNumber") ?? string.Empty,
                        BankName = row.Field<string>("BankName") ?? string.Empty,
                        BankAccountNumber = row.Field<string>("BankAccountNumber") ?? string.Empty,
                        BankAddress = row.Field<string>("BankAddress") ?? string.Empty,
                        IFSCCode = row.Field<string>("IFSCCode") ?? string.Empty,
                        PINCode = row.Field<string>("PINCode") ?? string.Empty,
                        TINNumber = row.Field<string>("TINNumber") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                    }).ToList() ?? new List<BankDetailMasterModel>();

                    // Store ALL bank details in cache (no expiration)
                    if (allBankDetails.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allBankDetails);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All BankDetailMaster data cached permanently. Key={cacheKey}, Count={allBankDetails.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<BankDetailMasterModel> filteredBankDetails = allBankDetails;

                // Filter by BankId if provided
                if (bankId.HasValue)
                {
                    _log.Info($"Filtering cached data by BankId: {bankId.Value}");
                    filteredBankDetails = filteredBankDetails.Where(b => b.Id == bankId.Value).ToList();
                }

                // Filter by IsActive if provided
                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredBankDetails = filteredBankDetails.Where(b => b.IsActive == isActive.Value).ToList();
                }

                if (!filteredBankDetails.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No bank details found for BankId={bankId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<BankDetailMasterModel>>.Failure(
                        alert.Type,
                        bankId.HasValue
                            ? $"Bank detail not found for BankId: {bankId.Value}"
                            : "No bank details found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredBankDetails.Count} bank detail(s) from cache");

                return ServiceResult<IEnumerable<BankDetailMasterModel>>.Success(
                    filteredBankDetails,
                    "Info",
                    $"{filteredBankDetails.Count} bank detail(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<BankDetailMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        #region MRD Room Master

        public ServiceResult<MRDRoomMasterResponse> CreateUpdateMRDRoomMaster(
            MRDRoomMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateMRDRoomMaster called. RoomId={request.RoomId}, Name={request.Name}");

                var result = _sqlHelper.DML("IU_MRDRoomMaster", CommandType.StoredProcedure, new
                {
                    @RoomId = request.RoomId,
                    @Name = request.Name,
                    @IsActive = request.IsActive,
                    @UserId = globalValues.userId,
                    @IPAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"MRD Room name already exists: {request.Name}");
                    return ServiceResult<MRDRoomMasterResponse>.Failure(
                        alert.Type,
                        "Room name already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new MRDRoomMasterResponse { RoomId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.RoomId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"MRD Room {(request.RoomId == 0 ? "created" : "updated")} successfully. RoomId={resultValue}");

                    return ServiceResult<MRDRoomMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.RoomId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"MRD Room operation failed with result: {resultValue}");
                return ServiceResult<MRDRoomMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<MRDRoomMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<MRDRoomMasterModel>> GetMRDRoomMaster(
            int? roomId = 0,
            int? activeFlag = 0)
        {
            try
            {
                _log.Info($"GetMRDRoomMaster called. RoomId={roomId?.ToString() ?? "All"}, ActiveFlag={activeFlag?.ToString() ?? "All"}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetMRDRoomMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @RoomId = roomId ?? 0,
                        @ActiveFlag = activeFlag ?? 0
                    }
                );

                var rooms = dataTable?.AsEnumerable().Select(row => new MRDRoomMasterModel
                {
                    RoomId = row.Field<int>("RoomId"),
                    Name = row.Field<string>("Name") ?? string.Empty,
                    IsActive = row.Field<int>("IsActive"),
                }).ToList() ?? new List<MRDRoomMasterModel>();

                if (!rooms.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No MRD rooms found");
                    return ServiceResult<IEnumerable<MRDRoomMasterModel>>.Failure(
                        alert.Type,
                        "No rooms found",
                        404
                    );
                }

                _log.Info($"Retrieved {rooms.Count} MRD room(s)");

                return ServiceResult<IEnumerable<MRDRoomMasterModel>>.Success(
                    rooms,
                    "Info",
                    $"{rooms.Count} room(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<MRDRoomMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        #endregion

        #region MRD Rack Master

        public ServiceResult<MRDRackMasterResponse> CreateUpdateMRDRackMaster(
            MRDRackMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateMRDRackMaster called. RackId={request.RackId}, RoomId={request.RoomId}, Name={request.Name}");

                var result = _sqlHelper.DML("IU_MRDRackMaster", CommandType.StoredProcedure, new
                {
                    @RoomId = request.RoomId,
                    @RackId = request.RackId,
                    @Name = request.Name,
                    @IsActive = request.IsActive,
                    @UserId = globalValues.userId,
                    @IPAddress = globalValues.ipAddress,
                    @AutoCreateShelfs = request.AutoCreateShelfs
                },
                new
                {
                    result = 0
                });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"MRD Rack name already exists: {request.Name} for RoomId={request.RoomId}");
                    return ServiceResult<MRDRackMasterResponse>.Failure(
                        alert.Type,
                        "Rack name already exists for this room",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new MRDRackMasterResponse { RackId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.RackId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"MRD Rack {(request.RackId == 0 ? "created" : "updated")} successfully. RackId={resultValue}");

                    return ServiceResult<MRDRackMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.RackId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"MRD Rack operation failed with result: {resultValue}");
                return ServiceResult<MRDRackMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<MRDRackMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<MRDRackMasterModel>> GetMRDRackMaster(
            int roomId,
            int? rackId = 0,
            int? activeFlag = 0)
        {
            try
            {
                _log.Info($"GetMRDRackMaster called. RoomId={roomId}, RackId={rackId?.ToString() ?? "All"}, ActiveFlag={activeFlag?.ToString() ?? "All"}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetMRDRackMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @RackId = rackId ?? 0,
                        @RoomId = roomId,
                        @ActiveFlag = activeFlag ?? 0
                    }
                );

                var racks = dataTable?.AsEnumerable().Select(row => new MRDRackMasterModel
                {
                    RackId = row.Field<int>("RackId"),
                    RoomId = row.Field<int>("RoomId"),
                    Name = row.Field<string>("Name") ?? string.Empty,
                    IsActive = row.Field<int>("IsActive"),
                }).ToList() ?? new List<MRDRackMasterModel>();

                if (!racks.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No MRD racks found for RoomId={roomId}");
                    return ServiceResult<IEnumerable<MRDRackMasterModel>>.Failure(
                        alert.Type,
                        "No racks found",
                        404
                    );
                }

                _log.Info($"Retrieved {racks.Count} MRD rack(s)");

                return ServiceResult<IEnumerable<MRDRackMasterModel>>.Success(
                    racks,
                    "Info",
                    $"{racks.Count} rack(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<MRDRackMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        #endregion

        #region MRD Shelf Master

        public ServiceResult<MRDShelfMasterResponse> CreateUpdateMRDShelfMaster(
            MRDShelfMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateMRDShelfMaster called. ShelfId={request.ShelfId}, RoomId={request.RoomId}, RackId={request.RackId}, Name={request.Name}");

                var result = _sqlHelper.DML("IU_MRDShelfmaster", CommandType.StoredProcedure, new
                {
                    @ShelfId = request.ShelfId,
                    @RoomId = request.RoomId,
                    @RackId = request.RackId,
                    @Name = request.Name,
                    @IsActive = request.IsActive,
                    @UserId = globalValues.userId,
                    @IPAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"MRD Shelf name already exists: {request.Name} for RackId={request.RackId}, RoomId={request.RoomId}");
                    return ServiceResult<MRDShelfMasterResponse>.Failure(
                        alert.Type,
                        "Shelf name already exists for this rack",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    var responseData = new MRDShelfMasterResponse { ShelfId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ShelfId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"MRD Shelf {(request.ShelfId == 0 ? "created" : "updated")} successfully. ShelfId={resultValue}");

                    return ServiceResult<MRDShelfMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.ShelfId == 0 ? 201 : 200
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"MRD Shelf operation failed with result: {resultValue}");
                return ServiceResult<MRDShelfMasterResponse>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<MRDShelfMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<MRDShelfMasterModel>> GetMRDShelfMaster(
            int roomId,
            int rackId,
            int? shelfId = 0,
            int? activeFlag = 0)
        {
            try
            {
                _log.Info($"GetMRDShelfMaster called. RoomId={roomId}, RackId={rackId}, ShelfId={shelfId?.ToString() ?? "All"}, ActiveFlag={activeFlag?.ToString() ?? "All"}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetMRDShelfmaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @ShelfId = shelfId ?? 0,
                        @RackId = rackId,
                        @RoomId = roomId,
                        @ActiveFlag = activeFlag ?? 0
                    }
                );

                var shelves = dataTable?.AsEnumerable().Select(row => new MRDShelfMasterModel
                {
                    ShelfId = row.Field<int>("ShelfId"),
                    RoomId = row.Field<int>("RoomId"),
                    RackId = row.Field<int>("RackId"),
                    Name = row.Field<string>("Name") ?? string.Empty,
                    IsActive = row.Field<int>("IsActive"),
                }).ToList() ?? new List<MRDShelfMasterModel>();

                if (!shelves.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No MRD shelves found for RoomId={roomId}, RackId={rackId}");
                    return ServiceResult<IEnumerable<MRDShelfMasterModel>>.Failure(
                        alert.Type,
                        "No shelves found",
                        404
                    );
                }

                _log.Info($"Retrieved {shelves.Count} MRD shelf/shelves");

                return ServiceResult<IEnumerable<MRDShelfMasterModel>>.Success(
                    shelves,
                    "Info",
                    $"{shelves.Count} shelf/shelves retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<MRDShelfMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        #endregion


        public ServiceResult<PatientDocumentMasterResponse> CreateUpdatePatientDocumentMaster(
    PatientDocumentMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdatePatientDocumentMaster called. DocumentId={request.DocumentId}, DocumentName={request.DocumentName}");

                var result = _sqlHelper.DML("IU_PatientDocumentMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @documentId = request.DocumentId,
                    @documentName = request.DocumentName,
                    @documentCode = request.DocumentCode,
                    @isActive = request.IsActive,
                    @documentCategory = request.DocumentCategory,
                    @documentCategoryId = request.DocumentCategoryId,
                    @isMandatory = request.IsMandatory,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                // Clear the single cache key after successful operation
                _distributedCache.Remove("_PatientDocumentMaster_All");
                GlobalFunctions.ClearCacheByPattern(_configuration, "_PatientDocumentMapping_*");
                GlobalFunctions.ClearCacheByPattern(_configuration, "_VisitWisePatientDocumentMapping_*");

                _log.Info("Cleared PatientDocumentMaster cache after create/update operation");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate document name attempted: {request.DocumentName}");
                    return ServiceResult<PatientDocumentMasterResponse>.Failure(
                        alert.Type,
                        "Document Name already exists",
                        409
                    );
                }

                var responseData = new PatientDocumentMasterResponse { DocumentId = result };

                if (request.DocumentId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Patient Document created successfully. DocumentId={result}");
                    return ServiceResult<PatientDocumentMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        "Document saved successfully",
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Patient Document updated successfully. DocumentId={result}");
                    return ServiceResult<PatientDocumentMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        "Document updated successfully",
                        200
                    );
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<PatientDocumentMasterResponse>.Failure(
                alert.Type,
                alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<PatientDocumentMasterModel>> GetPatientDocumentMaster(int? isActive = null)
        {
            try
            {
                _log.Info($"GetPatientDocumentMaster called. IsActive={isActive?.ToString() ?? "All"}");

                // Always use the same cache key for ALL documents
                string cacheKey = "_PatientDocumentMaster_All";

                // Try to get ALL documents from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<PatientDocumentMasterModel> allDocuments;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PatientDocumentMaster data retrieved from cache. Key={cacheKey}");
                    allDocuments = System.Text.Json.JsonSerializer.Deserialize<List<PatientDocumentMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"PatientDocumentMaster cache miss. Fetching ALL data from database. Key={cacheKey}");

                    // Fetch ALL documents from database (NO parameters - SP returns everything)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_PatientDocumentMaster",
                        CommandType.StoredProcedure
                    // No parameters - SP returns all documents
                    );

                    allDocuments = dataTable?.AsEnumerable().Select(row => new PatientDocumentMasterModel
                    {
                        DocumentId = row.Field<int>("DocumentId"),
                        DocumentName = row.Field<string>("DocumentName") ?? string.Empty,
                        DocumentCode = row.Field<string>("DocumentCode") ?? string.Empty,
                        DocumentCategory = row.Field<string>("DocumentCategory") ?? string.Empty,
                        DocumentCategoryId = row.Field<int>("DocumentCategoryId"),
                        IsActive = row.Field<int>("IsActive"),
                        IsMandatory = row.Field<int>("IsMandatory"),
                        CreatedBy = row.Field<string>("CreatedBy") ?? string.Empty,
                        CreatedOn = row.Field<string>("CreatedOn") ?? string.Empty,
                        LastModifiedBy = row.Field<string>("LastModifiedBy") ?? string.Empty,
                        LastModifiedOn = row.Field<string>("LastModifiedOn") ?? string.Empty
                    }).ToList() ?? new List<PatientDocumentMasterModel>();

                    // Store ALL documents in cache (no expiration - permanent until manually cleared)
                    if (allDocuments.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allDocuments);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All PatientDocumentMaster data cached permanently. Key={cacheKey}, Count={allDocuments.Count}");
                    }
                }

                // Filter in memory based on isActive parameter (always from cache)
                List<PatientDocumentMasterModel> filteredDocuments;
                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredDocuments = allDocuments.Where(d => d.IsActive == isActive.Value).ToList();
                }
                else
                {
                    _log.Info("Returning all cached documents");
                    filteredDocuments = allDocuments;
                }

                if (!filteredDocuments.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No patient documents found for IsActive: {isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<PatientDocumentMasterModel>>.Failure(
                        alert.Type,
                        isActive.HasValue
                            ? $"No patient documents found for IsActive: {isActive.Value}"
                            : "No patient documents found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredDocuments.Count} patient document(s) from cache");

                return ServiceResult<IEnumerable<PatientDocumentMasterModel>>.Success(
                    filteredDocuments,
                    "Info",
                    $"{filteredDocuments.Count} patient document(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<PatientDocumentMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        private const string OutSourceLabMasterCacheKey = "_OutSourceLabMaster_All";

        public ServiceResult<IEnumerable<OutSourceLabMasterModel>>
            GetOutSourceLabMasterList(int? isActive = null)
        {
            try
            {
                _log.Info($"GetOutSourceLabMasterList called. isActive={isActive?.ToString() ?? "null (all)"}");

                List<OutSourceLabMasterModel> allRecords;
                var cachedData = _distributedCache.GetString(OutSourceLabMasterCacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"Cache hit. Key={OutSourceLabMasterCacheKey}");
                    allRecords = JsonSerializer.Deserialize<List<OutSourceLabMasterModel>>(cachedData)
                                 ?? new List<OutSourceLabMasterModel>();
                }
                else
                {
                    _log.Info($"Cache miss. Fetching from DB. Key={OutSourceLabMasterCacheKey}");

                    var dt = _sqlHelper.GetDataTable(
                        "S_getOutSourceLabMasterList",
                        CommandType.StoredProcedure);

                    allRecords = dt?.AsEnumerable().Select(row =>
                        new OutSourceLabMasterModel
                        {
                            OutSourceLabId = row.Field<int>("OutSourceLabId"),
                            OutSourceLab = row.Field<string>("OutSourceLab") ?? string.Empty,
                            BranchId = row.Field<int>("BranchId"),
                            BranchName = row.Field<string>("BranchName") ?? string.Empty,
                            ContactPerson = row.Field<string>("ContactPerson") ?? string.Empty,
                            ContactNumber = row.Field<string>("ContactNumber") ?? string.Empty,
                            Address = row.Field<string>("Address") ?? string.Empty,
                            IsActive = row.Field<int>("IsActive")
                        }).ToList()
                    ?? new List<OutSourceLabMasterModel>();

                    if (allRecords.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allRecords);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(OutSourceLabMasterCacheKey, serialized, cacheOptions);
                        _log.Info($"Cached {allRecords.Count} record(s). Key={OutSourceLabMasterCacheKey}");
                    }
                }

                // Filter in-memory — null means return all
                var filtered = isActive.HasValue
                    ? allRecords.Where(x => x.IsActive == isActive.Value).ToList()
                    : allRecords;

                if (!filtered.Any())
                {
                    var notFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No OutSourceLab records matched the filter.");
                    return ServiceResult<IEnumerable<OutSourceLabMasterModel>>.Failure(
                        notFound.Type, notFound.Message, 404);
                }

                _log.Info($"Returning {filtered.Count()} record(s).");
                return ServiceResult<IEnumerable<OutSourceLabMasterModel>>.Success(
                    filtered, "Info", $"{filtered.Count()} record(s) retrieved successfully.", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<OutSourceLabMasterModel>>.Failure(
                    alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<SaveOutSourceLabMasterResponse> SaveOutSourceLabMaster(
     SaveOutSourceLabMasterRequest request,
     AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveOutSourceLabMaster called. OutSourceLabId={request.OutSourceLabId}, " +
                          $"OutSourceLab={request.OutSourceLab}");

                var result = _sqlHelper.DML(
                    "IU_OutSourceLabMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @hospId = globalValues.hospId,
                        @branchId = request.branchId,
                        @outsourcelabId = request.OutSourceLabId,
                        @outsourcelab = request.OutSourceLab,
                        @contactperson = request.ContactPerson,
                        @contactnumber = request.ContactNumber,
                        @address = request.Address,
                        @IsActive = request.IsActive,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }  // output param seed
                );

                // SP returns -1 when OutSourceLab name already exists for this branch
                if (result == -1)
                {
                    _log.Warn($"Duplicate OutSourceLab name='{request.OutSourceLab}' " +
                              $"for BranchId={request.branchId}");
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    return ServiceResult<SaveOutSourceLabMasterResponse>.Failure(
                        dupAlert.Type, dupAlert.Message, 409);
                }

                if (result <= 0)
                {
                    _log.Warn($"SaveOutSourceLabMaster unexpected result={result}");
                    var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<SaveOutSourceLabMasterResponse>.Failure(
                        failAlert.Type, failAlert.Message, 400);
                }

                // Invalidate cache so next GET fetches fresh data
                _distributedCache.Remove(OutSourceLabMasterCacheKey);
                _log.Info($"Cache invalidated. Key={OutSourceLabMasterCacheKey}");

                bool isUpdate = request.OutSourceLabId > 0;
                var alertCode = isUpdate ? "DATA_UPDATED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY";
                var okAlert = _messageService.GetMessageAndTypeByAlertCode(alertCode);

                _log.Info($"OutSourceLab {(isUpdate ? "updated" : "added")} successfully. Id={result}");

                return ServiceResult<SaveOutSourceLabMasterResponse>.Success(
                    new SaveOutSourceLabMasterResponse { OutSourceLabId = result },
                    okAlert.Type, okAlert.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveOutSourceLabMasterResponse>.Failure(
                    alert.Type, alert.Message, 500);
            }
        }

        private const string RateListMasterCacheKey = "_RateListMaster_All";
        public ServiceResult<IEnumerable<RateListMasterModel>> GetRateListMaster(string? rateListName, int? isActive)
        {
            try
            {
                _log.Info($"GetRateListMaster called. RateListName={rateListName ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                // ── 1. Try cache ──────────────────────────────────────────────
                var cachedData = _distributedCache.GetString(RateListMasterCacheKey);
                List<RateListMasterModel> allRateLists;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"RateListMaster data retrieved from cache. Key={RateListMasterCacheKey}");
                    allRateLists = JsonSerializer.Deserialize<List<RateListMasterModel>>(cachedData)
                                   ?? new List<RateListMasterModel>();
                }
                else
                {
                    // ── 2. Cache miss — fetch ALL records from DB ─────────────
                    _log.Info($"RateListMaster cache miss. Fetching ALL records from database. Key={RateListMasterCacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetRateListMaster",
                        CommandType.StoredProcedure
                    );

                    allRateLists = dataTable?.AsEnumerable().Select(row => new RateListMasterModel
                    {
                        RateListId = row.Field<int>("RateListId"),
                        RateListName = row.Field<string>("RateListName") ?? string.Empty,
                        ApplicableDate = row.Field<string>("ApplicableDate") ?? string.Empty,
                        ExpiryDate = row.Field<string>("ExpiryDate") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<RateListMasterModel>();

                    // Store ALL records under single key (no expiration — cleared on save/update)
                    if (allRateLists.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allRateLists);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(RateListMasterCacheKey, serialized, cacheOptions);
                        _log.Info($"RateListMaster ALL data cached permanently. Key={RateListMasterCacheKey}, Count={allRateLists.Count}");
                    }
                }

                // ── 3. Filter in memory (always from the single cached list) ──
                List<RateListMasterModel> filteredList = allRateLists;

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredList = filteredList.Where(r => r.IsActive == isActive.Value).ToList();
                }

                if (!string.IsNullOrWhiteSpace(rateListName))
                {
                    _log.Info($"Filtering cached data by RateListName: {rateListName}");
                    filteredList = filteredList
                        .Where(r => r.RateListName.Contains(rateListName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (!filteredList.Any())
                {
                    var notFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No RateListMaster records found for the given filters.");
                    return ServiceResult<IEnumerable<RateListMasterModel>>.Failure(
                        notFound.Type,
                        notFound.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {filteredList.Count} RateListMaster record(s).");
                return ServiceResult<IEnumerable<RateListMasterModel>>.Success(
                    filteredList,
                    "Info",
                    $"{filteredList.Count} record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<RateListMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

      
        public ServiceResult<string> CreateUpdateRateListMaster(
            CreateUpdateRateListMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateRateListMaster called. RateListId={request.RateListId}, RateListName={request.RateListName}");

                // Parse expiry date: client sends dd-MM-yyyy, SP expects datetime
                if (!DateTime.TryParseExact(
                        request.ExpiryDate, "dd-MM-yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out DateTime expiryDate))
                {
                    var validationAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                    return ServiceResult<string>.Failure(
                        validationAlert.Type,
                        "Invalid ExpiryDate format. Expected dd-MM-yyyy.",
                        400
                    );
                }

                if (!DateTime.TryParseExact(
                      request.ApplicableDate, "dd-MM-yyyy",
                      System.Globalization.CultureInfo.InvariantCulture,
                      System.Globalization.DateTimeStyles.None,
                      out DateTime ApplicableDate))
                {
                    var validationAlert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                    return ServiceResult<string>.Failure(
                        validationAlert.Type,
                        "Invalid ApplicableDate format. Expected dd-MM-yyyy.",
                        400
                    );
                }

                long result = _sqlHelper.RunProcedureInsert(
                    "IU_RateListMaster",
                    new IDataParameter[]
                    {
                      new SqlParameter("@HospId",          globalValues.hospId),
                      new SqlParameter("@RateListId",      request.RateListId),
                      new SqlParameter("@RateListName",    request.RateListName),
                      new SqlParameter("@ApplicableDate",  ApplicableDate.ToString("yyyy-MM-dd")),
                      new SqlParameter("@ExpiryDate",      expiryDate.ToString("yyyy-MM-dd")),
                      new SqlParameter("@IsActive",        request.IsActive),
                      new SqlParameter("@UserId",          globalValues.userId),
                      new SqlParameter("@IpAddress",       string.IsNullOrEmpty(globalValues.ipAddress)
                                                               ? (object)DBNull.Value
                                                               : globalValues.ipAddress),
                      new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    }
                );

                // SP returns -1 when name already exists
                if (result < 0)
                {
                    _log.Warn($"Duplicate RateListName detected: {request.RateListName}");
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("DUPLICATE_ENTRY");
                    return ServiceResult<string>.Failure(
                        dupAlert.Type,
                        "Rate List Name Already Exists.",
                        409
                    );
                }


                if(result>0 && request.RateListId==0 && request.ImportFromRateListId > 0)
                {
                         _sqlHelper.DML(
                         "I_ImportRateListByRateListId",
                         CommandType.StoredProcedure,
                         new
                         {
                             @rateListId = result,
                             @ImportFromRateListId= request.ImportFromRateListId,
                             @userId = globalValues.userId,
                             @IpAddress = globalValues.ipAddress
                         }
                     );

                }

                // Invalidate the single cache key so next GET re-fetches fresh data from DB
                _distributedCache.Remove(RateListMasterCacheKey);
                _log.Info($"RateListMaster cache cleared. Key={RateListMasterCacheKey}");

                bool isInsert = request.RateListId == 0;
                string successMsg = isInsert ? "Rate List Saved Successfully" : "Rate List Updated Successfully";
                _log.Info($"{successMsg}. RateListId={result}");

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    result.ToString(),
                    successAlert.Type,
                    successMsg,
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


        public ServiceResult<IEnumerable<object>> GetTariffMaster(
    string rateListId, string patientType, string bedTypeId,
    string doctorId, string categoryId, string subCategoryId,
    string subSubCategoryId, string serviceItemId, string serviceName)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetTariffMaster", CommandType.StoredProcedure, new
                {
                    @rateListId = rateListId,
                    @patientType = patientType,
                    @bedTypeId = bedTypeId,
                    @doctorId = doctorId,
                    @categoryId = categoryId,
                    @subCategoryId = subCategoryId,
                    @subSubCategoryId = subSubCategoryId,
                    @serviceItemId = serviceItemId,
                    @serviceName = serviceName
                });

                if (dt == null || dt.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("NO_RECORD_FOUND");
                    return ServiceResult<IEnumerable<object>>.Failure(alert.Type, "No tariff records found.", 404);
                }

                var sorted = dt.AsEnumerable()
                    .OrderBy(r => r.Field<string>("SubCategory"))
                    .ThenBy(r => r.Field<string>("SubSubCategory"))
                    .ThenBy(r => r.Field<string>("ServiceItemName"))
                    .Select(r => dt.Columns.Cast<DataColumn>()
                        .ToDictionary(c => c.ColumnName, c => r[c] == DBNull.Value ? null : r[c]))
                    .ToList<object>();

                return ServiceResult<IEnumerable<object>>.Success(
                    sorted, "Info", $"{sorted.Count} tariff record(s) fetched successfully.", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.GetTariffMaster");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR");
                return ServiceResult<IEnumerable<object>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> CreateUpdateTariffMaster(
            CreateUpdateTariffMasterRequest request, AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con);
            try
            {
                foreach (var item in request.TariffMasterData)
                {
                    _sqlHelper.DML(tnx, "IU_TariffMaster", CommandType.StoredProcedure, new
                    {
                        @isCopyRateforIPD = request.IsCopyRateForIPD,
                        @hospId = globalValues.hospId,
                        @tariffId = item.TariffId,
                        @rateListId = item.RateListId,
                        @serviceItemId = item.ServiceItemId,
                        @bedTypeId = item.BedTypeId,
                        @alias = item.Alias ?? string.Empty,
                        @serviceCode = item.ServiceCode ?? string.Empty,
                        @doctorId = item.DoctorId,
                        @validityDays = item.ValidityDays,
                        @emergencyCharges = item.EmergencyCharges,
                        @rate = item.Rate,
                        @isRateEditable = item.IsRateEditable,
                        @isActive = item.IsActive,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });
                }

                tnx.Commit();
                return ServiceResult<string>.Success(
                    null, "Info", "Tariff Master Saved Successfully.", 200);
            }
            catch (Exception ex)
            {
                tnx.Rollback();
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.CreateUpdateTariffMaster");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx.Dispose();
                con.Close();
                con.Dispose();
            }
        }

        public ServiceResult<InsuranceCompanyMasterResponse> CreateUpdateInsuranceCompanyMaster(
    InsuranceCompanyMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateInsuranceCompanyMaster called. InsuranceCompanyId={request.InsuranceCompanyId}, InsuranceCompanyName={request.InsuranceCompanyName}");

                var result = _sqlHelper.DML("IU_InsuranceCompanyMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @insuranceCompanyId = request.InsuranceCompanyId,
                    @insuranceCompanyName = request.InsuranceCompanyName,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new { result = 0 });

                // Clear cache after successful operation
                _distributedCache.Remove("WEB_InsuranceCompanyMaster_All");
                _log.Info("Cleared InsuranceCompanyMaster cache");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate insurance company name: {request.InsuranceCompanyName}");
                    return ServiceResult<InsuranceCompanyMasterResponse>.Failure(
                        alert.Type,
                        "Insurance Company Name Already Exists",
                        409
                    );
                }

                var responseData = new InsuranceCompanyMasterResponse { InsuranceCompanyId = result };

                if (request.InsuranceCompanyId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Insurance Company created successfully. InsuranceCompanyId={result}");
                    return ServiceResult<InsuranceCompanyMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Insurance Company updated successfully. InsuranceCompanyId={result}");
                    return ServiceResult<InsuranceCompanyMasterResponse>.Success(
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
                return ServiceResult<InsuranceCompanyMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<InsuranceCompanyMasterModel>> GetInsuranceCompanyMasterList()
        {
            try
            {
                _log.Info("GetInsuranceCompanyMasterList called.");

                string cacheKey = "WEB_InsuranceCompanyMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<InsuranceCompanyMasterModel> allInsuranceCompanies;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"InsuranceCompanyMaster data retrieved from cache. Key={cacheKey}");
                    allInsuranceCompanies = System.Text.Json.JsonSerializer.Deserialize<List<InsuranceCompanyMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"InsuranceCompanyMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetInsuranceCompanyMaster",
                        CommandType.StoredProcedure
                    );

                    allInsuranceCompanies = dataTable?.AsEnumerable().Select(row => new InsuranceCompanyMasterModel
                    {
                        InsuranceCompanyId = row.Field<int>("InsuranceCompanyId"),
                        InsuranceCompanyName = row.Field<string>("InsuranceCompanyName") ?? string.Empty
                    }).ToList() ?? new List<InsuranceCompanyMasterModel>();

                    if (allInsuranceCompanies.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allInsuranceCompanies);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All InsuranceCompanyMaster data cached permanently. Key={cacheKey}, Count={allInsuranceCompanies.Count}");
                    }
                }

                if (!allInsuranceCompanies.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No insurance companies found");
                    return ServiceResult<IEnumerable<InsuranceCompanyMasterModel>>.Failure(
                        alert.Type,
                        "No insurance companies found",
                        404
                    );
                }

                _log.Info($"Retrieved {allInsuranceCompanies.Count} insurance company/companies from cache");

                return ServiceResult<IEnumerable<InsuranceCompanyMasterModel>>.Success(
                    allInsuranceCompanies,
                    "Info",
                    $"{allInsuranceCompanies.Count} insurance company/companies retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<InsuranceCompanyMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CorporateTypeMasterResponse> CreateUpdateCorporateTypeMaster(
    CorporateTypeMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateCorporateTypeMaster called. CorporateTypeId={request.CorporateTypeId}, CorporateTypeName={request.CorporateTypeName}");

                var result = _sqlHelper.DML("IU_CorporateTypeMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @corporateTypeId = request.CorporateTypeId,
                    @corporateTypeName = request.CorporateTypeName,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new { result = 0 });

                // Clear cache after successful operation
                _distributedCache.Remove("_CorporateTypeMaster_All");
                _log.Info("Cleared CorporateTypeMaster cache");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate corporate type name: {request.CorporateTypeName}");
                    return ServiceResult<CorporateTypeMasterResponse>.Failure(
                        alert.Type,
                        "Corporate Type Name Already Exists",
                        409
                    );
                }

                var responseData = new CorporateTypeMasterResponse { CorporateTypeId = result };

                if (request.CorporateTypeId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Corporate Type created successfully. CorporateTypeId={result}");
                    return ServiceResult<CorporateTypeMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Corporate Type updated successfully. CorporateTypeId={result}");
                    return ServiceResult<CorporateTypeMasterResponse>.Success(
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
                return ServiceResult<CorporateTypeMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<CorporateTypeMasterModel>> GetCorporateTypeMasterList()
        {
            try
            {
                _log.Info("GetCorporateTypeMasterList called.");

                string cacheKey = "_CorporateTypeMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<CorporateTypeMasterModel> allCorporateTypes;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"CorporateTypeMaster data retrieved from cache. Key={cacheKey}");
                    allCorporateTypes = System.Text.Json.JsonSerializer.Deserialize<List<CorporateTypeMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"CorporateTypeMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetCorporateTypeMaster",
                        CommandType.StoredProcedure
                    );

                    allCorporateTypes = dataTable?.AsEnumerable().Select(row => new CorporateTypeMasterModel
                    {
                        CorporateTypeId = row.Field<int>("CorporateTypeId"),
                        CorporateTypeName = row.Field<string>("CorporateTypeName") ?? string.Empty
                    }).ToList() ?? new List<CorporateTypeMasterModel>();

                    if (allCorporateTypes.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allCorporateTypes);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All CorporateTypeMaster data cached permanently. Key={cacheKey}, Count={allCorporateTypes.Count}");
                    }
                }

                if (!allCorporateTypes.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No corporate types found");
                    return ServiceResult<IEnumerable<CorporateTypeMasterModel>>.Failure(
                        alert.Type,
                        "No corporate types found",
                        404
                    );
                }

                _log.Info($"Retrieved {allCorporateTypes.Count} corporate type(s) from cache");

                return ServiceResult<IEnumerable<CorporateTypeMasterModel>>.Success(
                    allCorporateTypes,
                    "Info",
                    $"{allCorporateTypes.Count} corporate type(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<CorporateTypeMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CorporateMasterResponse> CreateUpdateCorporateMaster(
        CorporateMasterRequest request,
        AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateCorporateMaster called. CorporateId={request.CorporateId}, CorporateName={request.CorporateName}");

                // Parse dates - input format is dd-MM-yyyy
                if (!DateTime.TryParseExact(request.ContractStartFrom,
                    new[] { "dd-MM-yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime contractStartFrom))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn($"Invalid ContractStartFrom date format: {request.ContractStartFrom}");
                    return ServiceResult<CorporateMasterResponse>.Failure(
                        alertDate.Type,
                        "Invalid ContractStartFrom date format. Use dd-MM-yyyy (e.g. 20-04-2026)",
                        400
                    );
                }

                if (!DateTime.TryParseExact(request.ContractExpiresOn,
                    new[] { "dd-MM-yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime contractExpiresOn))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn($"Invalid ContractExpiresOn date format: {request.ContractExpiresOn}");
                    return ServiceResult<CorporateMasterResponse>.Failure(
                        alertDate.Type,
                        "Invalid ContractExpiresOn date format. Use dd-MM-yyyy (e.g. 31-12-2028)",
                        400
                    );
                }

                var result = _sqlHelper.DML("IU_CorporateMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @corporateId = request.CorporateId,
                    @corporateName = request.CorporateName,
                    @insuranceCompanyName = request.InsuranceCompanyName ?? string.Empty,
                    @insuranceCompanyId = request.InsuranceCompanyId,
                    @corporateTypeName = request.CorporateTypeName ?? string.Empty,
                    @corporateTypeId = request.CorporateTypeId,
                    @paymentTypeId = request.PaymentTypeId,
                    @corporateCode = request.CorporateCode ?? string.Empty,
                    @corporateContact1 = request.CorporateContact1 ?? string.Empty,
                    @corporateContact2 = request.CorporateContact2 ?? string.Empty,
                    @corporateEmail = request.CorporateEmail ?? string.Empty,
                    @corporateAddress1 = request.CorporateAddress1 ?? string.Empty,
                    @corporateAddress2 = request.CorporateAddress2 ?? string.Empty,
                    @isActive = request.IsActive,
                    @contractStartFrom = contractStartFrom,
                    @contractExpiresOn = contractExpiresOn,
                    @copaymentPer = request.CopaymentPer,
                    @discountPerOut = request.DiscountPerOut,
                    @discountPerIn = request.DiscountPerIn,
                    @hikePerOut = request.HikePerOut,
                    @hikePerIn = request.HikePerIn,
                    @activePaymentModes = request.ActivePaymentModes ?? string.Empty,
                    @isRegistrationChargeApplicable = request.IsRegistrationChargeApplicable,
                    @isCaseBillingApplicable = request.IsCaseBillingApplicable,

                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                // Clear cache after successful operation
                _distributedCache.Remove("_CorporateMaster_All");
                _distributedCache.Remove("_Corporate_All");
                _distributedCache.Remove("_BranchWiseCorporate_All");
                _log.Info("Cleared CorporateMaster and Corporate cache");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate corporate name: {request.CorporateName} for InsuranceCompanyId={request.InsuranceCompanyId}");
                    return ServiceResult<CorporateMasterResponse>.Failure(
                        alert.Type,
                        "Corporate name already exists for this insurance company",
                        409
                    );
                }

                var responseData = new CorporateMasterResponse { CorporateId = result };

                if (request.CorporateId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Corporate created successfully. CorporateId={result}");
                    return ServiceResult<CorporateMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Corporate updated successfully. CorporateId={result}");
                    return ServiceResult<CorporateMasterResponse>.Success(
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
                return ServiceResult<CorporateMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
        public ServiceResult<IEnumerable<CorporateMasterDetailModel>> GetCorporateMasterList(
            int? corporateId = null,
            string corporateName = null,
            int? insuranceCompanyId = null,
            string insuranceCompanyName = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetCorporateMasterList called. CorporateId={corporateId?.ToString() ?? "All"}, CorporateName={corporateName ?? "All"}, InsuranceCompanyId={insuranceCompanyId?.ToString() ?? "All"}, InsuranceCompanyName={insuranceCompanyName ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                string cacheKey = "_CorporateMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<CorporateMasterDetailModel> allCorporates;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"CorporateMaster data retrieved from cache. Key={cacheKey}");
                    allCorporates = System.Text.Json.JsonSerializer.Deserialize<List<CorporateMasterDetailModel>>(cachedData);
                }
                else
                {
                    _log.Info($"CorporateMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetCoporateDetails",
                        CommandType.StoredProcedure
                    );

                    allCorporates = dataTable?.AsEnumerable().Select(row => new CorporateMasterDetailModel
                    {
                        CorporateId = row.Field<int>("CorporateId"),
                        CorporateName = row.Field<string>("CorporateName") ?? string.Empty,
                        InsuranceCompanyName = row.Field<string>("InsuranceCompanyName") ?? string.Empty,
                        InsuranceCompanyId = row.Field<int?>("InsuranceCompanyId") ?? 0,
                        CorporateTypeId = row.Field<int?>("CorporateTypeId") ?? 0,
                        PaymentTypeId = row.Field<int?>("PaymentTypeId") ?? 0,
                        CorporateCode = row.Field<string>("CorporateCode") ?? string.Empty,
                        CorporateContact1 = row.Field<string>("CorporateContact1") ?? string.Empty,
                        CorporateContact2 = row.Field<string>("CorporateContact2") ?? string.Empty,
                        CorporateEmail = row.Field<string>("CorporateEmail") ?? string.Empty,
                        CorporateAddress1 = row.Field<string>("CorporateAddress1") ?? string.Empty,
                        CorporateAddress2 = row.Field<string>("CorporateAddress2") ?? string.Empty,
                        IsActive = row.Field<int?>("IsActive") ?? 0,
                        ContractStartFrom = row.Field<string>("ContractStartFrom") ?? string.Empty,
                        ContractExpiresOn = row.Field<string>("ContractExpiresOn") ?? string.Empty,
                        CopaymentPer = row.Field<decimal?>("CopaymentPer") ?? 0,
                        DiscountPerOut = row.Field<decimal?>("DiscountPerOut") ?? 0,
                        DiscountPerIn = row.Field<decimal?>("DiscountPerIn") ?? 0,
                        HikePerOut = row.Field<decimal?>("HikePerOut") ?? 0,
                        HikePerIn = row.Field<decimal?>("HikePerIn") ?? 0,
                        ActivePaymentModes = row.Field<string>("ActivePaymentModes") ?? string.Empty,
                        IsRegistrationChargeApplicable = row.Field<int?>("IsRegistrationChargeApplicable") ?? 0,
                        IsCaseBillingApplicable = row.Field<int?>("IsCaseBillingApplicable") ?? 0,


                    }).ToList() ?? new List<CorporateMasterDetailModel>();

                    if (allCorporates.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allCorporates);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All CorporateMaster data cached permanently. Key={cacheKey}, Count={allCorporates.Count}");
                    }
                }

                // Filter in memory from cache — null/blank = return all
                List<CorporateMasterDetailModel> filteredCorporates = allCorporates;

                if (corporateId.HasValue && corporateId.Value > 0)
                {
                    _log.Info($"Filtering by CorporateId: {corporateId.Value}");
                    filteredCorporates = filteredCorporates
                        .Where(c => c.CorporateId == corporateId.Value)
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(corporateName))
                {
                    _log.Info($"Filtering by CorporateName containing: {corporateName}");
                    filteredCorporates = filteredCorporates
                        .Where(c => c.CorporateName.Contains(corporateName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (insuranceCompanyId.HasValue && insuranceCompanyId.Value > 0)
                {
                    _log.Info($"Filtering by InsuranceCompanyId: {insuranceCompanyId.Value}");
                    filteredCorporates = filteredCorporates
                        .Where(c => c.InsuranceCompanyId == insuranceCompanyId.Value)
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(insuranceCompanyName))
                {
                    _log.Info($"Filtering by InsuranceCompanyName containing: {insuranceCompanyName}");
                    filteredCorporates = filteredCorporates
                        .Where(c => c.InsuranceCompanyName.Contains(insuranceCompanyName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering by IsActive: {isActive.Value}");
                    filteredCorporates = filteredCorporates
                        .Where(c => c.IsActive == isActive.Value)
                        .ToList();
                }

                if (!filteredCorporates.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No corporates found for the given filters");
                    return ServiceResult<IEnumerable<CorporateMasterDetailModel>>.Failure(
                        alert.Type,
                        "No corporates found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredCorporates.Count} corporate(s) from cache");

                return ServiceResult<IEnumerable<CorporateMasterDetailModel>>.Success(
                    filteredCorporates,
                    "Info",
                    $"{filteredCorporates.Count} corporate(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<CorporateMasterDetailModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
        public ServiceResult<string> UpdateCorporateMasterStatus(int corporateId, int isActive, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UpdateCorporateMasterStatus", CommandType.StoredProcedure, new
                {
                    @CorporateId = corporateId,
                    @userId = globalValues.userId,
                    @isActive = isActive
                });

                _distributedCache.Remove("_CorporateMaster_All");
                _distributedCache.Remove("_Corporate_All");

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Corporate status updated successfully. CorporateId={corporateId}, IsActive={isActive}");
                    return ServiceResult<string>.Success(
                        "Corporate status updated successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"Corporate not found for CorporateId={corporateId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Corporate not found",
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


        public ServiceResult<DiscountApprovalMasterResponse> CreateUpdateDiscountApprovalMaster(
    DiscountApprovalMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDiscountApprovalMaster called. DiscountApprovalId={request.DiscountApprovalId}, Name={request.DiscountApprovalName}");

                var result = _sqlHelper.DML("IU_DiscountApprovalMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @discountApprovalId = request.DiscountApprovalId,
                    @discountApprovalName = request.DiscountApprovalName,
                    @hmsUserId = request.HmsUserId,
                    @isActive = request.IsActive,
                    @mappingBranch = request.MappingBranch,
                    @mappingDiscountType = request.MappingDiscountType,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new { result = 0 });

                // Clear cache after successful operation
                _distributedCache.Remove("_DiscountApprovalMaster_All");
                _log.Info("Cleared DiscountApprovalMaster cache");

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate discount approval name: {request.DiscountApprovalName}");
                    return ServiceResult<DiscountApprovalMasterResponse>.Failure(
                        alert.Type,
                        "Approval Name Already Exists",
                        409
                    );
                }

                var responseData = new DiscountApprovalMasterResponse { Id = result };

                if (request.DiscountApprovalId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Discount approval created successfully. Id={result}");
                    return ServiceResult<DiscountApprovalMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Discount approval updated successfully. Id={result}");
                    return ServiceResult<DiscountApprovalMasterResponse>.Success(
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
                return ServiceResult<DiscountApprovalMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<DiscountApprovalMasterModel>> GetDiscountApprovalMasterList(
            string name = null,
            int? isActive = null)
        {
            try
            {
                _log.Info($"GetDiscountApprovalMasterList called. Name={name ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                string cacheKey = "_DiscountApprovalMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<DiscountApprovalMasterModel> allRecords;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DiscountApprovalMaster data retrieved from cache. Key={cacheKey}");
                    allRecords = System.Text.Json.JsonSerializer.Deserialize<List<DiscountApprovalMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DiscountApprovalMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getDiscountMasterList",
                        CommandType.StoredProcedure
                    );

                    allRecords = dataTable?.AsEnumerable().Select(row => new DiscountApprovalMasterModel
                    {
                        Id = row.Field<int>("Id"),
                        Name = row.Field<string>("Name") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive"),
                        DiscountType = row.Field<string>("DiscountType") ?? string.Empty,
                        BranchName = row.Field<string>("BranchName") ?? string.Empty,
                        FirstName = row.Field<string>("FirstName") ?? string.Empty
                    }).ToList() ?? new List<DiscountApprovalMasterModel>();

                    if (allRecords.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allRecords);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All DiscountApprovalMaster data cached permanently. Key={cacheKey}, Count={allRecords.Count}");
                    }
                }

                // Filter in memory
                List<DiscountApprovalMasterModel> filtered = allRecords;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    _log.Info($"Filtering by Name: {name}");
                    filtered = filtered
                        .Where(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering by IsActive: {isActive.Value}");
                    filtered = filtered.Where(x => x.IsActive == isActive.Value).ToList();
                }

                if (!filtered.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No discount approval records found after filtering");
                    return ServiceResult<IEnumerable<DiscountApprovalMasterModel>>.Failure(
                        alert.Type,
                        "No discount approval records found",
                        404
                    );
                }

                _log.Info($"Retrieved {filtered.Count} discount approval record(s) from cache");

                return ServiceResult<IEnumerable<DiscountApprovalMasterModel>>.Success(
                    filtered,
                    "Info",
                    $"{filtered.Count} record(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DiscountApprovalMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<object> SaveUserwiseDiscountMaster(List<UserwiseDiscountMasterRequest> request, AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;

            try
            {
                _log.Info($"SaveUserwiseDiscountMaster called. Records Count={request?.Count ?? 0}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Delete all existing records
                _sqlHelper.DML(tnx, "D_UserwiseDiscountMaster", CommandType.StoredProcedure);
                _log.Info("Deleted existing UserwiseDiscountMaster records");

                // Insert new records
                if (request != null && request.Any())
                {
                    foreach (var r in request)
                    {
                        _sqlHelper.DML(tnx, "I_UserwiseDiscountMaster", CommandType.StoredProcedure, new
                        {
                            @userId = r.userId,
                            @discPerOPD = r.discPerOPD,
                            @discPerIPD = r.discPerIPD,
                            @discPerPharmacy = r.discPerPharmacy,
                            @discPerDayCare = r.discPerDayCare,
                            @discPerDialysis = r.discPerDialysis,
                            @discPerEmergency = r.discPerEmergency,
                            @createdBy = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        });
                    }
                    _log.Info($"Inserted {request.Count} UserwiseDiscountMaster records");
                }

                tnx.Commit();
                _log.Info("Transaction committed successfully");

                // Clear cache after save
                _distributedCache.Remove("_UserwiseDiscountMaster_All");
                GlobalFunctions.ClearCacheByPattern(_configuration, "_UserDiscountRights_User*");
                _log.Info("Cleared UserwiseDiscountMaster cache");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    null,
                    alert.Type,
                    alert.Message,
                    200
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
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                if (tnx != null) tnx.Dispose();
                if (con != null)
                {
                    if (con.State == ConnectionState.Open) con.Close();
                    con.Dispose();
                }
            }
        }

        public ServiceResult<object> GetUserwiseDiscountMaster()
        {
            try
            {
                _log.Info("GetUserwiseDiscountMaster called");

                string cacheKey = "_UserwiseDiscountMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserwiseDiscountMaster data retrieved from cache. Key={cacheKey}");
                    return ServiceResult<object>.Success(
                        System.Text.Json.JsonSerializer.Deserialize<object>(cachedData),
                        "Info",
                        "Data retrieved successfully",
                        200
                    );
                }

                _log.Info($"UserwiseDiscountMaster cache miss. Fetching from database. Key={cacheKey}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_UserwiseDiscountMaster",
                    CommandType.StoredProcedure
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No UserwiseDiscountMaster records found");
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
                _log.Info($"UserwiseDiscountMaster data cached permanently. Key={cacheKey}, Count={rawData.Count}");

                return ServiceResult<object>.Success(
                    rawData,
                    "Info",
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

        public ServiceResult<CreateUpdateDoctorHeaderResponse> CreateUpdateDoctorHeader(
           CreateUpdateDoctorHeaderRequest request,
           AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;

            try
            {
                _log.Info($"CreateUpdateDoctorHeader called. HeaderId={request.HeaderId}, HeaderName={request.HeaderName}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Upsert header master
                var headerResult = _sqlHelper.DML(tnx, "IU_DoctorHeaderMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @headerId = request.HeaderId,
                    @headerName = request.HeaderName,
                    @displayName = request.DisplayName ?? string.Empty,
                    @controlType = request.ControlType ?? string.Empty,
                    @controlTypeId = request.ControlTypeId,
                    @isPrint = request.IsPrint,
                    @isShowInTempRoom = request.IsShowInTempRoom,
                    @usedForPatientType = request.UsedForPatientType,
                    @isMandatory = request.IsMandatory,
                    @isActive = request.IsActive,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress,
                    @queries = request.Queries ?? string.Empty

                },
                new { result = 0 });

                int headerId = Convert.ToInt32(headerResult);

                if (headerId < 0)
                {
                    tnx.Rollback();
                    var alertDup = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate header name: {request.HeaderName}");
                    return ServiceResult<CreateUpdateDoctorHeaderResponse>.Failure(
                        alertDup.Type,
                        "Header Name Already Exists",
                        409);
                }

                // Step 2 – Check duplicate for controlTypeId 7 or 8 (Investigations / Medicine)
                if (request.ControlTypeId == 7 || request.ControlTypeId == 8)
                {
                    int isExist = Convert.ToInt32(_sqlHelper.ExecuteScalar(tnx,
                        "S_CheckDuplicateInvestMedicineHeaderMaster",
                        CommandType.StoredProcedure,
                        new
                        {
                            @controlTypeId = request.ControlTypeId,
                            @headerId = request.HeaderId
                        }));

                    if (isExist > 0)
                    {
                        tnx.Rollback();
                        var alertType = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                        _log.Warn($"ControlType {request.ControlType} already exists in master");
                        return ServiceResult<CreateUpdateDoctorHeaderResponse>.Failure(
                            alertType.Type,
                            $"{request.ControlType} Type Already Exists in Master",
                            409);
                    }
                }

                // Step 3 – Delete existing LOV rows then re-insert
                _sqlHelper.DML(tnx, "D_DoctorHeaderLOVMapping", CommandType.StoredProcedure,
                    new { @headerId = headerId });

                if (request.ListOfValues != null && request.ListOfValues.Any())
                {
                    foreach (var lov in request.ListOfValues)
                    {
                        // Convert options list to comma-separated string (or JSON)
                        string optionsString = null;
                        if (lov.Options != null && lov.Options.Any())
                        {
                            optionsString = string.Join("##", lov.Options);
                        }

                        _sqlHelper.DML(tnx, "I_DoctorHeaderLOVMapping", CommandType.StoredProcedure, new
                        {
                            @headerId = headerId,
                            @value = lov.Value ?? string.Empty,
                            @dataTypeId = lov.DataTypeId,
                            @score = lov.Score,
                            @base64Data = lov.Base64Data ?? (object)DBNull.Value,
                            @description = lov.Description ?? (object)DBNull.Value,
                            @headerName = lov.HeaderName ?? (object)DBNull.Value,
                            @options = optionsString ?? (object)DBNull.Value
                        });
                    }
                }

                tnx.Commit();

                // Invalidate cache
                _distributedCache.Remove(CACHE_KEY_DOCTOR_HEADER_ALL);
                _log.Info($"Cleared DoctorHeaderMaster cache after create/update. HeaderId={headerId}");

                var responseData = new CreateUpdateDoctorHeaderResponse { HeaderId = headerId };
                var alert = _messageService.GetMessageAndTypeByAlertCode(
                    request.HeaderId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                return ServiceResult<CreateUpdateDoctorHeaderResponse>.Success(
                    responseData,
                    alert.Type,
                    alert.Message,
                    request.HeaderId == 0 ? 201 : 200);
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch { /* swallow rollback errors */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateDoctorHeaderResponse>.Failure(alert.Type, alert.Message, 500);
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

        /// <summary>
        /// Returns all DoctorHeaderMaster rows (cached permanently).
        /// Optional in-memory filter by headerId.
        /// Mirrors: getAllDoctorHeaderMaster
        /// </summary>
        public ServiceResult<IEnumerable<DoctorHeaderMasterModel>> GetAllDoctorHeaderMaster(int? headerId = null)
        {
            try
            {
                _log.Info($"GetAllDoctorHeaderMaster called. HeaderId={headerId?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_DOCTOR_HEADER_ALL);
                List<DoctorHeaderMasterModel> allHeaders;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorHeaderMaster data retrieved from cache. Key={CACHE_KEY_DOCTOR_HEADER_ALL}");
                    allHeaders = JsonSerializer.Deserialize<List<DoctorHeaderMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorHeaderMaster cache miss. Fetching all data from database.");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetAllDoctorHeaderMaster",
                        CommandType.StoredProcedure);

                    allHeaders = dataTable?.AsEnumerable().Select(row => new DoctorHeaderMasterModel
                    {
                        HeaderId = row.Field<int>("HeaderId"),
                        HeaderName = row.Field<string>("HeaderName") ?? string.Empty,
                        DisplayName = row.Field<string>("DisplayName") ?? string.Empty,
                        ControlType = row.Field<string>("ControlType") ?? string.Empty,
                        ControlTypeId = row.Field<int?>("ControlTypeId"),
                        IsPrint = row.Field<int>("IsPrint"),
                        IsShowInTempRoom = row.Field<int>("IsShowInTempRoom"),
                        IsMandatory = row.Field<int>("IsMandatory"),
                        UsedForPatientType = row.Field<int>("UsedForPatientType"),
                        UsedForPatientTypeName = row.Field<string>("UsedForPatientTypeName"),
                        IsActive = row.Field<int>("IsActive"),
                        Queries = row.Field<string>("Queries") ?? string.Empty

                    }).ToList() ?? new List<DoctorHeaderMasterModel>();

                    if (allHeaders.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allHeaders);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_DOCTOR_HEADER_ALL, serialized, cacheOptions);
                        _log.Info($"DoctorHeaderMaster cached permanently. Count={allHeaders.Count}");
                    }
                }

                // In-memory filter
                var filtered = headerId.HasValue
                    ? allHeaders.Where(h => h.HeaderId == headerId.Value).ToList()
                    : allHeaders;

                if (!filtered.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<DoctorHeaderMasterModel>>.Failure(
                        alert.Type,
                        headerId.HasValue ? $"Header not found for HeaderId: {headerId.Value}" : "No headers found",
                        404);
                }

                _log.Info($"Retrieved {filtered.Count} header(s) from cache");

                return ServiceResult<IEnumerable<DoctorHeaderMasterModel>>.Success(
                    filtered,
                    "Info",
                    $"{filtered.Count} header(s) retrieved successfully",
                    200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorHeaderMasterModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        /// <summary>
        /// Returns LOV rows for a given header (raw data from SP, no cache – small & volatile).
        /// Mirrors: getDoctorHeaderLOVs
        /// </summary>
        public ServiceResult<IEnumerable<DoctorHeaderLOVModel>> GetDoctorHeaderLOVs(int headerId)
        {
            try
            {
                _log.Info($"GetDoctorHeaderLOVs called. HeaderId={headerId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetDoctorHeaderLOVMapping",
                    CommandType.StoredProcedure,
                    new { @headerId = headerId });

                var lovs = dataTable?.AsEnumerable().Select(row => new DoctorHeaderLOVModel
                {
                    Value = row.Field<string>("Value") ?? string.Empty,
                    DataTypeId = row.Field<int?>("DataTypeId") ?? 0,
                    HeaderName = row.Field<string>("HeaderName") ?? string.Empty,
                    Options = row.Field<string>("Options") ?? string.Empty,
                    Score = row.Field<int?>("Score") ?? 0,
                    Base64Data = row.Field<string>("Base64Data") ?? string.Empty,
                    Description = row.Field<string>("Description") ?? string.Empty,


                }).ToList() ?? new List<DoctorHeaderLOVModel>();

                if (!lovs.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<DoctorHeaderLOVModel>>.Failure(
                        alert.Type,
                        $"No LOV values found for HeaderId: {headerId}",
                        404);
                }

                return ServiceResult<IEnumerable<DoctorHeaderLOVModel>>.Success(
                    lovs,
                    "Info",
                    $"{lovs.Count} LOV value(s) retrieved successfully",
                    200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorHeaderLOVModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        /// <summary>
        /// Returns all active header masters with their mapping status for a given type/relatedTo.
        /// Raw data from SP – no cache (mapping changes frequently).
        /// Mirrors: getDoctorHeaderMappingForMaster
        /// </summary>
        public ServiceResult<IEnumerable<DoctorHeaderMappingModel>> GetDoctorHeaderMappingForMaster(
            int typeId,
            int relatedToId)
        {
            try
            {
                _log.Info($"GetDoctorHeaderMappingForMaster called. TypeId={typeId}, RelatedToId={relatedToId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetDoctorHeaderMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @typeId = typeId,
                        @relatedToId = relatedToId
                    });

                var mappings = dataTable?.AsEnumerable().Select(row => new DoctorHeaderMappingModel
                {
                    HeaderId = row.Field<int>("HeaderId"),
                    HeaderName = row.Field<string>("HeaderName") ?? string.Empty,
                    DisplayName = row.Field<string>("DisplayName") ?? string.Empty,
                    ControlType = row.Field<string>("ControlType") ?? string.Empty,
                    MappingId = row.Field<long?>("MappingId") ?? 0,
                    SequenceNo = row.Field<int?>("SequenceNo") ?? 9999
                }).ToList() ?? new List<DoctorHeaderMappingModel>();

                if (!mappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<DoctorHeaderMappingModel>>.Failure(
                        alert.Type,
                        "No header mapping data found",
                        404);
                }

                return ServiceResult<IEnumerable<DoctorHeaderMappingModel>>.Success(
                    mappings,
                    "Info",
                    $"{mappings.Count} header mapping record(s) retrieved successfully",
                    200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorHeaderMappingModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        /// <summary>
        /// Delete existing mapping for typeId/relatedToId, then bulk-insert the new set (transactional).
        /// Mirrors: saveDoctorHeaderDepartmentMapping
        /// </summary>
        public ServiceResult<string> SaveDoctorHeaderDepartmentMapping(
            SaveDoctorHeaderMappingRequest request,
            AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;

            try
            {
                _log.Info($"SaveDoctorHeaderDepartmentMapping called. TypeId={request.TypeId}, RelatedToId={request.RelatedToId}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Delete existing mappings
                _sqlHelper.DML(tnx, "D_DeleteDoctorHeaderMapping", CommandType.StoredProcedure, new
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
                        _sqlHelper.DML(tnx, "I_DoctorHeaderMapping", CommandType.StoredProcedure, new
                        {
                            @hospId = globalValues.hospId,
                            @typeId = item.TypeId,
                            @typeName = item.TypeName ?? string.Empty,
                            @headerId = item.HeaderId,
                            @retatedToId = item.RelatedToId,
                            @sequenceNo = item.SequenceNo,
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        });
                        insertedCount++;
                    }
                }

                tnx.Commit();

                _log.Info($"SaveDoctorHeaderDepartmentMapping committed. Inserted={insertedCount}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{insertedCount} mapping(s) saved successfully",
                    alert.Type,
                    "Mapping Updated Successfully",
                    200);
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
        public ServiceResult<object> CreateUpdateServiceItemMaster(
            CreateUpdateServiceItemMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateServiceItemMaster called. ServiceItemId={request.ServiceItemId}, Name={request.Name}");

                // ── Registration Charge duplicate check ──────────────────────────────
                // Only one service item across the whole master can have IsRegistrationCharge = 1
                if (request.IsRegistrationCharge == 1)
                {
                    var dupTable = _sqlHelper.GetDataTable(
                        "S_CheckDuplicateRegistrationChargeInServiceItemMaster",
                        CommandType.StoredProcedure,
                        new { @ServiceItemId = request.ServiceItemId }
                    );

                    if (dupTable != null && dupTable.Rows.Count > 0)
                    {
                        int duplicateCount = Convert.ToInt32(dupTable.Rows[0]["DuplicateCount"]);

                        if (duplicateCount > 0)
                        {
                            string existingServiceName = dupTable.Rows[0]["ServiceName"]?.ToString() ?? string.Empty;

                            _log.Warn($"Registration charge service '{existingServiceName}' already exists. Blocking duplicate for ServiceItemId={request.ServiceItemId}");

                            var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                            return ServiceResult<object>.Failure(
                                dupAlert.Type,
                                $"Registration Charge is already assigned to service '{existingServiceName}'.",
                                409
                            );
                        }
                    }
                }

                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@hospId", globalValues.hospId),
            new SqlParameter("@serviceItemId", request.ServiceItemId),
            new SqlParameter("@categoryId", request.CategoryId),
            new SqlParameter("@subCategoryId", request.SubCategoryId),
            new SqlParameter("@subSubCategoryId", request.SubSubCategoryId),
            new SqlParameter("@name", request.Name),
            new SqlParameter("@code", (object?)request.Code ?? DBNull.Value),

            new SqlParameter("@roomTypeId", (object?)request.RoomTypeId ?? DBNull.Value),
            new SqlParameter("@roomType", (object?)request.RoomType ?? DBNull.Value),
            new SqlParameter("@isICU", (object?)request.IsICU ?? DBNull.Value),
            new SqlParameter("@gstPer", request.GstPer),

            new SqlParameter("@OPDConsultationTypeId", (object?)request.OPDConsultationTypeId ?? DBNull.Value),
            new SqlParameter("@OPDConsultationType", (object?)request.OPDConsultationType ?? DBNull.Value),
            new SqlParameter("@SNOMEDCode", (object?)request.SNOMEDCode ?? DBNull.Value),
            new SqlParameter("@isRequiredSeparatePerformingDoctor", request.IsRequiredSeparatePerformingDoctor),
            new SqlParameter("@doctorDepartmentIds", (object?)request.DoctorDepartmentIds ?? DBNull.Value),
            new SqlParameter("@isOnlineConsultationAllow", (object?)request.IsOnlineConsultationAllow ?? DBNull.Value),
            new SqlParameter("@isTeleConsultationService", (object?)request.IsTeleConsultationService ?? DBNull.Value),
            new SqlParameter("@isRegistrationCharge", (object?)request.IsRegistrationCharge ?? DBNull.Value),
            new SqlParameter("@registrationChargeValidityDays", (object?)request.RegistrationChargeValidityDays ?? DBNull.Value),

            new SqlParameter("@isActive", request.IsActive),
            new SqlParameter("@userId", globalValues.userId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_ServiceItemMaster", parameters);

                // Clear Redis cache so next GET re-fetches fresh data
                _distributedCache.Remove("_ServiceItemMaster_All");
                _distributedCache.Remove("_ServiceInvestigationItemMaster_All");
                _distributedCache.Remove("_BedMaster_All");

                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate service item name or code: Name={request.Name}, Code={request.Code}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "Service/Item Name (in same Sub Sub Category) or Code already exists",
                        409
                    );
                }

                var responseData = new CreateUpdateServiceItemMasterResponse { ServiceItemId = (int)result };

                if (request.ServiceItemId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Service item created successfully. ServiceItemId={result}");
                    return ServiceResult<object>.Success(responseData, alert.Type, alert.Message, 201);
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Service item updated successfully. ServiceItemId={result}");
                    return ServiceResult<object>.Success(responseData, alert.Type, alert.Message, 200);
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> CreateUpdatePrintGroupMaster(
    CreateUpdatePrintGroupMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdatePrintGroupMaster called. PrintGroupId={request.PrintGroupId}, PrintGroupName={request.PrintGroupName}");

                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@PrintGroupId", request.PrintGroupId),
            new SqlParameter("@PrintGroupName", request.PrintGroupName),
            new SqlParameter("@PrintOrder",(object?)request.PrintOrder ?? DBNull.Value),
            new SqlParameter("@UserId", globalValues.userId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_PrintGroupMaster", parameters);

                _distributedCache.Remove("_PrintGroupMaster_All");
                _log.Info("Cleared PrintGroupMaster cache.");

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate PrintGroupName: {request.PrintGroupName}");
                    return ServiceResult<object>.Failure(dupAlert.Type, "Print group name already exists", 409);
                }

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.PrintGroupId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"PrintGroupMaster {(request.PrintGroupId == 0 ? "created" : "updated")} successfully. PrintGroupId={result}");
                    return ServiceResult<object>.Success(
                        new { printGroupId = result },
                        alert.Type,
                        alert.Message,
                        request.PrintGroupId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                _log.Error($"PrintGroupMaster operation failed with result: {result}");
                return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetPrintGroupMaster(int? printGroupId)
        {
            try
            {
                _log.Info($"GetPrintGroupMaster called. PrintGroupId={printGroupId?.ToString() ?? "All"}");

                const string cacheKey = "_PrintGroupMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allGroups;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("PrintGroupMaster data retrieved from Redis cache.");
                    allGroups = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData)
                        ?? new List<Dictionary<string, object>>();
                }
                else
                {
                    _log.Info("PrintGroupMaster cache miss. Fetching from DB.");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetPrintGroupMaster",
                        CommandType.StoredProcedure
                    );

                    allGroups = dataTable?.AsEnumerable().Select(row =>
                        row.Table.Columns.Cast<DataColumn>()
                           .ToDictionary(
                               col => col.ColumnName,
                               col => row[col] == DBNull.Value ? null : row[col]
                           )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allGroups.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allGroups);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"PrintGroupMaster cached permanently. Count={allGroups.Count}");
                    }
                }

                // Filter by PrintGroupId in memory
                if (printGroupId.HasValue && printGroupId.Value > 0)
                {
                    allGroups = allGroups.Where(row =>
                        row.TryGetValue("PrintGroupId", out var val) &&
                        val != null &&
                        Convert.ToInt32(((System.Text.Json.JsonElement)val).GetRawText()) == printGroupId.Value
                    ).ToList();

                    _log.Info($"Filtered to {allGroups.Count} group(s) for PrintGroupId={printGroupId.Value}");
                }

                if (!allGroups.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No print groups found", 404);
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allGroups,
                    alert.Type,
                    $"{allGroups.Count} print group(s) retrieved successfully",
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


        public ServiceResult<object> CreateUpdateWardNameMaster(
    CreateUpdateWardNameMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateWardNameMaster called. WardNameId={request.WardNameId}, WardName={request.WardName}");

                SqlParameter[] parameters = new SqlParameter[]
                {
            new SqlParameter("@WardNameId", request.WardNameId),
            new SqlParameter("@WardName", request.WardName),
            new SqlParameter("@UserId", globalValues.userId),
            new SqlParameter("@IpAddress", globalValues.ipAddress),
            new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_WardNameMaster", parameters);

                _distributedCache.Remove("_WardNameMaster_All");
                _distributedCache.Remove("_BedMaster_All");

                _log.Info("Cleared WardNameMaster cache.");

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate WardName: {request.WardName}");
                    return ServiceResult<object>.Failure(dupAlert.Type, "Ward name already exists", 409);
                }

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.WardNameId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"WardNameMaster {(request.WardNameId == 0 ? "created" : "updated")} successfully. WardNameId={result}");
                    return ServiceResult<object>.Success(
                        new { wardNameId = result },
                        alert.Type,
                        alert.Message,
                        request.WardNameId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                _log.Error($"WardNameMaster operation failed with result: {result}");
                return ServiceResult<object>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetWardNameMaster(int? wardNameId)
        {
            try
            {
                _log.Info($"GetWardNameMaster called. WardNameId={wardNameId?.ToString() ?? "All"}");

                const string cacheKey = "_WardNameMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allWards;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("WardNameMaster data retrieved from Redis cache.");
                    allWards = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData)
                        ?? new List<Dictionary<string, object>>();
                }
                else
                {
                    _log.Info("WardNameMaster cache miss. Fetching from DB.");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetWardNameMaster",
                        CommandType.StoredProcedure
                    );

                    allWards = dataTable?.AsEnumerable().Select(row =>
                        row.Table.Columns.Cast<DataColumn>()
                           .ToDictionary(
                               col => col.ColumnName,
                               col => row[col] == DBNull.Value ? null : row[col]
                           )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allWards.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allWards);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"WardNameMaster cached permanently. Count={allWards.Count}");
                    }
                }

                if (wardNameId.HasValue && wardNameId.Value > 0)
                {
                    allWards = allWards.Where(row =>
                        row.TryGetValue("WardNameId", out var val) &&
                        val != null &&
                        Convert.ToInt32(((System.Text.Json.JsonElement)val).GetRawText()) == wardNameId.Value
                    ).ToList();

                    _log.Info($"Filtered to {allWards.Count} ward(s) for WardNameId={wardNameId.Value}");
                }

                if (!allWards.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No ward names found", 404);
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allWards,
                    alert.Type,
                    $"{allWards.Count} ward name(s) retrieved successfully",
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

        public ServiceResult<CreateUpdateBlockMasterResponse> CreateUpdateBlockMaster(
    CreateUpdateBlockMasterRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateBlockMaster called. BlockId={request.BlockId}, BlockName={request.BlockName}");

                var result = _sqlHelper.DML("IU_BlockMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @BlockId = request.BlockId,
                    @BlockName = request.BlockName,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                int resultValue = Convert.ToInt32(result);

                // Clear cache after any write operation
                _distributedCache.Remove("_BlockMaster_All");
                _distributedCache.Remove("_BedMaster_All");

                _log.Info("Cleared BlockMaster cache");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate Block name: {request.BlockName}");
                    return ServiceResult<CreateUpdateBlockMasterResponse>.Failure(
                        alert.Type,
                        "Block Name Already Exists",
                        409
                    );
                }

                var responseData = new CreateUpdateBlockMasterResponse { BlockId = resultValue };

                if (request.BlockId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Block created successfully. BlockId={resultValue}");
                    return ServiceResult<CreateUpdateBlockMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Block updated successfully. BlockId={resultValue}");
                    return ServiceResult<CreateUpdateBlockMasterResponse>.Success(
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
                return ServiceResult<CreateUpdateBlockMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<BlockMasterModel>> GetBlockList(int? BlockId = null)
        {
            try
            {
                _log.Info($"GetBlockList called. BlockId={BlockId?.ToString() ?? "All"}");

                // Always use the same cache key for all Blocks
                string cacheKey = "_BlockMaster_All";

                // Try to get all Blocks from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<BlockMasterModel> allBlocks;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"BlockMaster data retrieved from cache. Key={cacheKey}");
                    allBlocks = System.Text.Json.JsonSerializer.Deserialize<List<BlockMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"BlockMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL Blocks from database — SP returns everything, no parameters
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetBlockList",
                        CommandType.StoredProcedure
                    );

                    // Bind raw from SP — no mapping
                    allBlocks = dataTable?.AsEnumerable().Select(row => new BlockMasterModel
                    {
                        BlockId = row.Field<int>("BlockId"),
                        BlockName = row.Field<string>("BlockName") ?? string.Empty
                    }).ToList() ?? new List<BlockMasterModel>();

                    // Store ALL Blocks in cache (no expiration — cleared on write)
                    if (allBlocks.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allBlocks);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All BlockMaster data cached permanently. Key={cacheKey}, Count={allBlocks.Count}");
                    }
                }

                // Filter in memory (always from cache)
                List<BlockMasterModel> filteredBlocks;
                if (BlockId.HasValue)
                {
                    _log.Info($"Filtering cached data by BlockId: {BlockId.Value}");
                    filteredBlocks = allBlocks.Where(f => f.BlockId == BlockId.Value).ToList();
                }
                else
                {
                    _log.Info("Returning all cached Blocks");
                    filteredBlocks = allBlocks;
                }

                if (!filteredBlocks.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No Blocks found for BlockId: {BlockId?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<BlockMasterModel>>.Failure(
                        alert.Type,
                        BlockId.HasValue ? $"Block not found for BlockId: {BlockId.Value}" : "No Blocks found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredBlocks.Count} Block(s) from cache");

                return ServiceResult<IEnumerable<BlockMasterModel>>.Success(
                    filteredBlocks,
                    "Info",
                    $"{filteredBlocks.Count} Block(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<BlockMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<CreateUpdateFloorMasterResponse> CreateUpdateFloorMaster(
  CreateUpdateFloorMasterRequest request,
  AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateFloorMaster called. FloorId={request.FloorId}, FloorName={request.FloorName}");

                var result = _sqlHelper.DML("IU_FloorMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @floorId = request.FloorId,
                    @floorName = request.FloorName,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                int resultValue = Convert.ToInt32(result);

                // Clear cache after any write operation
                _distributedCache.Remove("_FloorMaster_All");
                _distributedCache.Remove("_BedMaster_All");

                _log.Info("Cleared FloorMaster cache");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate floor name: {request.FloorName}");
                    return ServiceResult<CreateUpdateFloorMasterResponse>.Failure(
                        alert.Type,
                        "Floor Name Already Exists",
                        409
                    );
                }

                var responseData = new CreateUpdateFloorMasterResponse { FloorId = resultValue };

                if (request.FloorId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Floor created successfully. FloorId={resultValue}");
                    return ServiceResult<CreateUpdateFloorMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Floor updated successfully. FloorId={resultValue}");
                    return ServiceResult<CreateUpdateFloorMasterResponse>.Success(
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
                return ServiceResult<CreateUpdateFloorMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<FloorMasterModel>> GetFloorList(int? floorId = null)
        {
            try
            {
                _log.Info($"GetFloorList called. FloorId={floorId?.ToString() ?? "All"}");

                // Always use the same cache key for all floors
                string cacheKey = "_FloorMaster_All";

                // Try to get all floors from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<FloorMasterModel> allFloors;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"FloorMaster data retrieved from cache. Key={cacheKey}");
                    allFloors = System.Text.Json.JsonSerializer.Deserialize<List<FloorMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"FloorMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL floors from database — SP returns everything, no parameters
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetFloorList",
                        CommandType.StoredProcedure
                    );

                    // Bind raw from SP — no mapping
                    allFloors = dataTable?.AsEnumerable().Select(row => new FloorMasterModel
                    {
                        FloorId = row.Field<int>("FloorId"),
                        FloorName = row.Field<string>("FloorName") ?? string.Empty
                    }).ToList() ?? new List<FloorMasterModel>();

                    // Store ALL floors in cache (no expiration — cleared on write)
                    if (allFloors.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allFloors);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All FloorMaster data cached permanently. Key={cacheKey}, Count={allFloors.Count}");
                    }
                }

                // Filter in memory (always from cache)
                List<FloorMasterModel> filteredFloors;
                if (floorId.HasValue)
                {
                    _log.Info($"Filtering cached data by FloorId: {floorId.Value}");
                    filteredFloors = allFloors.Where(f => f.FloorId == floorId.Value).ToList();
                }
                else
                {
                    _log.Info("Returning all cached floors");
                    filteredFloors = allFloors;
                }

                if (!filteredFloors.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No floors found for FloorId: {floorId?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<FloorMasterModel>>.Failure(
                        alert.Type,
                        floorId.HasValue ? $"Floor not found for FloorId: {floorId.Value}" : "No floors found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredFloors.Count} floor(s) from cache");

                return ServiceResult<IEnumerable<FloorMasterModel>>.Success(
                    filteredFloors,
                    "Info",
                    $"{filteredFloors.Count} floor(s) fetched successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<FloorMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        // -----------------------------------------------------------------------
        // BED MASTER
        // -----------------------------------------------------------------------

        public ServiceResult<CreateUpdateBedMasterResponse> CreateUpdateBedMaster(
            CreateUpdateBedMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateBedMaster called. BedId={request.BedId}, BranchId={request.BranchId}, WardNameId={request.WardNameId}, BedNo={request.BedNo}");

                var result = _sqlHelper.DML("IU_BedMaster", CommandType.StoredProcedure, new
                {
                    @hospId = globalValues.hospId,
                    @bedId = request.BedId,
                    @branchId = request.BranchId,
                    @typeId = request.TypeId,
                    @blockId = request.BlockId,
                    @floorId = request.FloorId,
                    @wardNameId = request.WardNameId,
                    @roomName = request.RoomName ?? string.Empty,
                    @gender= request.Gender,
                    @bedNo = request.BedNo,
                    @isActive = request.IsActive,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                },
                new
                {
                    result = 0
                });

                int resultValue = Convert.ToInt32(result);

                // Clear cache after any write operation
                _distributedCache.Remove("_BedMaster_All");
                _log.Info("Cleared BedMaster cache");

                if (resultValue == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Bed No already exists in same Ward Name. WardNameId={request.WardNameId}, BedNo={request.BedNo}");
                    return ServiceResult<CreateUpdateBedMasterResponse>.Failure(
                        alert.Type,
                        "Bed No Already Exists in same Ward Name",
                        409
                    );
                }

                var responseData = new CreateUpdateBedMasterResponse { BedId = resultValue };

                if (request.BedId == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Bed(s) created successfully. LastBedId={resultValue}");
                    return ServiceResult<CreateUpdateBedMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    _log.Info($"Bed updated successfully. BedId={resultValue}");
                    return ServiceResult<CreateUpdateBedMasterResponse>.Success(
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
                return ServiceResult<CreateUpdateBedMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
        public ServiceResult<object> GetAllBedList(int? bedId = null, int? isActive = null, int? blockId = null, int? floorId = null, int? wardNameId = null, int? branchId = null, int? typeId = null)
        {
            try
            {
                _log.Info($"GetAllBedList called. BedId={bedId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}, FloorId={floorId?.ToString() ?? "All"}, WardNameId={wardNameId?.ToString() ?? "All"}, BranchId={branchId?.ToString() ?? "All"}, TypeId={typeId?.ToString() ?? "All"}");

                const string cacheKey = "_BedMaster_All";
                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allBeds;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("BedMaster data retrieved from Redis cache.");
                    allBeds = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData)
                        ?? new List<Dictionary<string, object>>();
                }
                else
                {
                    _log.Info("BedMaster cache miss. Fetching from DB.");
                    var dataTable = _sqlHelper.GetDataTable("S_GetAllBedList", CommandType.StoredProcedure);

                    allBeds = dataTable?.AsEnumerable().Select(row =>
                        row.Table.Columns.Cast<DataColumn>()
                           .ToDictionary(
                               col => col.ColumnName,
                               col => row[col] == DBNull.Value ? null : row[col]
                           )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allBeds.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allBeds);
                        _distributedCache.SetString(cacheKey, serialized, new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        });
                        _log.Info($"BedMaster cached permanently. Count={allBeds.Count}");
                    }
                }

                // Helper to safely extract int value from JsonElement or raw object
                static int? GetInt(Dictionary<string, object> row, string key)
                {
                    if (!row.TryGetValue(key, out var val) || val == null) return null;
                    if (val is System.Text.Json.JsonElement je)
                    {
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out int n)) return n;
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Null) return null;
                    }
                    return Convert.ToInt32(val);
                }

                if (bedId.HasValue)
                    allBeds = allBeds.Where(r => GetInt(r, "BedId") == bedId.Value).ToList();

                if (isActive.HasValue)
                    allBeds = allBeds.Where(r => GetInt(r, "IsActive") == isActive.Value).ToList();

                if (blockId.HasValue)
                    allBeds = allBeds.Where(r => GetInt(r, "BlockId") == blockId.Value).ToList();

                if (floorId.HasValue)
                    allBeds = allBeds.Where(r => GetInt(r, "FloorId") == floorId.Value).ToList();

                if (wardNameId.HasValue)
                    allBeds = allBeds.Where(r => GetInt(r, "WardNameId") == wardNameId.Value).ToList();

                if (branchId.HasValue)
                    allBeds = allBeds.Where(r => GetInt(r, "BranchId") == branchId.Value).ToList();

                if (typeId.HasValue)
                    allBeds = allBeds.Where(r => GetInt(r, "TypeId") == typeId.Value).ToList();

                _log.Info($"After filtering: {allBeds.Count} bed(s) remaining.");

                if (!allBeds.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No beds found", 404);
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(allBeds, alert.Type, $"{allBeds.Count} bed(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }


        public ServiceResult<object> CreateUpdateTabGroupTypeMaster(
           CreateUpdateTabGroupTypeMasterRequest request,
           AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateTabGroupTypeMaster called. GroupTypeId={request.GroupTypeId}, GroupTypeName={request.GroupTypeName}");

                var result = _sqlHelper.DML(
                    "IU_TabGroupTypeMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @groupTypeId = request.GroupTypeId,
                        @groupTypeName = request.GroupTypeName,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate GroupTypeName: {request.GroupTypeName}");
                    return ServiceResult<object>.Failure(dupAlert.Type, "Group Type Name already exists", 409);
                }

                if (resultValue > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_TabGroupType_All);
                    _log.Info($"Cleared TabGroupTypeMaster cache. GroupTypeId={resultValue}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.GroupTypeId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    return ServiceResult<object>.Success(
                        new { groupTypeId = resultValue },
                        alert.Type,
                        alert.Message,
                        request.GroupTypeId == 0 ? 201 : 200
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

        public ServiceResult<object> GetTabGroupTypeMaster(int? groupTypeId, int? isActive)
        {
            try
            {
                _log.Info($"GetTabGroupTypeMaster called. GroupTypeId={groupTypeId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_TabGroupType_All);
                List<Dictionary<string, object>> allRows;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"TabGroupTypeMaster data retrieved from cache. Key={CACHE_KEY_TabGroupType_All}");
                    allRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"TabGroupTypeMaster cache miss. Fetching from database.");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetTabGroupTypeMaster",
                        CommandType.StoredProcedure
                    );

                    allRows = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allRows.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allRows);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_TabGroupType_All, serialized, cacheOptions);
                        _log.Info($"TabGroupTypeMaster cached permanently. Count={allRows.Count}");
                    }
                }

                // In-memory filtering
                var filtered = allRows;

                // Helper to safely extract int value from JsonElement or raw object
                static int? GetInt(Dictionary<string, object> row, string key)
                {
                    if (!row.TryGetValue(key, out var val) || val == null) return null;
                    if (val is System.Text.Json.JsonElement je)
                    {
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out int n)) return n;
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Null) return null;
                    }
                    return Convert.ToInt32(val);
                }

                if (groupTypeId.HasValue)
                {
                    filtered = filtered.Where(r => GetInt(r, "GroupTypeId") == groupTypeId.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    filtered = filtered.Where(r => GetInt(r, "IsActive") == isActive.Value).ToList();

                }

                if (!filtered.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No tab group types found", 404);
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    filtered,
                    successAlert.Type,
                    $"{filtered.Count} record(s) retrieved successfully",
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

        // ─── IPDTabMaster ─────────────────────────────────────────────────────────

        public ServiceResult<object> CreateUpdateIPDTabMaster(
            CreateUpdateIPDTabMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateIPDTabMaster called. TabId={request.TabId}, TabName={request.TabName}");

                var result = _sqlHelper.DML(
                    "IU_IPDTabMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @tabId = request.TabId,
                        @groupTypeId = request.GroupTypeId,
                        @tabName = request.TabName,
                        @tabViewURL = request.TabViewURL ?? (object)DBNull.Value,
                        @sequenceNo = request.SequenceNo,
                        @tabTypeId = request.TabTypeId,
                        @tabType = request.TabType,
                        @roomTypeId = request.RoomTypeId ?? (object)DBNull.Value,
                        @isActive = request.IsActive,
                        @faIconId = request.FaIconId,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate TabViewURL: {request.TabViewURL}");
                    return ServiceResult<object>.Failure(dupAlert.Type, "Tab Name(in Same Tab Type) or Tab URL already exists", 409);
                }


                if (resultValue > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_IPDTab_All);
                    GlobalFunctions.ClearCacheByPattern(_configuration, "_RoleWiseIPDTabMapping_*");
                    GlobalFunctions.ClearCacheByPattern(_configuration, "_UserIPDTabMapping_*");

                    _log.Info($"Cleared IPDTabMaster cache. TabId={resultValue}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.TabId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    return ServiceResult<object>.Success(
                        new { tabId = resultValue },
                        alert.Type,
                        alert.Message,
                        request.TabId == 0 ? 201 : 200
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

        public ServiceResult<object> GetIPDTabMaster(
            int? tabId,
            int? groupTypeId,
            int? tabTypeId,
            int? roomTypeId,
            string tabName,
            int? isActive)
        {
            try
            {
                _log.Info($"GetIPDTabMaster called. TabId={tabId?.ToString() ?? "All"}, GroupTypeId={groupTypeId?.ToString() ?? "All"}, TabTypeId={tabTypeId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_IPDTab_All);
                List<Dictionary<string, object>> allRows;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"IPDTabMaster data retrieved from cache. Key={CACHE_KEY_IPDTab_All}");
                    allRows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"IPDTabMaster cache miss. Fetching from database.");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetIPDTabMaster",
                        CommandType.StoredProcedure
                    );

                    allRows = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allRows.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allRows);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_IPDTab_All, serialized, cacheOptions);
                        _log.Info($"IPDTabMaster cached permanently. Count={allRows.Count}");
                    }
                }

                // In-memory filtering
                var filtered = allRows;


                // Helper to safely extract int value from JsonElement or raw object
                static int? GetInt(Dictionary<string, object> row, string key)
                {
                    if (!row.TryGetValue(key, out var val) || val == null) return null;
                    if (val is System.Text.Json.JsonElement je)
                    {
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out int n)) return n;
                        if (je.ValueKind == System.Text.Json.JsonValueKind.Null) return null;
                    }
                    return Convert.ToInt32(val);
                }

                if (tabId.HasValue)
                {
                    filtered = filtered.Where(r => GetInt(r, "TabId") == tabId.Value).ToList();
                }

                if (groupTypeId.HasValue)
                {
                    filtered = filtered.Where(r => GetInt(r, "GroupTypeId") == groupTypeId.Value).ToList();

                }

                if (tabTypeId.HasValue)
                {
                    filtered = filtered.Where(r => GetInt(r, "TabTypeId") == tabTypeId.Value).ToList();

                }
                if (roomTypeId.HasValue)
                {
                    filtered = filtered.Where(r => GetInt(r, "RoomTypeId") == roomTypeId.Value).ToList();

                }
                if (isActive.HasValue)
                {
                    filtered = filtered.Where(r => GetInt(r, "IsActive") == isActive.Value).ToList();

                }

                if (!string.IsNullOrWhiteSpace(tabName))
                {
                    filtered = filtered.Where(r =>
                        r.TryGetValue("TabName", out var val) &&
                        val != null &&
                        val.ToString().Contains(tabName.Trim(), StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }


                if (!filtered.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No tab records found", 404);
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    filtered,
                    successAlert.Type,
                    $"{filtered.Count} record(s) retrieved successfully",
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


        public ServiceResult<string> SaveUpdateRoleWiseIPDTabMapping(SaveRoleWiseIPDTabMappingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                // Delete existing role-wise IPD tab mappings for this role
                var deleteResult = _sqlHelper.DML("D_DeleteRoleWiseIPDTabMapping", CommandType.StoredProcedure, new
                {
                    @RoleId = request.RoleId
                },
                new
                {
                    result = 0
                });

                _log.Info($"Deleted existing role-wise IPD tab mappings for RoleId={request.RoleId}");

                // Generate cache key for this specific role
                string cacheKey = $"_RoleWiseIPDTabMapping_{request.RoleId}";

                // Clear cache after delete
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                // If TabMappings list is empty or null, only delete operation was needed
                if (request.TabMappings == null || !request.TabMappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info("Role-wise IPD tab mappings deleted successfully. No new tabs to insert.");

                    return ServiceResult<string>.Success(
                        "Role-wise  tab mappings deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Filter out items with TabId = 0
                var validTabMappings = request.TabMappings.Where(t => t.TabId != 0).ToList();

                if (!validTabMappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info("Role-wise IPD tab mappings deleted successfully. No valid tabs to insert.");

                    return ServiceResult<string>.Success(
                        "Role-wise  tab mappings deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Insert new role-wise IPD tab mappings
                int insertedCount = 0;
                foreach (var tabMapping in validTabMappings)
                {
                    var result = _sqlHelper.DML("I_RoleWiseIPDTabMapping", CommandType.StoredProcedure, new
                    {
                        @RoleId = request.RoleId,
                        @TabId = tabMapping.TabId,
                        @IpAddress = globalValues.ipAddress,
                        @CreatedBy = globalValues.userId
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }

                _log.Info($"Inserted {insertedCount} role-wise tab mappings for RoleId={request.RoleId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"Role-wise tab mappings updated successfully. {insertedCount} tab(s) assigned.",
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetRoleWiseIPDTabListMaster(int roleId)
        {
            try
            {
                _log.Info($"GetRoleWiseIPDTabListMaster called. RoleId={roleId}");

                // Generate dynamic cache key based on roleId
                string cacheKey = $"_RoleWiseIPDTabMapping_{roleId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> tabMappings;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"RoleWiseIPDTabMapping data retrieved from cache. Key={cacheKey}");
                    tabMappings = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"RoleWiseIPDTabMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_RoleWiseIPDTabListMaster",
                        CommandType.StoredProcedure,
                        new
                        {
                            @RoleId = roleId
                        }
                    );

                    tabMappings = new List<Dictionary<string, object>>();

                    if (dataTable != null)
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            var dict = new Dictionary<string, object>();
                            foreach (DataColumn col in dataTable.Columns)
                            {
                                dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                            }
                            tabMappings.Add(dict);
                        }
                    }

                    // Store data in cache with no expiration (permanent until manually cleared)
                    if (tabMappings.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(tabMappings);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"RoleWiseIPDTabMapping data cached permanently. Key={cacheKey}, Count={tabMappings.Count}");
                    }
                }

                if (!tabMappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No role-wise IPD tab mapping found for RoleId={roleId}");

                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {tabMappings.Count} role-wise tab mapping records from cache");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    tabMappings,
                    "Info",
                    $"{tabMappings.Count} tab mapping(s) retrieved successfully",
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
        public ServiceResult<string> SaveUpdateUserIPDTabMapping(SaveUserIPDTabMappingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                // Delete existing user IPD tab mappings for this user/branch/role/type combination
                var deleteResult = _sqlHelper.DML("D_DeleteUserIPDTabMapping", CommandType.StoredProcedure, new
                {
                    @TypeId = request.TypeId,
                    @UserId = request.UserId,
                    @BranchId = request.BranchId,
                    @RoleId = request.RoleId
                },
                new
                {
                    result = 0
                });

                _log.Info($"Deleted existing IPD tab mappings for UserId={request.UserId}, BranchId={request.BranchId}, RoleId={request.RoleId}, TypeId={request.TypeId}");

                // Generate cache key for this specific IPD tab mapping
                string cacheKey = $"_UserIPDTabMapping_{request.BranchId}_{request.TypeId}_{request.UserId}_{request.RoleId}";

                // Clear cache after delete
                _distributedCache.Remove(cacheKey);
                _log.Info($"Cleared cache for key: {cacheKey}");

                // If TabMappings list is empty or null, only delete operation was needed
                if (request.TabMappings == null || !request.TabMappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info("IPD tab mappings deleted successfully. No new tabs to insert.");

                    return ServiceResult<string>.Success(
                        "Tab mappings deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Filter out items with TabId = 0
                var validTabMappings = request.TabMappings.Where(t => t.TabId != 0).ToList();

                if (!validTabMappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                    _log.Info("IPD tab mappings deleted successfully. No valid tabs to insert.");

                    return ServiceResult<string>.Success(
                        "Tab mappings deleted successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Insert new IPD tab mappings
                int insertedCount = 0;
                foreach (var tabMapping in validTabMappings)
                {
                    var result = _sqlHelper.DML("I_UserIPDTabMapping", CommandType.StoredProcedure, new
                    {
                        @TypeId = request.TypeId,
                        @UserId = request.UserId,
                        @BranchId = request.BranchId,
                        @RoleId = request.RoleId,
                        @TabId = tabMapping.TabId,
                        @IpAddress = globalValues.ipAddress,
                        @CreatedBy = globalValues.userId
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }

                _log.Info($"Inserted {insertedCount} IPD tab mappings for UserId={request.UserId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"Tab mappings updated successfully. {insertedCount} tab(s) assigned.",
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetUserGrantedRemainingTabMaster(
            int branchId,
            int typeId,
            int userId,
            int roleId)
        {
            try
            {
                _log.Info($"GetUserGrantedRemainingTabMaster called. BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

                // Generate dynamic cache key based on all parameters
                string cacheKey = $"_UserIPDTabMapping_{branchId}_{typeId}_{userId}_{roleId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> tabMappings;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserIPDTabMapping data retrieved from cache. Key={cacheKey}");
                    tabMappings = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"UserIPDTabMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_UserGrantedRemainingTabMaster",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = branchId,
                            @TypeId = typeId,
                            @UserId = userId,
                            @RoleId = roleId
                        }
                    );

                    tabMappings = new List<Dictionary<string, object>>();

                    if (dataTable != null)
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            var dict = new Dictionary<string, object>();
                            foreach (DataColumn col in dataTable.Columns)
                            {
                                dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                            }
                            tabMappings.Add(dict);
                        }
                    }

                    // Store data in cache with no expiration (permanent until manually cleared)
                    if (tabMappings.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(tabMappings);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"UserIPDTabMapping data cached permanently. Key={cacheKey}, Count={tabMappings.Count}");
                    }
                }

                if (!tabMappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No IPD tab mapping found for BranchId={branchId}, TypeId={typeId}, UserId={userId}, RoleId={roleId}");

                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {tabMappings.Count} IPD tab mapping records from cache");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    tabMappings,
                    "Info",
                    $"{tabMappings.Count} tab mapping(s) retrieved successfully",
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


        public ServiceResult<CreateUpdateApprovalAuthorityMasterResponse> CreateUpdateApprovalAuthorityMaster(
           CreateUpdateApprovalAuthorityMasterRequest request,
           AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateApprovalAuthorityMaster called. Id={request.Id}, ApprovalTypeId={request.ApprovalTypeId}, BranchId={request.BranchId}");

               var result = _sqlHelper.DML(
                   "IU_ApprovalAuthorityMaster",
                   CommandType.StoredProcedure,
                   new
                   {
                       @hospId = globalValues.hospId,
                       @branchId = request.BranchId,
                       @id = request.Id,
                       @approvalFlow = request.ApprovalFlow,
                       @approvalFlowId = request.ApprovalFlowId,
                       @isAllApprovalRequired = request.IsAllApprovalRequired,
                       @approvalTypeId = request.ApprovalTypeId,
                       @approvalType = request.ApprovalType,
                       @roleId = request.RoleId,
                       @approvalLevelId = request.ApprovalLevelId,
                       @approvalLevel = request.ApprovalLevel,
                       @level1UserId = request.Level1UserId,
                       @level2UserId = request.Level2UserId,
                       @level3UserId = request.Level3UserId,
                       @level4UserId = request.Level4UserId,
                       @amountUpTo = request.AmountUpTo,
                       @userId = globalValues.userId,
                       @IpAddress = globalValues.ipAddress
                   },
                   new { result = 0 }   
               );
               
               long resultValue = Convert.ToInt64(result);
               
                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate ApprovalAuthority found for ApprovalTypeId={request.ApprovalTypeId}, BranchId={request.BranchId}");
                    return ServiceResult<CreateUpdateApprovalAuthorityMasterResponse>.Failure(
                        dupAlert.Type,
                        "Approval Authority already exists with the same type, role and amount.",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    // Invalidate cache for this approvalTypeId
                    string cacheKey = $"{CACHE_KEY_PREFIX_ApprovalAuthority}{request.ApprovalTypeId}";
                    _distributedCache.Remove(cacheKey);
                    _log.Info($"Cleared ApprovalAuthorityMaster cache. Key={cacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.Id == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"ApprovalAuthority {(request.Id == 0 ? "created" : "updated")} successfully. Id={resultValue}");

                    return ServiceResult<CreateUpdateApprovalAuthorityMasterResponse>.Success(
                        new CreateUpdateApprovalAuthorityMasterResponse { Id = resultValue },
                        alert.Type,
                        alert.Message,
                        request.Id == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"ApprovalAuthority operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateApprovalAuthorityMasterResponse>.Failure(
                    failAlert.Type,
                    failAlert.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateApprovalAuthorityMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetApprovalAuthorityMasterList(int approvalTypeId)
        {
            try
            {
                _log.Info($"GetApprovalAuthorityMasterList called. ApprovalTypeId={approvalTypeId}");

                string cacheKey = $"{CACHE_KEY_PREFIX_ApprovalAuthority}{approvalTypeId}";

                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"ApprovalAuthorityMaster data retrieved from cache. Key={cacheKey}");
                    return ServiceResult<object>.Success(
                        System.Text.Json.JsonSerializer.Deserialize<object>(cachedData),
                        "Info",
                        "Data retrieved successfully",
                        200
                    );
                }

                _log.Info($"ApprovalAuthorityMaster cache miss. Fetching from database. Key={cacheKey}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetApprovalAuthorityMasterList",
                    CommandType.StoredProcedure,
                    new { @approvalTypeId = approvalTypeId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No ApprovalAuthority records found for ApprovalTypeId={approvalTypeId}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No approval authority records found",
                        404
                    );
                }

                // Raw DataTable → list of dictionaries (no model mapping)
                var rawData = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                // Cache permanently (invalidated on write)
                var serialized = System.Text.Json.JsonSerializer.Serialize(rawData);
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = null,
                    SlidingExpiration = null
                };
                _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                _log.Info($"ApprovalAuthorityMaster cached permanently. Key={cacheKey}, Count={rawData.Count}");

                return ServiceResult<object>.Success(
                    rawData,
                    "Info",
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

        public ServiceResult<string> UpdateApprovalAuthorityMasterStatus(int id, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateApprovalAuthorityMasterStatus called. Id={id}");

                _sqlHelper.DML(
                    "D_DeleteApprovalAuthorityMaster",
                    CommandType.StoredProcedure,
                    new { @ApprovalAuthorityId = id }
                );

                // Invalidate all ApprovalAuthority cache keys (pattern-based)
                GlobalFunctions.ClearCacheByPattern(_configuration, $"{CACHE_KEY_PREFIX_ApprovalAuthority}*");
                _log.Info($"Cleared all ApprovalAuthorityMaster cache entries.");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                _log.Info($"ApprovalAuthority status toggled successfully. Id={id}");

                return ServiceResult<string>.Success(
                    "Status updated successfully",
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


        #region Branch Corporate Ratelist Mapping

        public ServiceResult<object> SaveBranchCorporateRatelistMapping(
            SaveBranchCorporateRatelistMappingRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveBranchCorporateRatelistMapping called. BranchId={request.BranchId}, CorporateId={request.CorporateId}, Count={request.Mappings.Count}");

                // Step 1: Deactivate existing mappings via U_ SP
                _sqlHelper.DML(
                    "U_BranchCorporateRatelistMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @BranchId = request.BranchId,
                        @CorporateId = request.CorporateId,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                _log.Info($"Deactivated existing BranchCorporateRatelistMapping for BranchId={request.BranchId}, CorporateId={request.CorporateId}");

                // Step 2: Insert each new mapping via I_ SP
                if (request.Mappings != null && request.Mappings.Any())
                {
                    foreach (var item in request.Mappings)
                    {
                        _sqlHelper.DML(
                            "I_BranchCorporateRatelistMapping",
                            CommandType.StoredProcedure,
                            new
                            {
                                @BranchId = request.BranchId,
                                @CorporateId = request.CorporateId,
                                @RateListIdOPD = item.RateListIdOPD,
                                @RateListIdIPD = item.RateListIdIPD,
                                @userId = globalValues.userId,
                                @IpAddress = globalValues.ipAddress
                            }
                        );
                    }

                    _log.Info($"Inserted {request.Mappings.Count} BranchCorporateRatelistMapping record(s).");
                }

                // Invalidate cache
                _distributedCache.Remove("_BranchCorporateRatelistMapping_All");
                _distributedCache.Remove("_BranchWiseCorporate_All");

                _log.Info("Cleared BranchCorporateRatelistMapping cache.");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    new { BranchId = request.BranchId, CorporateId = request.CorporateId, InsertedCount = request.Mappings?.Count ?? 0 },
                    alert.Type,
                    alert.Message,
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

        public ServiceResult<object> GetBranchCorporateRatelistMapping(int? branchId = null, int? corporateId = null)
        {
            try
            {
                _log.Info($"GetBranchCorporateRatelistMapping called. BranchId={branchId?.ToString() ?? "All"}, CorporateId={corporateId?.ToString() ?? "All"}");
                const string cacheKey = "_BranchCorporateRatelistMapping_All";
                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allData;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"BranchCorporateRatelistMapping retrieved from cache. Key={cacheKey}");
                    allData = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"BranchCorporateRatelistMapping cache miss. Fetching from database. Key={cacheKey}");
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_BranchCorporateRatelistMapping",
                        CommandType.StoredProcedure
                    );

                    if (dataTable == null || dataTable.Rows.Count == 0)
                    {
                        var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                        return ServiceResult<object>.Failure(notFoundAlert.Type, "No branch corporate ratelist mappings found", 404);
                    }

                    allData = dataTable.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList();

                    var serialized = System.Text.Json.JsonSerializer.Serialize(allData);
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = null,
                        SlidingExpiration = null
                    };
                    _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                    _log.Info($"BranchCorporateRatelistMapping cached permanently. Count={allData.Count}");
                }

                // Filter in memory from cache
                var filteredData = allData.AsEnumerable();

                if (branchId.HasValue)
                {
                    _log.Info($"Filtering by BranchId: {branchId.Value}");
                    filteredData = filteredData.Where(row =>
                        row.TryGetValue("BranchId", out var val) &&
                        val is System.Text.Json.JsonElement je &&
                        je.TryGetInt32(out int id) &&
                        id == branchId.Value
                    );
                }

                if (corporateId.HasValue)
                {
                    _log.Info($"Filtering by CorporateId: {corporateId.Value}");
                    filteredData = filteredData.Where(row =>
                        row.TryGetValue("CorporateId", out var val) &&
                        val is System.Text.Json.JsonElement je &&
                        je.TryGetInt32(out int id) &&
                        id == corporateId.Value
                    );
                }

                var result = filteredData.ToList();

                if (!result.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No records found after filtering.");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No records found for the given filters", 404);
                }

                _log.Info($"Returning {result.Count} record(s) after filtering.");
                return ServiceResult<object>.Success(
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
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }
        #endregion

        #region Branch Corporate Wise Service Exclusion Mapping

        public ServiceResult<object> SaveBranchCorporateServiceExclusionMapping(
            SaveBranchCorporateServiceExclusionRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveBranchCorporateServiceExclusionMapping called. BranchId={request.BranchId}, CorporateId={request.CorporateId}, Count={request.ServiceItemIds.Count}");

                // Step 1: Deactivate existing exclusions via U_ SP
                _sqlHelper.DML(
                    "U_BranchCorporateWiseServiceExclusionMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @BranchId = request.BranchId,
                        @CorporateId = request.CorporateId,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                _log.Info($"Deactivated existing BranchCorporateWiseServiceExclusionMapping for BranchId={request.BranchId}, CorporateId={request.CorporateId}");

                // Step 2: Insert each new exclusion via I_ SP
                if (request.ServiceItemIds != null && request.ServiceItemIds.Any())
                {
                    foreach (var serviceItemId in request.ServiceItemIds)
                    {
                        _sqlHelper.DML(
                            "I_BranchCorporateWiseServiceExclusionMapping",
                            CommandType.StoredProcedure,
                            new
                            {
                                @BranchId = request.BranchId,
                                @CorporateId = request.CorporateId,
                                @ServiceItemId = serviceItemId,
                                @userId = globalValues.userId,
                                @IpAddress = globalValues.ipAddress
                            }
                        );
                    }

                    _log.Info($"Inserted {request.ServiceItemIds.Count} BranchCorporateWiseServiceExclusionMapping record(s).");
                }

                // Invalidate cache
                _distributedCache.Remove("_BranchCorporateServiceExclusion_All");
                _log.Info("Cleared BranchCorporateServiceExclusion cache.");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    new { BranchId = request.BranchId, CorporateId = request.CorporateId, InsertedCount = request.ServiceItemIds?.Count ?? 0 },
                    alert.Type,
                    alert.Message,
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

        public ServiceResult<object> GetBranchCorporateServiceExclusionMapping(int? branchId = null, int? corporateId = null)
        {
            try
            {
                _log.Info($"GetBranchCorporateServiceExclusionMapping called. BranchId={branchId?.ToString() ?? "All"}, CorporateId={corporateId?.ToString() ?? "All"}");
                const string cacheKey = "_BranchCorporateServiceExclusion_All";
                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> allData;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"BranchCorporateServiceExclusion retrieved from cache. Key={cacheKey}");
                    allData = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"BranchCorporateServiceExclusion cache miss. Fetching from database. Key={cacheKey}");
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_BranchCorporateWiseServiceExclusionMapping",
                        CommandType.StoredProcedure
                    );

                    if (dataTable == null || dataTable.Rows.Count == 0)
                    {
                        var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                        return ServiceResult<object>.Failure(notFoundAlert.Type, "No branch corporate service exclusion mappings found", 404);
                    }

                    allData = dataTable.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList();

                    var serialized = System.Text.Json.JsonSerializer.Serialize(allData);
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = null,
                        SlidingExpiration = null
                    };
                    _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                    _log.Info($"BranchCorporateServiceExclusion cached permanently. Count={allData.Count}");
                }

                // Filter in memory from cache
                var filteredData = allData.AsEnumerable();

                if (branchId.HasValue)
                {
                    _log.Info($"Filtering by BranchId: {branchId.Value}");
                    filteredData = filteredData.Where(row =>
                        row.TryGetValue("BranchId", out var val) &&
                        val != null &&
                        Convert.ToInt32(((System.Text.Json.JsonElement)val).GetRawText()) == branchId.Value
                    );
                }

                if (corporateId.HasValue)
                {
                    _log.Info($"Filtering by CorporateId: {corporateId.Value}");
                    filteredData = filteredData.Where(row =>
                        row.TryGetValue("CorporateId", out var val) &&
                        val != null &&
                        Convert.ToInt32(((System.Text.Json.JsonElement)val).GetRawText()) == corporateId.Value
                    );
                }

                var result = filteredData.ToList();

                if (!result.Any())
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No records found after filtering.");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No records found for the given filters", 404);
                }

                _log.Info($"Returning {result.Count} record(s) after filtering.");
                return ServiceResult<object>.Success(
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
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        #endregion

        #region Branch Right Mapping

        public ServiceResult<object> SaveBranchRightMapping(
            SaveBranchRightMappingRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveBranchRightMapping called. BranchId={request.BranchId}, RightCount={request.BranchRightIds.Count}");

                // Step 1: Delete existing mappings via D_ SP (hard delete per SP definition)
                _sqlHelper.DML(
                    "D_BranchRightMapping",
                    CommandType.StoredProcedure,
                    new { @BranchId = request.BranchId }
                );

                _log.Info($"Deleted existing BranchRightMapping for BranchId={request.BranchId}");

                // Step 2: Insert each right via I_ SP
                if (request.BranchRightIds != null && request.BranchRightIds.Any())
                {
                    foreach (var branchRightId in request.BranchRightIds)
                    {
                        _sqlHelper.DML(
                            "I_BranchRightMapping",
                            CommandType.StoredProcedure,
                            new
                            {
                                @BranchId = request.BranchId,
                                @BranchRightId = branchRightId,
                                @userId = globalValues.userId,
                                @IpAddress = globalValues.ipAddress
                            }
                        );
                    }

                    _log.Info($"Inserted {request.BranchRightIds.Count} BranchRightMapping record(s).");
                }

                // Invalidate cache
                _distributedCache.Remove($"_BranchRightMapping_{request.BranchId}");
                _distributedCache.Remove($"_AssignBranchRight_{request.BranchId}");
                _log.Info("Cleared BranchRightMapping cache.");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    new { BranchId = request.BranchId, InsertedCount = request.BranchRightIds?.Count ?? 0 },
                    alert.Type,
                    alert.Message,
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

        public ServiceResult<object> GetBranchRightMapping(int branchId)
        {
            try
            {
                _log.Info("GetBranchRightMapping called.");

                string cacheKey = $"_BranchRightMapping_{branchId}";

                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"BranchRightMapping retrieved from cache. Key={cacheKey}");
                    return ServiceResult<object>.Success(
                        System.Text.Json.JsonSerializer.Deserialize<object>(cachedData),
                        "Info",
                        "Data retrieved successfully",
                        200
                    );
                }

                _log.Info($"BranchRightMapping cache miss. Fetching from database. Key={cacheKey}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_getAssignBranchRight",
                     CommandType.StoredProcedure,
                    new { @BranchId = branchId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No branch right mappings found", 404);
                }

                var rawData = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                var serialized = System.Text.Json.JsonSerializer.Serialize(rawData);
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = null,
                    SlidingExpiration = null
                };
                _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                _log.Info($"BranchRightMapping cached permanently. Count={rawData.Count}");

                return ServiceResult<object>.Success(
                    rawData,
                    "Info",
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

        public ServiceResult<string> UpdateDefaultBranchSetting(UpdateDefaultBranchSettingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateDefaultBranchSetting called. BranchId={request.BranchId}");

                var result = _sqlHelper.DML(
                    "U_DefaultBranchSetting",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = request.BranchId,
                        @defaultCountryId = request.DefaultCountryId,
                        @defaultStateId = request.DefaultStateId,
                        @defaultDistrictId = request.DefaultDistrictId,
                        @defaultCityId = request.DefaultCityId,
                        @defaultInsuranceCompanyId = request.DefaultInsuranceCompanyId,
                        @defaultCorporateId = request.DefaultCorporateId,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    }
                );

                // Clear branch cache
                _distributedCache.Remove("_BranchMaster_All");
                _log.Info($"Cleared BranchMaster cache after default settings update. BranchId={request.BranchId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Default branch settings updated successfully",
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

        #endregion



        private const string CACHE_KEY_VitalMaster_All = "_VitalMaster_All";
        private const string CACHE_KEY_VitalUnitMaster_All = "_VitalUnitMaster_All";

        public ServiceResult<object> GetVitalMasterList(int? isActive)
        {
            try
            {
                _log.Info($"GetVitalMasterList called. IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_VitalMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"VitalMaster data retrieved from cache. Key={CACHE_KEY_VitalMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"VitalMaster cache miss. Fetching from database. Key={CACHE_KEY_VitalMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetVitalMasterList",
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
                        _distributedCache.SetString(CACHE_KEY_VitalMaster_All, serialized, cacheOptions);
                        _log.Info($"VitalMaster data cached permanently. Key={CACHE_KEY_VitalMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by IsActive
                if (isActive.HasValue)
                {
                    allItems = allItems.Where(row =>
                    {
                        if (row.TryGetValue("IsActive", out var val) && val != null)
                        {
                            return val.ToString() == isActive.Value.ToString();
                        }
                        return false;
                    }).ToList();
                    _log.Info($"Filtered by IsActive={isActive.Value}. Count={allItems.Count}");
                }

                if (!allItems.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No vital records found", 404);
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    successAlert.Type,
                    $"{allItems.Count} vital record(s) retrieved successfully",
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

        public ServiceResult<object> CreateUpdateVitalMaster(
            CreateUpdateVitalMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateVitalMaster called. VitalId={request.VitalId}, VitalName={request.VitalName}");

                var parameters = new SqlParameter[]
                {
            new SqlParameter("@vitalId",   SqlDbType.Int)          { Value = request.VitalId },
            new SqlParameter("@vitalName", SqlDbType.NVarChar, 256){ Value = request.VitalName },
            new SqlParameter("@unitID",    SqlDbType.Int)          { Value = request.UnitId },
            new SqlParameter("@unitName",  SqlDbType.NVarChar, 256){ Value = request.UnitName ?? (object)DBNull.Value },
            new SqlParameter("@minValue",  SqlDbType.NVarChar, 256){ Value = request.MinValue ?? (object)DBNull.Value },
            new SqlParameter("@maxValue",  SqlDbType.NVarChar, 256){ Value = request.MaxValue ?? (object)DBNull.Value },
            new SqlParameter("@snomedCode",  SqlDbType.NVarChar, 256){ Value = request.snomedCode ?? (object)DBNull.Value },
            new SqlParameter("@active",    SqlDbType.Int)          { Value = request.Active },
            new SqlParameter("@isMandatory",    SqlDbType.Int)          { Value = request.IsMandatory },
            new SqlParameter("@isBodyMeasurement",    SqlDbType.Int)          { Value = request.IsBodyMeasurement },
            new SqlParameter("@userId",    SqlDbType.Int)          { Value = globalValues.userId },
            new SqlParameter("@IpAddress", SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
            new SqlParameter("@Result",    SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_VitalMaster", parameters);

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate VitalName: {request.VitalName}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Vital name already exists",
                        409
                    );
                }

                if (result > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_VitalMaster_All);
                    _log.Info($"Cleared VitalMaster cache. VitalId={result}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.VitalId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    return ServiceResult<object>.Success(
                        new { VitalId = result },
                        alert.Type,
                        alert.Message,
                        request.VitalId == 0 ? 201 : 200
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

        public ServiceResult<object> GetVitalUnitMasterList()
        {
            try
            {
                _log.Info("GetVitalUnitMasterList called.");

                var cachedData = _distributedCache.GetString(CACHE_KEY_VitalUnitMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"VitalUnitMaster data retrieved from cache. Key={CACHE_KEY_VitalUnitMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"VitalUnitMaster cache miss. Fetching from database. Key={CACHE_KEY_VitalUnitMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetVitalUnitMasterList",
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
                        _distributedCache.SetString(CACHE_KEY_VitalUnitMaster_All, serialized, cacheOptions);
                        _log.Info($"VitalUnitMaster data cached permanently. Key={CACHE_KEY_VitalUnitMaster_All}, Count={allItems.Count}");
                    }
                }

                if (!allItems.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No vital unit records found", 404);
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    successAlert.Type,
                    $"{allItems.Count} vital unit record(s) retrieved successfully",
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

        public ServiceResult<object> CreateUpdateVitalUnitMaster(
            CreateUpdateVitalUnitMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateVitalUnitMaster called. Id={request.Id}, UnitName={request.UnitName}");

                var parameters = new SqlParameter[]
                {
            new SqlParameter("@Id",        SqlDbType.Int)          { Value = request.Id },
            new SqlParameter("@unitName",  SqlDbType.NVarChar, 256){ Value = request.UnitName },
            new SqlParameter("@userId",    SqlDbType.Int)          { Value = globalValues.userId },
            new SqlParameter("@IpAddress", SqlDbType.NVarChar, 20) { Value = globalValues.ipAddress ?? (object)DBNull.Value },
            new SqlParameter("@Result",    SqlDbType.Int)          { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("IU_VitalUnitMaster", parameters);

                if (result == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate UnitName: {request.UnitName}");
                    return ServiceResult<object>.Failure(
                        dupAlert.Type,
                        "Vital unit name already exists",
                        409
                    );
                }

                if (result > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_VitalUnitMaster_All);
                    _log.Info($"Cleared VitalUnitMaster cache. Id={result}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.Id == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );
                    return ServiceResult<object>.Success(
                        new { Id = result },
                        alert.Type,
                        alert.Message,
                        request.Id == 0 ? 201 : 200
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


        public ServiceResult<object> GetVitalDepartmentMapping(int typeId, int relatedToId)
        {
            try
            {
                _log.Info($"GetVitalDepartmentMapping called. TypeId={typeId}, RelatedToId={relatedToId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetVitalDepartmentMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @typeId = typeId,
                        @relatedToId = relatedToId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No vital department mapping found for TypeId={typeId}, RelatedToId={relatedToId}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No vital department mapping found",
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

                _log.Info($"GetVitalDepartmentMapping retrieved {rawData.Count} record(s)");

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

        public ServiceResult<string> SaveVitalDepartmentMapping(
            SaveVitalDepartmentMappingRequest request,
            AllGlobalValues globalValues)
        {
            SqlConnection con = null;
            SqlTransaction tnx = null;
            try
            {
                _log.Info($"SaveVitalDepartmentMapping called. TypeId={request.TypeId}, RelatedToId={request.RelatedToId}, Items={request.HeaderMappingData?.Count ?? 0}");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException("Connection string 'ConnectionString' not found.");

                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                // Step 1 – Delete existing mappings
                _sqlHelper.DML(tnx, "D_DeleteVitalDepartmentMapping", CommandType.StoredProcedure, new
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
                        _sqlHelper.DML(tnx, "I_VitalDepartmentMapping", CommandType.StoredProcedure, new
                        {
                            @hospId = globalValues.hospId,
                            @typeId = request.TypeId,
                            @typeName = request.TypeName ?? string.Empty,
                            @vitalId = item.vitalId,
                            @retatedToId = request.RelatedToId,
                            @sequenceNo = item.SequenceNo,
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        });
                        insertedCount++;
                    }
                }

                tnx.Commit();
                _log.Info($"SaveVitalDepartmentMapping committed. Inserted={insertedCount}");

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

        public ServiceResult<CreateUpdatePackageMasterResponse> CreateUpdatePackageMaster(
    CreateUpdatePackageMasterRequest request,
    AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"CreateUpdatePackageMaster called. PackageId={request.PackageId}, Name={request.Name}");

                // ── Parse validity dates ──────────────────────────────────────────
                if (!DateTime.TryParse(request.ValidityStartsFrom, out DateTime validityStartsFrom))
                {
                    tnx.Rollback();
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<CreateUpdatePackageMasterResponse>.Failure(
                        alertDate.Type, "Invalid ValidityStartsFrom format", 400);
                }

                if (!DateTime.TryParse(request.ValidityEndsOn, out DateTime validityEndsOn))
                {
                    tnx.Rollback();
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<CreateUpdatePackageMasterResponse>.Failure(
                        alertDate.Type, "Invalid ValidityEndsOn format", 400);
                }

                // ── 1. IU_ServiceItemMaster (shared SP used across multiple masters) ──
                var result = _sqlHelper.DML(
                    tnx,
                    "IU_ServiceItemMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @hospId = globalValues.hospId,
                        @serviceItemId = request.PackageId,
                        @categoryId = request.CategoryId,
                        @subCategoryId = request.SubCategoryId,
                        @subSubCategoryId = request.SubSubCategoryId,
                        @name = request.Name,
                        @code = (object)request.Code ?? DBNull.Value,
                        @validityStartsFrom = validityStartsFrom.ToString("yyyy-MM-dd"),
                        @validityEndsOn = validityEndsOn.ToString("yyyy-MM-dd"),
                        @isMultipleVisitAllow = (object)request.IsMultipleVisitAllow ?? DBNull.Value,
                        @visitDuration = (object)request.VisitDuration ?? DBNull.Value,
                        @visitDurationType = (object)request.VisitDurationType ?? DBNull.Value,
                        @isActive = request.IsActive,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int packageId = Convert.ToInt32(result);

                if (packageId < 0)
                {
                    tnx.Rollback();
                    var alertDup = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Package Name/Code already exists. Name={request.Name}, Code={request.Code}");
                    return ServiceResult<CreateUpdatePackageMasterResponse>.Failure(
                        alertDup.Type,
                        "Package Name (in same Sub Sub Category) or Code already exists",
                        409
                    );
                }

                // ── 2. Delete-then-insert package service mapping ─────────────────
                _sqlHelper.DML(
                    tnx,
                    "D_PackageServiceMapping",
                    CommandType.StoredProcedure,
                    new { @packageId = packageId }
                );

                foreach (var item in request.PackageServices)
                {
                    _sqlHelper.DML(
                        tnx,
                        "IU_PackageServiceMapping",
                        CommandType.StoredProcedure,
                        new
                        {
                            @packageId = packageId,
                            @serviceItemId = item.ServiceItemId,
                            @qty = item.Qty,
                            @userId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        }
                    );
                }

                tnx.Commit();
                _log.Info($"Package master saved successfully. PackageId={packageId}, ServicesCount={request.PackageServices.Count}");

                // ── Invalidate cache AFTER successful DB write ─────────────────────
                _distributedCache.Remove("_ServiceItemMaster_All");
                _log.Info("Cleared ServiceItemMaster cache after package master save.");

                var responseData = new CreateUpdatePackageMasterResponse { PackageId = packageId };
                var alert = _messageService.GetMessageAndTypeByAlertCode(
                    request.PackageId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                );

                return ServiceResult<CreateUpdatePackageMasterResponse>.Success(
                    responseData,
                    alert.Type,
                    request.PackageId == 0 ? "Package saved successfully" : "Package updated successfully",
                    request.PackageId == 0 ? 201 : 200
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdatePackageMasterResponse>.Failure(
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

        public ServiceResult<string> UpdateNavigationSubMenuSequenceNo(
    UpdateNavigationSubMenuSequenceRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateNavigationSubMenuSequenceNo called. Count={request.SubMenus?.Count ?? 0}");

                foreach (var item in request.SubMenus)
                {
                    _sqlHelper.DML(
                        "U_NavigationSubMenuSequenceNo",
                        CommandType.StoredProcedure,
                        new
                        {
                            @SubMenuId = item.SubMenuId,
                            @SequenceNo = item.SequenceNo
                        }
                    );
                }

                _log.Info($"UpdateNavigationSubMenuSequenceNo completed. Updated {request.SubMenus.Count} record(s)");


                GlobalFunctions.ClearCacheByPattern(_configuration, "_RoleWiseMenuMapping_*");
                GlobalFunctions.ClearCacheByPattern(_configuration, "_UserWiseMenuMapping_*");
                GlobalFunctions.ClearCacheByPattern(_configuration, "_UserTabMenu_*");


                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{request.SubMenus.Count} sub menu sequence(s) updated successfully",
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

        public ServiceResult<string> UpdateNavigationTabSequenceNo(
            UpdateNavigationTabSequenceRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateNavigationTabSequenceNo called. Count={request.Tabs?.Count ?? 0}");

                foreach (var item in request.Tabs)
                {
                    _sqlHelper.DML(
                        "U_NavigationTabSequenceNo",
                        CommandType.StoredProcedure,
                        new
                        {
                            @TabId = item.TabId,
                            @SequenceNo = item.SequenceNo
                        }
                    );
                }

                _log.Info($"UpdateNavigationTabSequenceNo completed. Updated {request.Tabs.Count} record(s)");

                GlobalFunctions.ClearCacheByPattern(_configuration, "_UserTabMenu_*");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"{request.Tabs.Count} tab sequence(s) updated successfully",
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

        private const string CACHE_KEY_SurgeryComponentMaster_All = "_SurgeryComponentMaster_All";

        public ServiceResult<CreateUpdateSurgeryComponentMasterResponse> CreateUpdateSurgeryComponentMaster(
            CreateUpdateSurgeryComponentMasterRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSurgeryComponentMaster called. ComponentId={request.ComponentId}, ComponentName={request.ComponentName}");

                // IU_SurgeryComponentMaster ends with an unconditional trailing SELECT @Result;,
                // so ExecuteScalar is the correct helper here (not RunProcedureInsert).
                var result = _sqlHelper.ExecuteScalar(
                    "IU_SurgeryComponentMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @hospId = globalValues.hospId,
                        @componentId = request.ComponentId,
                        @componentName = request.ComponentName,
                        @hasDoctor = request.HasDoctor,
                        @isBaseComponent = request.IsBaseComponent,
                        @sharePercentage = request.SharePercentage,
                        @isActive = request.IsActive,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress ?? (object)DBNull.Value
                    }
                );

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate ComponentName: {request.ComponentName}");
                    return ServiceResult<CreateUpdateSurgeryComponentMasterResponse>.Failure(
                        dupAlert.Type,
                        "Component name already exists",
                        409
                    );
                }

                if (resultValue == -2)
                {
                    var conflictAlert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("Another active base component already exists.");
                    return ServiceResult<CreateUpdateSurgeryComponentMasterResponse>.Failure(
                        conflictAlert.Type,
                        "An active base component already exists. Only one base component is allowed.",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_SurgeryComponentMaster_All);
                    _log.Info($"Cleared SurgeryComponentMaster cache. ComponentId={resultValue}");

                    var responseData = new CreateUpdateSurgeryComponentMasterResponse { ComponentId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.ComponentId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"SurgeryComponent {(request.ComponentId == 0 ? "created" : "updated")} successfully. ComponentId={resultValue}");

                    return ServiceResult<CreateUpdateSurgeryComponentMasterResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.ComponentId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                _log.Error($"SurgeryComponentMaster operation failed with result: {resultValue}");
                return ServiceResult<CreateUpdateSurgeryComponentMasterResponse>.Failure(
                    failAlert.Type,
                    failAlert.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSurgeryComponentMasterResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> GetSurgeryComponentsList(int? isActive)
        {
            try
            {
                _log.Info($"GetSurgeryComponentsList called. IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_SurgeryComponentMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"SurgeryComponentMaster data retrieved from cache. Key={CACHE_KEY_SurgeryComponentMaster_All}");
                    allItems = System.Text.Json.JsonSerializer
                        .Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"SurgeryComponentMaster cache miss. Fetching from database. Key={CACHE_KEY_SurgeryComponentMaster_All}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetSurgeryComponentsList",
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
                        _distributedCache.SetString(CACHE_KEY_SurgeryComponentMaster_All, serialized, cacheOptions);
                        _log.Info($"SurgeryComponentMaster data cached permanently. Key={CACHE_KEY_SurgeryComponentMaster_All}, Count={allItems.Count}");
                    }
                }

                // In-memory filter by IsActive; null or blank = return all
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
                    _log.Info($"No surgery components found for IsActive: {isActive?.ToString() ?? "All"}");
                    return ServiceResult<object>.Failure(
                        notFoundAlert.Type,
                        "No surgery components found",
                        404
                    );
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    allItems,
                    alert.Type,
                    $"{allItems.Count} surgery component(s) retrieved successfully",
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

        private const string CACHE_KEY_DischargeProcessMaster_All = "_DischargeProcessMaster_All";

        public ServiceResult<object> GetDischargeProcessMaster(int? isActive)
        {
            try
            {
                _log.Info($"GetDischargeProcessMaster called. IsActive={isActive?.ToString() ?? "All"}");

                var cachedData = _distributedCache.GetString(CACHE_KEY_DischargeProcessMaster_All);
                List<Dictionary<string, object>> allItems;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    allItems = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    var dataTable = _sqlHelper.GetDataTable("S_DischargeProcessMaster", CommandType.StoredProcedure);
                    allItems = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    if (allItems.Any())
                    {
                        var serialized = JsonSerializer.Serialize(allItems);
                        var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpiration = null, SlidingExpiration = null };
                        _distributedCache.SetString(CACHE_KEY_DischargeProcessMaster_All, serialized, cacheOptions);
                        _log.Info($"DischargeProcessMaster cached permanently. Count={allItems.Count}");
                    }
                }

                if (isActive.HasValue)
                {
                    allItems = allItems.Where(row =>
                        row.TryGetValue("IsActive", out var val) && val != null && val.ToString() == isActive.Value.ToString()
                    ).ToList();
                }

                if (!allItems.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No discharge processes found", 404);
                }

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(allItems, success.Type, $"{allItems.Count} discharge process(es) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetDischargeProcessMasterById(int dischargeProcessId)
        {
            try
            {
                _log.Info($"GetDischargeProcessMasterById called. DischargeProcessId={dischargeProcessId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_DischargeProcessMasterById",
                    CommandType.StoredProcedure,
                    new { @DischargeProcessId = dischargeProcessId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "Discharge process not found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).First();

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, success.Type, "Discharge process retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<CreateUpdateDischargeProcessMasterResponse> CreateUpdateDischargeProcessMaster(
            CreateUpdateDischargeProcessMasterRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateDischargeProcessMaster called. DischargeProcessId={request.DischargeProcessId}, ProcessKey={request.ProcessKey}");

                long result = _sqlHelper.RunProcedureInsert(
                    "IU_DischargeProcessMaster",
                    new IDataParameter[]
                    {
                        new SqlParameter("@DischargeProcessId", request.DischargeProcessId),
                        new SqlParameter("@ProcessKey", request.ProcessKey),
                        new SqlParameter("@ProcessName", request.ProcessName),
                        new SqlParameter("@IsMandatory", request.IsMandatory),
                        new SqlParameter("@FaIconId ", request.FaIconId),
                        new SqlParameter("@IsActive", request.IsActive),
                        new SqlParameter("@IsSystemProcess", request.IsSystemProcess),
                        new SqlParameter("@UserId", globalValues.userId),
                        new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                        new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    return ServiceResult<CreateUpdateDischargeProcessMasterResponse>.Failure(
                        dupAlert.Type, "ProcessKey already exists", 409);
                }

                if (resultValue == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<CreateUpdateDischargeProcessMasterResponse>.Failure(
                        alert.Type, "Discharge process not found for update", 404);
                }

                if (resultValue > 0)
                {
                    _distributedCache.Remove(CACHE_KEY_DischargeProcessMaster_All);
                    GlobalFunctions.ClearCacheByPattern(_configuration, "_UserDischargeProcessMapping_*");

                    _log.Info($"Cleared DischargeProcessMaster cache. DischargeProcessId={resultValue}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.DischargeProcessId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");

                    return ServiceResult<CreateUpdateDischargeProcessMasterResponse>.Success(
                        new CreateUpdateDischargeProcessMasterResponse { DischargeProcessId = resultValue },
                        alert.Type, alert.Message, request.DischargeProcessId == 0 ? 201 : 200);
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateDischargeProcessMasterResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateDischargeProcessMasterResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UpdateDischargeProcessSequence(
            UpdateDischargeProcessSequenceRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateDischargeProcessSequence called. Count={request.Sequences.Count}");

                foreach (var item in request.Sequences)
                {
                    _sqlHelper.DML(
                        "U_DischargeProcessSequence",
                        CommandType.StoredProcedure,
                        new
                        {
                            @DischargeProcessId = item.DischargeProcessId,
                            @SequenceNo = item.SequenceNo,
                            @UserId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        });
                }

                _distributedCache.Remove(CACHE_KEY_DischargeProcessMaster_All);
                _log.Info("Cleared DischargeProcessMaster cache after sequence update.");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success("Sequence updated successfully", alert.Type, alert.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ─── Corporate Mapping ─────────────────────────────────────────────

        public ServiceResult<string> SaveDischargeProcessCorporateMapping(
            SaveDischargeProcessCorporateMappingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveDischargeProcessCorporateMapping called. DischargeProcessId={request.DischargeProcessId}, Count={request.CorporateIds.Count}");

                _sqlHelper.DML(
                    "D_DischargeProcessCorporateMapping",
                    CommandType.StoredProcedure,
                    new { @DischargeProcessId = request.DischargeProcessId });

                foreach (var corporateId in request.CorporateIds)
                {
                    _sqlHelper.DML(
                        "I_DischargeProcessCorporateMapping",
                        CommandType.StoredProcedure,
                        new
                        {
                            @DischargeProcessId = request.DischargeProcessId,
                            @CorporateId = corporateId,
                            @UserId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        });
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success("Corporate mapping saved successfully", alert.Type, alert.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetDischargeProcessCorporateMapping(int? dischargeProcessId)
        {
            try
            {
                _log.Info($"GetDischargeProcessCorporateMapping called. DischargeProcessId={dischargeProcessId?.ToString() ?? "All"}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_DischargeProcessCorporateMapping",
                    CommandType.StoredProcedure,
                    new { @DischargeProcessId = dischargeProcessId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No corporate mappings found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, success.Type, $"{result.Count} mapping(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }



        public ServiceResult<string> SaveUpdateUserDischargeProcessMapping(
    SaveUserDischargeProcessMappingRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveUpdateUserDischargeProcessMapping called. TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}, IsFirst={request.IsFirst}");

                // Delete existing user discharge process mappings if IsFirst = 1
                if (request.IsFirst == 1)
                {
                    _sqlHelper.DML("D_DeleteUserDischargeProcessMapping", CommandType.StoredProcedure, new
                    {
                        @UserId = request.UserId,
                        @TypeId = request.TypeId,
                        @BranchId = request.BranchId
                    },
                    new
                    {
                        result = 0
                    });

                    _log.Info($"Deleted existing user discharge process mapping for TypeId={request.TypeId}, UserId={request.UserId}, BranchId={request.BranchId}");
                }

                string clearCacheKey = $"_UserDischargeProcessMapping_{request.BranchId}_{request.TypeId}_{request.UserId}";

                // If mapping list is empty or null, only delete operation was needed
                if (request.UserDischargeProcessMappings == null || !request.UserDischargeProcessMappings.Any())
                {
                    _distributedCache.Remove(clearCacheKey);
                    _log.Info($"Cleared cache for key: {clearCacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "User discharge process mappings deleted successfully" : "No discharge process mappings to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Filter out items with DischargeProcessId = 0
                var validMappings = request.UserDischargeProcessMappings.Where(m => m.DischargeProcessId != 0).ToList();

                if (!validMappings.Any())
                {
                    _distributedCache.Remove(clearCacheKey);
                    _log.Info($"Cleared cache for key: {clearCacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.IsFirst == 1 ? "DATA_DELETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY"
                    );

                    return ServiceResult<string>.Success(
                        request.IsFirst == 1 ? "User discharge process mappings deleted successfully" : "No valid discharge process mappings to save",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }

                // Validate consistency of all items with parent request
                bool isConsistent = validMappings.All(x =>
                    x.TypeId == request.TypeId &&
                    x.UserId == request.UserId &&
                    x.BranchId == request.BranchId);

                if (!isConsistent)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("Inconsistent TypeId, UserId, or BranchId in user discharge process mapping list.");

                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "All user discharge process mapping items must have the same TypeId, UserId, and BranchId as the request",
                        400
                    );
                }

                // Insert new user discharge process mappings
                int insertedCount = 0;
                foreach (var mapping in validMappings)
                {
                    var result = _sqlHelper.DML("IU_UserDischargeProcessMapping", CommandType.StoredProcedure, new
                    {
                        @HospId = globalValues.hospId,
                        @UserId = mapping.UserId,
                        @TypeId = mapping.TypeId,
                        @BranchId = mapping.BranchId,
                        @DischargeProcessId = mapping.DischargeProcessId,
                        @CreatedBy = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new
                    {
                        result = 0
                    });

                    if (result > 0)
                    {
                        insertedCount++;
                    }
                }

                _distributedCache.Remove(clearCacheKey);
                _log.Info($"Cleared cache for key: {clearCacheKey}");
                _log.Info($"Inserted {insertedCount} user discharge process mapping records for UserId={request.UserId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    $"User discharge process mapping updated successfully. {insertedCount} process(es) assigned.",
                    alert1.Type,
                    alert1.Message,
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

        public ServiceResult<object> GetUserWiseDischargeProcessMapping(
            int branchId,
            int typeId,
            int userId)
        {
            try
            {
                _log.Info($"GetUserWiseDischargeProcessMapping called. BranchId={branchId}, TypeId={typeId}, UserId={userId}");

                // Generate dynamic cache key based on branchId, typeId, and userId
                string cacheKey = $"_UserDischargeProcessMapping_{branchId}_{typeId}_{userId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<Dictionary<string, object>> mappings;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserDischargeProcessMapping data retrieved from cache. Key={cacheKey}");
                    mappings = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"UserDischargeProcessMapping cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetRemainingAssignDischargeProcessForUserAuthorization",
                        CommandType.StoredProcedure,
                        new
                        {
                            @BranchId = branchId,
                            @typeId = typeId,
                            @UserId = userId
                        }
                    );

                    // Raw DataTable -> List<Dictionary<string,object>> (no model mapping),
                    // so any new columns added to the SP automatically flow through
                    mappings = dataTable?.AsEnumerable().Select(row =>
                        dataTable.Columns.Cast<DataColumn>().ToDictionary(
                            col => col.ColumnName,
                            col => row[col] == DBNull.Value ? null : row[col]
                        )
                    ).ToList() ?? new List<Dictionary<string, object>>();

                    // Store data in cache with no expiration
                    if (mappings.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(mappings);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"UserDischargeProcessMapping data cached permanently. Key={cacheKey}, Count={mappings.Count}");
                    }
                }

                if (!mappings.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No user discharge process mapping found for BranchId={branchId}, TypeId={typeId}, UserId={userId}");

                    return ServiceResult<object>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {mappings.Count} user discharge process mapping records (Granted + Remaining) from cache");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    mappings,
                    alert1.Type,
                    $"{mappings.Count} user discharge process mapping(s) retrieved successfully",
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

    }
}
