using LandaDoc.Shared.DTOs;

namespace LandaDoc.Notification.Services;

public interface INotificationQueryService
{
    Task<List<NotificationDto>> GetMineAsync(Guid userId, bool unreadOnly);
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId);
}
