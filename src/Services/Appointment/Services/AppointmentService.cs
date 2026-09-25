using LandaDoc.Appointment.Data;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Appointment.Services;

public class AppointmentService(AppointmentDbContext db, IPublishEndpoint bus) : IAppointmentService
{
    public async Task<CreateAppointmentResult> CreateAsync(Guid callerId, CreateAppointmentRequest req)
    {
        // BookForPatientId is a target, never trusted directly: the caller's own identity
        // still comes exclusively from the JWT. When booking for someone else, the target
        // must be backed by a server-side authorization record populated only from
        // Identity-originated family-link/dependent events — never from client input.
        var targetPatientId = req.BookForPatientId ?? callerId;
        if (targetPatientId != callerId)
        {
            var authorized = await db.BookingAuthorizations.AnyAsync(a =>
                a.BookerId == callerId && a.TargetId == targetPatientId);
            if (!authorized)
                return new CreateAppointmentResult(CreateAppointmentResultStatus.Forbidden);
        }

        // Conflict check — no two active bookings at the same slot
        var conflict = await db.Appointments.AnyAsync(a =>
            a.DoctorId == req.DoctorId &&
            a.SlotStart == req.SlotStart &&
            a.Status != AppointmentStatus.Cancelled);
        if (conflict)
            return new CreateAppointmentResult(CreateAppointmentResultStatus.SlotConflict);

        var appt = new Models.Appointment
        {
            DoctorId = req.DoctorId,
            PatientId = targetPatientId,
            BookedByUserId = callerId,
            SlotStart = req.SlotStart,
            SlotEnd = req.SlotStart.AddMinutes(30),
            Motif = req.Motif,
            Status = AppointmentStatus.Pending
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // Publish domain event AFTER the DB commit
        await bus.Publish(new BookingCreatedEvent(
            AppointmentId: appt.Id,
            DoctorId: appt.DoctorId,
            PatientId: appt.PatientId,
            ClinicId: appt.ClinicId,
            SlotStart: appt.SlotStart,
            ConsultationFee: null, // enriched by Payment service from its own data
            PlatformFeePct: null,
            PatientEmail: null, // Notification service looks this up
            PatientPhone: null, // Notification service looks this up
            DoctorName: null,
            PatientName: null,
            OccurredAt: DateTime.UtcNow,
            BookedByUserId: appt.BookedByUserId
        ));

        return new CreateAppointmentResult(CreateAppointmentResultStatus.Success, appt);
    }

    public Task<Models.Appointment?> GetByIdAsync(Guid id) =>
        db.Appointments.FirstOrDefaultAsync(a => a.Id == id);

    public Task<List<Models.Appointment>> GetMineAsync(Guid callerId, string role) =>
        db.Appointments
            .Where(a => role == "Doctor" ? a.DoctorId == callerId : a.PatientId == callerId)
            .OrderByDescending(a => a.SlotStart)
            .ToListAsync();

    public async Task<CompleteAppointmentResult> CompleteAsync(Guid appointmentId, Guid doctorId)
    {
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appt is null) return new CompleteAppointmentResult(CompleteAppointmentResultStatus.NotFound);
        if (appt.DoctorId != doctorId) return new CompleteAppointmentResult(CompleteAppointmentResultStatus.Forbidden);
        if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            return new CompleteAppointmentResult(CompleteAppointmentResultStatus.InvalidStatus);

        appt.Status = AppointmentStatus.Completed;
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Publish domain event AFTER the DB commit
        await bus.Publish(new AppointmentCompletedEvent(
            AppointmentId: appt.Id,
            DoctorId: appt.DoctorId,
            PatientId: appt.PatientId,
            CompletedAt: appt.UpdatedAt,
            OccurredAt: DateTime.UtcNow
        ));

        return new CompleteAppointmentResult(CompleteAppointmentResultStatus.Success, appt);
    }

    public async Task<RescheduleAppointmentResult> RescheduleAsync(Guid appointmentId, Guid patientId, RescheduleRequest req)
    {
        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appt is null) return new RescheduleAppointmentResult(RescheduleAppointmentResultStatus.NotFound);
        if (appt.PatientId != patientId) return new RescheduleAppointmentResult(RescheduleAppointmentResultStatus.Forbidden);
        if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            return new RescheduleAppointmentResult(RescheduleAppointmentResultStatus.InvalidStatus);

        var conflict = await db.Appointments.AnyAsync(a =>
            a.Id != appointmentId &&
            a.DoctorId == appt.DoctorId &&
            a.SlotStart == req.SlotStart &&
            a.Status != AppointmentStatus.Cancelled);
        if (conflict)
            return new RescheduleAppointmentResult(RescheduleAppointmentResultStatus.SlotConflict);

        var oldSlotStart = appt.SlotStart;
        appt.SlotStart = req.SlotStart;
        appt.SlotEnd = req.SlotStart.AddMinutes(30);
        appt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Publish domain event AFTER the DB commit
        await bus.Publish(new AppointmentRescheduledEvent(
            AppointmentId: appt.Id,
            DoctorId: appt.DoctorId,
            PatientId: appt.PatientId,
            OldSlotStart: oldSlotStart,
            NewSlotStart: appt.SlotStart,
            OccurredAt: DateTime.UtcNow
        ));

        return new RescheduleAppointmentResult(RescheduleAppointmentResultStatus.Success, appt);
    }

    public async Task<AppointmentStatsDto> GetStatsByDoctorAsync()
    {
        var grouped = await db.Appointments
            .GroupBy(a => a.DoctorId)
            .Select(g => new { DoctorId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        var byDoctor = grouped.Select(g => new DoctorAppointmentCountDto(g.DoctorId, g.Count)).ToList();
        return new AppointmentStatsDto(byDoctor.Sum(d => d.Count), byDoctor);
    }
}
