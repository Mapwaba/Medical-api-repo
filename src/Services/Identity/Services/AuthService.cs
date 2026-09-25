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
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
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
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return new AuthResult(AuthResultStatus.EmailAlreadyRegistered);

        Enum.TryParse<Gender>(req.Gender, true, out var gender);

        var user = new User
        {
            Email = req.Email,
            PasswordHash = hasher.Hash(req.Password),
            Role = UserRole.Patient,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Phone = req.Phone,
            DateOfBirth = req.DateOfBirth,
            Gender = string.IsNullOrEmpty(req.Gender) ? null : gender,
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
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return new AuthResult(AuthResultStatus.EmailAlreadyRegistered);

        var user = new User
        {
            Email = req.Email,
            PasswordHash = hasher.Hash(req.Password),
            Role = UserRole.Doctor,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Phone = req.Phone,
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
        user.IsApproved, user.IsActive, user.DateOfBirth, user.Gender?.ToString());
}
