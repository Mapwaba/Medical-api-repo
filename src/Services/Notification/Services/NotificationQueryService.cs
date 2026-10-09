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

    // Page 1 is the newest. A page past the end comes back as the last page, so deleting or
    // reading notifications never strands someone on an empty page.
    public async Task<NotificationPageDto> GetPageAsync(Guid userId, int page, int pageSize, bool unreadOnly)
    {
        if (!NotificationPageDto.PageSizes.Contains(pageSize)) pageSize = NotificationPageDto.DefaultPageSize;

        var mine = db.Notifications.Where(n => n.UserId == userId);
        var unreadCount = await mine.CountAsync(n => !n.IsRead);
        var query = unreadOnly ? mine.Where(n => !n.IsRead) : mine;
        var total = unreadOnly ? unreadCount : await mine.CountAsync();

        var lastPage = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, lastPage);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.IsRead, n.CreatedAt))
            .ToListAsync();
        return new NotificationPageDto(items, total, unreadCount, page, pageSize);
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
