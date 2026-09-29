using LandaDoc.Identity.Models;
using LandaDoc.Identity.Services;
using LandaDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Data;

// The web apps can only register patients and doctors, so there is no way to
// create an admin from the UI. Setting Admin__Email on the host makes that
// account an Admin on startup. With Admin__Password also set, the account is
// created if it doesn't exist, and its password is reset to that value if it
// differs. Keep both in the host's secret settings, never in the repo.
// The user has to log in again afterwards to get a token with the new role.
public static class AdminPromoter
{
    public static async Task PromoteAsync(
        IdentityDbContext db, IPasswordHasher hasher, string? email, string? password, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        email = email.Trim();
        if (string.IsNullOrEmpty(password)) password = null;
        if (password is { Length: < 8 })
        {
            logger.LogWarning("Admin:Password is shorter than 8 characters and can't be used to log in; ignoring it");
            password = null;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        if (user is null)
        {
            if (password is null)
            {
                logger.LogWarning("Admin:Email is set to {Email} but no account with that email exists; register it first or set Admin:Password", email);
                return;
            }
            db.Users.Add(new User
            {
                Email = email,
                PasswordHash = hasher.Hash(password),
                Role = UserRole.Admin,
                FirstName = "Admin",
                LastName = "LandaDoc",
                IsActive = true,
                IsApproved = true
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Created admin account {Email}", email);
            return;
        }

        var changed = false;
        if (user.Role != UserRole.Admin || !user.IsActive || !user.IsApproved)
        {
            user.Role = UserRole.Admin;
            user.IsActive = true;
            user.IsApproved = true;
            changed = true;
            logger.LogInformation("Promoted {Email} to Admin", email);
        }
        if (password is not null && !hasher.Verify(password, user.PasswordHash))
        {
            user.PasswordHash = hasher.Hash(password);
            changed = true;
            logger.LogInformation("Reset the password of admin account {Email}", email);
        }
        if (changed) await db.SaveChangesAsync();
    }
}
