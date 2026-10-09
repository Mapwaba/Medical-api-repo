using LandaDoc.Shared.Security;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;

namespace LandaDoc.Identity.Services;

// User is null when the account wasn't found (Status is then InvalidCredentials) or on a conflict
public record UpdateMeResult(AuthResultStatus Status, UserDto? User);

public enum AuthResultStatus
{
    Success,
    InvalidCredentials,
    AccountInactive,
    PendingApproval,
    EmailAlreadyRegistered,
    PhoneAlreadyRegistered,
    NameAlreadyRegistered,
    InvalidRefreshToken,
    WeakPassword           // a doctor's password breaks PasswordPolicy (FailedRules says which rules)
}

public record AuthResult(AuthResultStatus Status, AuthResponse? Response = null, IReadOnlyList<PasswordRule>? FailedRules = null);

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest req);
    Task<AuthResult> RefreshAsync(string refreshToken);
    Task<AuthResult> RegisterPatientAsync(RegisterPatientRequest req);
    Task<AuthResult> RegisterDoctorAsync(RegisterDoctorRequest req);
    Task<UserDto?> GetMeAsync(Guid userId);
    Task<UpdateMeResult> UpdateMeAsync(Guid userId, UpdateMeRequest req);
    Task<UserDto?> UpdateAddressAsync(Guid userId, UpdateAddressRequest req);
    Task<UserDto?> UpdatePhotoAsync(Guid userId, string? photoDataUrl);
    // InvalidCredentials when the current password is wrong; on success, fresh tokens for this session
    Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest req);
    Task<List<UserDto>> GetUsersAsync(UserRole? role);
    Task<UserCountsDto> GetUserCountsAsync();
}
