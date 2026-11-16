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
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly int _maxFailedAttempts;
        private readonly int _lockoutMinutes;

        public UserRepository(
            ICustomSqlHelper sqlHelper,
            IResponseMessageService messageService,
            ISmsService smsService,
            IEmailService emailService,
            IJwtService jwtService,
            IOptions<JwtSettings> jwtSettings,
            IDistributedCache distributedCache,
            IConfiguration configuration)
        {
            // login lockout settings (appsettings "LoginLockout"), defaults 5 attempts / 15 minutes
            _maxFailedAttempts = Math.Max(1, configuration.GetValue("LoginLockout:MaxFailedAttempts", 5));
            _lockoutMinutes = Math.Max(1, configuration.GetValue("LoginLockout:LockoutMinutes", 15));
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

        public ServiceResult<UserLoginResponseData> UserLogin(UserLoginRequest request, string? ipAddress = null)
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


                // Generate token with userId, username, hospId,roleId,userName,name and branchId
                string accessToken = _jwtService.GenerateToken(
                    userId,
                    userName.ToString(),
                    name.ToString(),
                    hospId
                );

                var responseData = new UserLoginResponseData
                {
                    userId = userId,
                    userName = request.UserName,
                    email = Convert.ToString(userRow["Email"]),
                    contact = Convert.ToString(userRow["Contact"]),
                    isContactVerified = Convert.ToBoolean(userRow["IsContactVerified"]),
                    isEmailVerified = Convert.ToBoolean(userRow["IsEmailVerified"]),
                    branchId = request.BranchId,
                    accessToken = accessToken,
                    tokenType = "Bearer",
                    expiresIn = _jwtSettings.ExpiryMinutes * 60 // Convert minutes to seconds
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
      
        public ServiceResult<string> Logout(LogoutRequest request)
        {
            try
            {
                if (request?.SessionId > 0)
                {
                    bool sessionUpdated = UpdateLoginSessionInternal(request.SessionId, "Logged Out", request.LogoutReason ?? "User Logout");
                    bool tokenInvalidated = InvalidateRefreshTokenInternal(request.SessionId);

                    if (!sessionUpdated || !tokenInvalidated)
                    {
                        _log.Warn($"Logout partially failed for SessionId={request.SessionId}");
                    }
                }

                var alert = _messageService.GetMessageAndTypeByAlertCode("LOGOUT_SUCCESS");
                _log.Info($"User logged out successfully. SessionId={request.SessionId}");

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

                string otp = GenerateOtp();
                bool otpStored = StoreOtpForPasswordResetInternal(userId, otp, 5);

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
                    contactHint = contactHintSuccess
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
                var (result, message) = VerifySmsOtpInternal(request.UserId, request.Otp);

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
                    case -4:
                    case -5:
                        var alert3 = _messageService.GetMessageAndTypeByAlertCode("OTP_VALIDATION_FAILED");
                        _log.Warn($"OTP validation failed for UserId {request.UserId}: {message}");
                        return ServiceResult<OtpVerificationResponseData>.Failure(
                            alert3.Type,
                            alert3.Message,
                            400
                        );

                    case -6:
                        var alert4 = _messageService.GetMessageAndTypeByAlertCode("INVALID_OTP");
                        _log.Warn($"Invalid OTP entered for UserId: {request.UserId}");
                        return ServiceResult<OtpVerificationResponseData>.Failure(
                            alert4.Type,
                            alert4.Message,
                            400
                        );

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

                string otp = GenerateOtp();
                bool otpStored = StoreEmailOtpForPasswordResetInternal(userId, otp, 5);

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
                    emailHint = emailHintSuccess
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

        public ServiceResult<string> VerifyEmailOtp(VerifyEmailOtpRequest request)
        {
            try
            {
                var (result, message) = VerifyEmailOtpInternal(request.UserId, request.Otp);

                switch (result)
                {
                    case 1:
                        var alert = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VERIFIED");
                        _log.Info($"Email OTP verified successfully for UserId: {request.UserId}");
                        return ServiceResult<string>.Success(
                            "Email OTP verified",
                            alert.Type,
                            alert.Message,
                            200
                        );

                    case -1:
                        var alert1 = _messageService.GetMessageAndTypeByAlertCode("USER_NOT_FOUND_OTP");
                        _log.Warn($"User not found: UserId={request.UserId}");
                        return ServiceResult<string>.Failure(
                            alert1.Type,
                            alert1.Message,
                            404
                        );

                    case -2:
                        var alert2 = _messageService.GetMessageAndTypeByAlertCode("USER_INACTIVE");
                        _log.Warn($"Inactive user attempted OTP verification: UserId={request.UserId}");
                        return ServiceResult<string>.Failure(
                            alert2.Type,
                            alert2.Message,
                            400
                        );

                    case -3:
                    case -4:
                    case -5:
                        var alert3 = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VALIDATION_FAILED");
                        _log.Warn($"Email OTP validation failed for UserId {request.UserId}: {message}");
                        return ServiceResult<string>.Failure(
                            alert3.Type,
                            alert3.Message,
                            400
                        );

                    case -6:
                        var alert4 = _messageService.GetMessageAndTypeByAlertCode("INVALID_EMAIL_OTP");
                        _log.Warn($"Invalid email OTP entered for UserId: {request.UserId}");
                        return ServiceResult<string>.Failure(
                            alert4.Type,
                            alert4.Message,
                            400
                        );

                    default:
                        var alert5 = _messageService.GetMessageAndTypeByAlertCode("EMAIL_OTP_VERIFICATION_ERROR");
                        _log.Error($"Unknown error during email OTP verification for UserId: {request.UserId}. Result code: {result}");
                        return ServiceResult<string>.Failure(
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
                return ServiceResult<string>.Failure(
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
                    IconClass = row.Field<string>("IconClass"),
                    ImagePath = row.Field<string>("ImagePath"),
                    IsFavoriteRole = row.Field<int>("isFavoriteRole")
                }).ToList() ?? new List<UserRoleModel>();

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

        private (int result, string message) VerifySmsOtpInternal(long userId, string otp)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Otp", otp),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output },
                new SqlParameter("@Message", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Output }
            };

            _sqlHelper.RunProcedure("S_VerifySmsOtp", parameters);

            int result = parameters[2].Value != DBNull.Value ? Convert.ToInt32(parameters[2].Value) : 0;
            string message = parameters[3].Value != DBNull.Value ? parameters[3].Value.ToString() : "Unknown error";

            return (result, message);
        }

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

        private (int result, string message) VerifyEmailOtpInternal(long userId, string otp)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Otp", otp),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output },
                new SqlParameter("@Message", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Output }
            };

            _sqlHelper.RunProcedure("S_VerifyEmailOtp", parameters);

            int result = parameters[2].Value != DBNull.Value ? Convert.ToInt32(parameters[2].Value) : 0;
            string message = parameters[3].Value != DBNull.Value ? parameters[3].Value.ToString() : "Unknown error";

            return (result, message);
        }

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