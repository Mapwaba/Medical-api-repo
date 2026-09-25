using System.Security.Claims;
using LandaDoc.Notification.Data;
using LandaDoc.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Hubs;

[Authorize]
public class NotificationHub(NotificationDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);

        // Push the caller's current pending count immediately so the badge is correct
        // on first load/reconnect, not just after the next event.
        var id = Guid.Parse(userId);
        var count = await db.AppointmentPendingProjections.CountAsync(p =>
            p.Status == AppointmentStatus.Pending && (p.PatientId == id || p.DoctorId == id));
        await Clients.Caller.SendAsync("PendingCountChanged", count);

        await base.OnConnectedAsync();
    }
}
