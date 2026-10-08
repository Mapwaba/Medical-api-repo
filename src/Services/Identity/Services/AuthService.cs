using LandaDoc.Identity.Data;
using LandaDoc.Identity.Models;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Services;

public class AuthService(
    IdentityDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IPublishEndpoint bus) : IAuthService
{
    public async Task<AuthResult> LoginAsync(LoginRequest req)
    {
        var email = AccountUniqueness.NormalizeEmail(req.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
            return new AuthResult(AuthResultStatus.InvalidCredentials);
        if (!user.IsActive)
            return new AuthResult(AuthResultStatus.AccountInactive);
        // Pending-approval doctors can still log in — they need to reach /me and
        // /api/doctors/me to build their profile and check status while waiting.
        // Approval gates what they can DO (e.g. booking-related endpoints), not login.

        var response = await IssueTokensAsync(user, includeProfile: true);
        return new AuthResult(AuthResultStatus.Success, response);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken)
    {
        var hash = tokens.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash
                && !r.IsRevoked
                && r.ExpiresAt > DateTime.UtcNow);
        if (stored is null)
            return new AuthResult(AuthResultStatus.InvalidRefreshToken);

        // Rotate: revoke old, issue new
        stored.IsRevoked = true;
        var response = await IssueTokensAsync(stored.User, includeProfile: false);
        return new AuthResult(AuthResultStatus.Success, response);
    }

    public async Task<AuthResult> RegisterPatientAsync(RegisterPatientRequest req)
    {
        var conflict = await AccountUniqueness.FindConflictAsync(
            db, req.Email, req.Phone, req.FirstName, req.LastName, UserRole.Patient);
        if (conflict is not null) return new AuthResult(conflict.Value);

        Enum.TryParse<Gender>(req.Gender, true, out var gender);

        var user = new User
        {
            Email = req.Email.Trim(),
            PasswordHash = hasher.Hash(req.Password),
            Role = UserRole.Patient,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            DateOfBirth = req.DateOfBirth,
            Gender = string.IsNullOrEmpty(req.Gender) ? null : gender,
            Country = CountryCode(req.Country),
            Province = Clean(req.Province),
            City = Clean(req.City),
            PostalCode = Clean(req.PostalCode),
            IdNumber = Clean(req.IdNumber),
            IsActive = true,
            IsApproved = true // patients are auto-approved; doctors require admin approval
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await bus.Publish(new UserRegisteredEvent(
            UserId: user.Id,
            Email: user.Email,
            Role: user.Role.ToString(),
            FirstName: user.FirstName ?? "",
            LastName: user.LastName ?? "",
            Phone: user.Phone,
            OccurredAt: DateTime.UtcNow
        ));

        var response = await IssueTokensAsync(user, includeProfile: true);
        return new AuthResult(AuthResultStatus.Success, response);
    }

    public async Task<AuthResult> RegisterDoctorAsync(RegisterDoctorRequest req)
    {
        var conflict = await AccountUniqueness.FindConflictAsync(
            db, req.Email, req.Phone, req.FirstName, req.LastName, UserRole.Doctor);
        if (conflict is not null) return new AuthResult(conflict.Value);

        var user = new User
        {
            Email = req.Email.Trim(),
            PasswordHash = hasher.Hash(req.Password),
            Role = UserRole.Doctor,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            Country = CountryCode(req.Country),
            Province = Clean(req.Province),
            City = Clean(req.City),
            PostalCode = Clean(req.PostalCode),
            IsActive = true,
            IsApproved = false // doctors require admin approval before they can log in
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await bus.Publish(new UserRegisteredEvent(
            UserId: user.Id,
            Email: user.Email,
            Role: user.Role.ToString(),
            FirstName: user.FirstName ?? "",
            LastName: user.LastName ?? "",
            Phone: user.Phone,
            OccurredAt: DateTime.UtcNow
        ));

        var response = await IssueTokensAsync(user, includeProfile: true);
        return new AuthResult(AuthResultStatus.Success, response);
    }

    public async Task<UserDto?> GetMeAsync(Guid userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return user is null ? null : MapToDto(user);
    }

    public async Task<List<UserDto>> GetUsersAsync(UserRole? role)
    {
        var query = db.Users.AsQueryable();
        if (role is not null) query = query.Where(u => u.Role == role);
        var users = await query.OrderBy(u => u.LastName).ToListAsync();
        return users.Select(MapToDto).ToList();
    }

    public async Task<UserCountsDto> GetUserCountsAsync()
    {
        var patientCount = await db.Users.CountAsync(u => u.Role == UserRole.Patient);
        var doctorCount = await db.Users.CountAsync(u => u.Role == UserRole.Doctor && u.IsApproved && u.IsActive);
        return new UserCountsDto(patientCount, doctorCount);
    }

    public async Task<UpdateMeResult> UpdateMeAsync(Guid userId, UpdateMeRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return new UpdateMeResult(AuthResultStatus.InvalidCredentials, null);

        // Only check what changes, so someone who already shares a value can still save other fields
        var phoneChanged = AccountUniqueness.NormalizePhone(req.Phone) != AccountUniqueness.NormalizePhone(user.Phone);
        var nameChanged = AccountUniqueness.NormalizeName(req.FirstName) != AccountUniqueness.NormalizeName(user.FirstName)
            || AccountUniqueness.NormalizeName(req.LastName) != AccountUniqueness.NormalizeName(user.LastName);
        var conflict = await AccountUniqueness.FindConflictAsync(db, null,
            phoneChanged ? req.Phone : null,
            nameChanged ? req.FirstName : null, nameChanged ? req.LastName : null,
            user.Role, exceptUserId: user.Id);
        if (conflict is not null) return new UpdateMeResult(conflict.Value, null);

        user.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new UpdateMeResult(AuthResultStatus.Success, MapToDto(user));
    }

    public async Task<UserDto?> UpdateAddressAsync(Guid userId, UpdateAddressRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;

        user.Country = CountryCode(req.Country);
        user.Province = Clean(req.Province);
        user.City = Clean(req.City);
        user.PostalCode = Clean(req.PostalCode);
        user.IdNumber = Clean(req.IdNumber);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return MapToDto(user);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Callers check Locations.IsCountry first; stored upper-case ("CD")
    private static string? CountryCode(string? value) => Clean(value)?.ToUpperInvariant();

    public async Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !hasher.Verify(req.CurrentPassword, user.PasswordHash))
            return new AuthResult(AuthResultStatus.InvalidCredentials);

        user.PasswordHash = hasher.Hash(req.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Sign out every other session (other browsers/devices) by revoking their refresh
        // tokens, then hand this session fresh tokens so the user stays logged in here.
        await db.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true));

        var response = await IssueTokensAsync(user, includeProfile: true); // also saves the new hash
        return new AuthResult(AuthResultStatus.Success, response);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, bool includeProfile)
    {
        var accessToken = tokens.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var refreshToken = tokens.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashRefreshToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        var userDto = includeProfile ? MapToDto(user) : null;

        return new AuthResponse(accessToken, refreshToken, userDto!);
    }

    private static UserDto MapToDto(User user) => new(
        user.Id, user.Email, user.Role.ToString(),
        user.FirstName ?? "", user.LastName ?? "", user.Phone, user.AvatarUrl, null,
        user.IsApproved, user.IsActive, user.DateOfBirth, user.Gender?.ToString(),
        user.Country, user.Province, user.City, user.PostalCode, user.IdNumber);
}
