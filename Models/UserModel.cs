namespace HISWEBAPI.Models
{
    public class UserRoleModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public int RoleIconId { get; set; }
        /// <summary>Data URL of the role icon (RoleIconMaster, from the Redis cache).</summary>
        public string? RoleIcon { get; set; }
        public int IsFavoriteRole { get; set; }
    }

    public class UserLoginResponseData
    {
        public int userId { get; set; }
        public string userName { get; set; }
        public string email { get; set; }
        public string contact { get; set; }
        public bool isContactVerified { get; set; }
        public bool isEmailVerified { get; set; }
        /// <summary>User Master "Contact verification required on login": false = skip the mobile/email OTP popup.</summary>
        public bool contactVerificationRequired { get; set; } = true;
        public int branchId { get; set; }
        public string accessToken { get; set; }
        public string tokenType { get; set; }
        public int expiresIn { get; set; }
        /// <summary>UserLoginSessions.SessionId of this login (0 when the history row could not be saved); send it to api/User/logout.</summary>
        public long sessionId { get; set; }
    }

    public class TokenResponseData
    {
        public string accessToken { get; set; }
        public string refreshToken { get; set; }
        public string tokenType { get; set; }
        public int expiresIn { get; set; }
    }

    public class UserSignupResponseData
    {
        public long userId { get; set; }
    }

    public class SmsOtpResponseData
    {
        public long userId { get; set; }
        public string contactHint { get; set; }
        /// <summary>Seconds before another OTP may be requested.</summary>
        public int resendAfterSeconds { get; set; }
        /// <summary>Seconds left on the wrong-OTP lock (0 = not locked).</summary>
        public int lockedSeconds { get; set; }
        public int otpExpiryMinutes { get; set; }
        /// <summary>Wrong OTPs allowed per user (SMS + email) before the lock.</summary>
        public int maxAttempts { get; set; }
    }

    public class EmailOtpResponseData
    {
        public long userId { get; set; }
        public string emailHint { get; set; }
        public int resendAfterSeconds { get; set; }
        /// <summary>Seconds left on the wrong-OTP lock (0 = not locked).</summary>
        public int lockedSeconds { get; set; }
        public int otpExpiryMinutes { get; set; }
        public int maxAttempts { get; set; }
    }

    public class OtpVerificationResponseData
    {
        public int userId { get; set; }
        public string otp { get; set; }
        /// <summary>Wrong OTPs left before the user is locked (set on a wrong OTP; 0 = locked).</summary>
        public int? attemptsLeft { get; set; }
        /// <summary>Seconds left on the wrong-OTP lock (set when locked).</summary>
        public int lockedSeconds { get; set; }
    }


    public class UserTabModel
    {
        public int TabId { get; set; }
        public string TabName { get; set; }
        public string IconClass { get; set; }
    }

    public class UserSubMenuModel
    {
        public int SubMenuId { get; set; }
        public string SubMenuName { get; set; }
        public string URL { get; set; }
        public int TabId { get; set; }
    }

    public class UserTabMenuMappingResponse
    {
        public List<UserTabModel> Tabs { get; set; }
        public List<UserSubMenuModel> SubMenus { get; set; }
        public List<UserSubMenuModel> favoriteSubMenus { get; set; }
    }

}