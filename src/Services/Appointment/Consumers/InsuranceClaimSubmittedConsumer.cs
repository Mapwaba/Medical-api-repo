using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Consumers;

// The patient chose to pay through their insurer — hold the booking (no unpaid expiry)
// until the doctor approves or declines the claim.
public class InsuranceClaimSubmittedConsumer(AppointmentDbContext db) : IConsumer<InsuranceClaimSubmittedEvent>
{
    public async Task Consume(ConsumeContext<InsuranceClaimSubmittedEvent> context)
    {
        var appt = await db.Appointments
            .FirstOrDefaultAsync(a => a.Id == context.Message.AppointmentId);
        if (appt is null || appt.Status != AppointmentStatus.Pending) return;

        appt.AwaitingInsuranceReview = true;
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
