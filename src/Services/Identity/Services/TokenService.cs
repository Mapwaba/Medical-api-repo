using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
namespace LandaDoc.Identity.Services;
public class TokenService(IConfiguration cfg) : ITokenService
{
private string Secret => cfg["Jwt:Secret"]!;
private string Issuer => cfg["Jwt:Issuer"] ?? "landadoc";
// Access token — short lived (15 min)
public string GenerateAccessToken(Guid userId, string email, string role)
{
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
var claims = new[]
{
new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
new Claim(ClaimTypes.Email, email),
new Claim(ClaimTypes.Role, role),
new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
};
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