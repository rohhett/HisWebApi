using System.Data;
using System.Reflection;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Services.Interfaces;
using log4net;
using Microsoft.Extensions.Caching.Distributed;

namespace HISWEBAPI.Services.Implementations
{
    /// <summary>
    /// Cancels access tokens on logout. A JWT stays valid until it expires, so a logged-out
    /// token's id (jti) is kept in Redis until that expiry and every request is checked
    /// against it (JwtBearer OnTokenValidated). If Redis cannot be reached, the check falls
    /// back to the login session's status in dbo.UserLoginSessions.
    /// </summary>
    public class TokenRevocationService : ITokenRevocationService
    {
        private readonly IDistributedCache _cache;
        private readonly ICustomSqlHelper _sqlHelper;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public TokenRevocationService(IDistributedCache cache, ICustomSqlHelper sqlHelper)
        {
            _cache = cache;
            _sqlHelper = sqlHelper;
        }

        private static string Key(string jti) => "_RevokedToken_" + jti;

        public void Revoke(string? jti, DateTime expiresUtc)
        {
            if (string.IsNullOrWhiteSpace(jti)) return;

            // keep the entry only as long as the token could still be used
            var ttl = expiresUtc - DateTime.UtcNow;
            if (ttl <= TimeSpan.Zero) return;

            try
            {
                _cache.SetString(Key(jti), "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
                _log.Info($"Access token revoked. jti={jti}, until={expiresUtc:u}");
            }
            catch (Exception ex)
            {
                // the session row is already marked "Logged Out", so the DB fallback still blocks it
                _log.Error($"Token revocation could not be saved in Redis. jti={jti}: {ex.Message}");
            }
        }

        public bool IsRevoked(string? jti, long sessionId)
        {
            if (!string.IsNullOrWhiteSpace(jti))
            {
                try
                {
                    return _cache.GetString(Key(jti)) != null;
                }
                catch (Exception ex)
                {
                    _log.Error($"Token revocation list could not be read from Redis, checking the session in DB. jti={jti}: {ex.Message}");
                }
            }

            if (sessionId <= 0) return false; // token from before sessions were recorded

            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetUserLoginSessionStatus", CommandType.StoredProcedure, new { SessionId = sessionId });
                if (dt == null || dt.Rows.Count == 0) return true; // unknown session -> refuse
                string status = Convert.ToString(dt.Rows[0]["Status"]) ?? "";
                return !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                // neither Redis nor DB reachable: do not lock everybody out
                _log.Error($"Login session status could not be read. SessionId={sessionId}: {ex.Message}");
                return false;
            }
        }
    }
}
