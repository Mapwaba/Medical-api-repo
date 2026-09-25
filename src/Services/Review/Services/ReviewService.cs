using LandaDoc.Review.Data;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Review.Services;

public class ReviewService(
    ReviewDbContext db,
    IAppointmentServiceClient appointments,
    IPublishEndpoint bus) : IReviewService
{
    public async Task<SubmitReviewResult> SubmitAsync(Guid patientId, string bearerToken, CreateReviewRequest req)
    {
        var appt = await appointments.GetAppointmentAsync(req.AppointmentId, bearerToken);
        if (appt is null) return new SubmitReviewResult(SubmitReviewResultStatus.AppointmentNotFound);
        if (appt.PatientId != patientId) return new SubmitReviewResult(SubmitReviewResultStatus.NotYourAppointment);
        if (appt.Status != AppointmentStatus.Completed) return new SubmitReviewResult(SubmitReviewResultStatus.NotCompleted);

        var alreadyReviewed = await db.Reviews.AnyAsync(r => r.AppointmentId == req.AppointmentId);
        if (alreadyReviewed) return new SubmitReviewResult(SubmitReviewResultStatus.AlreadyReviewed);

        var review = new Models.Review
        {
            AppointmentId = req.AppointmentId,
            DoctorId = appt.DoctorId,
            PatientId = patientId,
            Rating = req.Rating,
            Comment = req.Comment
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        await bus.Publish(new ReviewSubmittedEvent(
            ReviewId: review.Id,
            AppointmentId: review.AppointmentId,
            DoctorId: review.DoctorId,
            PatientId: review.PatientId,
            Rating: review.Rating,
            OccurredAt: DateTime.UtcNow
        ));

        return new SubmitReviewResult(SubmitReviewResultStatus.Success, review);
    }

    public Task<List<Models.Review>> GetForDoctorAsync(Guid doctorId) =>
        db.Reviews.Where(r => r.DoctorId == doctorId).OrderByDescending(r => r.CreatedAt).ToListAsync();
}
