namespace LandaDoc.Notification.Services;

public interface INotificationPublisher
{
    // Inserts an in-app notification and pushes it live to the user over SignalR.
    Task PublishAsync(Guid userId, string type, string title, string body, string? payload = null);

    // Recomputes the user's pending-appointment count from the projection table and pushes it live.
    Task PushPendingCountAsync(Guid userId);
}
