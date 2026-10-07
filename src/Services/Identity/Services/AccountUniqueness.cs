using LandaDoc.Identity.Data;
using LandaDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Services;

// One account per email and per phone number (across doctors and patients alike), and no two
// doctors — or two patients — with the same first and last name. Checked in code rather than by
// unique indexes because existing data may already hold duplicates.
public static class AccountUniqueness
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // Digits only, so "+243 822-583-524" and "243822583524" are the same number
    public static string NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? "" : new string(phone.Where(char.IsDigit).ToArray());

    public static string NormalizeName(string? name) => (name ?? "").Trim().ToLowerInvariant();

    // The first rule the values break, or null. Pass null for a value that isn't being set or
    // changed, and the caller's own id when editing so they don't clash with themselves.
    public static async Task<AuthResultStatus?> FindConflictAsync(
        IdentityDbContext db, string? email, string? phone, string? firstName, string? lastName,
        UserRole role, Guid? exceptUserId = null)
    {
        if (email is not null)
        {
            var normalized = NormalizeEmail(email);
            if (await db.Users.AnyAsync(u => u.Id != exceptUserId && u.Email.ToLower() == normalized))
                return AuthResultStatus.EmailAlreadyRegistered;
        }

        var digits = NormalizePhone(phone);
        if (digits.Length > 0)
        {
            // Stored numbers keep their formatting, so compare digit-for-digit in memory
            var phones = await db.Users
                .Where(u => u.Id != exceptUserId && u.Phone != null)
                .Select(u => u.Phone!)
                .ToListAsync();
            if (phones.Any(p => NormalizePhone(p) == digits))
                return AuthResultStatus.PhoneAlreadyRegistered;
        }

        if (firstName is not null && lastName is not null)
        {
            var first = NormalizeName(firstName);
            var last = NormalizeName(lastName);
            if (await db.Users.AnyAsync(u => u.Id != exceptUserId && u.Role == role
                    && u.FirstName != null && u.LastName != null
                    && u.FirstName.Trim().ToLower() == first && u.LastName.Trim().ToLower() == last))
                return AuthResultStatus.NameAlreadyRegistered;
        }

        return null;
    }
}
