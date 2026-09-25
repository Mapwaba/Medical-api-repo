using System.Text.Json;
using Hangfire;
using LandaDoc.Notification.Data;
using LandaDoc.Notification.Models;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

public class BookingCreatedConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher,
    IBackgroundJobClient hangfire) : IConsumer<BookingCreatedEvent>
{
    private const string NotificationType = "booking_created";
    private const string ReminderType = "appointment_reminder";

    public async Task Consume(ConsumeContext<BookingCreatedEvent> ctx)
    {
        var msg = ctx.Message;

        // MassTransit redelivers on faults/restarts — the projection row (one per appointment)
        // is the idempotency guard against duplicate notifications and scheduled reminders.
        var alreadyHandled = await db.AppointmentPendingProjections
            .AnyAsync(p => p.AppointmentId == msg.AppointmentId);
        if (alreadyHandled) return;

        db.AppointmentPendingProjections.Add(new AppointmentPendingProjection
        {
            AppointmentId = msg.AppointmentId,
            PatientId = msg.PatientId,
            DoctorId = msg.DoctorId,
            Status = AppointmentStatus.Pending
        });
        await db.SaveChangesAsync();

        var doctor = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.DoctorId);
        var doctorName = doctor is null ? "votre médecin" : $"{doctor.FirstName} {doctor.LastName}".Trim();
        var patient = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.PatientId);
        var patientName = patient is null ? "Un patient" : $"{patient.FirstName} {patient.LastName}".Trim();

        // 1. In-app "pending" notification for both sides of the booking, pushed live over SignalR
        await publisher.PublishAsync(msg.PatientId, NotificationType, "Rendez-vous en attente",
            $"Votre RDV avec {doctorName} le {msg.SlotStart:dd/MM/yyyy HH:mm} est en attente.",
            JsonSerializer.Serialize(new { msg.AppointmentId }));
        await publisher.PublishAsync(msg.DoctorId, NotificationType, "Nouvelle demande de rendez-vous",
            $"{patientName} a demandé un RDV le {msg.SlotStart:dd/MM/yyyy HH:mm}.",
            JsonSerializer.Serialize(new { msg.AppointmentId }));

        // 2. Live pending-count badge for both sides of the booking
        await publisher.PushPendingCountAsync(msg.PatientId);
        await publisher.PushPendingCountAsync(msg.DoctorId);

        // 3. Reminders stay in-app only — SMS/Email are reserved for the payment flow
        var remind24h = msg.SlotStart.AddHours(-24);
        var remind1h = msg.SlotStart.AddHours(-1);
        if (remind24h > DateTime.UtcNow)
            hangfire.Schedule<INotificationPublisher>(p => p.PublishAsync(
                msg.PatientId, ReminderType, "Rappel de rendez-vous",
                $"Rappel: RDV demain à {msg.SlotStart:HH:mm} avec {doctorName}.", null),
                remind24h);
        if (remind1h > DateTime.UtcNow)
            hangfire.Schedule<INotificationPublisher>(p => p.PublishAsync(
                msg.PatientId, ReminderType, "Rappel de rendez-vous",
                $"Rappel: RDV dans 1h à {msg.SlotStart:HH:mm} avec {doctorName}.", null),
                remind1h);
    }
}
