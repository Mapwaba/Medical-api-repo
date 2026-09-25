using LandaDoc.Admin.Data;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Admin.Services;

public class ClinicService(AdminDbContext db, IPublishEndpoint bus) : IClinicService
{
    public Task<List<Models.Clinic>> GetAllAsync() => db.Clinics.OrderBy(c => c.Name).ToListAsync();

    public Task<Models.Clinic?> GetByIdAsync(Guid id) =>
        db.Clinics.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Models.Clinic> CreateAsync(CreateClinicRequest req)
    {
        var clinic = new Models.Clinic
        {
            Name = req.Name,
            Type = req.Type,
            Address = req.Address,
            City = req.City,
            Phone = req.Phone
        };
        db.Clinics.Add(clinic);
        await db.SaveChangesAsync();
        await PublishUpdatedAsync(clinic);
        return clinic;
    }

    public async Task<Models.Clinic?> UpdateAsync(Guid id, UpdateClinicRequest req)
    {
        var clinic = await db.Clinics.FirstOrDefaultAsync(c => c.Id == id);
        if (clinic is null) return null;

        if (req.Name is not null) clinic.Name = req.Name;
        if (req.Type is not null) clinic.Type = req.Type.Value;
        if (req.Address is not null) clinic.Address = req.Address;
        if (req.City is not null) clinic.City = req.City;
        if (req.Phone is not null) clinic.Phone = req.Phone;
        clinic.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await PublishUpdatedAsync(clinic);
        return clinic;
    }

    private Task PublishUpdatedAsync(Models.Clinic clinic) => bus.Publish(new ClinicUpdatedEvent(
        ClinicId: clinic.Id,
        Name: clinic.Name,
        City: clinic.City,
        Address: clinic.Address,
        Type: clinic.Type,
        OccurredAt: DateTime.UtcNow
    ));
}
