namespace LandaDoc.Notification.Models;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Type { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string? Payload { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
