using System.ComponentModel.DataAnnotations;

namespace LandaDoc.Shared.DTOs;

public record CreateReviewRequest(
    [Required] Guid AppointmentId,
    [Required, Range(1, 5)] int Rating,
    [MaxLength(1000)] string? Comment
);

public record ReviewDto(
    Guid Id, Guid AppointmentId, Guid DoctorId, Guid PatientId,
    int Rating, string? Comment, DateTime CreatedAt
);
