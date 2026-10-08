using LandaDoc.Payment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Payment.Consumers;

// The doctor didn't review an insurance claim before its deadline (Appointment's sweep) —
// decline it as Expired, exactly as if the doctor had declined it, so the patient gets time to
// pay another way. A claim the doctor answered in the meantime is left alone.
public class InsuranceReviewTimedOutConsumer(PaymentDbContext db, IPublishEndpoint bus, ILogger<InsuranceReviewTimedOutConsumer> log) : IConsumer<InsuranceReviewTimedOutEvent>
{
    public async Task Consume(ConsumeContext<InsuranceReviewTimedOutEvent> ctx)
    {
        var msg = ctx.Message;
        var claim = await db.InsuranceClaims
            .Where(c => c.AppointmentId == msg.AppointmentId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
        if (claim is null || claim.Status != InsuranceClaimStatus.Submitted) return;

        claim.Status = InsuranceClaimStatus.Expired;
        claim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Appointment gives the patient time to pay another way; Notification tells them why
        await bus.Publish(new InsuranceClaimDeclinedEvent(
            claim.AppointmentId, claim.DoctorId, claim.PatientId, null, DateTime.UtcNow, TimedOut: true));
        log.LogInformation("Insurance claim {ClaimId} expired: doctor {DoctorId} didn't review it in time; appointment {AppointmentId} must be paid another way",
            claim.Id, claim.DoctorId, claim.AppointmentId);
    }
}
