using System.Security.Claims;
using LandaDoc.Notification.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Notification.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController(INotificationQueryService notifications) : ControllerBase
{
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine([FromQuery] bool unreadOnly = false)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await notifications.GetMineAsync(userId, unreadOnly));
    }

    [HttpPatch("{id:guid}/read")]
    [Authorize]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var found = await notifications.MarkReadAsync(userId, id);
        return found ? NoContent() : NotFound();
    }
}
