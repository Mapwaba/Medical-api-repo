using LandaDoc.Admin.Data;
using LandaDoc.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Admin.Controllers;

// The patient home page shows how many doctors are live. Counting here, where Approve and
// Suspend are saved, makes that number change straight away; Search and Identity only catch
// up once they have processed the approval message, which can lag while they're asleep.
[ApiController]
[Route("api/doctors/count")]
public class DoctorStatsController(AdminDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous] // returns a single number that the public patient home page already shows
    public async Task<IActionResult> ApprovedCount() =>
        Ok(await db.DoctorProfiles.CountAsync(d => d.Status == DoctorApprovalStatus.Approved));
}
