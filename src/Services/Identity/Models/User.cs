using LandaDoc.Shared.Models;
namespace LandaDoc.Identity.Models;
public class User
{
public Guid Id { get; set; } = Guid.NewGuid();
public string Email { get; set; } = "";
public string PasswordHash { get; set; } = "";
public UserRole Role { get; set; } = UserRole.Patient;
public string? FirstName { get; set; }
public string? LastName { get; set; }
public string? Phone { get; set; }
public string? AvatarUrl { get; set; }
public DateOnly? DateOfBirth { get; set; }
public Gender? Gender { get; set; }
public bool IsActive { get; set; } = true;
public bool IsApproved { get; set; } = true; // false for new doctors
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

}



public class RefreshToken
{
public Guid Id { get; set; } = Guid.NewGuid();
public Guid UserId { get; set; }
public string TokenHash { get; set; } = ""; // stored hashed
public DateTime ExpiresAt { get; set; }
public bool IsRevoked { get; set; } = false;
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
public User User { get; set; } = null!;
}