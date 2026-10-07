using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;
using LandaDoc.Document.Data;
using LandaDoc.Document.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Document.Controllers;

// A patient's documents can be read or added only by that patient and by doctors who have a
// (non-cancelled) appointment with them. Every endpoint below goes through CheckAccessAsync.
[ApiController]
[Route("api/[controller]")]
public class DocumentsController(
    DocumentDbContext db, IAmazonS3 s3, IConfiguration cfg, IAppointmentAccessClient appointments) : ControllerBase
{
    private string BucketName => cfg["S3:BucketName"]!;

    [HttpPost]
    [Authorize(Roles = "Patient,Doctor")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Upload(
        [FromForm] Guid patientId,
        [FromForm] Guid? doctorId,
        [FromForm] Guid? appointmentId,
        [FromForm] string category,
        [FromForm] IFormFile file)
    {
        if (file.Length == 0) return BadRequest(new { error = "File is empty" });

        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // Patients upload their own documents; doctors upload for patients they've seen.
        var denied = await CheckAccessAsync(patientId);
        if (denied is not null) return denied;

        var storageKey = $"{patientId}/{Guid.NewGuid()}-{file.FileName}";
        await using (var stream = file.OpenReadStream())
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = BucketName,
                Key = storageKey,
                InputStream = stream,
                ContentType = file.ContentType
            });
        }

        var doc = new Models.Document
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentId = appointmentId,
            UploadedByUserId = callerId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category,
            StorageKey = storageKey
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        return StatusCode(201, MapToDto(doc));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        return await CheckAccessAsync(doc.PatientId) ?? Ok(MapToDto(doc));
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid? patientId, [FromQuery] Guid? appointmentId)
    {
        if (patientId is null && appointmentId is null) return BadRequest();

        if (patientId is not null)
        {
            var denied = await CheckAccessAsync(patientId.Value);
            if (denied is not null) return denied;
        }

        var docs = await db.Documents
            .Where(d => (patientId == null || d.PatientId == patientId) &&
                        (appointmentId == null || d.AppointmentId == appointmentId))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        // Asked by appointment only: the caller must have access to every patient in the result
        if (patientId is null)
        {
            foreach (var docPatientId in docs.Select(d => d.PatientId).Distinct())
            {
                var denied = await CheckAccessAsync(docPatientId);
                if (denied is not null) return denied;
            }
        }
        return Ok(docs.Select(MapToDto));
    }

    [HttpGet("{id:guid}/download-url")]
    [Authorize]
    public async Task<IActionResult> GetDownloadUrl(Guid id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();
        var denied = await CheckAccessAsync(doc.PatientId);
        if (denied is not null) return denied;

        var url = s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = doc.StorageKey,
            Expires = DateTime.UtcNow.AddMinutes(15)
        });
        return Ok(new { url });
    }

    // null when the caller may access this patient's documents, otherwise the response to return
    private async Task<IActionResult?> CheckAccessAsync(Guid patientId)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (callerId == patientId) return null;
        if (!User.IsInRole("Doctor")) return Forbid();

        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        return await appointments.DoctorHasSeenPatientAsync(patientId, token) switch
        {
            true => null,
            false => Forbid(),
            // Fail closed: if the Appointment service can't confirm, don't hand out the documents
            null => Problem("Couldn't verify access right now. Please try again.", statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }

    private static DocumentDto MapToDto(Models.Document d) => new(
        d.Id, d.PatientId, d.DoctorId, d.AppointmentId, d.FileName, d.ContentType, d.SizeBytes, d.Category, d.CreatedAt);
}
