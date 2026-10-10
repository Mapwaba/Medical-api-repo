using LandaDoc.Admin.Data;
using LandaDoc.Admin.Models;
using LandaDoc.Admin.Services;
using LandaDoc.Shared.DTOs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace LandaDoc.Admin.Tests;

// A clinic no doctor works at is deleted; one with doctors is switched off instead
public class ClinicDeleteTests
{
    private readonly AdminDbContext _db = new(new DbContextOptionsBuilder<AdminDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private ClinicService Service => new(_db, Substitute.For<IPublishEndpoint>());

    private Clinic Add(string name)
    {
        var clinic = new Clinic { Name = name, City = "Kinshasa" };
        _db.Clinics.Add(clinic);
        _db.SaveChanges();
        return clinic;
    }

    [Fact]
    public async Task A_clinic_no_doctor_works_at_is_deleted()
    {
        var clinic = Add("Clinique Typo");

        var result = await Service.DeleteAsync(clinic.Id);

        Assert.Equal(new DeleteClinicResult(true, false, 0), result);
        Assert.False(await _db.Clinics.AnyAsync());
    }

    [Fact]
    public async Task A_clinic_with_doctors_is_switched_off_and_stays_on_their_profiles()
    {
        var clinic = Add("Hopital Mokole");
        var doctor = new DoctorProfile { UserId = Guid.NewGuid(), FirstName = "Bruce", LastName = "Maz", Specialty = "Generalist", Clinics = [clinic] };
        _db.DoctorProfiles.Add(doctor);
        await _db.SaveChangesAsync();

        var result = await Service.DeleteAsync(clinic.Id);

        Assert.Equal(new DeleteClinicResult(false, true, 1), result);
        var kept = await _db.Clinics.Include(c => c.DoctorProfiles).SingleAsync();
        Assert.False(kept.IsActive);
        Assert.Single(kept.DoctorProfiles);
    }

    [Fact]
    public async Task An_unknown_clinic_is_not_found() =>
        Assert.Null(await Service.DeleteAsync(Guid.NewGuid()));

    [Fact]
    public async Task Editing_can_switch_a_clinic_back_on_and_clear_its_phone()
    {
        var clinic = Add("Clinique Ngaliema");
        clinic.IsActive = false;
        clinic.Phone = "+243 81 000 0000";
        await _db.SaveChangesAsync();

        var updated = await Service.UpdateAsync(clinic.Id, new UpdateClinicRequest(null, null, null, null, "", IsActive: true));

        Assert.True(updated!.IsActive);
        Assert.Null(updated.Phone);
        Assert.Equal("Clinique Ngaliema", updated.Name);   // null fields left unchanged
    }
}
