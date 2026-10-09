using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using LandaDoc.ServiceDefaults;
namespace LandaDoc.Identity.Services;
public class TokenService(IConfiguration cfg) : ITokenService
{
private string Secret => cfg.GetJwtSecret();
private string Issuer => cfg["Jwt:Issuer"] ?? "landadoc";
// Access token — short lived (15 min)
// country (ISO code, when on file) decides which payment methods the Payment service offers
public string GenerateAccessToken(Guid userId, string email, string role, string? country = null)
{
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
var claims = new List<Claim>
{
new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
new Claim(ClaimTypes.Email, email),
new Claim(ClaimTypes.Role, role),
new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
};
if (!string.IsNullOrWhiteSpace(country)) claims.Add(new Claim(LandaDoc.Shared.Models.PaymentMethods.CountryClaim, country));
var token = new JwtSecurityToken(
issuer: Issuer, audience: Issuer, claims: claims, expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: creds);
return new JwtSecurityTokenHandler().WriteToken(token);
}       

// Refresh token — opaque random bytes, stored hashed in DB
public string GenerateRefreshToken()
{
var bytes = new byte[64];
System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
return Convert.ToBase64String(bytes);
}
public string HashRefreshToken(string token)
=> Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
Encoding.UTF8.GetBytes(token)));
}