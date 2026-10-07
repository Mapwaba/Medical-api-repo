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
    Task<UserDto?> UpdateMeAsync(Guid userId, UpdateMeRequest req);
    // InvalidCredentials when the current password is wrong; on success, fresh tokens for this session
    Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest req);
    Task<List<UserDto>> GetUsersAsync(UserRole? role);
    Task<UserCountsDto> GetUserCountsAsync();
}
