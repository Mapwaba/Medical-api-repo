namespace LandaDoc.Payment.Models;

// An insurance company / medical aid that LandaDoc partners with. Admins maintain the list;
// patients pick from the active ones at checkout.
public class Insurer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
