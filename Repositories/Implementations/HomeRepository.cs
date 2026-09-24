using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using HISWEBAPI.Services.Implementations;
using HISWEBAPI.Services.Interfaces;
using HISWEBAPI.Utilities;
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace HISWEBAPI.Repositories.Implementations
{
    public class HomeRepository : IHomeRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private readonly ISmsService _smsService;
        private readonly IEmailService _emailService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public HomeRepository(
            ICustomSqlHelper sqlHelper,
            IResponseMessageService messageService,
            IDistributedCache distributedCache,
            IConfiguration configuration,
            ISmsService smsService,
            IEmailService emailService)
        {
            _sqlHelper = sqlHelper;
            _messageService = messageService;
            _distributedCache = distributedCache;
            _configuration = configuration;
            _smsService = smsService;
            _emailService = emailService;
        }

        public ServiceResult<string> ClearAllCache()
        {
            try
            {
                _log.Info("ClearAllCache called - Attempting to clear all Redis cache");

                // Get Redis connection string from configuration
                var redisConnection = _configuration.GetValue<string>("Redis:Configuration") ?? "localhost:6379";

                using (var redis = ConnectionMultiplexer.Connect(redisConnection))
                {
                    var server = redis.GetServer(redis.GetEndPoints().First());
                    var db = redis.GetDatabase();

                    // Get all keys from Redis
                    var keys = server.Keys(pattern: "*").ToList();

                    if (!keys.Any())
                    {
                        _log.Info("No cache keys found in Redis");
                        var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                        return ServiceResult<string>.Success(
                            "No cache entries found to clear",
                            alert.Type,
                            "No cache entries found",
                            200
                        );
                    }

                    int clearedCount = 0;
                    foreach (var key in keys)
                    {
                        try
                        {
                            db.KeyDelete(key);
                            _log.Info($"Cleared cache key: {key}");
                            clearedCount++;
                        }
                        catch (Exception ex)
                        {
                            _log.Warn($"Failed to clear cache key '{key}': {ex.Message}");
                        }
                    }

                    _log.Info($"Total {clearedCount} cache entries cleared out of {keys.Count}");

                    var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        $"{clearedCount} cache entries cleared successfully",
                        alert1.Type,
                        $"Successfully cleared {clearedCount} cache entries from Redis",
                        200
                    );
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    $"Failed to clear cache: {ex.Message}",
                    500
                );
            }
        }
        public ServiceResult<MobileAppSettingsResponse> GetMobileAppSettings()
        {
            try
            {
                _log.Info("GetMobileAppSettings called.");

                bool isMobileAppActive = _configuration.GetValue<bool>("MobileAppSettings:IsMobileAppActive");

                var result = new MobileAppSettingsResponse
                {
                    IsMobileAppActive = isMobileAppActive,
                    ApiBaseUrl = isMobileAppActive
                        ? _configuration.GetValue<string>("MobileAppSettings:ApiBaseUrl")
                        : null
                };

                _log.Info($"GetMobileAppSettings resolved. IsMobileAppActive={isMobileAppActive}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<MobileAppSettingsResponse>.Success(
                    result,
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<MobileAppSettingsResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
        public ServiceResult<IEnumerable<BranchModel>> GetActiveBranchList()
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetActiveBranchList",
                    CommandType.StoredProcedure
                );

                var branches = dataTable?.AsEnumerable().Select(row => new BranchModel
                {
                    branchId = row.Field<int>("BranchId"),
                    branchName = row.Field<string>("BranchName")
                }).ToList() ?? new List<BranchModel>();

                if (!branches.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No active branches found in database");

                    return ServiceResult<IEnumerable<BranchModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                 _log.Info($"Retrieved {branches.Count} active branches");

                return ServiceResult<IEnumerable<BranchModel>>.Success(
                    branches,
                    "Info",
                    $"{branches.Count} branch(es) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<BranchModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

   
        public ServiceResult<IEnumerable<PickListModel>> GetPickListMaster(string fieldName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fieldName))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    _log.Warn("GetPickListMaster called with empty fieldName");

                    return ServiceResult<IEnumerable<PickListModel>>.Failure(
                        alert.Type,
                        "Field name is required",
                        400
                    );
                }

                _log.Info($"GetPickListMaster called. FieldName={fieldName}");

                // Generate dynamic cache key based on fieldName
                string cacheKey = $"_PickListMaster_{fieldName}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<PickListModel> pickList;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PickListMaster data retrieved from cache. Key={cacheKey}");
                    pickList = System.Text.Json.JsonSerializer.Deserialize<List<PickListModel>>(cachedData);
                }
                else
                {
                    _log.Info($"PickListMaster cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetPickListMaster",
                        CommandType.StoredProcedure,
                        new { fieldName = fieldName }
                    );

                    pickList = dataTable?.AsEnumerable().Select(row => new PickListModel
                    {
                        value = row.Field<string>("Value"),
                        key = row.Field<string>("Key")
                    }).ToList() ?? new List<PickListModel>();

                    // Store data in cache (permanent until manually cleared)
                    if (pickList.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(pickList);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"PickListMaster data cached permanently. Key={cacheKey}, Count={pickList.Count}");
                    }
                }

                if (!pickList.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No picklist items found for field: {fieldName}");

                    return ServiceResult<IEnumerable<PickListModel>>.Failure(
                        alert.Type,
                        $"No data found for field: {fieldName}",
                        404
                    );
                }

                _log.Info($"Retrieved {pickList.Count} picklist items from cache for field: {fieldName}");

                return ServiceResult<IEnumerable<PickListModel>>.Success(
                    pickList,
                    "Info",
                    $"{pickList.Count} item(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<PickListModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        private const string CACHE_KEY_PredefineQueryMaster_All = "_PredefineQueryMaster_All";

        public ServiceResult<object> GetPredefineQueryResult(string queryName, string filter1, string filter2)
        {
            try
            {
                _log.Info($"GetPredefineQueryResult called. QueryName={queryName}, Filter1={filter1 ?? "null"}, Filter2={filter2 ?? "null"}");

                // ── 1. Load & cache the QueryName -> SQLQuery master list (reference data) ──
                var cachedData = _distributedCache.GetString(CACHE_KEY_PredefineQueryMaster_All);
                List<Dictionary<string, object>> allQueries;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PredefineQueryMaster data retrieved from cache. Key={CACHE_KEY_PredefineQueryMaster_All}");
                    allQueries = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cachedData);
                }
                else
                {
                    _log.Info($"PredefineQueryMaster cache miss. Fetching from database. Key={CACHE_KEY_PredefineQueryMaster_All}");

                    var masterTable = _sqlHelper.GetDataTable(
                        "SELECT QueryId, QueryName, SQLQuery FROM PredefineQueryMaster where isActive=1",
                        CommandType.Text
                    );

                    allQueries = masterTable.ToRawList();

                    if (allQueries.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allQueries);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(CACHE_KEY_PredefineQueryMaster_All, serialized, cacheOptions);
                        _log.Info($"PredefineQueryMaster data cached permanently. Key={CACHE_KEY_PredefineQueryMaster_All}, Count={allQueries.Count}");
                    }
                }

                // ── 2. Resolve SQLQuery text for the requested QueryName (case-insensitive) ──
                var queryRow = allQueries.FirstOrDefault(row =>
                    row.TryGetValue("QueryName", out var val) &&
                    val != null &&
                    val.ToString().Equals(queryName, StringComparison.OrdinalIgnoreCase));

                if (queryRow == null)
                {
                    var alertNotFound = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"No predefined query found for QueryName={queryName}");
                    return ServiceResult<object>.Failure(
                        alertNotFound.Type,
                        $"No predefined query found for QueryName: {queryName}",
                        404
                    );
                }

                string sqlQueryText = queryRow["SQLQuery"]?.ToString();

                if (string.IsNullOrWhiteSpace(sqlQueryText))
                {
                    var alertEmpty = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"SQLQuery is empty for QueryName={queryName}");
                    return ServiceResult<object>.Failure(
                        alertEmpty.Type,
                        $"No SQL text configured for QueryName: {queryName}",
                        404
                    );
                }

                // ── 3. Execute the resolved query with the optional filters ─────────────────
                var resultTable = _sqlHelper.GetDataTable(
                    sqlQueryText,
                    CommandType.Text,
                    new
                    {
                        filter1 = string.IsNullOrWhiteSpace(filter1) ? (object)DBNull.Value : filter1,
                        filter2 = string.IsNullOrWhiteSpace(filter2) ? (object)DBNull.Value : filter2
                    }
                );

                var result = resultTable.ToRawList();

                if (!result.Any())
                {
                    var alertNoRows = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"Predefined query '{queryName}' returned no rows.");
                    return ServiceResult<object>.Failure(
                        alertNoRows.Type,
                        "No data found for the given query",
                        404
                    );
                }

                _log.Info($"GetPredefineQueryResult retrieved {result.Count} row(s) for QueryName={queryName}");

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

        public ServiceResult<AllGlobalValues> GetAllGlobalValues()
        {
            try
            {
               
                var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                _log.Info("GetAllGlobalValues method called successfully");

                // Return empty model - controller will populate with actual values
                return ServiceResult<AllGlobalValues>.Success(
                    new AllGlobalValues(),
                    alert.Type,
                    "Global values retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<AllGlobalValues>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetDashBoardStates(int branchId, int userId, int roleId)
        {
            try
            {
                _log.Info($"GetDashBoardStates called. BranchId={branchId}, UserId={userId}, RoleId={roleId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_DashBoard_States",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @userId = userId,
                        @roleId = roleId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No dashboard states found for BranchId={branchId}");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        "No dashboard states found",
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

                _log.Info($"GetDashBoardStates retrieved {result.Count} record(s) for BranchId={branchId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    alert1.Type,
                    "Dashboard states retrieved successfully",
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
        public ServiceResult<MobileVerificationOtpResponseData> SendMobileVerificationOtp(SendMobileVerificationOtpRequest request)
        {
            try
            {
                _log.Info($"SendMobileVerificationOtp called. MobileNumber={request.MobileNumber}");

                string otp = GenerateOtp();

                var result = _sqlHelper.GetDataTable(
                    "IU_StoreMobileVerificationOtp",
                    CommandType.StoredProcedure,
                    new { MobileNumber = request.MobileNumber, Otp = otp, ExpiryMinutes = 5 }
                );

                int resultValue = (result != null && result.Rows.Count > 0)
                    ? Convert.ToInt32(result.Rows[0]["Result"])
                    : 0;

                if (resultValue != 1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OTP_GENERATION_FAILED");
                    _log.Error($"Failed to store mobile verification OTP. MobileNumber={request.MobileNumber}");
                    return ServiceResult<MobileVerificationOtpResponseData>.Failure(alert.Type, alert.Message, 500);
                }

                bool smsSent = _smsService.SendOtp(request.MobileNumber, otp);
                if (!smsSent)
                    _log.Warn($"OTP stored but SMS failed for MobileNumber={request.MobileNumber}");

                string mobileHint = GenerateContactHint(request.MobileNumber);
                var responseData = new MobileVerificationOtpResponseData { MobileHint = mobileHint };

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OTP_SENT_SMS");
                _log.Info($"Mobile verification OTP sent successfully. MobileNumber={request.MobileNumber}");

                return ServiceResult<MobileVerificationOtpResponseData>.Success(
                    responseData, alert1.Type, $"{alert1.Message} to {mobileHint}", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<MobileVerificationOtpResponseData>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> VerifyMobileVerificationOtp(VerifyMobileVerificationOtpRequest request)
        {
            try
            {
                _log.Info($"VerifyMobileVerificationOtp called. MobileNumber={request.MobileNumber}");

                var (result, message) = VerifyMobileOtpInternal(request.MobileNumber, request.Otp);

                switch (result)
                {
                    case 1:
                        var alert = _messageService.GetMessageAndTypeByAlertCode("OTP_VERIFIED"); // was "MOBILE_VERIFIED"
                        _log.Info($"Mobile number verified successfully. MobileNumber={request.MobileNumber}");
                        return ServiceResult<string>.Success("Mobile number verified successfully", alert.Type, alert.Message, 200);

                    case -1:
                    case -3:
                        var alert2 = _messageService.GetMessageAndTypeByAlertCode("OTP_VALIDATION_FAILED");
                        _log.Warn($"Mobile OTP validation failed for MobileNumber={request.MobileNumber}: {message}");
                        return ServiceResult<string>.Failure(alert2.Type, alert2.Message, 400);

                    case -6:
                        var alert3 = _messageService.GetMessageAndTypeByAlertCode("INVALID_OTP");
                        _log.Warn($"Invalid mobile OTP entered for MobileNumber={request.MobileNumber}");
                        return ServiceResult<string>.Failure(alert3.Type, alert3.Message, 400);

                    default:
                        var alert4 = _messageService.GetMessageAndTypeByAlertCode("OTP_VERIFICATION_ERROR");
                        _log.Error($"Unknown error verifying mobile OTP for MobileNumber={request.MobileNumber}. Code={result}");
                        return ServiceResult<string>.Failure(alert4.Type, alert4.Message, 500);
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<EmailVerificationOtpResponseData> SendEmailVerificationOtp(SendEmailVerificationOtpRequest request)
        {
            try
            {
                _log.Info($"SendEmailVerificationOtp called. Email={request.Email}");

                string otp = GenerateOtp();

                var result = _sqlHelper.GetDataTable(
                    "IU_StoreEmailVerificationOtp",
                    CommandType.StoredProcedure,
                    new { Email = request.Email, Otp = otp, ExpiryMinutes = 5 }
                );

                int resultValue = (result != null && result.Rows.Count > 0)
                    ? Convert.ToInt32(result.Rows[0]["Result"])
                    : 0;

                if (resultValue != 1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OTP_GENERATION_FAILED");
                    _log.Error($"Failed to store email verification OTP. Email={request.Email}");
                    return ServiceResult<EmailVerificationOtpResponseData>.Failure(alert.Type, alert.Message, 500);
                }

                bool emailSent = _emailService.SendOtpEmail(request.Email, otp, "Email Verification").GetAwaiter().GetResult();
                if (!emailSent)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("EMAIL_SEND_FAILED");
                    _log.Error($"Failed to send verification email to: {request.Email}");
                    return ServiceResult<EmailVerificationOtpResponseData>.Failure(alert.Type, alert.Message, 500);
                }

                string emailHint = GenerateEmailHint(request.Email);
                var responseData = new EmailVerificationOtpResponseData { EmailHint = emailHint };

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OTP_SENT_EMAIL");
                _log.Info($"Email verification OTP sent successfully. Email={request.Email}");

                return ServiceResult<EmailVerificationOtpResponseData>.Success(
                    responseData, alert1.Type, $"{alert1.Message} to {emailHint}", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<EmailVerificationOtpResponseData>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> VerifyEmailVerificationOtp(VerifyEmailVerificationOtpRequest request)
        {
            try
            {
                _log.Info($"VerifyEmailVerificationOtp called. Email={request.Email}");

                var (result, message) = VerifyEmailOtpInternal(request.Email, request.Otp);

                switch (result)
                {
                    case 1:
                        var alert = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VERIFIED"); // was "EMAIL_VERIFIED"
                        _log.Info($"Email verified successfully. Email={request.Email}");
                        return ServiceResult<string>.Success("Email verified successfully", alert.Type, alert.Message, 200);

                    case -1:
                    case -3:
                        var alert2 = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VALIDATION_FAILED");
                        _log.Warn($"Email OTP validation failed for Email={request.Email}: {message}");
                        return ServiceResult<string>.Failure(alert2.Type, alert2.Message, 400);

                    case -6:
                        var alert3 = _messageService.GetMessageAndTypeByAlertCode("INVALID_EMAIL_OTP");
                        _log.Warn($"Invalid email OTP entered for Email={request.Email}");
                        return ServiceResult<string>.Failure(alert3.Type, alert3.Message, 400);

                    default:
                        var alert4 = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VERIFICATION_ERROR");
                        _log.Error($"Unknown error verifying email OTP for Email={request.Email}. Code={result}");
                        return ServiceResult<string>.Failure(alert4.Type, alert4.Message, 500);
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private (int result, string message) VerifyMobileOtpInternal(string mobileNumber, string otp)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
        new SqlParameter("@MobileNumber", mobileNumber),
        new SqlParameter("@Otp", otp),
        new SqlParameter("@ExpiryMinutes", 5),
        new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output },
        new SqlParameter("@Message", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Output }
            };

            _sqlHelper.RunProcedure("U_VerifyMobileVerificationOtp", parameters);

            int result = parameters[3].Value != DBNull.Value ? Convert.ToInt32(parameters[3].Value) : 0;
            string message = parameters[4].Value != DBNull.Value ? parameters[4].Value.ToString() : "Unknown error";

            return (result, message);
        }

        private (int result, string message) VerifyEmailOtpInternal(string email, string otp)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
        new SqlParameter("@Email", email),
        new SqlParameter("@Otp", otp),
        new SqlParameter("@ExpiryMinutes", 5),
        new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output },
        new SqlParameter("@Message", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Output }
            };

            _sqlHelper.RunProcedure("U_VerifyEmailVerificationOtp", parameters);

            int result = parameters[3].Value != DBNull.Value ? Convert.ToInt32(parameters[3].Value) : 0;
            string message = parameters[4].Value != DBNull.Value ? parameters[4].Value.ToString() : "Unknown error";

            return (result, message);
        }

        private string GenerateOtp()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private string GenerateContactHint(string contact)
        {
            if (string.IsNullOrEmpty(contact)) return string.Empty;
            int length = contact.Length;
            if (length < 4) return new string('*', length);
            string first2 = contact.Substring(0, 2);
            string last2 = contact.Substring(length - 2, 2);
            string middle = new string('*', length - 4);
            return $"{first2}{middle}{last2}";
        }

        private string GenerateEmailHint(string email)
        {
            if (string.IsNullOrEmpty(email)) return string.Empty;
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            string localPart = parts[0];
            string domain = parts[1];
            if (localPart.Length <= 2) return $"{new string('*', localPart.Length)}@{domain}";
            string first2 = localPart.Substring(0, 2);
            string lastChar = localPart.Substring(localPart.Length - 1, 1);
            string middle = new string('*', localPart.Length - 3);
            return $"{first2}{middle}{lastChar}@{domain}";
        }


        public ServiceResult<IEnumerable<CountryMasterModel>> GetCountryMaster(int? isActive)
        {
            try
            {
                _log.Info($"GetCountryMaster called. IsActive={isActive?.ToString() ?? "All"}");

                // Generate dynamic cache key based on isActive parameter
                string cacheKey = $"_CountryMaster_{(isActive.HasValue ? isActive.Value.ToString() : "All")}";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<CountryMasterModel> countries;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"CountryMaster data retrieved from cache. Key={cacheKey}");
                    countries = System.Text.Json.JsonSerializer.Deserialize<List<CountryMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"CountryMaster cache miss. Fetching data from database. Key={cacheKey}");

                    // Fetch data from database
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetCountryMaster",
                        CommandType.StoredProcedure,
                        new { IsActive = isActive }
                    );

                    countries = dataTable?.AsEnumerable().Select(row => new CountryMasterModel
                    {
                        CountryId = row.Field<int>("CountryId"),
                        CountryName = row.Field<string>("CountryName") ?? string.Empty,
                        Currency = row.Field<string>("Currency") ?? string.Empty,
                        ConversionFactor = row.Field<decimal?>("ConversionFactor"),
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<CountryMasterModel>();

                    // Store data in Redis cache (permanent until manually cleared)
                    if (countries.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(countries);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"CountryMaster data cached permanently. Key={cacheKey}, Count={countries.Count}");
                    }
                }

                if (!countries.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No countries found for IsActive: {isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<CountryMasterModel>>.Failure(
                        alert.Type,
                        "No countries found",
                        404
                    );
                }

                _log.Info($"Retrieved {countries.Count} country/countries from cache");

                return ServiceResult<IEnumerable<CountryMasterModel>>.Success(
                    countries,
                    "Info",
                    $"{countries.Count} country/countries retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<CountryMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<StateMasterModel>> GetStateMaster(int countryId, int? isActive)
        {
            try
            {
                _log.Info($"GetStateMaster called. CountryId={countryId}, IsActive={isActive?.ToString() ?? "All"}");

                // Generate dynamic cache key based on countryId and isActive
                string cacheKey = $"_StateMaster_Country{countryId}_{(isActive.HasValue ? isActive.Value.ToString() : "All")}";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<StateMasterModel> states;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"StateMaster data retrieved from cache. Key={cacheKey}");
                    states = System.Text.Json.JsonSerializer.Deserialize<List<StateMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"StateMaster cache miss. Fetching data from database. Key={cacheKey}");

                    // Fetch data from database
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetStateMaster",
                        CommandType.StoredProcedure,
                        new { CountryId = countryId, IsActive = isActive }
                    );

                    states = dataTable?.AsEnumerable().Select(row => new StateMasterModel
                    {
                        CountryId = row.Field<int>("CountryId"),
                        StateId = row.Field<int>("StateId"),
                        StateName = row.Field<string>("StateName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<StateMasterModel>();

                    // Store data in Redis cache (permanent until manually cleared)
                    if (states.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(states);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"StateMaster data cached permanently. Key={cacheKey}, Count={states.Count}");
                    }
                }

                if (!states.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No states found for CountryId={countryId}, IsActive: {isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<StateMasterModel>>.Failure(
                        alert.Type,
                        $"No states found for CountryId: {countryId}",
                        404
                    );
                }

                _log.Info($"Retrieved {states.Count} state(s) from cache");

                return ServiceResult<IEnumerable<StateMasterModel>>.Success(
                    states,
                    "Info",
                    $"{states.Count} state(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<StateMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<DistrictMasterModel>> GetDistrictMaster(int stateId, int? isActive)
        {
            try
            {
                _log.Info($"GetDistrictMaster called. StateId={stateId}, IsActive={isActive?.ToString() ?? "All"}");

                // Generate dynamic cache key based on stateId and isActive
                string cacheKey = $"_DistrictMaster_State{stateId}_{(isActive.HasValue ? isActive.Value.ToString() : "All")}";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DistrictMasterModel> districts;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DistrictMaster data retrieved from cache. Key={cacheKey}");
                    districts = System.Text.Json.JsonSerializer.Deserialize<List<DistrictMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DistrictMaster cache miss. Fetching data from database. Key={cacheKey}");

                    // Fetch data from database
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetDistrictMaster",
                        CommandType.StoredProcedure,
                        new { StateId = stateId, IsActive = isActive }
                    );

                    districts = dataTable?.AsEnumerable().Select(row => new DistrictMasterModel
                    {
                        CountryId = row.Field<int>("CountryId"),
                        StateId = row.Field<int>("StateId"),
                        DistrictId = row.Field<int>("DistrictId"),
                        DistrictName = row.Field<string>("DistrictName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<DistrictMasterModel>();

                    // Store data in Redis cache (permanent until manually cleared)
                    if (districts.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(districts);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"DistrictMaster data cached permanently. Key={cacheKey}, Count={districts.Count}");
                    }
                }

                if (!districts.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No districts found for StateId={stateId}, IsActive: {isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<DistrictMasterModel>>.Failure(
                        alert.Type,
                        $"No districts found for StateId: {stateId}",
                        404
                    );
                }

                _log.Info($"Retrieved {districts.Count} district(s) from cache");

                return ServiceResult<IEnumerable<DistrictMasterModel>>.Success(
                    districts,
                    "Info",
                    $"{districts.Count} district(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DistrictMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<CityMasterModel>> GetCityMaster(int districtId, int? isActive)
        {
            try
            {
                _log.Info($"GetCityMaster called. DistrictId={districtId}, IsActive={isActive?.ToString() ?? "All"}");

                // Generate dynamic cache key based on districtId and isActive
                string cacheKey = $"_CityMaster_District{districtId}_{(isActive.HasValue ? isActive.Value.ToString() : "All")}";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<CityMasterModel> cities;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"CityMaster data retrieved from cache. Key={cacheKey}");
                    cities = System.Text.Json.JsonSerializer.Deserialize<List<CityMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"CityMaster cache miss. Fetching data from database. Key={cacheKey}");

                    // Fetch data from database
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetCityMaster",
                        CommandType.StoredProcedure,
                        new { DistrictId = districtId, IsActive = isActive }
                    );

                    cities = dataTable?.AsEnumerable().Select(row => new CityMasterModel
                    {
                        CountryId = row.Field<int>("CountryId"),
                        StateId = row.Field<int>("StateId"),
                        DistrictId = row.Field<int>("DistrictId"),
                        CityId = row.Field<int>("CityId"),
                        CityName = row.Field<string>("CityName") ?? string.Empty,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<CityMasterModel>();

                    // Store data in Redis cache (permanent until manually cleared)
                    if (cities.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(cities);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"CityMaster data cached permanently. Key={cacheKey}, Count={cities.Count}");
                    }
                }

                if (!cities.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No cities found for DistrictId={districtId}, IsActive: {isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<CityMasterModel>>.Failure(
                        alert.Type,
                        $"No cities found for DistrictId: {districtId}",
                        404
                    );
                }

                _log.Info($"Retrieved {cities.Count} city/cities from cache");

                return ServiceResult<IEnumerable<CityMasterModel>>.Success(
                    cities,
                    "Info",
                    $"{cities.Count} city/cities retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<CityMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<IEnumerable<PincodeMasterModel>> GetPincodeMaster(int cityId, int? isActive)
        {
            try
            {
                _log.Info($"GetPincodeMaster called. CityId={cityId}, IsActive={isActive?.ToString() ?? "All"}");

                // Generate dynamic cache key based on cityId and isActive
                string cacheKey = $"_PincodeMaster_City{cityId}_{(isActive.HasValue ? isActive.Value.ToString() : "All")}";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<PincodeMasterModel> pincodes;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PincodeMaster data retrieved from cache. Key={cacheKey}");
                    pincodes = System.Text.Json.JsonSerializer.Deserialize<List<PincodeMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"PincodeMaster cache miss. Fetching data from database. Key={cacheKey}");

                    // Fetch data from database
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetPincodeMaster",
                        CommandType.StoredProcedure,
                        new { CityId = cityId, IsActive = isActive }
                    );

                    pincodes = dataTable?.AsEnumerable().Select(row => new PincodeMasterModel
                    {
                        CityId = row.Field<int>("CityId"),
                        PincodeId = row.Field<int>("PincodeId"),
                        Pincode = row.Field<int>("Pincode"),
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<PincodeMasterModel>();

                    // Store data in Redis cache (permanent until manually cleared)
                    if (pincodes.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(pincodes);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"PincodeMaster data cached permanently. Key={cacheKey}, Count={pincodes.Count}");
                    }
                }

                if (!pincodes.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No pincodes found for CityId={cityId}, IsActive: {isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<PincodeMasterModel>>.Failure(
                        alert.Type,
                        $"No pincodes found for CityId: {cityId}",
                        404
                    );
                }

                _log.Info($"Retrieved {pincodes.Count} pincode(s) from cache");

                return ServiceResult<IEnumerable<PincodeMasterModel>>.Success(
                    pincodes,
                    "Info",
                    $"{pincodes.Count} pincode(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<PincodeMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<LocationByPincodeModel> GetLocationByPincode(int pincode)
        {
            try
            {
                _log.Info($"GetLocationByPincode called. Pincode={pincode}");

                // Validate pincode format (6 digits)
                if (pincode < 100000 || pincode > 999999)
                {
                    _log.Warn($"Invalid pincode format: {pincode}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<LocationByPincodeModel>.Failure(
                        alert.Type,
                        "Pincode must be exactly 6 digits",
                        400
                    );
                }

                // Fetch data from database (no cache)
                var dataTable = _sqlHelper.GetDataTable(
                    "S_getLocationByPincode",
                    CommandType.StoredProcedure,
                    new { Pincode = pincode }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No location found for pincode: {pincode}");
                    return ServiceResult<LocationByPincodeModel>.Failure(
                        alert.Type,
                        $"No location found for pincode: {pincode}",
                        404
                    );
                }

                // Map the first row to LocationByPincodeModel
                var row = dataTable.Rows[0];
                var location = new LocationByPincodeModel
                {
                    CountryId = row.Field<int>("CountryId"),
                    CountryName = row.Field<string>("CountryName") ?? string.Empty,
                    StateId = row.Field<int>("StateId"),
                    StateName = row.Field<string>("StateName") ?? string.Empty,
                    DistrictId = row.Field<int>("DistrictId"),
                    DistrictName = row.Field<string>("DistrictName") ?? string.Empty,
                    CityId = row.Field<int>("CityId"),
                    CityName = row.Field<string>("CityName") ?? string.Empty,
                    Pincode = row.Field<int>("Pincode")
                };

                _log.Info($"Location retrieved successfully for pincode: {pincode}");

                return ServiceResult<LocationByPincodeModel>.Success(
                    location,
                    "Info",
                    "Location retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<LocationByPincodeModel>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
        public ServiceResult<IEnumerable<InsuranceCompanyModel>> GetAllInsuranceCompanyList()
        {
            try
            {
                _log.Info("GetAllInsuranceCompanyList called.");

                // Define cache key
                string cacheKey = "_InsuranceCompany_All";

                // Try to get data from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<InsuranceCompanyModel> insuranceCompanies;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"InsuranceCompany data retrieved from cache. Key={cacheKey}");
                    insuranceCompanies = System.Text.Json.JsonSerializer.Deserialize<List<InsuranceCompanyModel>>(cachedData);
                }
                else
                {
                    _log.Info($"InsuranceCompany cache miss. Fetching data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetInsuranceCompanyMaster",
                        CommandType.StoredProcedure
                    );

                    insuranceCompanies = dataTable?.AsEnumerable().Select(row => new InsuranceCompanyModel
                    {
                        InsuranceCompanyId = row.Field<int>("InsuranceCompanyId"),
                        InsuranceCompanyName = row.Field<string>("InsuranceCompanyName") ?? string.Empty
                    }).ToList() ?? new List<InsuranceCompanyModel>();

                    // Store data in Redis cache (no expiration - cache persists until manually cleared)
                    if (insuranceCompanies.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(insuranceCompanies);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All InsuranceCompany data cached permanently. Key={cacheKey}, Count={insuranceCompanies.Count}");
                    }
                }

                if (!insuranceCompanies.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("No insurance companies found");

                    return ServiceResult<IEnumerable<InsuranceCompanyModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {insuranceCompanies.Count} insurance companies from cache");

                return ServiceResult<IEnumerable<InsuranceCompanyModel>>.Success(
                    insuranceCompanies,
                    "Info",
                    $"{insuranceCompanies.Count} insurance company(ies) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<InsuranceCompanyModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<CorporateModel>> GetCorporateListByInsuranceCompanyId(int? insuranceCompanyId, int? isActive)
        {
            try
            {
                _log.Info($"GetCorporateListByInsuranceCompanyId called. InsuranceCompanyId={insuranceCompanyId}, IsActive={isActive?.ToString() ?? "All"}");

                // Define cache key - cache ALL corporates together
                string cacheKey = "_Corporate_All";

                // Try to get all corporates from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<CorporateModel> allCorporates;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"Corporate data retrieved from cache. Key={cacheKey}");
                    allCorporates = System.Text.Json.JsonSerializer.Deserialize<List<CorporateModel>>(cachedData);
                }
                else
                {
                    _log.Info($"Corporate cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL corporates from database (no filtering in SP call)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetCorporateList",
                        CommandType.StoredProcedure,
                        new
                        {
                           
                        }
                    );

                    allCorporates = dataTable?.AsEnumerable().Select(row => new CorporateModel
                    {
                        CorporateId = row.Field<int>("CorporateId"),
                        CorporateName = row.Field<string>("CorporateName") ?? string.Empty,
                        InsuranceCompanyId = row.Field<int>("InsuranceCompanyId"),
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<CorporateModel>();

                    // Store ALL corporates in cache (no expiration)
                    if (allCorporates.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allCorporates);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All Corporate data cached permanently. Key={cacheKey}, Count={allCorporates.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<CorporateModel> filteredCorporates = allCorporates;

                if (insuranceCompanyId.HasValue)
                {
                    _log.Info($"Filtering cached data by InsuranceCompanyId: {insuranceCompanyId.Value}");
                    filteredCorporates = filteredCorporates.Where(c => c.InsuranceCompanyId == insuranceCompanyId.Value).ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredCorporates = filteredCorporates.Where(c => c.IsActive == isActive.Value).ToList();
                }

                if (!filteredCorporates.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No corporates found for InsuranceCompanyId={insuranceCompanyId?.ToString() ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                    return ServiceResult<IEnumerable<CorporateModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {filteredCorporates.Count} corporates from cache");

                return ServiceResult<IEnumerable<CorporateModel>>.Success(
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
                return ServiceResult<IEnumerable<CorporateModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<IEnumerable<CorporateBranchMappingModel>> GetCorporateListByBranchIdAndInsuranceCompanyId(int? branchId, int? insuranceCompanyId)
        {
            try
            {

                // Define cache key - cache ALL corporates together
                string cacheKey = "_BranchWiseCorporate_All";

                // Try to get all corporates from Redis cache
                var cachedData = _distributedCache.GetString(cacheKey);
                List<CorporateBranchMappingModel> allCorporates;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"Corporate data retrieved from cache. Key={cacheKey}");
                    allCorporates = System.Text.Json.JsonSerializer.Deserialize<List<CorporateBranchMappingModel>>(cachedData);
                }
                else
                {
                    _log.Info($"Corporate cache miss. Fetching all data from database. Key={cacheKey}");

                    // Fetch ALL corporates from database (no filtering in SP call)
                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetBranchMappingWiseCorporateList",
                        CommandType.StoredProcedure,
                        new
                        {

                        }
                    );

                    allCorporates = dataTable?.AsEnumerable().Select(row => new CorporateBranchMappingModel
                    {
                        CorporateId = row.Field<int>("CorporateId"),
                        CorporateName = row.Field<string>("CorporateName") ?? string.Empty,
                        BranchId = row.Field<int>("BranchId"),
                        InsuranceCompanyId = row.Field<int>("InsuranceCompanyId"),
                        PaymentType = row.Field<string>("PaymentType") ?? string.Empty,
                        PaymentTypeId = row.Field<int>("PaymentTypeId"),
                        IsRegistrationChargeApplicable = row.Field<int?>("IsRegistrationChargeApplicable") ?? 0,
                        IsCaseBillingApplicable = row.Field<int?>("IsCaseBillingApplicable") ?? 0,

                    }).ToList() ?? new List<CorporateBranchMappingModel>();

                    // Store ALL corporates in cache (no expiration)
                    if (allCorporates.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allCorporates);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All Corporate data cached permanently. Key={cacheKey}, Count={allCorporates.Count}");
                    }
                }

                // Filter in memory based on parameters (always from cache)
                List<CorporateBranchMappingModel> filteredCorporates = allCorporates;

                if (branchId.HasValue)
                {
                    _log.Info($"Filtering cached data by branchId: {branchId.Value}");
                    filteredCorporates = filteredCorporates.Where(c => c.BranchId == branchId.Value).ToList();
                }

                if (insuranceCompanyId.HasValue)
                {
                    _log.Info($"Filtering cached data by InsuranceCompanyId: {insuranceCompanyId.Value}");
                    filteredCorporates = filteredCorporates.Where(c => c.InsuranceCompanyId == insuranceCompanyId.Value).ToList();
                }

              

                if (!filteredCorporates.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No corporates found for InsuranceCompanyId={insuranceCompanyId?.ToString() ?? "All"}");

                    return ServiceResult<IEnumerable<CorporateBranchMappingModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                _log.Info($"Retrieved {filteredCorporates.Count} corporates from cache");

                return ServiceResult<IEnumerable<CorporateBranchMappingModel>>.Success(
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
                return ServiceResult<IEnumerable<CorporateBranchMappingModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<UploadDocumentFromFileManagerResponse> UploadDocument(
            UploadDocumentFromFileManagerRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UploadDocument called. FileName={request?.File?.FileName}");

                // File select hua ya nahi check karna
                if (request.File == null || request.File.Length == 0)
                {
                    _log.Warn("File is null or empty");
                    var alertEmpty = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<UploadDocumentFromFileManagerResponse>.Failure(
                        alertEmpty.Type,
                        "File is required",
                        400
                    );
                }

                _log.Info($"Processing file: {request.File.FileName}, Size: {request.File.Length} bytes");

                // FileUploadHelper appsettings ke DMS:RootPath, DMS:AllowedExtensions, DMS:MaxFileSizeMB
                // khud se use karta hai — UploadedDocument subfolder me save hoga
                var fileUploadHelper = new FileUploadHelper(_configuration);
                var (uploadSuccess, filePath, uploadError) = fileUploadHelper.UploadFile(
                    request.File,
                    "UploadedDocument"
                );

                if (!uploadSuccess)
                {
                    _log.Error($"Document upload failed: {uploadError}");
                    var alertUpload = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<UploadDocumentFromFileManagerResponse>.Failure(
                        alertUpload.Type,
                        $"Document upload failed: {uploadError}",
                        400
                    );
                }

                _log.Info($"Document uploaded successfully: {filePath}");

                var response = new UploadDocumentFromFileManagerResponse
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    FileExtension = Path.GetExtension(filePath)?.TrimStart('.'),
                    FileSizeMB = Math.Round(request.File.Length / (1024.0 * 1024.0), 2)
                };

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<UploadDocumentFromFileManagerResponse>.Success(
                    response,
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<UploadDocumentFromFileManagerResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<FileStreamResult> GetFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    _log.Warn("File path is null or empty");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<FileStreamResult>.Failure(
                        alert.Type,
                        "File path is required",
                        400
                    );
                }

                // Security: Prevent path traversal attacks
                if (filePath.Contains("..") || filePath.Contains("~"))
                {
                    _log.Warn($"Potential path traversal attack detected: {filePath}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<FileStreamResult>.Failure(
                        alert.Type,
                        "Invalid file path",
                        400
                    );
                }

                // Get base DMS path
                string baseDmsPath = _configuration.GetValue<string>("DMS:RootPath") ?? "D:\\DMS";

                // Handle both relative and absolute paths
                string fullFilePath;
                if (Path.IsPathRooted(filePath))
                {
                    fullFilePath = filePath.Replace("/", "\\");
                }
                else
                {
                    fullFilePath = Path.Combine(baseDmsPath, filePath.Replace("/", "\\"));
                }

                // Check if file exists
                if (!File.Exists(fullFilePath))
                {
                    _log.Warn($"File not found: {fullFilePath}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<FileStreamResult>.Failure(
                        alert.Type,
                        "File not found",
                        404
                    );
                }

                // Get file extension and MIME type
                string fileExtension = Path.GetExtension(fullFilePath).ToLower();
                string contentType = GetContentType(fileExtension);
                string fileName = Path.GetFileName(fullFilePath);

                // Create file stream
                var fileStream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var result = new FileStreamResult
                {
                    FileStream = fileStream,
                    ContentType = contentType,
                    FileName = fileName
                };

                _log.Info($"File retrieved successfully: {fullFilePath}, ContentType: {contentType}");

                return ServiceResult<FileStreamResult>.Success(
                    result,
                    "Info",
                    "File retrieved successfully",
                    200
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                _log.Error($"Unauthorized access to file: {ex.Message}", ex);
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<FileStreamResult>.Failure(
                    alert.Type,
                    "Access denied to the requested file",
                    403
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<FileStreamResult>.Failure(
                    alert.Type,
                    "Failed to retrieve file",
                    500
                );
            }
        }

        public ServiceResult<FileBase64Result> GetFileAsBase64(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    _log.Warn("File path is null or empty");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<FileBase64Result>.Failure(
                        alert.Type,
                        "File path is required",
                        400
                    );
                }

                // Security: Prevent path traversal attacks
                if (filePath.Contains("..") || filePath.Contains("~"))
                {
                    _log.Warn($"Potential path traversal attack detected: {filePath}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<FileBase64Result>.Failure(
                        alert.Type,
                        "Invalid file path",
                        400
                    );
                }

                string baseDmsPath = _configuration.GetValue<string>("DMS:RootPath") ?? "D:\\DMS";

                string fullFilePath;
                if (Path.IsPathRooted(filePath))
                {
                    fullFilePath = filePath.Replace("/", "\\");
                }
                else
                {
                    fullFilePath = Path.Combine(baseDmsPath, filePath.Replace("/", "\\"));
                }

                if (!File.Exists(fullFilePath))
                {
                    _log.Warn($"File not found: {fullFilePath}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<FileBase64Result>.Failure(
                        alert.Type,
                        "File not found",
                        404
                    );
                }

                // Read file as bytes
                byte[] fileBytes = File.ReadAllBytes(fullFilePath);
                string base64String = Convert.ToBase64String(fileBytes);

                // Get file info
                string fileExtension = Path.GetExtension(fullFilePath).ToLower();
                string contentType = GetContentType(fileExtension);
                string fileName = Path.GetFileName(fullFilePath);
                FileInfo fileInfo = new FileInfo(fullFilePath);

                var result = new FileBase64Result
                {
                    FileName = fileName,
                    FileExtension = fileExtension,
                    ContentType = contentType,
                    FileSize = fileInfo.Length,
                    FileSizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2),
                    Base64Data = $"data:{contentType};base64,{base64String}",
                    CreatedDate = fileInfo.CreationTime,
                    LastModified = fileInfo.LastWriteTime
                };

                _log.Info($"File retrieved as base64: {fullFilePath}");

                return ServiceResult<FileBase64Result>.Success(
                    result,
                    "Info",
                    "File retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<FileBase64Result>.Failure(
                    alert.Type,
                    "Failed to retrieve file",
                    500
                );
            }
        }

        public ServiceResult<FileExistsResult> CheckFileExists(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<FileExistsResult>.Failure(
                        alert.Type,
                        "File path is required",
                        400
                    );
                }

                // Security check
                if (filePath.Contains("..") || filePath.Contains("~"))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<FileExistsResult>.Failure(
                        alert.Type,
                        "Invalid file path",
                        400
                    );
                }

                string baseDmsPath = _configuration.GetValue<string>("DMS:RootPath") ?? "D:\\DMS";

                string fullFilePath;
                if (Path.IsPathRooted(filePath))
                {
                    fullFilePath = filePath.Replace("/", "\\");
                }
                else
                {
                    fullFilePath = Path.Combine(baseDmsPath, filePath.Replace("/", "\\"));
                }

                bool exists = File.Exists(fullFilePath);

                var result = new FileExistsResult
                {
                    Exists = exists,
                    FilePath = filePath
                };

                return ServiceResult<FileExistsResult>.Success(
                    result,
                    "Info",
                    exists ? "File exists" : "File not found",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<FileExistsResult>.Failure(
                    alert.Type,
                    "Error checking file existence",
                    500
                );
            }
        }

        /// <summary>
        /// Get content type based on file extension
        /// </summary>
        private string GetContentType(string fileExtension)
        {
            return fileExtension.ToLower() switch
            {
                // Images
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".tiff" or ".tif" => "image/tiff",

                // Documents
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".xml" => "application/xml",
                ".json" => "application/json",

                // Archives
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",

                // Default
                _ => "application/octet-stream"
            };
        }

        public ServiceResult<IEnumerable<DoctorMasterModel>> GetDoctorMasterListByBranchId(
      int branchId,
      string departmentId = null,
      string specializationId = null,
      int? canApproveLabReport = null,
      byte? isDoctorUnit = null)
        {
            try
            {
                _log.Info($"GetDoctorMasterListByBranchId called. BranchId={branchId}, DepartmentId={departmentId ?? "All"}, SpecializationId={specializationId ?? "All"}, CanApproveLabReport={canApproveLabReport?.ToString() ?? "All"}, IsDoctorUnit={isDoctorUnit?.ToString() ?? "All"}");

                if (branchId <= 0)
                {
                    _log.Warn("Invalid BranchId provided.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<IEnumerable<DoctorMasterModel>>.Failure(
                        alert.Type,
                        "BranchId must be greater than 0",
                        400
                    );
                }

                // Parse comma-separated departmentId and specializationId into HashSets for fast lookup
                HashSet<int> departmentIds = null;
                if (!string.IsNullOrWhiteSpace(departmentId))
                {
                    departmentIds = new HashSet<int>(
                        departmentId.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                            .Where(x => int.TryParse(x, out _))
                            .Select(int.Parse)
                    );
                    _log.Info($"Parsed DepartmentIds: {string.Join(",", departmentIds)}");
                }

                HashSet<int> specializationIds = null;
                if (!string.IsNullOrWhiteSpace(specializationId))
                {
                    specializationIds = new HashSet<int>(
                        specializationId.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                            .Where(x => int.TryParse(x, out _))
                            .Select(int.Parse)
                    );
                    _log.Info($"Parsed SpecializationIds: {string.Join(",", specializationIds)}");
                }

                string cacheKey = $"_DoctorMaster_Branch{branchId}";
                var cachedData = _distributedCache.GetString(cacheKey);
                List<DoctorMasterModel> allDoctors;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"DoctorMaster data retrieved from cache. Key={cacheKey}");
                    allDoctors = System.Text.Json.JsonSerializer.Deserialize<List<DoctorMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"DoctorMaster cache miss. Fetching ALL data from database for BranchId={branchId}. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_getDoctorMasterListByBranchId",
                        CommandType.StoredProcedure,
                        new { branchId = branchId }
                    );

                    allDoctors = dataTable?.AsEnumerable().Select(row => new DoctorMasterModel
                    {
                        DoctorId = row.Field<int>("DoctorId"),
                        Name = row.Field<string>("Name") ?? string.Empty,
                        SpecializationId = row.Field<int>("SpecializationId"),
                        DepartmentId = row.Field<int>("DepartmentId"),
                        CanApproveLabReport = row.Field<int>("CanApproveLabReport"),
                        IsDoctorUnit = row.Field<byte>("IsDoctorUnit")
                    }).ToList() ?? new List<DoctorMasterModel>();

                    if (allDoctors.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allDoctors);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All DoctorMaster data cached permanently. Key={cacheKey}, Count={allDoctors.Count}");
                    }
                }

                // Filter in memory
                List<DoctorMasterModel> filteredDoctors = allDoctors;

                if (departmentIds != null && departmentIds.Any())
                {
                    _log.Info($"Filtering cached data by DepartmentIds: {string.Join(",", departmentIds)}");
                    filteredDoctors = filteredDoctors.Where(d => departmentIds.Contains(d.DepartmentId)).ToList();
                }

                if (specializationIds != null && specializationIds.Any())
                {
                    _log.Info($"Filtering cached data by SpecializationIds: {string.Join(",", specializationIds)}");
                    filteredDoctors = filteredDoctors.Where(d => specializationIds.Contains(d.SpecializationId)).ToList();
                }

                if (canApproveLabReport.HasValue)
                {
                    _log.Info($"Filtering cached data by CanApproveLabReport: {canApproveLabReport.Value}");
                    filteredDoctors = filteredDoctors.Where(d => d.CanApproveLabReport == canApproveLabReport.Value).ToList();
                }

                if (isDoctorUnit.HasValue)
                {
                    _log.Info($"Filtering cached data by IsDoctorUnit: {isDoctorUnit.Value}");
                    filteredDoctors = filteredDoctors.Where(d => d.IsDoctorUnit == isDoctorUnit.Value).ToList();
                }

                if (!filteredDoctors.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No doctors found for BranchId={branchId} with applied filters");
                    return ServiceResult<IEnumerable<DoctorMasterModel>>.Failure(
                        alert.Type,
                        "No doctors found for the specified criteria",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredDoctors.Count} doctor(s) from cache after filtering");

                return ServiceResult<IEnumerable<DoctorMasterModel>>.Success(
                    filteredDoctors,
                    "Info",
                    $"{filteredDoctors.Count} doctor(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DoctorMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<CategoryTypeModel>> GetCategoryTypeList(string categoryTypeIds)
        {
            try
            {

                const string cacheKey = "_CategoryTypeMaster_All";

                // 1. Try Redis cache first
                var cachedData = _distributedCache.GetString(cacheKey);
                List<CategoryTypeModel> allCategories;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    allCategories = System.Text.Json.JsonSerializer.Deserialize<List<CategoryTypeModel>>(cachedData)
                                    ?? new List<CategoryTypeModel>();
                }
                else
                {

                    // 2. Fetch ALL data from SP (no filter — SP returns all active categories)
                    DataTable dt = _sqlHelper.GetDataTable(
                        "S_GetCategoryTypeList",
                        CommandType.StoredProcedure,
                        new { }
                    );

                    allCategories = dt.AsEnumerable().Select(row => new CategoryTypeModel
                    {
                        CategoryTypeId = row.Field<int>("CategoryTypeId"),
                        CategoryTypeName = row.Field<string>("CategoryTypeName") ?? string.Empty
                    }).ToList();

                    // 3. Store full list in Redis (no expiry — cleared manually)
                    if (allCategories.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allCategories);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                    }
                }

                // 4. Filter in memory by requested CategoryIds (e.g. "3,4,5,6")
                if (!string.IsNullOrWhiteSpace(categoryTypeIds))
                {
                    var requestedIds = categoryTypeIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => int.TryParse(id.Trim(), out int parsed) ? parsed : (int?)null)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToHashSet();

                    allCategories = allCategories
                        .Where(c => requestedIds.Contains(c.CategoryTypeId))
                        .ToList();

                }

                if (!allCategories.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<CategoryTypeModel>>.Failure(alert.Type, "No categories type found", 404);
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<CategoryTypeModel>>.Success(
                    allCategories,
                    successAlert.Type,
                    $"{allCategories.Count} category type retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<CategoryTypeModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<CategoryModel>> GetCategoryList(string categoryIds, string categoryTypeIds)
        {
            try
            {
                _log.Info($"GetCategoryList called. CategoryIds={categoryIds}, CategoryTypeIds={categoryTypeIds}");

                const string cacheKey = "_CategoryMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<CategoryModel> allCategories;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("CategoryMaster data retrieved from Redis cache.");
                    allCategories = System.Text.Json.JsonSerializer.Deserialize<List<CategoryModel>>(cachedData)
                                    ?? new List<CategoryModel>();
                }
                else
                {
                    _log.Info("CategoryMaster not in cache. Fetching from DB via SP.");

                    DataTable dt = _sqlHelper.GetDataTable(
                        "S_GetCategoryList",
                        CommandType.StoredProcedure,
                        new { }
                    );

                    allCategories = dt.AsEnumerable().Select(row => new CategoryModel
                    {
                        CategoryId = row.Field<int>("CategoryId"),
                        CategoryName = row.Field<string>("CategoryName") ?? string.Empty,
                        CategoryTypeId = row.Field<int>("CategoryTypeId"),
                        CategoryTypeName = row.Field<string>("CategoryTypeName") ?? string.Empty,
                        CreatedBy = row.Field<string>("CreatedBy"),
                        CreatedOn = row.Field<string>("CreatedOn"),
                        LastModifiedBy = row.Field<string>("LastModifiedBy"),
                        LastModifiedOn = row.Field<string>("LastModifiedOn")
                    }).ToList();

                    if (allCategories.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allCategories);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"CategoryMaster cached permanently. Count={allCategories.Count}");
                    }
                }

                // Filter by CategoryIds
                if (!string.IsNullOrWhiteSpace(categoryIds))
                {
                    var requestedIds = categoryIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => int.TryParse(id.Trim(), out int parsed) ? parsed : (int?)null)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToHashSet();

                    allCategories = allCategories
                        .Where(c => requestedIds.Contains(c.CategoryId))
                        .ToList();

                    _log.Info($"Filtered to {allCategories.Count} categories for CategoryIds: {categoryIds}");
                }

                // Filter by CategoryTypeIds
                if (!string.IsNullOrWhiteSpace(categoryTypeIds))
                {
                    var requestedTypeIds = categoryTypeIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => int.TryParse(id.Trim(), out int parsed) ? parsed : (int?)null)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToHashSet();

                    allCategories = allCategories
                        .Where(c => requestedTypeIds.Contains(c.CategoryTypeId))
                        .ToList();

                    _log.Info($"Filtered to {allCategories.Count} categories for CategoryTypeIds: {categoryTypeIds}");
                }

                if (!allCategories.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<CategoryModel>>.Failure(alert.Type, "No categories found", 404);
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<CategoryModel>>.Success(
                    allCategories,
                    successAlert.Type,
                    $"{allCategories.Count} category/categories retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<CategoryModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }


        public ServiceResult<CreateUpdateCategoryResponse> CreateUpdateCategory(
    CreateUpdateCategoryRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateCategory called. CategoryId={request.CategoryId}, CategoryName={request.CategoryName}");

                var result = _sqlHelper.DML(
                    "IU_CategoryMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @categoryId = request.CategoryId,
                        @categoryName = request.CategoryName,
                        @categoryTypeId = request.CategoryTypeId,
                        @categoryTypeName = request.CategoryTypeName,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate CategoryName: {request.CategoryName}");
                    return ServiceResult<CreateUpdateCategoryResponse>.Failure(
                        dupAlert.Type,
                        "Category Name already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    // Clear Category cache so next GET re-fetches fresh data
                    _distributedCache.Remove("_CategoryMaster_All");
                    _log.Info($"Cleared CategoryMaster cache. CategoryId={resultValue}");

                    var responseData = new CreateUpdateCategoryResponse { CategoryId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.CategoryId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"Category {(request.CategoryId == 0 ? "created" : "updated")} successfully. CategoryId={resultValue}");

                    return ServiceResult<CreateUpdateCategoryResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.CategoryId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateCategoryResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateCategoryResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<SubCategoryModel>> GetSubCategoryList(string categoryIds)
        {
            try
            {
                _log.Info($"GetSubCategoryList called. CategoryIds={categoryIds}");

                const string cacheKey = "_SubCategoryMaster_All";

                // 1. Try Redis cache first
                var cachedData = _distributedCache.GetString(cacheKey);
                List<SubCategoryModel> allSubCategories;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("SubCategoryMaster data retrieved from Redis cache.");
                    allSubCategories = System.Text.Json.JsonSerializer.Deserialize<List<SubCategoryModel>>(cachedData)
                                       ?? new List<SubCategoryModel>();
                }
                else
                {
                    _log.Info("SubCategoryMaster not in cache. Fetching from DB via SP.");

                    // 2. Fetch ALL active subcategories from SP
                    DataTable dt = _sqlHelper.GetDataTable(
                        "S_GetSubCategoryList",
                        CommandType.StoredProcedure,
                        new { }
                    );

                    allSubCategories = dt.AsEnumerable().Select(row => new SubCategoryModel
                    {
                        CategoryId = row.Field<int>("CategoryId"),
                        SubCategoryId = row.Field<int>("SubCategoryId"),
                        SubCategoryName = row.Field<string>("SubCategoryName") ?? string.Empty,
                        LabTypeId = row.Field<int>("LabTypeId")
                    }).ToList();

                    // 3. Store full list in Redis permanently (cleared manually via clearAllCache)
                    if (allSubCategories.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allSubCategories);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"SubCategoryMaster cached permanently. Count={allSubCategories.Count}");
                    }
                }

                // 4. Filter in memory by requested CategoryIds (e.g. "3,4,5,6")
                if (!string.IsNullOrWhiteSpace(categoryIds))
                {
                    var requestedIds = categoryIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => int.TryParse(id.Trim(), out int parsed) ? parsed : (int?)null)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToHashSet();

                    allSubCategories = allSubCategories
                        .Where(s => requestedIds.Contains(s.CategoryId))
                        .ToList();

                    _log.Info($"Filtered to {allSubCategories.Count} subcategories for CategoryIds: {categoryIds}");
                }

                if (!allSubCategories.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<SubCategoryModel>>.Failure(
                        alert.Type,
                        "No subcategories found",
                        404
                    );
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<SubCategoryModel>>.Success(
                    allSubCategories,
                    successAlert.Type,
                    $"{allSubCategories.Count} subcategory/subcategories retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SubCategoryModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
        public ServiceResult<CreateUpdateSubCategoryResponse> CreateUpdateSubCategory(
 CreateUpdateSubCategoryRequest request,
 AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSubCategory called. SubCategoryId={request.SubCategoryId}, SubCategoryName={request.SubCategoryName}");

                var result = _sqlHelper.DML(
                    "IU_SubCategoryMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @hospId = globalValues.hospId,
                        @subCategoryId = request.SubCategoryId,
                        @subCategoryName = request.SubCategoryName,
                        @categoryId = request.CategoryId,
                        @labTypeId = request.LabTypeId,
                        @labType = request.LabType,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate SubCategoryName: {request.SubCategoryName}");
                    return ServiceResult<CreateUpdateSubCategoryResponse>.Failure(
                        dupAlert.Type,
                        "Sub Category Name already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    // Clear SubCategory cache so next GET re-fetches fresh data
                    _distributedCache.Remove("_SubCategoryMaster_All");
                    _log.Info($"Cleared SubCategoryMaster cache. SubCategoryId={resultValue}");

                    var responseData = new CreateUpdateSubCategoryResponse { SubCategoryId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.SubCategoryId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"SubCategory {(request.SubCategoryId == 0 ? "created" : "updated")} successfully. SubCategoryId={resultValue}");

                    return ServiceResult<CreateUpdateSubCategoryResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.SubCategoryId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateSubCategoryResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSubCategoryResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<IEnumerable<SubSubCategoryModel>> GetSubSubCategoryList(string subCategoryIds)
        {
            try
            {
                _log.Info($"GetSubSubCategoryList called. SubCategoryIds={subCategoryIds}");

                const string cacheKey = "_SubSubCategoryMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<SubSubCategoryModel> allSubSubCategories;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info("SubSubCategoryMaster data retrieved from Redis cache.");
                    allSubSubCategories = System.Text.Json.JsonSerializer.Deserialize<List<SubSubCategoryModel>>(cachedData)
                                          ?? new List<SubSubCategoryModel>();
                }
                else
                {
                    _log.Info("SubSubCategoryMaster not in cache. Fetching from DB via SP.");

                    DataTable dt = _sqlHelper.GetDataTable(
                        "S_GetSubSubCategoryList",
                        CommandType.StoredProcedure,
                        new { }
                    );

                    allSubSubCategories = dt.AsEnumerable().Select(row => new SubSubCategoryModel
                    {
                        SubCategoryId = row.Field<int>("SubCategoryId"),
                        SubSubCategoryId = row.Field<int>("SubSubCategoryId"),
                        SubSubCategoryName = row.Field<string>("SubSubCategoryName") ?? string.Empty,
                        DepartmentId = row.Field<int?>("DepartmentId"),
                        PrintGroupId = row.Field<int?>("PrintGroupId"),

                    }).ToList();

                    if (allSubSubCategories.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allSubSubCategories);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"SubSubCategoryMaster cached permanently. Count={allSubSubCategories.Count}");
                    }
                }

                // Filter by SubCategoryIds — if null return all
                if (!string.IsNullOrWhiteSpace(subCategoryIds))
                {
                    var subCategoryIdSet = subCategoryIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Where(id => int.TryParse(id.Trim(), out _))
                        .Select(id => int.Parse(id.Trim()))
                        .ToHashSet();

                    allSubSubCategories = allSubSubCategories
                        .Where(s => subCategoryIdSet.Contains(s.SubCategoryId))
                        .ToList();

                    _log.Info($"Filtered by SubCategoryIds. Result count={allSubSubCategories.Count}");
                }
                else
                {
                    _log.Info($"No filter applied. Returning all {allSubSubCategories.Count} sub-subcategories.");
                }

                if (!allSubSubCategories.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<SubSubCategoryModel>>.Failure(alert.Type, "No sub-subcategories found", 404);
                }

                var successAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<SubSubCategoryModel>>.Success(
                    allSubSubCategories,
                    successAlert.Type,
                    $"{allSubSubCategories.Count} sub-subcategory/sub-subcategories retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<SubSubCategoryModel>>.Failure(alert.Type, alert.Message, 500);
            }
        }

     
        public ServiceResult<CreateUpdateSubSubCategoryResponse> CreateUpdateSubSubCategory(
            CreateUpdateSubSubCategoryRequest request,
            AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateSubSubCategory called. SubSubCategoryId={request.SubSubCategoryId}, SubSubCategoryName={request.SubSubCategoryName}");

                var result = _sqlHelper.DML(
                    "IU_SubSubCategoryMaster",
                    CommandType.StoredProcedure,
                    new
                    {
                        @hospId = globalValues.hospId,
                        @subSubCategoryId = request.SubSubCategoryId,
                        @subSubCategoryName = request.SubSubCategoryName,
                        @subCategoryId = request.SubCategoryId,
                        @departmentId = request.DepartmentId,
                        @printGroupId = request.PrintGroupId,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    },
                    new { result = 0 }
                );

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -1)
                {
                    var dupAlert = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                    _log.Warn($"Duplicate SubSubCategoryName: {request.SubSubCategoryName}");
                    return ServiceResult<CreateUpdateSubSubCategoryResponse>.Failure(
                        dupAlert.Type,
                        "Sub Sub Category Name already exists",
                        409
                    );
                }

                if (resultValue > 0)
                {
                    // Clear SubSubCategory cache so next GET re-fetches fresh data
                    _distributedCache.Remove("_SubSubCategoryMaster_All");
                    _log.Info($"Cleared SubSubCategoryMaster cache. SubSubCategoryId={resultValue}");

                    var responseData = new CreateUpdateSubSubCategoryResponse { SubSubCategoryId = resultValue };
                    var alert = _messageService.GetMessageAndTypeByAlertCode(
                        request.SubSubCategoryId == 0 ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY"
                    );

                    _log.Info($"SubSubCategory {(request.SubSubCategoryId == 0 ? "created" : "updated")} successfully. SubSubCategoryId={resultValue}");

                    return ServiceResult<CreateUpdateSubSubCategoryResponse>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        request.SubSubCategoryId == 0 ? 201 : 200
                    );
                }

                var failAlert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                return ServiceResult<CreateUpdateSubSubCategoryResponse>.Failure(failAlert.Type, failAlert.Message, 500);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateUpdateSubSubCategoryResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

          public ServiceResult<IEnumerable<ServiceItemMasterModel>> GetServiceItemList(
    int? serviceItemId,
    int? isActive,
    string categoryTypeId,
    string categoryId,
    int? subCategoryId,
    int? subSubCategoryId,
    int? labTypeId,
    int? reportTypeId,
    string serviceName,
    int? isRegistrationCharge)
        {
            try
            {
                _log.Info($"GetServiceItemList called. ServiceItemId={serviceItemId}, IsActive={isActive}, CategoryId={categoryId}, SubCategoryId={subCategoryId}, SubSubCategoryId={subSubCategoryId}, ServiceName={serviceName}");

                const string cacheKey = "_ServiceItemMaster_All";

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
                        CategoryName = row.Field<string>("CategoryName") ?? string.Empty,

                        SubCategoryId = row.Field<int>("SubCategoryId"),
                        SubCategoryName = row.Field<string>("SubCategoryName") ?? string.Empty,

                        SubSubCategoryId = row.Field<int>("SubSubCategoryId"),
                        SubSubCategoryName = row.Field<string>("SubSubCategoryName") ?? string.Empty,

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
                        IsDepartmentReceivingRequired = row.Field<int?>("IsDepartmentReceivingRequired"),
                        ShortName = row.Field<string>("ShortName") ?? string.Empty,
                        SampleVolume = row.Field<string>("SampleVolume") ?? string.Empty,
                        InvestigationComment = row.Field<string>("InvestigationComment") ?? string.Empty,
                        TatInMin = row.Field<int?>("tatInMin") ?? 0,
                        IsActive = row.Field<int?>("IsActive") ?? 0,
                        GSTPer = row.Field<decimal>("GSTPer"),
                        RoomTypeId = row.Field<int?>("RoomTypeId") ?? 0,
                        RoomType = row.Field<string>("RoomType") ?? string.Empty,
                        IsICU = row.Field<int?>("IsICU") ?? 0,
                        OPDConsultationTypeId = row.Field<int?>("OPDConsultationTypeId") ?? 0,
                        OPDConsultationType = row.Field<string>("OPDConsultationType") ?? string.Empty,
                        SNOMEDCode = row.Field<string>("SNOMEDCode") ?? string.Empty,
                        DoctorDepartmentIds = row.Field<string>("DoctorDepartmentIds") ?? string.Empty,
                        IsRequiredSeparatePerformingDoctor = row.Field<int?>("IsRequiredSeparatePerformingDoctor") ?? 0,
                        IsOnlineConsultationAllow = row.Field<int?>("isOnlineConsultationAllow") ?? 0,
                        IsTeleConsultationService = row.Field<int?>("isTeleConsultationService") ?? 0,
                        IsRegistrationCharge = row.Field<int?>("IsRegistrationCharge") ?? 0,
                        RegistrationChargeValidityDays = row.Field<int?>("RegistrationChargeValidityDays") ?? 0,
                        PackageDurationDays = row.Field<int?>("PackageDurationDays"),
                        StartsFrom = row.Field<string>("StartsFrom") ?? string.Empty,
                        ExpiresOn = row.Field<string>("ExpiresOn") ?? string.Empty,
                        IsPackageExpired = row.Field<int?>("IsPackageExpired") ?? 0,
                        SaltName = row.Field<string>("SaltName") ?? string.Empty,


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

                if (isRegistrationCharge.HasValue && isRegistrationCharge.Value > 0)
                {
                    allItems = allItems.Where(s => s.IsRegistrationCharge == isRegistrationCharge.Value).ToList();
                    _log.Info($"Filtered by reportTypeId={isRegistrationCharge}. Count={allItems.Count}");
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
        public ServiceResult<IEnumerable<PaymentModeMasterModel>> GetPaymentModeMasterList(
    string paymentModeName = null,
    int? isActive = null)
        {
            try
            {
                _log.Info($"GetPaymentModeMasterList called. PaymentModeName={paymentModeName ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");

                string cacheKey = "_PaymentModeMaster_All";

                var cachedData = _distributedCache.GetString(cacheKey);
                List<PaymentModeMasterModel> allPaymentModes;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"PaymentModeMaster data retrieved from cache. Key={cacheKey}");
                    allPaymentModes = System.Text.Json.JsonSerializer.Deserialize<List<PaymentModeMasterModel>>(cachedData);
                }
                else
                {
                    _log.Info($"PaymentModeMaster cache miss. Fetching all data from database. Key={cacheKey}");

                    var dataTable = _sqlHelper.GetDataTable(
                        "S_GetPaymentModeMaster",
                        CommandType.StoredProcedure
                    );

                    allPaymentModes = dataTable?.AsEnumerable().Select(row => new PaymentModeMasterModel
                    {
                        PaymentModeId = row.Field<int>("PaymentModeId"),
                        PaymentModeName = row.Field<string>("PaymentModeName") ?? string.Empty,
                        PayModeType = row.Field<string>("PayModeType") ?? string.Empty,
                        PayModeTypeId = row.Field<int?>("PayModeTypeId") ?? 0,
                        IsRefundAllowed = row.Field<int?>("IsRefundAllowed") ?? 0,
                        IsActive = row.Field<int>("IsActive")
                    }).ToList() ?? new List<PaymentModeMasterModel>();

                    if (allPaymentModes.Any())
                    {
                        var serialized = System.Text.Json.JsonSerializer.Serialize(allPaymentModes);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"All PaymentModeMaster data cached permanently. Key={cacheKey}, Count={allPaymentModes.Count}");
                    }
                }

                // Filter in memory from cache
                List<PaymentModeMasterModel> filteredPaymentModes = allPaymentModes;

                if (!string.IsNullOrWhiteSpace(paymentModeName))
                {
                    _log.Info($"Filtering cached data by PaymentModeName containing: {paymentModeName}");
                    filteredPaymentModes = filteredPaymentModes
                        .Where(p => p.PaymentModeName.Contains(paymentModeName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (isActive.HasValue)
                {
                    _log.Info($"Filtering cached data by IsActive: {isActive.Value}");
                    filteredPaymentModes = filteredPaymentModes
                        .Where(p => p.IsActive == isActive.Value)
                        .ToList();
                }

                if (!filteredPaymentModes.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No payment modes found for PaymentModeName={paymentModeName ?? "All"}, IsActive={isActive?.ToString() ?? "All"}");
                    return ServiceResult<IEnumerable<PaymentModeMasterModel>>.Failure(
                        alert.Type,
                        "No payment modes found",
                        404
                    );
                }

                _log.Info($"Retrieved {filteredPaymentModes.Count} payment mode(s) from cache");

                return ServiceResult<IEnumerable<PaymentModeMasterModel>>.Success(
                    filteredPaymentModes,
                    "Info",
                    $"{filteredPaymentModes.Count} payment mode(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<PaymentModeMasterModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> UpdateServiceItemMasterStatus(int serviceItemId, int isActive, AllGlobalValues globalValues)
        {
            try
            {
                var result = _sqlHelper.DML("U_UpdateServiceItemMasterStatus", CommandType.StoredProcedure, new
                {
                    @ServiceItemId = serviceItemId,
                    @userId = globalValues.userId,
                    @isActive = isActive
                });

                _distributedCache.Remove("_ServiceItemMaster_All");
                _distributedCache.Remove("_ServiceInvestigationItemMaster_All");

                if (result > 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    return ServiceResult<string>.Success(
                        "Service status updated successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        "Service not found",
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

        public ServiceResult<IEnumerable<CorporatePaymentModeModel>> GetCorporatePaymentModes(
           int corporateId,
           int isRefundPaymentModes)
        {
            try
            {
                _log.Info($"GetCorporatePaymentModes called. CorporateId={corporateId}, IsRefundPaymentModes={isRefundPaymentModes}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetCorporatePaymentModes",
                    CommandType.StoredProcedure,
                    new
                    {
                        @corporateId = corporateId,
                        @isRefundPaymentModes = isRefundPaymentModes
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"No payment modes found for CorporateId={corporateId}, IsRefundPaymentModes={isRefundPaymentModes}");
                    return ServiceResult<IEnumerable<CorporatePaymentModeModel>>.Failure(
                        alert.Type,
                        "No payment modes found",
                        404
                    );
                }

                var paymentModes = dataTable.AsEnumerable().Select(row => new CorporatePaymentModeModel
                {
                    PaymentModeId = row["PaymentModeId"] != DBNull.Value ? Convert.ToInt32(row["PaymentModeId"]) : 0,
                    PaymentModeName = row["PaymentModeName"]?.ToString() ?? string.Empty,
                    PayModeType = row["PayModeType"]?.ToString() ?? string.Empty,
                    PayModeTypeId = row["PayModeTypeId"] != DBNull.Value ? Convert.ToInt32(row["PayModeTypeId"]) : 0,
                    ShowBankField = row["ShowBankField"] != DBNull.Value ? Convert.ToInt32(row["ShowBankField"]) : 0,
                    ShowReferenceNumberField = row["ShowReferenceNumberField"] != DBNull.Value ? Convert.ToInt32(row["ShowReferenceNumberField"]) : 0,
                    IsExcludedFromPaymentList = row["IsExcludedFromPaymentList"] != DBNull.Value ? Convert.ToInt32(row["IsExcludedFromPaymentList"]) : 0
                }).ToList();

                _log.Info($"Retrieved {paymentModes.Count} payment mode(s) for CorporateId={corporateId}");

                return ServiceResult<IEnumerable<CorporatePaymentModeModel>>.Success(
                    paymentModes,
                    "Info",
                    $"{paymentModes.Count} payment mode(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<CorporatePaymentModeModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<DiscountApprovalModel>> GetDiscountApprovalForBilling(
         string discountType,
         int branchId)
        {
            try
            {

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetDiscountApprovalForBilling",
                    CommandType.StoredProcedure,
                    new
                    {
                        @discountType = discountType,
                        @branchId = branchId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<IEnumerable<DiscountApprovalModel>>.Failure(
                        alert.Type,
                        "No Discount Approval found",
                        404
                    );
                }

                var discountApproval = dataTable.AsEnumerable().Select(row => new DiscountApprovalModel
                {
                    Id = row["id"] != DBNull.Value ? Convert.ToInt32(row["id"]) : 0,
                    Name = row["name"]?.ToString() ?? string.Empty
                   
                }).ToList();


                return ServiceResult<IEnumerable<DiscountApprovalModel>>.Success(
                    discountApproval,
                    "Info",
                    $"{discountApproval.Count} Discount Approval(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<DiscountApprovalModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        public ServiceResult<object> CheckBedStatus(int bedId)
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable(
                    "S_CheckBedStatus",
                    CommandType.StoredProcedure,
                    new { @BedId = bedId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Warn($"No bed found for BedId={bedId}");
                    return ServiceResult<object>.Failure(alert.Type, "Bed not found", 404);
                }

                var row = dataTable.Rows[0];
                int currentStatus = Convert.ToInt32(row["CurrentStatus"]);
                int isAvailable = Convert.ToInt32(row["IsAvailable"]);
                int isOccupied = Convert.ToInt32(row["IsOccupid"]);

                // Build the same hint your frontend checks:
                // CurrentStatus==1 && IsAvailable==0 && IsOccupid==1 → occupied
                string hint = (currentStatus == 1 && isAvailable == 0 && isOccupied == 1)
                    ? "This bed is already occupied. Please select another bed."
                    : "Bed is available.";

                var data = new
                {
                    CurrentStatus = currentStatus,
                    IsAvailable = isAvailable,
                    IsOccupid = isOccupied,
                    StatusHint = hint
                };

                _log.Info($"CheckBedStatus: BedId={bedId}, IsAvailable={isAvailable}, Hint={hint}");
                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(data, alert1.Type, alert1.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> CheckPatientAdmitted(int patientId)
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable(
                    "S_CheckPatientAdmitted",
                    CommandType.StoredProcedure,
                    new { @PatientId = patientId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"Patient not currently admitted. PatientId={patientId}");
                    return ServiceResult<object>.Failure(alert.Type, "Patient is not currently admitted", 404);
                }

                // Raw SP data — SP returns VisitNo as IPDNo
                var rows = dataTable.AsEnumerable().Select(r => new
                {
                    IPDNo = r.Field<string>("IPDNo")
                }).ToList();

                _log.Info($"CheckPatientAdmitted: PatientId={patientId}, AdmittedCount={rows.Count}");
                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, "Patient is currently admitted", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetBedTypes(int branchId, int roomTypeId)
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetBedTypes",
                    CommandType.StoredProcedure,
                    new { @BranchId = branchId, @roomTypeId = roomTypeId }
                );

                // roomTypeId hint for API consumers
                string roomTypeLabel = roomTypeId switch
                {
                    1 => "Normal",
                    2 => "Day Care",
                    3 => "Dialysis",
                    4 => "Emergency",
                    _ => "Unknown"
                };

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No bed types found. BranchId={branchId}, RoomTypeId={roomTypeId} ({roomTypeLabel})");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        $"No bed types found for branch {branchId} under room type '{roomTypeLabel}'",
                        404
                    );
                }

                // Raw SP data: TypeId, RoomTypeName, TotalBeds, AvailableBeds, OccupiedBeds
                var rows = dataTable.AsEnumerable().Select(r => new
                {
                    TypeId = r.Field<int>("TypeId"),
                    RoomTypeName = r.Field<string>("RoomTypeName"),
                    TotalBeds = r.Field<int>("TotalBeds"),
                    AvailableBeds = r.Field<int>("AvailableBeds"),
                    OccupiedBeds = r.Field<int>("OccupiedBeds")
                }).ToList();

                _log.Info($"GetBedTypes: BranchId={branchId}, RoomTypeId={roomTypeId} ({roomTypeLabel}), Count={rows.Count}");
                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    new { roomCategory = roomTypeLabel, bedTypes = rows },
                    alert1.Type,
                    $"{rows.Count} bed type(s) retrieved for '{roomTypeLabel}'",
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

        public ServiceResult<object> GetAvailableBeds(int branchId, int typeId)
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetAvailableBeds",
                    CommandType.StoredProcedure,
                    new { @branchId = branchId, @typeId = typeId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No available beds. BranchId={branchId}, TypeId={typeId}");
                    return ServiceResult<object>.Failure(alert.Type, "No available beds found", 404);
                }

                // Raw SP data: BedId, BedName (WardName/BedNo)
                var rows = dataTable.AsEnumerable().Select(r => new
                {
                    BedId = r.Field<int>("BedId"),
                    BedName = r.Field<string>("BedName"),
                    Gender = r.Field<string>("Gender")
                }).ToList();

                _log.Info($"GetAvailableBeds: BranchId={branchId}, TypeId={typeId}, Available={rows.Count}");
                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} bed(s) available", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetBillingTabs(int branchId, int roleId, int tabTypeId, int roomServiceItemId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetBillingTabs called. BranchId={branchId}, RoleId={roleId}, TabTypeId={tabTypeId}, RoomServiceItemId={roomServiceItemId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_IPDTabs",
                    CommandType.StoredProcedure,
                    new
                    {
                        userId = globalValues.userId,
                        branchId = branchId,
                        roleId = roleId,
                        tabTypeId = tabTypeId,
                        roomServiceItemId = roomServiceItemId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No billing tabs found for BranchId={branchId}, RoleId={roleId}, TabTypeId={tabTypeId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No billing tabs found",
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

                _log.Info($"Retrieved {result.Count} billing tab(s)");

                return ServiceResult<object>.Success(
                    result,
                    "Info",
                    $"{result.Count} billing tab(s) retrieved successfully",
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


        public ServiceResult<string> UpdateFavoriteBillingTab(UpdateFavoriteBillingTabRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateFavoriteBillingTab called. BranchId={request.BranchId}, RoleId={request.RoleId}, TabId={request.TabId}, UserId={globalValues.userId}");

                _sqlHelper.DML(
                    "U_UpdateFavoriteBillingTabs",
                    CommandType.StoredProcedure,
                    new
                    {
                        @userId = globalValues.userId,
                        @branchId = request.BranchId,
                        @roleId = request.RoleId,
                        @tabId = request.TabId
                    }
                );

                _log.Info($"Favorite billing tab updated successfully. UserId={globalValues.userId}, TabId={request.TabId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Favorite billing tab updated successfully",
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

        public ServiceResult<object> GetAssignBranchRight(int branchId)
        {
            try
            {
                _log.Info($"GetAssignBranchRight called. BranchId={branchId}");

                string cacheKey = $"_AssignBranchRight_{branchId}";

                var cachedData = _distributedCache.GetString(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"AssignBranchRight retrieved from cache. Key={cacheKey}");
                    return ServiceResult<object>.Success(
                        System.Text.Json.JsonSerializer.Deserialize<object>(cachedData),
                        "Info",
                        "Data retrieved successfully",
                        200
                    );
                }

                _log.Info($"AssignBranchRight cache miss. Fetching from database. Key={cacheKey}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetBranchAccessRights",
                    CommandType.StoredProcedure,
                    new { @BranchId = branchId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var notFoundAlert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(notFoundAlert.Type, "No branch rights found", 404);
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
                _log.Info($"AssignBranchRight cached permanently. Key={cacheKey}, Count={rawData.Count}");

                return ServiceResult<object>.Success(
                    rawData,
                    "Info",
                    $"{rawData.Count} right(s) retrieved successfully",
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


        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetPatientLedgerBill(int patientId)
        {
            try
            {
                _log.Info($"GetPatientLedgerBill called. PatientId={patientId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientLedgerBill",
                    CommandType.StoredProcedure,
                    new { @patientId = patientId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No ledger bill found for PatientId={patientId}");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        "No ledger bill found for this patient",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetPatientLedgerBill retrieved {result.Count} record(s) for PatientId={patientId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} ledger bill record(s) retrieved successfully",
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



        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetReceiptPaymentDetails(int receiptId)
        {
            try
            {
                _log.Info($"GetReceiptPaymentDetails called. ReceiptId={receiptId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetReceiptPaymentDetails",
                    CommandType.StoredProcedure,
                    new { ReceiptId = receiptId }
                );

                var result = dataTable.ToRawList();

                if (!result.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No receipt payment details found for ReceiptId={receiptId}");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        $"No payment details found for ReceiptId: {receiptId}",
                        404
                    );
                }

                _log.Info($"Retrieved {result.Count} receipt payment detail(s) for ReceiptId={receiptId}");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    $"{result.Count} payment detail(s) retrieved successfully",
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

      
    }
}