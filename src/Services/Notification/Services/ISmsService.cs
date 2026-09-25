namespace LandaDoc.Notification.Services;

public interface ISmsService
{
    Task SendAsync(string to, string body);
}
