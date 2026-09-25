using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;

namespace LandaDoc.Identity.Services;

public enum AuthResultStatus
{
    Success,
    InvalidCredentials,
    AccountInactive,
    PendingApproval,
    EmailAlreadyRegistered,
    InvalidRefreshToken
}

public record AuthResult(AuthResultStatus Status, AuthResponse? Response = null);

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest req);
    Task<AuthResult> RefreshAsync(string refreshToken);
    Task<AuthResult> RegisterPatientAsync(RegisterPatientRequest req);
    Task<AuthResult> RegisterDoctorAsync(RegisterDoctorRequest req);
    Task<UserDto?> GetMeAsync(Guid userId);
    Task<List<UserDto>> GetUsersAsync(UserRole? role);
    Task<UserCountsDto> GetUserCountsAsync();
}
