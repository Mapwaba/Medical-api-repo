using System.Security.Cryptography;
using LandaDoc.Search.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Search.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(ISearchIndexService index) : ControllerBase
{
    // photos=false leaves each doctor's photo out of the answer (HasPhoto still says whether there
    // is one), for apps that load it separately from doctors/{id}/photo — far less data per search
    [HttpGet("doctors")]
    public async Task<IActionResult> SearchDoctors(
        [FromQuery] string? specialty, [FromQuery] string? city, [FromQuery] string? q, [FromQuery] bool photos = true)
    {
        var results = await index.SearchAsync(specialty, city, q);
        return Ok(photos ? results : results.Select(WithoutPhoto));
    }

    [HttpGet("doctors/{id:guid}")]
    public async Task<IActionResult> GetDoctor(Guid id, [FromQuery] bool photos = true)
    {
        var doctor = await index.GetByIdAsync(id);
        return doctor is null ? NotFound() : Ok(photos ? doctor : WithoutPhoto(doctor));
    }

    // The doctor's photo as an image, so apps can show and cache it like any picture. A new photo
    // gets a new ETag, so a cached copy is only reused while it's still the current one.
    [HttpGet("doctors/{id:guid}/photo")]
    public async Task<IActionResult> GetDoctorPhoto(Guid id)
    {
        var doctor = await index.GetByIdAsync(id);
        if (doctor?.PhotoDataUrl is not { } dataUrl || ImageOf(dataUrl) is not var (contentType, bytes))
            return NotFound();

        var etag = $"\"{Convert.ToHexString(SHA256.HashData(bytes))[..16]}\"";
        if (Request.Headers.IfNoneMatch == etag) return StatusCode(StatusCodes.Status304NotModified);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(bytes, contentType);
    }

    private static DoctorSearchResultDto WithoutPhoto(DoctorSearchResultDto d) => d with { PhotoDataUrl = null };

    // "data:image/jpeg;base64,...." → ("image/jpeg", bytes); null when it isn't a base64 image
    private static (string ContentType, byte[] Bytes)? ImageOf(string dataUrl)
    {
        const string prefix = "data:";
        var comma = dataUrl.IndexOf(',');
        if (!dataUrl.StartsWith(prefix, StringComparison.Ordinal) || comma < 0) return null;
        var header = dataUrl[prefix.Length..comma];   // "image/jpeg;base64"
        if (!header.EndsWith(";base64", StringComparison.Ordinal) || !header.StartsWith("image/", StringComparison.Ordinal)) return null;
        try
        {
            return (header[..^";base64".Length], Convert.FromBase64String(dataUrl[(comma + 1)..]));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
