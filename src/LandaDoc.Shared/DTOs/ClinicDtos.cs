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

public record UpdateClinicRequest(
    string? Name,
    ClinicType? Type,
    string? Address,
    string? City,
    string? Phone
);

// Just enough to pick a clinic on the public doctor registration form — no phone or address.
public record ClinicOptionDto(Guid Id, string Name, string City);

public record ClinicDto(
    Guid Id, string Name, ClinicType Type, string? Address, string City, string? Phone,
    DateTime CreatedAt
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
