using LandaDoc.Admin.Data;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Admin.Services;

public class DoctorProfileService(AdminDbContext db, IPublishEndpoint bus) : IDoctorProfileService
{
    public Task<List<Models.DoctorProfile>> GetAllAsync(DoctorApprovalStatus? status) =>
        db.DoctorProfiles
            .Include(d => d.Clinics)
            .Where(d => status == null || d.Status == status)
            .OrderBy(d => d.LastName)
            .ToListAsync();

    public Task<Models.DoctorProfile?> GetByIdAsync(Guid id) =>
        db.DoctorProfiles.Include(d => d.Clinics).FirstOrDefaultAsync(d => d.Id == id);

    public Task<Models.DoctorProfile?> GetByUserIdAsync(Guid userId) =>
        db.DoctorProfiles.Include(d => d.Clinics).FirstOrDefaultAsync(d => d.UserId == userId);

    public async Task<DoctorProfileResult> CreateAsync(CreateDoctorProfileRequest req, bool autoApprove = false)
    {
        var exists = await db.DoctorProfiles.AnyAsync(d => d.UserId == req.UserId);
        if (exists) return new DoctorProfileResult(DoctorProfileResultStatus.AlreadyExists);

        var clinics = req.ClinicIds.Count == 0
            ? new List<Models.Clinic>()
            : await db.Clinics.Where(c => req.ClinicIds.Contains(c.Id)).ToListAsync();

        var profile = new Models.DoctorProfile
        {
            UserId = req.UserId,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Specialty = req.Specialty,
            Bio = req.Bio,
            LicenseNumber = req.LicenseNumber,
            ConsultationFee = req.ConsultationFee,
            Clinics = clinics,
            // Admin-created profiles are already vetted by the admin creating them,
            // so they skip the review queue; self-service profiles still need approval.
            Status = autoApprove ? DoctorApprovalStatus.Approved : DoctorApprovalStatus.Pending
        };
        db.DoctorProfiles.Add(profile);
        await db.SaveChangesAsync();

        if (autoApprove)
            await PublishApprovedEventAsync(profile);

        return new DoctorProfileResult(DoctorProfileResultStatus.Success, profile);
    }

    public async Task<DoctorProfileResult> UpdateOwnAsync(Guid userId, UpdateOwnDoctorProfileRequest req)
    {
        var profile = await db.DoctorProfiles.Include(d => d.Clinics).FirstOrDefaultAsync(d => d.UserId == userId);
        if (profile is null) return new DoctorProfileResult(DoctorProfileResultStatus.NotFound);

        if (req.Bio is not null) profile.Bio = req.Bio;
        if (req.ClinicIds is not null)
        {
            var clinics = req.ClinicIds.Count == 0
                ? new List<Models.Clinic>()
                : await db.Clinics.Where(c => req.ClinicIds.Contains(c.Id)).ToListAsync();
            profile.Clinics = clinics;
        }
        if (req.ConsultationFee is not null) profile.ConsultationFee = req.ConsultationFee.Value;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return new DoctorProfileResult(DoctorProfileResultStatus.Success, profile);
    }

    public async Task<DoctorProfileResult> ApproveAsync(Guid id)
    {
        var profile = await db.DoctorProfiles.Include(d => d.Clinics).FirstOrDefaultAsync(d => d.Id == id);
        if (profile is null) return new DoctorProfileResult(DoctorProfileResultStatus.NotFound);
        if (profile.Status is not (DoctorApprovalStatus.Pending or DoctorApprovalStatus.Suspended))
            return new DoctorProfileResult(DoctorProfileResultStatus.InvalidTransition);

        profile.Status = DoctorApprovalStatus.Approved;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await PublishApprovedEventAsync(profile);

        return new DoctorProfileResult(DoctorProfileResultStatus.Success, profile);
    }

    private Task PublishApprovedEventAsync(Models.DoctorProfile profile) =>
        bus.Publish(new DoctorApprovedEvent(
            DoctorProfileId: profile.Id,
            UserId: profile.UserId,
            FirstName: profile.FirstName,
            LastName: profile.LastName,
            Specialty: profile.Specialty,
            Bio: profile.Bio,
            ConsultationFee: profile.ConsultationFee,
            Clinics: profile.Clinics.Select(c => new ClinicDto(
                c.Id, c.Name, c.Type, c.Address, c.City, c.Phone, c.CreatedAt)).ToList(),
            OccurredAt: DateTime.UtcNow
        ));

    public async Task<DoctorProfileResult> SuspendAsync(Guid id, SuspendDoctorRequest req)
    {
        var profile = await db.DoctorProfiles.FirstOrDefaultAsync(d => d.Id == id);
        if (profile is null) return new DoctorProfileResult(DoctorProfileResultStatus.NotFound);
        if (profile.Status != DoctorApprovalStatus.Approved)
            return new DoctorProfileResult(DoctorProfileResultStatus.InvalidTransition);

        profile.Status = DoctorApprovalStatus.Suspended;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await bus.Publish(new DoctorSuspendedEvent(
            DoctorProfileId: profile.Id,
            UserId: profile.UserId,
            Reason: req.Reason,
            OccurredAt: DateTime.UtcNow
        ));

        return new DoctorProfileResult(DoctorProfileResultStatus.Success, profile);
    }
}
