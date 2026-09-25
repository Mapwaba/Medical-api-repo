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
