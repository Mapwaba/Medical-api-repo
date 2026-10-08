using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

// The patient chose to pay through their insurer — hold the booking (no unpaid expiry)
// until the doctor approves or declines the claim, with a deadline so a doctor who never
// answers doesn't leave the patient stuck: the doctor is reminded halfway through, and at the
// deadline the claim is declined automatically (PendingPaymentExpiryService).
public class InsuranceClaimSubmittedConsumer(AppointmentDbContext db, IConfiguration cfg, ILogger<InsuranceClaimSubmittedConsumer> log) : IConsumer<InsuranceClaimSubmittedEvent>
{
    public async Task Consume(ConsumeContext<InsuranceClaimSubmittedEvent> context)
    {
        var appt = await db.Appointments
            .FirstOrDefaultAsync(a => a.Id == context.Message.AppointmentId);
        if (appt is null || appt.Status != AppointmentStatus.Pending) return;

        var now = DateTime.UtcNow;
        var dueAt = ReviewDeadline(now, appt.SlotStart);
        appt.AwaitingInsuranceReview = true;
        appt.InsuranceReviewDueAt = dueAt;
        appt.InsuranceReviewRemindAt = now + (dueAt - now) / 2;
        appt.UpdatedAt = now;
        await db.SaveChangesAsync();
        log.LogInformation("Appointment {AppointmentId} held for the doctor's insurance review until {ReviewDueAt} (no unpaid expiry meanwhile)", appt.Id, dueAt);
    }

    // Booking:InsuranceReviewHours (default 24) to answer, but ending Booking:InsuranceReviewLeadHours
    // (default 2) before the appointment so a declined patient still has time to pay another
    // way; never less than an hour, even for a booking made at the last minute.
    private DateTime ReviewDeadline(DateTime now, DateTime slotStart)
    {
        var dueAt = now.AddHours(cfg.GetValue("Booking:InsuranceReviewHours", 24));
        var beforeSlot = slotStart.AddHours(-cfg.GetValue("Booking:InsuranceReviewLeadHours", 2));
        if (beforeSlot < dueAt) dueAt = beforeSlot;
        var minimum = now.AddHours(1);
        return dueAt < minimum ? minimum : dueAt;
    }
}
