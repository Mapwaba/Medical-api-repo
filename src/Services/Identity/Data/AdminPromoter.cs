using LandaDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Data;

// The web apps can only register patients and doctors, so there is no way to
// create an admin from the UI. Setting Admin__Email on the host promotes that
// already-registered account to Admin on startup. The user has to log in again
// afterwards to get a token with the new role.
public static class AdminPromoter
{
    public static async Task PromoteAsync(IdentityDbContext db, string? email, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        email = email.Trim();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        if (user is null)
        {
            logger.LogWarning("Admin:Email is set to {Email} but no account with that email exists; register it first", email);
            return;
        }
        if (user.Role == UserRole.Admin && user.IsActive && user.IsApproved) return;

        user.Role = UserRole.Admin;
        user.IsActive = true;
        user.IsApproved = true;
        await db.SaveChangesAsync();
        logger.LogInformation("Promoted {Email} to Admin", email);
    }
}
