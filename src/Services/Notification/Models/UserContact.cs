namespace LandaDoc.Notification.Models;

// Local read-model projection built from UserRegisteredEvent — Notification doesn't own
// user profile data, only enough contact info to send payment receipts/failure notices.
public class UserContact
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
}
