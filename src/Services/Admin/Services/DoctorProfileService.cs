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
        if (await NameTakenAsync(req.FirstName, req.LastName, exceptProfileId: null))
            return new DoctorProfileResult(DoctorProfileResultStatus.NameTaken);

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
            PhotoDataUrl = req.PhotoDataUrl,
            Clinics = clinics,
            // Admin-created profiles are already vetted by the admin creating them, and
            // self-service ones wait for review unless Doctors:RequireApproval is false.
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

        var newFirst = string.IsNullOrWhiteSpace(req.FirstName) ? profile.FirstName : req.FirstName.Trim();
        var newLast = string.IsNullOrWhiteSpace(req.LastName) ? profile.LastName : req.LastName.Trim();
        var nameChanged = !string.Equals(newFirst, profile.FirstName.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(newLast, profile.LastName.Trim(), StringComparison.OrdinalIgnoreCase);
        if (nameChanged && await NameTakenAsync(newFirst, newLast, exceptProfileId: profile.Id))
            return new DoctorProfileResult(DoctorProfileResultStatus.NameTaken);

        if (!string.IsNullOrWhiteSpace(req.FirstName)) profile.FirstName = req.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(req.LastName)) profile.LastName = req.LastName.Trim();
        if (!string.IsNullOrWhiteSpace(req.Specialty)) profile.Specialty = req.Specialty.Trim();
        if (req.LicenseNumber is not null) profile.LicenseNumber = string.IsNullOrWhiteSpace(req.LicenseNumber) ? null : req.LicenseNumber.Trim();
        if (req.Bio is not null) profile.Bio = req.Bio;
        if (req.ClinicIds is not null)
        {
            var clinics = req.ClinicIds.Count == 0
                ? new List<Models.Clinic>()
                : await db.Clinics.Where(c => req.ClinicIds.Contains(c.Id)).ToListAsync();
            profile.Clinics = clinics;
        }
        if (req.ConsultationFee is not null) profile.ConsultationFee = req.ConsultationFee.Value;
        if (req.PhotoDataUrl is not null) profile.PhotoDataUrl = req.PhotoDataUrl;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Re-send a live doctor to Search (and Payment, for the fee) so patients see the changes
        // straight away; this event is how those services learn about approved doctors.
        if (profile.Status == DoctorApprovalStatus.Approved)
            await PublishApprovedEventAsync(profile);

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

    // Search keeps approved doctors in Redis, which can be wiped (e.g. a free-tier
    // restart) and misses approvals made while RabbitMQ was down. Re-sending every
    // approved doctor rebuilds it; Search's upsert makes repeats harmless.
    public async Task<int> RepublishApprovedAsync(CancellationToken ct = default)
    {
        var approved = await db.DoctorProfiles
            .Include(d => d.Clinics)
            .Where(d => d.Status == DoctorApprovalStatus.Approved)
            .ToListAsync(ct);
        foreach (var profile in approved)
            await PublishApprovedEventAsync(profile);
        return approved.Count;
    }

    // No two doctors with the same first and last name (case and surrounding spaces ignored)
    private Task<bool> NameTakenAsync(string firstName, string lastName, Guid? exceptProfileId)
    {
        var first = firstName.Trim().ToLower();
        var last = lastName.Trim().ToLower();
        return db.DoctorProfiles.AnyAsync(d => d.Id != exceptProfileId
            && d.FirstName.Trim().ToLower() == first && d.LastName.Trim().ToLower() == last);
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
            OccurredAt: DateTime.UtcNow,
            PhotoDataUrl: profile.PhotoDataUrl
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
