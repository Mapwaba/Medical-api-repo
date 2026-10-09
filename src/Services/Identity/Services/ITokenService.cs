namespace LandaDoc.Identity.Services;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, string role, string? country = null);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
}
