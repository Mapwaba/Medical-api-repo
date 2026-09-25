using LandaDoc.Document.Data;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Document.Consumers;

// Auto-links any documents the patient uploaded before this appointment was
// completed (e.g. lab results shared ahead of the visit) to the appointment.
public class AppointmentCompletedConsumer(DocumentDbContext db) : IConsumer<AppointmentCompletedEvent>
{
    public async Task Consume(ConsumeContext<AppointmentCompletedEvent> ctx)
    {
        var msg = ctx.Message;

        // Naturally idempotent — a redelivery only ever finds rows still
        // unlinked (AppointmentId == null); the second pass finds none.
        var unlinked = await db.Documents
            .Where(d => d.PatientId == msg.PatientId
                     && (d.DoctorId == null || d.DoctorId == msg.DoctorId)
                     && d.AppointmentId == null)
            .ToListAsync();
        if (unlinked.Count == 0) return;

        foreach (var doc in unlinked) doc.AppointmentId = msg.AppointmentId;
        await db.SaveChangesAsync();
    }
}
