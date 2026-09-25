using LandaDoc.Shared.DTOs;

namespace LandaDoc.Review.Services;

public enum SubmitReviewResultStatus
{
    Success,
    AppointmentNotFound,
    NotYourAppointment,
    NotCompleted,
    AlreadyReviewed
}

public record SubmitReviewResult(SubmitReviewResultStatus Status, Models.Review? Review = null);

public interface IReviewService
{
    Task<SubmitReviewResult> SubmitAsync(Guid patientId, string bearerToken, CreateReviewRequest req);
    Task<List<Models.Review>> GetForDoctorAsync(Guid doctorId);
}
