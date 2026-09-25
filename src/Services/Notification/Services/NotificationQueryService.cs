using LandaDoc.Notification.Data;
using LandaDoc.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Services;

public class NotificationQueryService(NotificationDbContext db) : INotificationQueryService
{
    public async Task<List<NotificationDto>> GetMineAsync(Guid userId, bool unreadOnly)
    {
        var query = db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.IsRead, n.CreatedAt))
            .ToListAsync();
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notification is null) return false;

        notification.IsRead = true;
        await db.SaveChangesAsync();
        return true;
    }
}
