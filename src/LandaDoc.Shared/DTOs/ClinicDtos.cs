using System.ComponentModel.DataAnnotations;
using LandaDoc.Shared.Models;

namespace LandaDoc.Shared.DTOs;

public record CreateClinicRequest(
    [Required] string Name,
    [Required] ClinicType Type,
    string? Address,
    [Required] string City,
    string? Phone
);

// null fields are left unchanged; IsActive switches the clinic on or off for doctors' lists
public record UpdateClinicRequest(
    string? Name,
    ClinicType? Type,
    string? Address,
    string? City,
    string? Phone,
    bool? IsActive = null
);

// What deleting a clinic did: Deleted, or (when doctors work there) SwitchedOff — kept for them,
// but no longer offered in the lists doctors pick from
public record DeleteClinicResult(bool Deleted, bool SwitchedOff, int Doctors);

// Just enough to pick a clinic on the public doctor registration form — no phone or address.
public record ClinicOptionDto(Guid Id, string Name, string City);

public record ClinicDto(
    Guid Id, string Name, ClinicType Type, string? Address, string City, string? Phone,
    DateTime CreatedAt,
    // false: no longer offered to doctors picking their clinics (see Clinic.IsActive)
    bool IsActive = true
);

// A doctor's own establishment, as shown and edited on the Doctor app's Workplace page.
// Kept separate from ClinicDto so the Search index and its events don't carry logos.
public record WorkplaceDto(
    Guid Id, string Name, ClinicType Type, string? Address, string City, string? Phone,
    string? Email, string? Website, string? Description, string? LogoDataUrl
);

public record UpdateWorkplaceRequest(
    [Required, StringLength(200)] string Name,
    [StringLength(500)] string? Address,
    [Required, StringLength(100)] string City,
    [StringLength(50)] string? Phone,
    [EmailAddress, StringLength(200)] string? Email,
    [Url, StringLength(300)] string? Website,
    [StringLength(4000)] string? Description,
    // data:image/...;base64,... — capped at ~500 KB of image
    [StringLength(700_000)] string? LogoDataUrl
);
