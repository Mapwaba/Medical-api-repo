using LandaDoc.Appointment.Data;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Services;

// Booking a slot and paying for it are two separate steps (CreateAsync marks the appointment
// Pending immediately; only a payment webhook later confirms or cancels it) — without this
// sweep, a patient who never pays keeps the slot reserved forever. Runs as a plain periodic
// timer rather than a scheduled-at-creation job (Notification already uses Hangfire for that
// shape) since re-checking "is anything overdue" on an interval is simpler than tracking and
// cancelling one delayed job per booking.
public class PendingPaymentExpiryService(
    IServiceScopeFactory scopeFactory,
    IConfiguration cfg,
    ILogger<PendingPaymentExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var expiryMinutes = cfg.GetValue("Booking:UnpaidExpiryMinutes", 15);
        var sweepInterval = TimeSpan.FromSeconds(cfg.GetValue("Booking:SweepIntervalSeconds", 60));
        using var timer = new PeriodicTimer(sweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(expiryMinutes, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pending payment expiry sweep failed");
            }
            try
            {
                await SweepInsuranceReviewsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Insurance review sweep failed");
            }
        }
    }

    private async Task SweepAsync(int expiryMinutes, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        // A Pending booking expires when its time to pay runs out:
        //  - normally Booking:UnpaidExpiryMinutes after it was made;
        //  - after a declined insurance claim, at PaymentDueAt (the patient gets longer);
        //  - never while an insurance claim is waiting for the doctor's review.
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-expiryMinutes);
        var expired = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Pending && !a.AwaitingInsuranceReview)
            .Where(a => a.PaymentDueAt != null ? a.PaymentDueAt < now : a.CreatedAt < cutoff)
            .ToListAsync(ct);
        if (expired.Count == 0) return;

        foreach (var appt in expired)
        {
            appt.Status = AppointmentStatus.Cancelled;
            appt.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);

        // Publish after the DB commit, same rule as everywhere else in this codebase.
        foreach (var appt in expired)
        {
            await bus.Publish(
                new BookingExpiredEvent(appt.Id, appt.DoctorId, appt.PatientId, DateTime.UtcNow), ct);
        }

        logger.LogInformation("Expired {Count} unpaid booking(s): {AppointmentIds}", expired.Count, expired.Select(a => a.Id).ToList());
    }

    // Insurance claims waiting for the doctor (deadlines set by InsuranceClaimSubmittedConsumer):
    // remind the doctor halfway, and at the deadline hand the claim to Payment to decline it.
    // The booking stays on hold until Payment's InsuranceClaimDeclinedEvent comes back, so a
    // doctor who approves at the last second still wins (Payment only declines a claim that's
    // still waiting).
    private async Task SweepInsuranceReviewsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var now = DateTime.UtcNow;

        var waiting = db.Appointments.Where(a => a.Status == AppointmentStatus.Pending && a.AwaitingInsuranceReview);
        var timedOut = await waiting
            .Where(a => a.InsuranceReviewDueAt != null && a.InsuranceReviewDueAt <= now)
            .ToListAsync(ct);
        var toRemind = await waiting
            .Where(a => a.InsuranceReviewRemindAt != null && a.InsuranceReviewRemindAt <= now)
            .Where(a => a.InsuranceReviewDueAt != null && a.InsuranceReviewDueAt > now)
            .ToListAsync(ct);
        if (timedOut.Count == 0 && toRemind.Count == 0) return;

        // Cleared before publishing so each goes out once
        var reminders = toRemind.Select(a => (a.Id, a.DoctorId, a.PatientId, DueAt: a.InsuranceReviewDueAt!.Value)).ToList();
        foreach (var appt in toRemind) appt.InsuranceReviewRemindAt = null;
        foreach (var appt in timedOut)
        {
            appt.InsuranceReviewRemindAt = null;
            appt.InsuranceReviewDueAt = null;
        }
        await db.SaveChangesAsync(ct);

        foreach (var (id, doctorId, patientId, dueAt) in reminders)
            await bus.Publish(new InsuranceReviewReminderEvent(id, doctorId, patientId, dueAt, now), ct);
        foreach (var appt in timedOut)
            await bus.Publish(new InsuranceReviewTimedOutEvent(appt.Id, appt.DoctorId, appt.PatientId, now), ct);

        if (reminders.Count > 0)
            logger.LogInformation("Reminded doctors about {Count} insurance claim(s) waiting for review: {AppointmentIds}",
                reminders.Count, reminders.Select(r => r.Id).ToList());
        if (timedOut.Count > 0)
            logger.LogInformation("Insurance review deadline passed for {Count} appointment(s), claims sent for automatic decline: {AppointmentIds}",
                timedOut.Count, timedOut.Select(a => a.Id).ToList());
    }
}
