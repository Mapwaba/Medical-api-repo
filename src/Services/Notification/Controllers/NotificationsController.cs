using LandaDoc.Shared.DTOs;
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

    // One page at a time, for the web apps' notifications page (page size 10, 20, 30, 50 or 100)
    [HttpGet("mine/page")]
    [Authorize]
    public async Task<IActionResult> GetMinePage([FromQuery] int page = 1, [FromQuery] int pageSize = NotificationPageDto.DefaultPageSize,
        [FromQuery] bool unreadOnly = false)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await notifications.GetPageAsync(userId, page, pageSize, unreadOnly));
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
