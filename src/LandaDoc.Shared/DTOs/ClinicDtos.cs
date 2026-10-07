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
