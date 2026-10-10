using LandaDoc.Payment.Controllers;
using LandaDoc.Payment.Data;
using LandaDoc.Payment.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LandaDoc.Payment.Tests;

// An insurer is deleted while no claim was made with it, and switched off once one was
public class InsurerDeleteTests
{
    private readonly PaymentDbContext _db = new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private InsurersController Controller() => new(_db, NullLogger<InsurersController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private Insurer Add(string name)
    {
        var insurer = new Insurer { Name = name, IsActive = true };
        _db.Insurers.Add(insurer);
        _db.SaveChanges();
        return insurer;
    }

    [Fact]
    public async Task An_unused_insurer_is_deleted_and_leaves_doctors_lists()
    {
        var mistake = Add("SONAS (typo)");
        var kept = Add("SONAS");
        var doctorId = Guid.NewGuid();
        _db.DoctorInsurerChoices.Add(new DoctorInsurerChoice { DoctorId = doctorId, InsurerIds = [mistake.Id, kept.Id] });
        await _db.SaveChangesAsync();

        var result = await Controller().Delete(mistake.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([kept.Id], (await _db.Insurers.ToListAsync()).Select(i => i.Id));
        Assert.Equal([kept.Id], (await _db.DoctorInsurerChoices.FindAsync(doctorId))!.InsurerIds);
    }

    [Fact]
    public async Task An_insurer_with_claims_is_switched_off_instead()
    {
        var used = Add("Rawsur");
        _db.InsuranceClaims.Add(new InsuranceClaim { InsurerId = used.Id, InsurerName = used.Name, MemberNumber = "M-1" });
        await _db.SaveChangesAsync();

        var result = await Controller().Delete(used.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(new LandaDoc.Shared.DTOs.DeleteInsurerResult(false, true, 1), ok.Value);
        var kept = await _db.Insurers.SingleAsync(i => i.Id == used.Id);
        Assert.False(kept.IsActive);   // hidden from patients, still there for its claim
    }

    [Fact]
    public async Task An_unknown_insurer_is_not_found() =>
        Assert.IsType<NotFoundResult>(await Controller().Delete(Guid.NewGuid()));
}
