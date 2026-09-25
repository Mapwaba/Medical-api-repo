using LandaDoc.Notification.Data;
using LandaDoc.Notification.Hubs;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Services;

public class NotificationPublisher(
    NotificationDbContext db,
    IHubContext<NotificationHub> hub) : INotificationPublisher
{
    public async Task PublishAsync(Guid userId, string type, string title, string body, string? payload = null)
    {
        var notification = new Models.Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            Payload = payload
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var dto = new NotificationDto(notification.Id, notification.Type, notification.Title,
            notification.Body, notification.IsRead, notification.CreatedAt);
        await hub.Clients.Group(userId.ToString()).SendAsync("NotificationReceived", dto);
    }

    public async Task PushPendingCountAsync(Guid userId)
    {
        var count = await db.AppointmentPendingProjections.CountAsync(p =>
            p.Status == AppointmentStatus.Pending && (p.PatientId == userId || p.DoctorId == userId));
        await hub.Clients.Group(userId.ToString()).SendAsync("PendingCountChanged", count);
    }
}
