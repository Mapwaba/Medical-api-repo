using LandaDoc.Search.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Search.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(ISearchIndexService index) : ControllerBase
{
    [HttpGet("doctors")]
    public async Task<IActionResult> SearchDoctors(
        [FromQuery] string? specialty, [FromQuery] string? city, [FromQuery] string? q) =>
        Ok(await index.SearchAsync(specialty, city, q));

    [HttpGet("doctors/{id:guid}")]
    public async Task<IActionResult> GetDoctor(Guid id)
    {
        var doctor = await index.GetByIdAsync(id);
        return doctor is null ? NotFound() : Ok(doctor);
    }
}
