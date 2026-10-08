using System.ComponentModel.DataAnnotations;
namespace LandaDoc.Shared.DTOs;

public record LoginRequest(
[Required, EmailAddress] string Email,
[Required, MinLength(8), MaxLength(100)] string Password
);
public record RegisterPatientRequest(
[Required, EmailAddress] string Email,
[Required, MinLength(8), MaxLength(100)] string Password,
[Required] string FirstName,
[Required] string LastName,
string? Phone,
DateOnly? DateOfBirth,
string? Gender
);

public record RegisterDoctorRequest(
[Required, EmailAddress] string Email,
[Required, MinLength(8), MaxLength(100)] string Password,
[Required] string FirstName,
[Required] string LastName,
string? Phone
);

public record AuthResponse(string Token, string RefreshToken, UserDto User);
public record UserDto(
Guid Id, string Email, string Role,
string FirstName, string LastName,
string? Phone, string? AvatarUrl,
Guid? ProfileId,
bool IsApproved, bool IsActive,
DateOnly? DateOfBirth, string? Gender
);

public record UserCountsDto(int PatientCount, int DoctorCount);

// What a doctor sees about one of their patients (Doctor app's Patients page and patient file)
public record PatientContactDto(
    Guid Id, string FirstName, string LastName, string Email, string? Phone,
    DateOnly? DateOfBirth, string? Gender
);

// A signed-in user editing their own account (name and phone; email stays the login)
// Why a browser session ended, reported by the web apps: "inactive" or "inactive-prompt-logout"
public record SessionEndedRequest([Required, StringLength(40)] string Reason);

public record UpdateMeRequest(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [StringLength(30)] string? Phone
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), StringLength(200)] string NewPassword
);
