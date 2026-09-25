using LandaDoc.Notification.Data;
using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// Unlike the other consumers here, reschedule isn't a terminal-state transition — a patient can
// reschedule the same appointment more than once, each a distinct, legitimately-notifiable event.
// So there's no "already handled" status to guard on the way BookingCreated/PaymentCompleted/
// PaymentFailed/AppointmentCompleted do; an at-least-once redelivery of the same reschedule would
// duplicate a notification, but that's the same risk the notification feed already accepts.
public class AppointmentRescheduledConsumer(
    NotificationDbContext db,
    INotificationPublisher publisher) : IConsumer<AppointmentRescheduledEvent>
{
    public async Task Consume(ConsumeContext<AppointmentRescheduledEvent> ctx)
    {
        var msg = ctx.Message;

        var doctor = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.DoctorId);
        var doctorName = doctor is null ? "votre médecin" : $"{doctor.FirstName} {doctor.LastName}".Trim();
        var patient = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.PatientId);
        var patientName = patient is null ? "Un patient" : $"{patient.FirstName} {patient.LastName}".Trim();

        await publisher.PublishAsync(msg.PatientId, "appointment_rescheduled", "Rendez-vous reprogrammé",
            $"Votre RDV avec {doctorName} a été déplacé du {msg.OldSlotStart:dd/MM/yyyy HH:mm} au {msg.NewSlotStart:dd/MM/yyyy HH:mm}.");
        await publisher.PublishAsync(msg.DoctorId, "appointment_rescheduled", "Rendez-vous reprogrammé",
            $"{patientName} a déplacé son RDV du {msg.OldSlotStart:dd/MM/yyyy HH:mm} au {msg.NewSlotStart:dd/MM/yyyy HH:mm}.");
    }
}
