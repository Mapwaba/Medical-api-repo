namespace LandaDoc.Identity.Services;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string email, string role);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
}
