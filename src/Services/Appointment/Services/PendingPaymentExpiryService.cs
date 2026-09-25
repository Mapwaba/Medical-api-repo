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
        }
    }

    private async Task SweepAsync(int expiryMinutes, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var cutoff = DateTime.UtcNow.AddMinutes(-expiryMinutes);
        var expired = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Pending && a.CreatedAt < cutoff)
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

        logger.LogInformation("Expired {Count} unpaid booking(s)", expired.Count);
    }
}
