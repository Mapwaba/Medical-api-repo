using System.ComponentModel.DataAnnotations;

namespace LandaDoc.Shared.DTOs;

public record DoctorServiceDto(Guid Id, string Name, string? Description, decimal Price, int DurationMinutes);

public record SaveDoctorServiceRequest(
    [Required, StringLength(200)] string Name,
    [StringLength(1000)] string? Description,
    [Range(0, 1_000_000)] decimal Price,
    [Range(5, 600)] int DurationMinutes
);
