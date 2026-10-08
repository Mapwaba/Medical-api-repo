using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;
using LandaDoc.Document.Data;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Document.Controllers;

// Documents a doctor sends to be verified (ID or passport, membership card, establishment
// contract). The doctor manages their own; admins read them when reviewing the doctor.
// Nobody else can see them, not even the doctor's patients.
[ApiController]
[Route("api/verification-documents")]
public class VerificationDocumentsController(
    DocumentDbContext db, IAmazonS3 s3, IConfiguration cfg, ILogger<VerificationDocumentsController> log) : ControllerBase
{
    private const long MaxBytes = 10_000_000;
    private static readonly string[] AllowedTypes = ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    private string BucketName => cfg["S3:BucketName"]!;

    [HttpPost]
    [Authorize(Roles = "Doctor")]
    [RequestSizeLimit(MaxBytes + 100_000)]
    public async Task<IActionResult> Upload([FromForm] string kind, [FromForm] IFormFile file)
    {
        if (!VerificationDocumentKinds.All.Contains(kind)) return BadRequest(new { error = "Unknown document kind" });
        if (file.Length == 0) return BadRequest(new { error = "File is empty" });
        if (file.Length > MaxBytes) return BadRequest(new { error = "The file must be smaller than 10 MB" });
        if (!AllowedTypes.Contains(file.ContentType)) return BadRequest(new { error = "Send a PDF or a photo (JPEG, PNG, WebP)" });

        var userId = CallerId();
        var storageKey = $"verification/{userId}/{Guid.NewGuid()}-{file.FileName}";
        try
        {
            await using var stream = file.OpenReadStream();
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = BucketName,
                Key = storageKey,
                InputStream = stream,
                ContentType = file.ContentType,
                // Same as DocumentsController.Upload: R2 (HTTPS) needs unsigned bodies, local MinIO signed ones
                DisablePayloadSigning = cfg["S3:ServiceUrl"]?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true,
            });
        }
        catch (Exception ex) when (ex is AmazonS3Exception or Amazon.Runtime.AmazonClientException or HttpRequestException or IOException)
        {
            var s3Error = ex as AmazonS3Exception;
            log.LogError(ex, "Couldn't store verification document ({Kind}, {SizeBytes} bytes) for doctor {UserId}. Storage said: {StorageErrorCode} {StorageMessage}",
                kind, file.Length, userId, s3Error?.ErrorCode ?? "no error code", s3Error?.Message ?? ex.GetBaseException().Message);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The file couldn't be stored. Please try again." });
        }

        var doc = new Models.VerificationDocument
        {
            UserId = userId,
            Kind = kind,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            StorageKey = storageKey,
        };
        db.VerificationDocuments.Add(doc);
        await db.SaveChangesAsync();
        log.LogInformation("Verification document {DocumentId} ({Kind}, {SizeBytes} bytes) uploaded by doctor {UserId}", doc.Id, kind, file.Length, userId);
        return StatusCode(201, MapToDto(doc));
    }

    [HttpGet("me")]
    [Authorize(Roles = "Doctor")]
    public Task<IActionResult> GetMine() => ListAsync(CallerId());

    [HttpGet("doctor/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> GetForDoctor(Guid userId) => ListAsync(userId);

    [HttpGet("{id:guid}/download-url")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> GetDownloadUrl(Guid id)
    {
        var doc = await db.VerificationDocuments.FindAsync(id);
        if (doc is null) return NotFound();
        if (!User.IsInRole("Admin") && doc.UserId != CallerId()) return Forbid();

        var url = s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = doc.StorageKey,
            Expires = DateTime.UtcNow.AddMinutes(15),
        });
        return Ok(new { url });
    }

    // The doctor replaces a wrong file: delete it, then upload the right one
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var doc = await db.VerificationDocuments.FindAsync(id);
        if (doc is null) return NotFound();
        if (doc.UserId != CallerId()) return Forbid();

        db.VerificationDocuments.Remove(doc);
        await db.SaveChangesAsync();
        try
        {
            await s3.DeleteObjectAsync(BucketName, doc.StorageKey);
        }
        catch (Exception ex) when (ex is AmazonS3Exception or Amazon.Runtime.AmazonClientException or HttpRequestException)
        {
            // The record is gone, so nobody can reach the file; it only takes up space
            log.LogWarning(ex, "Verification document {DocumentId} deleted, but its file {StorageKey} couldn't be removed from storage", doc.Id, doc.StorageKey);
        }
        log.LogInformation("Verification document {DocumentId} ({Kind}) deleted by doctor {UserId}", doc.Id, doc.Kind, doc.UserId);
        return NoContent();
    }

    private async Task<IActionResult> ListAsync(Guid userId)
    {
        var docs = await db.VerificationDocuments
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();
        return Ok(docs.Select(MapToDto));
    }

    private Guid CallerId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static VerificationDocumentDto MapToDto(Models.VerificationDocument d) =>
        new(d.Id, d.UserId, d.Kind, d.FileName, d.ContentType, d.SizeBytes, d.CreatedAt);
}
