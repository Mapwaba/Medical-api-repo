using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// Keeps the pending-count projection accurate even when a doctor completes an appointment
// directly from Pending (skipping Confirmed) — otherwise it would linger in the pending count.
public class AppointmentCompletedConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher) : IConsumer<AppointmentCompletedEvent>
{
    public async Task Consume(ConsumeContext<AppointmentCompletedEvent> ctx)
    {
        var msg = ctx.Message;

        var projection = await db.AppointmentPendingProjections
            .FirstOrDefaultAsync(p => p.AppointmentId == msg.AppointmentId);
        if (projection is null || projection.Status == AppointmentStatus.Completed) return;

        var wasPending = projection.Status == AppointmentStatus.Pending;
        projection.Status = AppointmentStatus.Completed;
        await db.SaveChangesAsync();

        if (wasPending)
        {
            await publisher.PushPendingCountAsync(projection.PatientId);
            await publisher.PushPendingCountAsync(projection.DoctorId);
        }

        var doctor = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == projection.DoctorId);
        var doctorName = doctor is null ? "votre médecin" : $"{doctor.FirstName} {doctor.LastName}".Trim();

        await publisher.PublishAsync(projection.PatientId, "appointment_completed", "Rendez-vous terminé",
            $"Votre rendez-vous avec {doctorName} est terminé.");
        await publisher.PublishAsync(projection.DoctorId, "appointment_completed", "Rendez-vous terminé",
            "Le rendez-vous a été marqué comme terminé.");
    }
}
