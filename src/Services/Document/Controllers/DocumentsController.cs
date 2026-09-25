using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;
using LandaDoc.Document.Data;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Document.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController(DocumentDbContext db, IAmazonS3 s3, IConfiguration cfg) : ControllerBase
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
        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        // Patients may only upload their own documents; doctors may upload on a patient's behalf.
        if (callerRole == "Patient" && callerId != patientId) return Forbid();

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
        return doc is null ? NotFound() : Ok(MapToDto(doc));
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid? patientId, [FromQuery] Guid? appointmentId)
    {
        if (patientId is null && appointmentId is null) return BadRequest();

        var docs = await db.Documents
            .Where(d => (patientId == null || d.PatientId == patientId) &&
                        (appointmentId == null || d.AppointmentId == appointmentId))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
        return Ok(docs.Select(MapToDto));
    }

    [HttpGet("{id:guid}/download-url")]
    [Authorize]
    public async Task<IActionResult> GetDownloadUrl(Guid id)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound();

        var url = s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = doc.StorageKey,
            Expires = DateTime.UtcNow.AddMinutes(15)
        });
        return Ok(new { url });
    }

    private static DocumentDto MapToDto(Models.Document d) => new(
        d.Id, d.PatientId, d.DoctorId, d.AppointmentId, d.FileName, d.ContentType, d.SizeBytes, d.Category, d.CreatedAt);
}
