using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using log4net;
using Microsoft.Data.SqlClient;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Models;
using HISWEBAPI.DTO;
using HISWEBAPI.Services;
using HISWEBAPI.Utilities;
using HISWEBAPI.Utilities.Helpers;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Services.Interfaces;
using HISWEBAPI.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace HISWEBAPI.Repositories.Implementations
{
    public class UserRepository : IUserRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly ISmsService _smsService;
        private readonly IEmailService _emailService;
        private readonly IJwtService _jwtService;
        private readonly JwtSettings _jwtSettings;
        private readonly IDistributedCache _distributedCache;
        private readonly ITokenRevocationService _tokenRevocation;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly int _maxFailedAttempts;
        private readonly int _lockoutMinutes;
        // SMS OTP policy (appsettings "OtpPolicy")
        private readonly int _otpExpiryMinutes;
        private readonly int _otpMaxWrongAttempts;
        private readonly int _otpResendCooldownSeconds;
        private readonly int _otpMaxSendsPerWindow;
        private readonly int _otpSendWindowMinutes;
        private readonly int _otpLockoutMinutes;

        public UserRepository(
            ICustomSqlHelper sqlHelper,
            IResponseMessageService messageService,
            ISmsService smsService,
            IEmailService emailService,
            IJwtService jwtService,
            IOptions<JwtSettings> jwtSettings,
            IDistributedCache distributedCache,
            IConfiguration configuration,
            ITokenRevocationService tokenRevocation)
        {
            _tokenRevocation = tokenRevocation;
            // login lockout settings (appsettings "LoginLockout"), defaults 5 attempts / 15 minutes
            _maxFailedAttempts = Math.Max(1, configuration.GetValue("LoginLockout:MaxFailedAttempts", 5));
            _lockoutMinutes = Math.Max(1, configuration.GetValue("LoginLockout:LockoutMinutes", 15));
            _otpExpiryMinutes = Math.Max(1, configuration.GetValue("OtpPolicy:ExpiryMinutes", 5));
            _otpMaxWrongAttempts = Math.Max(1, configuration.GetValue("OtpPolicy:MaxWrongAttempts", 5));
            _otpResendCooldownSeconds = Math.Max(0, configuration.GetValue("OtpPolicy:ResendCooldownSeconds", 60));
            _otpMaxSendsPerWindow = Math.Max(1, configuration.GetValue("OtpPolicy:MaxSendsPerWindow", 5));
            _otpSendWindowMinutes = Math.Max(1, configuration.GetValue("OtpPolicy:SendWindowMinutes", 60));
            _otpLockoutMinutes = Math.Max(1, configuration.GetValue("OtpPolicy:LockoutMinutes", 15));
            _sqlHelper = sqlHelper;
            _messageService = messageService;
            _smsService = smsService;
            _emailService = emailService;
            _jwtService = jwtService;
            _jwtSettings = jwtSettings.Value;
            _distributedCache = distributedCache;

        }

        #region Login lockout (wrong password limit, kept in Redis)

        private sealed class LoginLockState
        {
            public int FailedCount { get; set; }
            public DateTime FirstFailedUtc { get; set; }
            public DateTime? LockedUntilUtc { get; set; }
        }

        private static string LoginLockKey(string userName) =>
            "_LoginLockout_" + (userName ?? "").Trim().ToLowerInvariant();

        /// <summary>Current failed-attempt state of a user name; null when none or Redis is not reachable.</summary>
        private LoginLockState? GetLoginLockState(string userName)
        {
            try
            {
                string? json = _distributedCache.GetString(LoginLockKey(userName));
                return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<LoginLockState>(json);
            }
            catch (Exception ex)
            {
                // Redis down -> login keeps working without the limit
                _log.Error($"Login lockout state could not be read for UserName={userName}: {ex.Message}");
                return null;
            }
        }

        private void ClearLoginLockState(string userName)
        {
            try { _distributedCache.Remove(LoginLockKey(userName)); }
            catch (Exception ex) { _log.Error($"Login lockout state could not be cleared for UserName={userName}: {ex.Message}"); }
        }

        /// <summary>
        /// Counts one wrong login. Failed tries older than the lockout window stop counting;
        /// reaching the limit blocks the user name for LockoutMinutes.
        /// </summary>
        private ServiceResult<UserLoginResponseData> LoginFailedResult(string userName, string? ipAddress, string alertType, string alertMessage)
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                var window = TimeSpan.FromMinutes(_lockoutMinutes);
                var state = GetLoginLockState(userName);

                if (state == null || state.FirstFailedUtc.Add(window) <= now ||
                    (state.LockedUntilUtc.HasValue && state.LockedUntilUtc <= now))
                    state = new LoginLockState { FailedCount = 0, FirstFailedUtc = now };

                state.FailedCount++;
                DateTime expiresAt;

                if (state.FailedCount >= _maxFailedAttempts)
                {
                    state.LockedUntilUtc = now.Add(window);
                    expiresAt = state.LockedUntilUtc.Value;
                    _log.Warn($"Login locked for {_lockoutMinutes} min: UserName={userName}, IP={ipAddress}, failedAttempts={state.FailedCount}");
                }
                else
                {
                    expiresAt = state.FirstFailedUtc.Add(window);
                    _log.Warn($"Failed login {state.FailedCount}/{_maxFailedAttempts}: UserName={userName}, IP={ipAddress}");
                }

                _distributedCache.SetString(LoginLockKey(userName), JsonSerializer.Serialize(state),
                    new DistributedCacheEntryOptions { AbsoluteExpiration = new DateTimeOffset(expiresAt, TimeSpan.Zero) });

                if (state.LockedUntilUtc.HasValue)
                    return ServiceResult<UserLoginResponseData>.Failure(
                        "Error",
                        $"Too many failed login attempts. Your login is blocked for {_lockoutMinutes} minute(s).",
                        429
                    );

                int left = _maxFailedAttempts - state.FailedCount;
                if (left == 1)
                    alertMessage = $"{alertMessage} {left} attempt(s) left before the login is blocked for {_lockoutMinutes} minute(s).";
            }
            catch (Exception ex)
            {
                // Redis down -> plain "invalid credentials", no limit
                _log.Error($"Login lockout state could not be saved for UserName={userName}: {ex.Message}");
            }

            return ServiceResult<UserLoginResponseData>.Failure(alertType, alertMessage, 401);
        }

        #endregion

        #region Login history (dbo.UserLoginSessions)

        private static string? Cut(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) ? value : (value.Length > maxLength ? value.Substring(0, maxLength) : value);

        /// <summary>
        /// Saves one row in UserLoginSessions for a successful login (SP I_UserLoginSession) and
        /// returns its SessionId. A failure here is only logged: it never blocks the login.
        /// </summary>
        private long CreateLoginSession(int userId, int branchId, string? ipAddress, string? userAgent, string? clientType)
        {
            try
            {
                var (browser, browserVersion, os, device, deviceType) = BrowserDetectionHelper.ParseUserAgent(userAgent ?? "");

                // Native mobile apps send no browser user-agent (e.g. "okhttp/4.12.0"), so the
                // X-Client-Type header ("Mobile-Android-2.3") decides how the login is recorded.
                var (isMobile, platform, appVersion, isTablet) = BrowserDetectionHelper.ParseClientType(clientType);
                if (isMobile)
                {
                    browser = "Mobile App";
                    browserVersion = appVersion ?? "Unknown";
                    deviceType = isTablet ? "Tablet" : "Mobile";

                    // keep a more exact OS from the user-agent ("Android 14"), else use the header's platform
                    if (!string.IsNullOrEmpty(platform) &&
                        !os.StartsWith(platform, StringComparison.OrdinalIgnoreCase))
                        os = platform;

                    // keep a real model from the user-agent ("SM-S918B", "iPhone"), else a generic name
                    if (device is "Unknown" or "Desktop")
                        device = isTablet ? "Tablet" : "Mobile";
                }

                var dt = _sqlHelper.GetDataTable("I_UserLoginSession", CommandType.StoredProcedure, new
                {
                    UserId = userId,
                    BranchId = branchId,
                    IpAddress = Cut(ipAddress, 50),
                    UserAgent = Cut(userAgent, 500),
                    Browser = Cut(browser, 100),
                    BrowserVersion = Cut(browserVersion, 50),
                    OperatingSystem = Cut(os, 100),
                    Device = Cut(device, 200),
                    DeviceType = Cut(deviceType, 50),
                    Location = (string?)null
                });

                long sessionId = dt != null && dt.Rows.Count > 0 && dt.Rows[0]["SessionId"] != DBNull.Value
                    ? Convert.ToInt64(dt.Rows[0]["SessionId"])
                    : 0;

                _log.Info($"Login session saved. SessionId={sessionId}, UserId={userId}, BranchId={branchId}, IP={ipAddress}, Client={clientType}, Browser={browser} {browserVersion}, OS={os}, DeviceType={deviceType}");
                return sessionId;
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                return 0;
            }
        }

        #endregion

        public ServiceResult<UserLoginResponseData> UserLogin(UserLoginRequest request, string? ipAddress = null, string? userAgent = null, string? clientType = null)
        {
            try
            {
                // blocked after too many wrong passwords -> refuse even a correct password until the block ends
                var lockState = GetLoginLockState(request.UserName);
                if (lockState?.LockedUntilUtc is DateTime lockedUntil && lockedUntil > DateTime.UtcNow)
                {
                    int minutesLeft = (int)Math.Ceiling((lockedUntil - DateTime.UtcNow).TotalMinutes);
                    _log.Warn($"Login blocked (locked out) for UserName={request.UserName}, IP={ipAddress}, minutesLeft={minutesLeft}");
                    return ServiceResult<UserLoginResponseData>.Failure(
                        "Error",
                        $"Too many failed login attempts. Try again in {minutesLeft} minute(s).",
                        429
                    );
                }

                var dataTable = _sqlHelper.GetDataTable("S_UserLogin", CommandType.StoredProcedure, new
                {
                    BranchId = request.BranchId,
                    UserName = request.UserName
                });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_CREDENTIALS");
                    _log.Warn($"Invalid credentials for UserName={request.UserName}");
                    // unknown user names count too, so the lockout does not reveal which names exist
                    return LoginFailedResult(request.UserName, ipAddress, alert.Type, alert.Message);
                }

                DataRow userRow = dataTable.Rows[0];
                string storedHash = Convert.ToString(userRow["Password"]);
                bool isPasswordValid = PasswordHasher.VerifyPassword(request.Password, storedHash);

                if (!isPasswordValid)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_CREDENTIALS");
                    _log.Warn($"Invalid password for UserName={request.UserName}");
                    return LoginFailedResult(request.UserName, ipAddress, alert.Type, alert.Message);
                }

                // correct password -> start counting from zero again
                ClearLoginLockState(request.UserName);

                int userId = Convert.ToInt32(userRow["Id"]);
                string name = userRow["Name"]?.ToString() ?? "";
                string userName = userRow["UserName"]?.ToString() ?? "";
                int hospId = Convert.ToInt32(userRow["HospId"]);

                // login history row in UserLoginSessions (0 if it could not be saved)
                long sessionId = CreateLoginSession(userId, request.BranchId, ipAddress, userAgent, clientType);

                // Generate token with userId, username, name, hospId and the login sessionId
                string accessToken = _jwtService.GenerateToken(
                    userId,
                    userName.ToString(),
                    name.ToString(),
                    hospId,
                    sessionId
                );

                var responseData = new UserLoginResponseData
                {
                    userId = userId,
                    userName = request.UserName,
                    email = Convert.ToString(userRow["Email"]),
                    contact = Convert.ToString(userRow["Contact"]),
                    isContactVerified = Convert.ToBoolean(userRow["IsContactVerified"]),
                    isEmailVerified = Convert.ToBoolean(userRow["IsEmailVerified"]),
                    // column added by 2026-11-17_UserMaster_ContactVerificationRequired.sql; default Yes until it exists
                    contactVerificationRequired = !userRow.Table.Columns.Contains("IsContactVerificationRequired")
                        || userRow["IsContactVerificationRequired"] == DBNull.Value
                        || Convert.ToBoolean(userRow["IsContactVerificationRequired"]),
                    branchId = request.BranchId,
                    accessToken = accessToken,
                    tokenType = "Bearer",
                    expiresIn = _jwtSettings.ExpiryMinutes * 60, // Convert minutes to seconds
                    sessionId = sessionId
                };

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("LOGIN_SUCCESS");
                _log.Info($"Login successful for UserId={userId}, HospId={hospId}, BranchId={request.BranchId}");

                return ServiceResult<UserLoginResponseData>.Success(
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
                return ServiceResult<UserLoginResponseData>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
      
        /// <summary>
        /// Ends the caller's login: cancels the access token used for this call (jti, until it
        /// would expire), marks its UserLoginSessions row "Logged Out" and invalidates refresh tokens.
        /// The session id is taken from the token; the body value is only used for older tokens without it.
        /// </summary>
        public ServiceResult<string> Logout(LogoutRequest request, long tokenSessionId, string? tokenJti, DateTime tokenExpiresUtc)
        {
            try
            {
                // 1. the access token stops working at once
                _tokenRevocation.Revoke(tokenJti, tokenExpiresUtc);

                // 2. close the login session row
                long sessionId = tokenSessionId > 0 ? tokenSessionId : (request?.SessionId ?? 0);
                if (sessionId > 0)
                {
                    bool sessionUpdated = UpdateLoginSessionInternal(sessionId, "Logged Out", request?.LogoutReason ?? "User Logout");
                    InvalidateRefreshTokenInternal(sessionId); // no rows while refresh tokens are not issued

                    if (!sessionUpdated)
                        _log.Warn($"Logout: login session not updated for SessionId={sessionId}");
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("LOGOUT_SUCCESS");
                _log.Info($"User logged out successfully. SessionId={sessionId}, jti={tokenJti}");

                return ServiceResult<string>.Success(
                    "Logged out successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("LOGOUT_ERROR");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<UserSignupResponseData> NewUserSignUp(UserSignupRequest request)
        {
            try
            {
                SqlParameter[] parameters = new SqlParameter[]
                {
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
                    new SqlParameter("@Result", SqlDbType.BigInt) { Direction = ParameterDirection.Output }
                };

                long result = _sqlHelper.RunProcedureInsert("I_NewUserSignUp", parameters);
                _distributedCache.Remove("_UserMaster_All");
                if (result == -1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("USERNAME_EXISTS");
                    _log.Warn($"Duplicate username attempted: {request.UserName}");
                    return ServiceResult<UserSignupResponseData>.Failure(
                        alert.Type,
                        alert.Message,
                        409
                    );
                }

                if (result == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("LICENSE_LIMIT_REACHED");
                    _log.Warn("License validation failed for user insert");
                    return ServiceResult<UserSignupResponseData>.Failure(
                        alert.Type,
                        alert.Message,
                        400
                    );
                }

                if (result > 0)
                {
                    var responseData = new UserSignupResponseData { userId = result };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("USER_CREATED");
                    _log.Info($"User inserted successfully. UserId={result}");
                    return ServiceResult<UserSignupResponseData>.Success(
                        responseData,
                        alert.Type,
                        alert.Message,
                        201
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("USER_SAVE_FAILED");
                _log.Error("Failed to insert user. Result=0");
                return ServiceResult<UserSignupResponseData>.Failure(
                    alert1.Type,
                    alert1.Message,
                    500
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SIGNUP_ERROR");
                return ServiceResult<UserSignupResponseData>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<SmsOtpResponseData> SendSmsOtp(SendSmsOtpRequest request)
        {
            try
            {
                var (userExists, contactMatch, userId, registeredContact) = ValidateUserForPasswordResetInternal(request.UserName, request.Contact);

                if (!userExists)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("USERNAME_NOT_FOUND");
                    _log.Warn($"Username not found: {request.UserName}");
                    return ServiceResult<SmsOtpResponseData>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                if (!contactMatch)
                {
                    string contactHint = GenerateContactHint(registeredContact);
                    var alert = _messageService.GetMessageAndTypeByAlertCode("CONTACT_NOT_MATCH");
                    _log.Warn($"Contact mismatch for username: {request.UserName}");

                    var failureData = new SmsOtpResponseData { contactHint = contactHint };
                    return ServiceResult<SmsOtpResponseData>.Failure(
                        alert.Type,
                        $"{alert.Message} to {contactHint}",
                        400
                    );
                }

                // resend cooldown + cap on sends per window
                var (waitSeconds, reason) = GetOtpResendWait(userId, "SMS");
                if (waitSeconds > 0)
                {
                    var alert = OtpThrottleAlert(reason);
                    _log.Warn($"SMS OTP refused for {request.UserName}: {reason}, wait {waitSeconds}s");
                    var throttled = ServiceResult<SmsOtpResponseData>.Failure(
                        alert.Type, $"{alert.Message} Try again in {FormatWait(waitSeconds)}.", 429);
                    throttled.Data = new SmsOtpResponseData
                    {
                        userId = userId,
                        contactHint = GenerateContactHint(registeredContact),
                        resendAfterSeconds = waitSeconds,
                        lockedSeconds = reason == OtpWaitReason.Locked ? waitSeconds : 0,
                        otpExpiryMinutes = _otpExpiryMinutes,
                        maxAttempts = _otpMaxWrongAttempts
                    };
                    return throttled;
                }

                string otp = GenerateOtp();
                bool otpStored = StoreOtpForPasswordResetInternal(userId, otp, _otpExpiryMinutes);

                if (!otpStored)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OTP_GENERATION_FAILED");
                    _log.Error($"Failed to store OTP for user: {request.UserName}");
                    return ServiceResult<SmsOtpResponseData>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                bool smsSent = _smsService.SendOtp(registeredContact, otp);

                if (!smsSent)
                {
                    _log.Warn($"OTP stored but SMS failed for user: {request.UserName}");
                }

                string contactHintSuccess = GenerateContactHint(registeredContact);
                var responseData = new SmsOtpResponseData
                {
                    userId = userId,
                    contactHint = contactHintSuccess,
                    resendAfterSeconds = _otpResendCooldownSeconds,
                    otpExpiryMinutes = _otpExpiryMinutes,
                    maxAttempts = _otpMaxWrongAttempts
                };

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OTP_SENT_SMS");
                _log.Info($"OTP sent successfully for username: {request.UserName}, UserId: {userId}");

                return ServiceResult<SmsOtpResponseData>.Success(
                    responseData,
                    alert1.Type,
                    $"{alert1.Message} to {contactHintSuccess}",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SMS_OTP_ERROR");
                return ServiceResult<SmsOtpResponseData>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<OtpVerificationResponseData> VerifySmsOtp(VerifySmsOtpRequest request)
        {
            try
            {
                var (result, message, attemptsLeft, lockedSeconds) = VerifySmsOtpInternal(request.UserId, request.Otp);

                var responseData = new OtpVerificationResponseData
                {
                    userId = request.UserId,
                    otp = request.Otp
                };

                switch (result)
                {
                    case 1:
                        var alert = _messageService.GetMessageAndTypeByAlertCode("OTP_VERIFIED");
                        _log.Info($"OTP verified successfully for UserId: {request.UserId}");
                        return ServiceResult<OtpVerificationResponseData>.Success(
                            responseData,
                            alert.Type,
                            alert.Message,
                            200
                        );

                    case -1:
                        var alert1 = _messageService.GetMessageAndTypeByAlertCode("USER_NOT_FOUND_OTP");
                        _log.Warn($"User not found: UserId={request.UserId}");
                        return ServiceResult<OtpVerificationResponseData>.Failure(
                            alert1.Type,
                            alert1.Message,
                            404
                        );

                    case -2:
                        var alert2 = _messageService.GetMessageAndTypeByAlertCode("USER_INACTIVE");
                        _log.Warn($"Inactive user attempted OTP verification: UserId={request.UserId}");
                        return ServiceResult<OtpVerificationResponseData>.Failure(
                            alert2.Type,
                            alert2.Message,
                            400
                        );

                    case -3:
                        var alert3 = _messageService.GetMessageAndTypeByAlertCode("OTP_VALIDATION_FAILED");
                        _log.Warn($"OTP validation failed for UserId {request.UserId}: {message}");
                        return ServiceResult<OtpVerificationResponseData>.Failure(
                            alert3.Type,
                            alert3.Message,
                            400
                        );

                    case -4:
                        var alertUsed = OtpAlert("OTP_ALREADY_USED", "Error", "This OTP has already been used. Please request a new OTP.");
                        _log.Warn($"Used OTP entered for UserId {request.UserId}");
                        return ServiceResult<OtpVerificationResponseData>.Failure(alertUsed.Type, alertUsed.Message, 400);

                    case -5:
                        var alertExpired = _messageService.GetMessageAndTypeByAlertCode("OTP_EXPIRED");
                        _log.Warn($"Expired OTP entered for UserId {request.UserId}");
                        return ServiceResult<OtpVerificationResponseData>.Failure(alertExpired.Type, alertExpired.Message, 400);

                    case -6:
                        var alert4 = _messageService.GetMessageAndTypeByAlertCode("INVALID_OTP");
                        _log.Warn($"Invalid OTP entered for UserId: {request.UserId}, attempts left {attemptsLeft}");
                        responseData.attemptsLeft = attemptsLeft;
                        var wrong = ServiceResult<OtpVerificationResponseData>.Failure(
                            alert4.Type,
                            attemptsLeft.HasValue
                                ? $"{alert4.Message} {attemptsLeft} attempt{(attemptsLeft == 1 ? "" : "s")} left."
                                : alert4.Message,
                            400
                        );
                        wrong.Data = responseData;
                        return wrong;

                    case -7:
                    case -8:
                        var alert7 = OtpLockAlert(result, lockedSeconds);
                        _log.Warn($"OTP locked for UserId {request.UserId} (result {result}), {lockedSeconds}s left");
                        responseData.attemptsLeft = 0;
                        responseData.lockedSeconds = lockedSeconds;
                        var blocked = ServiceResult<OtpVerificationResponseData>.Failure(alert7.Type, alert7.Message, 429);
                        blocked.Data = responseData;
                        return blocked;

                    default:
                        var alert5 = _messageService.GetMessageAndTypeByAlertCode("OTP_VERIFICATION_ERROR");
                        _log.Error($"Unknown error during OTP verification for UserId: {request.UserId}. Result code: {result}");
                        return ServiceResult<OtpVerificationResponseData>.Failure(
                            alert5.Type,
                            alert5.Message,
                            500
                        );
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("VERIFY_OTP_ERROR");
                return ServiceResult<OtpVerificationResponseData>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<EmailOtpResponseData> SendEmailOtp(SendEmailOtpRequest request)
        {
            try
            {
                var (userExists, emailMatch, userId, registeredEmail) = ValidateUserForEmailPasswordResetInternal(request.UserName, request.Email);

                if (!userExists)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("USERNAME_NOT_FOUND");
                    _log.Warn($"Username not found: {request.UserName}");
                    return ServiceResult<EmailOtpResponseData>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                if (!emailMatch)
                {
                    string emailHint = GenerateEmailHint(registeredEmail);
                    var alert = _messageService.GetMessageAndTypeByAlertCode("EMAIL_NOT_MATCH");
                    _log.Warn($"Email mismatch for username: {request.UserName}");

                    var failureData = new EmailOtpResponseData { emailHint = emailHint };
                    return ServiceResult<EmailOtpResponseData>.Failure(
                        alert.Type,
                        $"{alert.Message} to {emailHint}",
                        400
                    );
                }

                // resend cooldown + cap on sends per window
                var (waitSeconds, reason) = GetOtpResendWait(userId, "EMAIL");
                if (waitSeconds > 0)
                {
                    var alert = OtpThrottleAlert(reason);
                    _log.Warn($"Email OTP refused for {request.UserName}: {reason}, wait {waitSeconds}s");
                    var throttled = ServiceResult<EmailOtpResponseData>.Failure(
                        alert.Type, $"{alert.Message} Try again in {FormatWait(waitSeconds)}.", 429);
                    throttled.Data = new EmailOtpResponseData
                    {
                        userId = userId,
                        emailHint = GenerateEmailHint(registeredEmail),
                        resendAfterSeconds = waitSeconds,
                        lockedSeconds = reason == OtpWaitReason.Locked ? waitSeconds : 0,
                        otpExpiryMinutes = _otpExpiryMinutes,
                        maxAttempts = _otpMaxWrongAttempts
                    };
                    return throttled;
                }

                string otp = GenerateOtp();
                bool otpStored = StoreEmailOtpForPasswordResetInternal(userId, otp, _otpExpiryMinutes);

                if (!otpStored)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OTP_GENERATION_FAILED");
                    _log.Error($"Failed to store OTP for user: {request.UserName}");
                    return ServiceResult<EmailOtpResponseData>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                bool emailSent = _emailService.SendOtpEmail(registeredEmail, otp, "Password Reset").GetAwaiter().GetResult();

                if (!emailSent)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("EMAIL_SEND_FAILED");
                    _log.Error($"Failed to send email to: {registeredEmail}");
                    return ServiceResult<EmailOtpResponseData>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                string emailHintSuccess = GenerateEmailHint(registeredEmail);
                var responseData = new EmailOtpResponseData
                {
                    userId = userId,
                    emailHint = emailHintSuccess,
                    resendAfterSeconds = _otpResendCooldownSeconds,
                    otpExpiryMinutes = _otpExpiryMinutes,
                    maxAttempts = _otpMaxWrongAttempts
                };

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OTP_SENT_EMAIL");
                _log.Info($"OTP sent successfully via email for username: {request.UserName}, UserId: {userId}");

                return ServiceResult<EmailOtpResponseData>.Success(
                    responseData,
                    alert1.Type,
                    $"{alert1.Message} to {emailHintSuccess}",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_ERROR");
                return ServiceResult<EmailOtpResponseData>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<object> VerifyEmailOtp(VerifyEmailOtpRequest request)
        {
            try
            {
                var (result, message, attemptsLeft, lockedSeconds) = VerifyEmailOtpInternal(request.UserId, request.Otp);

                switch (result)
                {
                    case 1:
                        var alert = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VERIFIED");
                        _log.Info($"Email OTP verified successfully for UserId: {request.UserId}");
                        return ServiceResult<object>.Success(
                            "Email OTP verified",
                            alert.Type,
                            alert.Message,
                            200
                        );

                    case -1:
                        var alert1 = _messageService.GetMessageAndTypeByAlertCode("USER_NOT_FOUND_OTP");
                        _log.Warn($"User not found: UserId={request.UserId}");
                        return ServiceResult<object>.Failure(
                            alert1.Type,
                            alert1.Message,
                            404
                        );

                    case -2:
                        var alert2 = _messageService.GetMessageAndTypeByAlertCode("USER_INACTIVE");
                        _log.Warn($"Inactive user attempted OTP verification: UserId={request.UserId}");
                        return ServiceResult<object>.Failure(
                            alert2.Type,
                            alert2.Message,
                            400
                        );

                    case -3:
                        var alert3 = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VALIDATION_FAILED");
                        _log.Warn($"Email OTP validation failed for UserId {request.UserId}: {message}");
                        return ServiceResult<object>.Failure(
                            alert3.Type,
                            alert3.Message,
                            400
                        );

                    case -4:
                        var alertUsed = OtpAlert("OTP_ALREADY_USED", "Error", "This OTP has already been used. Please request a new OTP.");
                        _log.Warn($"Used email OTP entered for UserId {request.UserId}");
                        return ServiceResult<object>.Failure(alertUsed.Type, alertUsed.Message, 400);

                    case -5:
                        var alertExpired = _messageService.GetMessageAndTypeByAlertCode("OTP_EXPIRED");
                        _log.Warn($"Expired email OTP entered for UserId {request.UserId}");
                        return ServiceResult<object>.Failure(alertExpired.Type, alertExpired.Message, 400);

                    case -6:
                        var alert4 = _messageService.GetMessageAndTypeByAlertCode("INVALID_EMAIL_OTP");
                        _log.Warn($"Invalid email OTP entered for UserId: {request.UserId}, attempts left {attemptsLeft}");
                        var wrong = ServiceResult<object>.Failure(
                            alert4.Type,
                            attemptsLeft.HasValue
                                ? $"{alert4.Message} {attemptsLeft} attempt{(attemptsLeft == 1 ? "" : "s")} left."
                                : alert4.Message,
                            400
                        );
                        wrong.Data = new { userId = request.UserId, attemptsLeft };
                        return wrong;

                    case -7:
                    case -8:
                        var alert7 = OtpLockAlert(result, lockedSeconds);
                        _log.Warn($"Email OTP locked for UserId {request.UserId} (result {result}), {lockedSeconds}s left");
                        var blocked = ServiceResult<object>.Failure(alert7.Type, alert7.Message, 429);
                        blocked.Data = new { userId = request.UserId, attemptsLeft = 0, lockedSeconds };
                        return blocked;

                    default:
                        var alert5 = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VERIFICATION_ERROR");
                        _log.Error($"Unknown error during email OTP verification for UserId: {request.UserId}. Result code: {result}");
                        return ServiceResult<object>.Failure(
                            alert5.Type,
                            alert5.Message,
                            500
                        );
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("VERIFY_EMAIL_OTP_ERROR");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> ResetPasswordByUserId(ResetPasswordRequest request)
        {
            try
            {
                if (request.NewPassword != request.ConfirmPassword)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("PASSWORD_MISMATCH");
                    _log.Warn($"Password mismatch for UserId: {request.UserId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        400
                    );
                }

                // wrong-OTP lockout also blocks password reset
                int lockedFor = GetOtpLockedSeconds(request.UserId);
                if (lockedFor > 0)
                {
                    var alertLocked = OtpLockAlert(-8, lockedFor);
                    _log.Warn($"Password reset refused, OTP lock active for UserId {request.UserId}, {lockedFor}s left");
                    return ServiceResult<string>.Failure(alertLocked.Type, alertLocked.Message, 429);
                }

                string hashedPassword = PasswordHasher.HashPassword(request.NewPassword);
                var (result, message) = ResetPasswordByUserIdInternal(request.UserId, request.Otp, hashedPassword);

                if (result)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("PASSWORD_RESET_SUCCESS");
                    _log.Info($"Password reset successfully for UserId: {request.UserId}");
                    return ServiceResult<string>.Success(
                        "Password reset successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("PASSWORD_RESET_FAILED");
                    _log.Warn($"Password reset failed for UserId: {request.UserId}. Message: {message}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        400
                    );
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("RESET_PASSWORD_ERROR");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> UpdatePassword(UpdatePasswordRequest request)
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable("S_GetUserByUserId", CommandType.StoredProcedure, new
                {
                    @userId = request.UserId
                });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("USER_NOT_FOUND");
                    _log.Warn($"User not found for UserId: {request.UserId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                string storedHash = dataTable.Rows[0]["Password"].ToString();
                bool isValid = PasswordHasher.VerifyPassword(request.CurrentPassword, storedHash);

                if (!isValid)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("CURRENT_PASSWORD_INCORRECT");
                    _log.Warn($"Current password incorrect for UserId: {request.UserId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        400
                    );
                }

                string newHashedPassword = PasswordHasher.HashPassword(request.NewPassword);
                var result = _sqlHelper.DML("U_UserPasswords", CommandType.StoredProcedure, new
                {
                    @userId = request.UserId,
                    @newPassword = newHashedPassword
                });

                if (result < 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("PASSWORD_UPDATE_FAILED");
                    _log.Error($"Password update failed for UserId: {request.UserId}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("PASSWORD_UPDATED");
                _log.Info($"Password updated successfully for UserId: {request.UserId}");
                return ServiceResult<string>.Success(
                    "Password updated",
                    alert1.Type,
                    alert1.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("UPDATE_PASSWORD_ERROR");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<IEnumerable<UserRoleModel>> GetUserRoles(UserRoleRequest request)
        {
            try
            {
                var dataTable = _sqlHelper.GetDataTable("S_GetUserRoles", CommandType.StoredProcedure, new
                {
                    @branchId = request.BranchId,
                    @userId = request.UserId
                });

                var roles = dataTable?.AsEnumerable().Select(row => new UserRoleModel
                {
                    RoleId = row.Field<int>("RoleId"),
                    RoleName = row.Field<string>("RoleName"),
                    RoleIconId = row.Field<int>("RoleIconId"),
                    IsFavoriteRole = row.Field<int>("isFavoriteRole")
                }).ToList() ?? new List<UserRoleModel>();

                // role icons as base64 data URLs from the Redis cache (_RoleIconMaster_All)
                var iconMap = RoleIconCache.GetMap(_distributedCache, _sqlHelper);
                foreach (var role in roles)
                    role.RoleIcon = iconMap.TryGetValue(role.RoleIconId, out var icon) ? icon : null;

                if (!roles.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("NO_ROLES_FOUND");
                    _log.Warn($"No roles found for UserId: {request.UserId}");
                    return ServiceResult<IEnumerable<UserRoleModel>>.Failure(
                        alert.Type,
                        alert.Message,
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("ROLES_RETRIEVED");
                _log.Info($"User roles retrieved successfully for UserId: {request.UserId}");
                return ServiceResult<IEnumerable<UserRoleModel>>.Success(
                    roles,
                    alert1.Type,
                    alert1.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("GET_ROLES_ERROR");
                return ServiceResult<IEnumerable<UserRoleModel>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }


        private (bool userExists, bool contactMatch, long userId, string registeredContact) ValidateUserForPasswordResetInternal(string userName, string contact)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@UserName", userName),
                new SqlParameter("@Contact", contact),
                new SqlParameter("@UserExists", SqlDbType.Bit) { Direction = ParameterDirection.Output },
                new SqlParameter("@ContactMatch", SqlDbType.Bit) { Direction = ParameterDirection.Output },
                new SqlParameter("@UserId", SqlDbType.BigInt) { Direction = ParameterDirection.Output },
                new SqlParameter("@RegisteredContact", SqlDbType.NVarChar, 50) { Direction = ParameterDirection.Output }
            };

            _sqlHelper.RunProcedure("sp_ValidateUserForPasswordReset", parameters);

            bool userExists = parameters[2].Value != DBNull.Value && (bool)parameters[2].Value;
            bool contactMatch = parameters[3].Value != DBNull.Value && (bool)parameters[3].Value;
            long userId = parameters[4].Value != DBNull.Value ? Convert.ToInt64(parameters[4].Value) : 0;
            string registeredContact = parameters[5].Value != DBNull.Value ? parameters[5].Value.ToString() : string.Empty;

            return (userExists, contactMatch, userId, registeredContact);
        }

        private bool StoreOtpForPasswordResetInternal(long userId, string otp, int expiryMinutes)
        {
            try
            {
                var result = _sqlHelper.GetDataTable("IU_StoreOtpForPasswordReset", CommandType.StoredProcedure,
                    new { UserId = userId, Otp = otp, ExpiryMinutes = expiryMinutes });

                if (result != null && result.Rows.Count > 0)
                {
                    int resultValue = Convert.ToInt32(result.Rows[0]["Result"]);
                    return resultValue == 1;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private (int result, string message, int? attemptsLeft, int lockedSeconds) VerifySmsOtpInternal(long userId, string otp) =>
            RunVerifyOtpProcedure("S_VerifySmsOtp", userId, otp);

        /// <summary>Runs S_VerifySmsOtp / S_VerifyEmailOtp (same parameters).</summary>
        private (int result, string message, int? attemptsLeft, int lockedSeconds) RunVerifyOtpProcedure(string procedure, long userId, string otp)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Otp", otp),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output },
                new SqlParameter("@Message", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Output },
                new SqlParameter("@MaxAttempts", _otpMaxWrongAttempts),
                new SqlParameter("@AttemptsLeft", SqlDbType.Int) { Direction = ParameterDirection.Output },
                new SqlParameter("@LockMinutes", _otpLockoutMinutes),
                new SqlParameter("@LockedSeconds", SqlDbType.Int) { Direction = ParameterDirection.Output }
            };

            _sqlHelper.RunProcedure(procedure, parameters);

            static int? IntOrNull(object? v) => v is null || v == DBNull.Value ? null : Convert.ToInt32(v);

            int result = IntOrNull(parameters[2].Value) ?? 0;
            string message = parameters[3].Value != DBNull.Value ? parameters[3].Value?.ToString() ?? "" : "Unknown error";
            return (result, message, IntOrNull(parameters[5].Value), IntOrNull(parameters[7].Value) ?? 0);
        }

        private enum OtpWaitReason { None, Cooldown, SendLimit, Locked }

        /// <summary>
        /// Seconds the user must wait before another OTP of this type, "SMS" / "EMAIL" (0 = may send now),
        /// and why: wrong-OTP lock, per-window send cap, or the short resend cooldown.
        /// If the check itself fails the send is allowed, so a DB glitch never blocks password reset.
        /// </summary>
        private (int waitSeconds, OtpWaitReason reason) GetOtpResendWait(long userId, string otpType)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetOtpSendInfo", CommandType.StoredProcedure,
                    new { UserId = userId, OtpType = otpType, WindowMinutes = _otpSendWindowMinutes });
                if (dt == null || dt.Rows.Count == 0) return (0, OtpWaitReason.None);

                var row = dt.Rows[0];
                int? sinceLast = row["SecondsSinceLastSend"] == DBNull.Value ? null : Convert.ToInt32(row["SecondsSinceLastSend"]);
                int sends = row["SendsInWindow"] == DBNull.Value ? 0 : Convert.ToInt32(row["SendsInWindow"]);
                int? oldestAgo = row["OldestSendSecondsAgo"] == DBNull.Value ? null : Convert.ToInt32(row["OldestSendSecondsAgo"]);
                int locked = dt.Columns.Contains("LockedSeconds") && row["LockedSeconds"] != DBNull.Value
                    ? Convert.ToInt32(row["LockedSeconds"]) : 0;

                if (locked > 0) return (locked, OtpWaitReason.Locked);

                if (sends >= _otpMaxSendsPerWindow && oldestAgo.HasValue)
                {
                    int wait = _otpSendWindowMinutes * 60 - oldestAgo.Value;
                    if (wait > 0) return (wait, OtpWaitReason.SendLimit);
                }

                if (sinceLast.HasValue && sinceLast.Value < _otpResendCooldownSeconds)
                    return (_otpResendCooldownSeconds - sinceLast.Value, OtpWaitReason.Cooldown);

                return (0, OtpWaitReason.None);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                return (0, OtpWaitReason.None);
            }
        }

        /// <summary>Seconds left on the user's wrong-OTP lock (0 = not locked or check failed).</summary>
        private int GetOtpLockedSeconds(long userId)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetOtpLockStatus", CommandType.StoredProcedure, new { UserId = userId });
                return dt != null && dt.Rows.Count > 0 && dt.Rows[0]["LockedSeconds"] != DBNull.Value
                    ? Convert.ToInt32(dt.Rows[0]["LockedSeconds"]) : 0;
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                return 0;
            }
        }

        private (string Type, string Message) OtpThrottleAlert(OtpWaitReason reason) => reason switch
        {
            OtpWaitReason.Locked => OtpAlert("OTP_LOCKED", "Error",
                "OTP verification and password reset are temporarily blocked because of too many wrong OTPs."),
            OtpWaitReason.SendLimit => OtpAlert("OTP_SEND_LIMIT_REACHED", "Error", "OTP request limit reached. Please try again later."),
            _ => OtpAlert("OTP_RESEND_WAIT", "Warn", "Please wait before requesting a new OTP.")
        };

        /// <summary>-7 = this wrong OTP started the lock, -8 = already locked.</summary>
        private (string Type, string Message) OtpLockAlert(int result, int lockedSeconds)
        {
            var alert = result == -7
                ? OtpAlert("OTP_ATTEMPTS_EXCEEDED", "Error",
                    "Too many wrong OTPs. OTP verification and password reset are blocked for this user.")
                : OtpAlert("OTP_LOCKED", "Error",
                    "OTP verification and password reset are temporarily blocked because of too many wrong OTPs.");
            int wait = lockedSeconds > 0 ? lockedSeconds : _otpLockoutMinutes * 60;
            return (alert.Type, $"{alert.Message} Try again in {FormatWait(wait)}.");
        }

        /// <summary>Alert from ResponseMessageMaster, with a fallback while a new code is not yet in the DB/cache.</summary>
        private (string Type, string Message) OtpAlert(string code, string fallbackType, string fallbackMessage)
        {
            var alert = _messageService.GetMessageAndTypeByAlertCode(code);
            if (string.IsNullOrWhiteSpace(alert.Message) || alert.Message == "Response message not found.")
                return (fallbackType, fallbackMessage);
            return (alert.Type, alert.Message);
        }

        private static string FormatWait(int seconds) =>
            seconds >= 60 ? $"{(seconds + 59) / 60} min" : $"{seconds} sec";

        private (bool userExists, bool emailMatch, long userId, string registeredEmail) ValidateUserForEmailPasswordResetInternal(string userName, string email)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@UserName", userName),
                new SqlParameter("@Email", email),
                new SqlParameter("@UserExists", SqlDbType.Bit) { Direction = ParameterDirection.Output },
                new SqlParameter("@EmailMatch", SqlDbType.Bit) { Direction = ParameterDirection.Output },
                new SqlParameter("@UserId", SqlDbType.BigInt) { Direction = ParameterDirection.Output },
                new SqlParameter("@RegisteredEmail", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Output }
            };

            _sqlHelper.RunProcedure("S_ValidateUserForEmailPasswordReset", parameters);

            bool userExists = parameters[2].Value != DBNull.Value && (bool)parameters[2].Value;
            bool emailMatch = parameters[3].Value != DBNull.Value && (bool)parameters[3].Value;
            long userId = parameters[4].Value != DBNull.Value ? Convert.ToInt64(parameters[4].Value) : 0;
            string registeredEmail = parameters[5].Value != DBNull.Value ? parameters[5].Value.ToString() : string.Empty;

            return (userExists, emailMatch, userId, registeredEmail);
        }

        private bool StoreEmailOtpForPasswordResetInternal(long userId, string otp, int expiryMinutes)
        {
            try
            {
                var result = _sqlHelper.GetDataTable("IU_StoreEmailOtpForPasswordReset", CommandType.StoredProcedure,
                    new { UserId = userId, Otp = otp, ExpiryMinutes = expiryMinutes });

                if (result != null && result.Rows.Count > 0)
                {
                    int resultValue = Convert.ToInt32(result.Rows[0]["Result"]);
                    return resultValue == 1;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private (int result, string message, int? attemptsLeft, int lockedSeconds) VerifyEmailOtpInternal(long userId, string otp) =>
            RunVerifyOtpProcedure("S_VerifyEmailOtp", userId, otp);

        private (bool result, string message) ResetPasswordByUserIdInternal(long userId, string otp, string hashedPassword)
        {
            try
            {
                SqlParameter[] parameters = new SqlParameter[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@otp", otp),
                    new SqlParameter("@NewPassword", hashedPassword),
                    new SqlParameter("@Result", SqlDbType.Bit) { Direction = ParameterDirection.Output },
                    new SqlParameter("@Message", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Output }
                };

                _sqlHelper.RunProcedure("U_ResetPasswordByUserId", parameters);

                bool result = parameters[3].Value != DBNull.Value && (bool)parameters[3].Value;
                string message = parameters[4].Value != DBNull.Value ? parameters[4].Value.ToString() : "Unknown error";

                return (result, message);
            }
            catch (Exception ex)
            {
                return (false, "Password reset failed");
            }
        }

        private bool UpdateLoginSessionInternal(long sessionId, string status, string logoutReason = null)
        {
            try
            {
                var result = _sqlHelper.DML("U_UserLoginSession", CommandType.StoredProcedure, new
                {
                    @SessionId = sessionId,
                    @Status = status,
                    @LogoutReason = logoutReason ?? (object)DBNull.Value
                });

                return result > 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        private bool InvalidateRefreshTokenInternal(long sessionId)
        {
            try
            {
                var result = _sqlHelper.DML("U_InvalidateRefreshToken", CommandType.StoredProcedure, new
                {
                    @SessionId = sessionId
                });

                return result > 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private string GenerateOtp()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private string GenerateContactHint(string contact)
        {
            if (string.IsNullOrEmpty(contact))
                return string.Empty;

            int length = contact.Length;

            if (length < 4)
            {
                return new string('*', length);
            }

            string first2 = contact.Substring(0, 2);
            string last2 = contact.Substring(length - 2, 2);
            string middle = new string('*', length - 4);

            return $"{first2}{middle}{last2}";
        }

        private string GenerateEmailHint(string email)
        {
            if (string.IsNullOrEmpty(email))
                return string.Empty;

            var parts = email.Split('@');
            if (parts.Length != 2)
                return email;

            string localPart = parts[0];
            string domain = parts[1];

            if (localPart.Length <= 2)
            {
                return $"{new string('*', localPart.Length)}@{domain}";
            }

            string first2 = localPart.Substring(0, 2);
            string lastChar = localPart.Substring(localPart.Length - 1, 1);
            string middle = new string('*', localPart.Length - 3);

            return $"{first2}{middle}{lastChar}@{domain}";
        }

        public ServiceResult<UserTabMenuMappingResponse> GetUserTabAndSubMenuMapping(int roleId, int branchId, int userId)
        {
            try
            {
                _log.Info($"GetUserTabAndSubMenuMapping called. RoleId={roleId}, BranchId={branchId}, UserId={userId}");

                // Create dynamic cache key based on parameters
                string cacheKey = $"_UserTabMenu_{branchId}_{roleId}_{userId}";

                // Try to get data from cache
                var cachedData = _distributedCache.GetString(cacheKey);
                UserTabMenuMappingResponse mappingResponse;

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _log.Info($"UserTabMenuMapping data retrieved from cache. Key={cacheKey}");
                    mappingResponse = JsonSerializer.Deserialize<UserTabMenuMappingResponse>(cachedData);
                }
                else
                {
                    _log.Info($"UserTabMenuMapping cache miss. Fetching data from database. Key={cacheKey}");

                    // Fetch data from stored procedure
                    var dataSet = _sqlHelper.GetDataSet(
                        "S_UserTabAndSubMenuMapping",
                        CommandType.StoredProcedure,
                        new
                        {
                            @RoleId = roleId,
                            @BranchId = branchId,
                            @UserId = userId
                        }
                    );

                    if (dataSet == null || dataSet.Tables.Count < 2)
                    {
                        var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                        _log.Warn($"No tab/menu data found for RoleId={roleId}, BranchId={branchId}, UserId={userId}");
                        return ServiceResult<UserTabMenuMappingResponse>.Failure(
                            alert.Type,
                            "No menu data found for the user",
                            404
                        );
                    }

                    // Map Tabs (First result set)
                    var tabsTable = dataSet.Tables[0];
                    var tabs = tabsTable?.AsEnumerable().Select(row => new UserTabModel
                    {
                        TabId = row.Field<int>("TabId"),
                        TabName = row.Field<string>("TabName") ?? string.Empty,
                        IconClass = row.Field<string>("IconClass") ?? string.Empty
                    }).ToList() ?? new List<UserTabModel>();

                    // Map SubMenus (Second result set)
                    var subMenusTable = dataSet.Tables[1];
                    var subMenus = subMenusTable?.AsEnumerable().Select(row => new UserSubMenuModel
                    {
                        SubMenuId = row.Field<int>("SubMenuId"),
                        SubMenuName = row.Field<string>("SubMenuName") ?? string.Empty,
                        URL = row.Field<string>("URL") ?? string.Empty,
                        TabId = row.Field<int>("TabId")
                    }).ToList() ?? new List<UserSubMenuModel>();

                    // Map SubMenus (Second result set)
                    var favoriteTable = dataSet.Tables[2];
                    var favoriteSubMenus = favoriteTable?.AsEnumerable().Select(row => new UserSubMenuModel
                    {
                        SubMenuId = row.Field<int>("SubMenuId"),
                        SubMenuName = row.Field<string>("SubMenuName") ?? string.Empty,
                        URL = row.Field<string>("URL") ?? string.Empty
                    }).ToList() ?? new List<UserSubMenuModel>();


                    mappingResponse = new UserTabMenuMappingResponse
                    {
                        Tabs = tabs,
                        SubMenus = subMenus,
                        favoriteSubMenus= favoriteSubMenus
                    };

                    // Cache the data permanently (no expiration)
                    if (tabs.Any() || subMenus.Any())
                    {
                        var serialized = JsonSerializer.Serialize(mappingResponse);
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            // No expiration - cache persists until manually cleared
                            AbsoluteExpiration = null,
                            SlidingExpiration = null
                        };
                        _distributedCache.SetString(cacheKey, serialized, cacheOptions);
                        _log.Info($"UserTabMenuMapping data cached permanently. Key={cacheKey}, Tabs={tabs.Count}, SubMenus={subMenus.Count}");
                    }
                }

                if ((mappingResponse.Tabs == null || !mappingResponse.Tabs.Any()) &&
                    (mappingResponse.SubMenus == null || !mappingResponse.SubMenus.Any()))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No tab/menu mapping found for user");
                    return ServiceResult<UserTabMenuMappingResponse>.Failure(
                        alert.Type,
                        "No menu data found for the user",
                        404
                    );
                }

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                _log.Info($"Retrieved {mappingResponse.Tabs?.Count ?? 0} tabs and {mappingResponse.SubMenus?.Count ?? 0} submenus from cache");

                return ServiceResult<UserTabMenuMappingResponse>.Success(
                    mappingResponse,
                    alert1.Type,
                    $"{mappingResponse.Tabs?.Count ?? 0} tab(s) and {mappingResponse.SubMenus?.Count ?? 0} submenu(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<UserTabMenuMappingResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> SaveUserFavoriteRoles(SaveUserFavoriteRolesRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveUserFavoriteRoles called. BranchId={request.BranchId}, UserId={request.UserId}, RoleIds Count={request.RoleIds.Count}");

                // Convert List<int> to comma-separated string
                string roleIdsString = string.Join(",", request.RoleIds);

                var dataTable = _sqlHelper.GetDataTable(
                    "I_SaveUserWiseFavoriteRoles",
                    CommandType.StoredProcedure,
                    new
                    {
                        BranchId = request.BranchId,
                        UserId = request.UserId,
                        RoleIds = roleIdsString,
                        CreatedBy = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error("No result returned from stored procedure");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                int result = Convert.ToInt32(dataTable.Rows[0]["Result"]);
                string message = Convert.ToString(dataTable.Rows[0]["Message"]);
                int insertedCount = dataTable.Rows[0]["InsertedCount"] != DBNull.Value
                    ? Convert.ToInt32(dataTable.Rows[0]["InsertedCount"])
                    : 0;

                if (result == 1)
                {
                    // Clear cache for user's favorite roles
                    string cacheKey = $"_UserFavoriteRoles_{request.BranchId}_{request.UserId}";
                    _distributedCache.Remove(cacheKey);
                    _log.Info($"Cleared cache for key: {cacheKey}");

                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"User favorite roles saved successfully: {message}, Inserted Count: {insertedCount}");

                    return ServiceResult<string>.Success(
                        $"{insertedCount} favorite role(s) saved successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    _log.Error($"Failed to save user favorite roles: {message}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        message,
                        500
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

        public ServiceResult<string> SaveRoleWiseUserFavoriteSubMenu(SaveRoleWiseUserFavoriteSubMenuRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveRoleWiseUserFavoriteSubMenu called. BranchId={request.BranchId}, UserId={request.UserId}, RoleId={request.RoleId}, SubMenuId={request.SubMenuId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "I_SaveRoleWiseUserFavoriteSubMenu",
                    CommandType.StoredProcedure,
                    new
                    {
                        BranchId = request.BranchId,
                        UserId = request.UserId,
                        RoleId = request.RoleId,
                        SubMenuId = request.SubMenuId,
                        CreatedBy = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    }
                );

                string cacheKey2 = $"_UserTabMenu_{request.BranchId}_{request.RoleId}_{request.UserId}";
                // Clear cache after delete
                _distributedCache.Remove(cacheKey2);
                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    _log.Error("No result returned from stored procedure");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        alert.Message,
                        500
                    );
                }

                int result = Convert.ToInt32(dataTable.Rows[0]["Result"]);
                string message = Convert.ToString(dataTable.Rows[0]["Message"]);

                if (result == 1)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    _log.Info($"Favorite submenu saved successfully: {message}");
                    return ServiceResult<string>.Success(
                        "Favorite submenu saved successfully",
                        alert.Type,
                        alert.Message,
                        200
                    );
                }
                else
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    _log.Error($"Failed to save favorite submenu: {message}");
                    return ServiceResult<string>.Failure(
                        alert.Type,
                        message,
                        500
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



        public ServiceResult<Dictionary<string, object>> GetUserAccessRights(
    int branchId,
    int roleId,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetUserAccessRights called. BranchId={branchId}, RoleId={roleId}, UserId={globalValues.userId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetUSerAccessRights",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @userId = globalValues.userId,
                        @roleId = roleId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No access rights found for UserId={globalValues.userId}, BranchId={branchId}, RoleId={roleId}");
                    return ServiceResult<Dictionary<string, object>>.Failure(
                        alert.Type,
                        "No access rights found for the specified user",
                        404
                    );
                }

                // Map the single pivot row to a Dictionary<string, object>
                // Each column = one user right, value = 0 or 1
                var row = dataTable.Rows[0];
                var accessRights = new Dictionary<string, object>();

                foreach (DataColumn col in dataTable.Columns)
                {
                    accessRights[col.ColumnName] = row[col] == DBNull.Value ? 0 : row[col];
                }

                _log.Info($"Retrieved {accessRights.Count} access right(s) for UserId={globalValues.userId}, BranchId={branchId}, RoleId={roleId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<Dictionary<string, object>>.Success(
                    accessRights,
                    alert1.Type,
                    $"{accessRights.Count} access right(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<Dictionary<string, object>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<Dictionary<string, object>> GetDashboardUserAccessRights(
    int branchId,
    int roleId,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetDashboardUserAccessRights called. BranchId={branchId}, RoleId={roleId}, UserId={globalValues.userId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetDashBoardUSerAccessRights",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @userId = globalValues.userId,
                        @roleId = roleId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No dashboard access rights found for UserId={globalValues.userId}, BranchId={branchId}, RoleId={roleId}");
                    return ServiceResult<Dictionary<string, object>>.Failure(
                        alert.Type,
                        "No dashboard access rights found for the specified user",
                        404
                    );
                }

                // Map the single pivot row to Dictionary<string, object>
                // Each column = one dashboard right, value = 0 or 1
                var row = dataTable.Rows[0];
                var dashboardRights = new Dictionary<string, object>();

                foreach (DataColumn col in dataTable.Columns)
                {
                    dashboardRights[col.ColumnName] = row[col] == DBNull.Value ? 0 : row[col];
                }

                _log.Info($"Retrieved {dashboardRights.Count} dashboard access right(s) for UserId={globalValues.userId}, BranchId={branchId}, RoleId={roleId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<Dictionary<string, object>>.Success(
                    dashboardRights,
                    alert1.Type,
                    $"{dashboardRights.Count} dashboard access right(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<Dictionary<string, object>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }
    }
}