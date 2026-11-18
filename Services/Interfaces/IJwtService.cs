using System.Security.Claims;

namespace HISWEBAPI.Services
{
    public interface IJwtService
    {
        string GenerateToken(int userId, string userName, string name, int hospId, long sessionId = 0);
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    }
}