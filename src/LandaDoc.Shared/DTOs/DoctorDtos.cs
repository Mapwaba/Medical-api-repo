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
    [Range(0, 100000)] decimal ConsultationFee,
    // Profile picture (ProfilePhoto); an admin may create a profile without one
    [StringLength(ProfilePhoto.MaxLength)] string? PhotoDataUrl = null
);

public record DoctorProfileDto(
    Guid Id, Guid UserId,
    string FirstName, string LastName, string Specialty, string? Bio, string? LicenseNumber,
    decimal ConsultationFee,
    List<ClinicDto> Clinics,
    DoctorApprovalStatus Status,
    DateTime CreatedAt,
    string? PhotoDataUrl = null
);

public record SuspendDoctorRequest(string? Reason);

public record DoctorSearchResultDto(
    Guid DoctorId,
    string FirstName, string LastName, string Specialty, string? Bio,
    decimal ConsultationFee,
    List<ClinicDto> Clinics,
    double AverageRating, int RatingCount,
    // The photo inline (data: URL) — left out when the caller asks for photos=false, and fetched
    // instead from GET api/search/doctors/{id}/photo, which can be cached. HasPhoto says if there's one.
    string? PhotoDataUrl = null,
    bool HasPhoto = false
);

public record CreateOwnDoctorProfileRequest(
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string Specialty,
    string? Bio,
    string? LicenseNumber,
    List<Guid> ClinicIds,
    [Range(0, 100000)] decimal ConsultationFee,
    // Required (ProfilePhoto): the server refuses a missing one with code "photo_required". Has a
    // default only so a caller without it gets that clear answer rather than a binding error.
    [StringLength(ProfilePhoto.MaxLength)] string? PhotoDataUrl = null
);

// null fields are left unchanged. The trailing ones have defaults so older callers still compile.
public record UpdateOwnDoctorProfileRequest(
    string? Bio,
    List<Guid>? ClinicIds,
    [Range(0, 100000)] decimal? ConsultationFee,
    [StringLength(100, MinimumLength = 1)] string? FirstName = null,
    [StringLength(100, MinimumLength = 1)] string? LastName = null,
    [StringLength(100, MinimumLength = 1)] string? Specialty = null,
    [StringLength(100)] string? LicenseNumber = null,
    [StringLength(ProfilePhoto.MaxLength)] string? PhotoDataUrl = null
);
