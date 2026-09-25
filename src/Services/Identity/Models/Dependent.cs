using LandaDoc.Shared.Models;
namespace LandaDoc.Identity.Models;

public class Dependent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GuardianUserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
