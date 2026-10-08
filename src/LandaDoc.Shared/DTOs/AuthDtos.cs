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
string? Gender,
// Where the patient lives (Country = ISO code, e.g. "CD") and their ID card / passport number.
// Optional here so older app versions can still register; the web app asks for the country.
[StringLength(2)] string? Country = null,
[StringLength(100)] string? Province = null,
[StringLength(100)] string? City = null,
[StringLength(20)] string? PostalCode = null,
[StringLength(50)] string? IdNumber = null
);

public record RegisterDoctorRequest(
[Required, EmailAddress] string Email,
[Required, MinLength(8), MaxLength(100)] string Password,
[Required] string FirstName,
[Required] string LastName,
string? Phone,
// Where the doctor lives (see RegisterPatientRequest); their ID documents are uploaded separately
[StringLength(2)] string? Country = null,
[StringLength(100)] string? Province = null,
[StringLength(100)] string? City = null,
[StringLength(20)] string? PostalCode = null
);

public record AuthResponse(string Token, string RefreshToken, UserDto User);
public record UserDto(
Guid Id, string Email, string Role,
string FirstName, string LastName,
string? Phone, string? AvatarUrl,
Guid? ProfileId,
bool IsApproved, bool IsActive,
DateOnly? DateOfBirth, string? Gender,
string? Country = null, string? Province = null, string? City = null, string? PostalCode = null,
string? IdNumber = null
);

public record UserCountsDto(int PatientCount, int DoctorCount);

// What a doctor sees about one of their patients (Doctor app's Patients page and patient file)
// A patient's name only (an account holder or a dependant), for showing on appointments
public record PatientNameDto(Guid Id, string FirstName, string LastName);

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

// Where someone lives, and (patients) their ID card / passport number. Country is an ISO code.
public record UpdateAddressRequest(
    [Required, StringLength(2)] string Country,
    [StringLength(100)] string? Province,
    [StringLength(100)] string? City,
    [StringLength(20)] string? PostalCode,
    [StringLength(50)] string? IdNumber
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), StringLength(200)] string NewPassword
);
