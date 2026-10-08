using LandaDoc.Shared.Locations;
using System.Security.Claims;
using LandaDoc.Identity.Services;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LandaDoc.Identity.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(IAuthService auth, ILogger<AuthController> log) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterPatientRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (req.Country is not null && !Locations.IsCountry(req.Country)) return UnknownCountry();
        var result = await auth.RegisterPatientAsync(req);
        if (result.Status == AuthResultStatus.Success)
            log.LogInformation("Patient account {UserId} registered", result.Response!.User.Id);
        else
            log.LogInformation("Patient registration refused: {Reason}", result.Status);
        return result.Status switch
        {
            AuthResultStatus.EmailAlreadyRegistered or AuthResultStatus.PhoneAlreadyRegistered
                or AuthResultStatus.NameAlreadyRegistered => DuplicateConflict(result.Status),
            AuthResultStatus.Success => StatusCode(201, result.Response),
            _ => Problem()
        };
    }

    [HttpPost("register-doctor")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterDoctor([FromBody] RegisterDoctorRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (req.Country is not null && !Locations.IsCountry(req.Country)) return UnknownCountry();
        var result = await auth.RegisterDoctorAsync(req);
        if (result.Status == AuthResultStatus.Success)
            log.LogInformation("Doctor account {UserId} registered", result.Response!.User.Id);
        else
            log.LogInformation("Doctor registration refused: {Reason}", result.Status);
        return result.Status switch
        {
            AuthResultStatus.EmailAlreadyRegistered or AuthResultStatus.PhoneAlreadyRegistered
                or AuthResultStatus.NameAlreadyRegistered => DuplicateConflict(result.Status),
            AuthResultStatus.WeakPassword => WeakPassword(result),
            AuthResultStatus.Success => StatusCode(201, result.Response),
            _ => Problem()
        };
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await auth.GetMeAsync(userId);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await auth.UpdateMeAsync(userId, req);
        if (result.Status == AuthResultStatus.Success)
            log.LogInformation("User {UserId} updated their account details", userId);
        return result.Status switch
        {
            AuthResultStatus.Success => Ok(result.User),
            AuthResultStatus.InvalidCredentials => NotFound(),
            _ => DuplicateConflict(result.Status),
        };
    }

    // The signed-in user's address and (patients) ID card / passport number
    [HttpPut("me/address")]
    [Authorize]
    public async Task<IActionResult> UpdateAddress([FromBody] UpdateAddressRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!Locations.IsCountry(req.Country)) return UnknownCountry();
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await auth.UpdateAddressAsync(userId, req);
        if (user is not null) log.LogInformation("User {UserId} updated their address (country {Country})", userId, user.Country);
        return user is null ? NotFound() : Ok(user);
    }

    // 422 (not 400, which the apps read as "wrong current password") with the broken rules by name
    private UnprocessableEntityObjectResult WeakPassword(AuthResult result) => UnprocessableEntity(new
    {
        error = "The password doesn't meet the requirements",
        code = "weak_password",
        rules = result.FailedRules!.Select(r => r.ToString()),
    });

    private BadRequestObjectResult UnknownCountry() => BadRequest(new { error = "Unknown country code" });

    // 409 with "code" (email | phone | name) so the apps can say exactly which value is taken
    private ConflictObjectResult DuplicateConflict(AuthResultStatus status) => status switch
    {
        AuthResultStatus.PhoneAlreadyRegistered => Conflict(new { error = "Phone number already registered", code = "phone" }),
        AuthResultStatus.NameAlreadyRegistered => Conflict(new { error = "Someone with this first and last name already exists", code = "name" }),
        _ => Conflict(new { error = "Email already registered", code = "email" }),
    };

    [HttpPost("change-password")]
    [EnableRateLimiting("auth")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await auth.ChangePasswordAsync(userId, req);
        if (result.Status == AuthResultStatus.Success)
            log.LogInformation("User {UserId} changed their password (other sessions signed out)", userId);
        else
            log.LogInformation("Password change for user {UserId} refused: {Reason}", userId, result.Status);
        return result.Status switch
        {
            AuthResultStatus.InvalidCredentials => BadRequest(new { error = "Current password is incorrect" }),
            AuthResultStatus.WeakPassword => WeakPassword(result),
            AuthResultStatus.Success => Ok(result.Response),
            _ => Problem()
        };
    }

    // The web apps call this just before signing someone out in the browser, so support can see why a
    // session ended (the server otherwise never hears about it). Only known reasons are recorded.
    [HttpPost("session-ended")]
    [Authorize]
    public IActionResult SessionEnded([FromBody] SessionEndedRequest req)
    {
        var reason = req.Reason switch
        {
            "inactive" => "logged out automatically after inactivity",
            "inactive-prompt-logout" => "chose to log out from the inactivity prompt",
            _ => null,
        };
        if (reason is null) return BadRequest();

        log.LogInformation("User {UserId} {SessionEndReason}", User.FindFirstValue(ClaimTypes.NameIdentifier), reason);
        return NoContent();
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await auth.LoginAsync(req);
        if (result.Status == AuthResultStatus.Success)
            log.LogInformation("User {UserId} ({Role}) logged in", result.Response!.User.Id, result.Response.User.Role);
        else
            log.LogInformation("Login refused from {ClientIp}: {Reason}", ClientAddress.Of(HttpContext), result.Status);
        return result.Status switch
        {
            AuthResultStatus.InvalidCredentials => Unauthorized(new { error = "Invalid credentials" }),
            AuthResultStatus.AccountInactive => Forbid(),
            AuthResultStatus.PendingApproval => StatusCode(403, new { error = "Pending approval" }),
            AuthResultStatus.Success => Ok(result.Response),
            _ => Problem()
        };
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats() => Ok(await auth.GetUserCountsAsync());

    [HttpGet("admin/users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUsers([FromQuery] UserRole? role) =>
        Ok(await auth.GetUsersAsync(role));

    // One account, for the Admin app's doctor page (email and phone aren't on the doctor profile)
    [HttpGet("admin/users/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminGetUser(Guid id)
    {
        var user = await auth.GetMeAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    // An admin corrects someone's name or phone. Same checks as a user editing their own
    // account (PUT me): a name or phone already used by someone else is refused with 409.
    [HttpPut("admin/users/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdateUser(Guid id, [FromBody] UpdateMeRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await auth.UpdateMeAsync(id, req);
        if (result.Status == AuthResultStatus.Success)
            log.LogInformation("Account {UserId} updated by admin {AdminId}", id, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return result.Status switch
        {
            AuthResultStatus.Success => Ok(result.User),
            AuthResultStatus.InvalidCredentials => NotFound(),
            _ => DuplicateConflict(result.Status),
        };
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await auth.RefreshAsync(req.RefreshToken);
        return result.Status switch
        {
            AuthResultStatus.InvalidRefreshToken => Unauthorized(),
            AuthResultStatus.Success => Ok(result.Response),
            _ => Problem()
        };
    }
}
