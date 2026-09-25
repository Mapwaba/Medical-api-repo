using System.ComponentModel.DataAnnotations;
using LandaDoc.Shared.Models;

namespace LandaDoc.Shared.DTOs;

public record CreateDoctorProfileRequest(
    [Required] Guid UserId,
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Specialty,
    string? Bio,
    string? LicenseNumber,
    List<Guid> ClinicIds,
    [Range(0, 100000)] decimal ConsultationFee
);

public record DoctorProfileDto(
    Guid Id, Guid UserId,
    string FirstName, string LastName, string Specialty, string? Bio, string? LicenseNumber,
    decimal ConsultationFee,
    List<ClinicDto> Clinics,
    DoctorApprovalStatus Status,
    DateTime CreatedAt
);

public record SuspendDoctorRequest(string? Reason);

public record DoctorSearchResultDto(
    Guid DoctorId,
    string FirstName, string LastName, string Specialty, string? Bio,
    decimal ConsultationFee,
    List<ClinicDto> Clinics,
    double AverageRating, int RatingCount
);

public record CreateOwnDoctorProfileRequest(
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Specialty,
    string? Bio,
    string? LicenseNumber,
    List<Guid> ClinicIds,
    [Range(0, 100000)] decimal ConsultationFee
);

public record UpdateOwnDoctorProfileRequest(
    string? Bio,
    List<Guid>? ClinicIds,
    [Range(0, 100000)] decimal? ConsultationFee
);
