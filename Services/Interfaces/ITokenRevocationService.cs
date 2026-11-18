namespace HISWEBAPI.Services.Interfaces
{
    /// <summary>Cancels access tokens on logout and tells the JWT check whether a token was cancelled.</summary>
    public interface ITokenRevocationService
    {
        /// <summary>Marks the token (by its jti claim) as cancelled until it would have expired.</summary>
        void Revoke(string? jti, DateTime expiresUtc);

        /// <summary>True when the token was cancelled (or its login session is no longer Active).</summary>
        bool IsRevoked(string? jti, long sessionId);
    }
}
