using System.Security.Claims;
using LandaDoc.Identity.Services;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Identity.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterPatientRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await auth.RegisterPatientAsync(req);
        return result.Status switch
        {
            AuthResultStatus.EmailAlreadyRegistered or AuthResultStatus.PhoneAlreadyRegistered
                or AuthResultStatus.NameAlreadyRegistered => DuplicateConflict(result.Status),
            AuthResultStatus.Success => StatusCode(201, result.Response),
            _ => Problem()
        };
    }

    [HttpPost("register-doctor")]
    public async Task<IActionResult> RegisterDoctor([FromBody] RegisterDoctorRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await auth.RegisterDoctorAsync(req);
        return result.Status switch
        {
            AuthResultStatus.EmailAlreadyRegistered or AuthResultStatus.PhoneAlreadyRegistered
                or AuthResultStatus.NameAlreadyRegistered => DuplicateConflict(result.Status),
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
        return result.Status switch
        {
            AuthResultStatus.Success => Ok(result.User),
            AuthResultStatus.InvalidCredentials => NotFound(),
            _ => DuplicateConflict(result.Status),
        };
    }

    // 409 with "code" (email | phone | name) so the apps can say exactly which value is taken
    private ConflictObjectResult DuplicateConflict(AuthResultStatus status) => status switch
    {
        AuthResultStatus.PhoneAlreadyRegistered => Conflict(new { error = "Phone number already registered", code = "phone" }),
        AuthResultStatus.NameAlreadyRegistered => Conflict(new { error = "Someone with this first and last name already exists", code = "name" }),
        _ => Conflict(new { error = "Email already registered", code = "email" }),
    };

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await auth.ChangePasswordAsync(userId, req);
        return result.Status switch
        {
            AuthResultStatus.InvalidCredentials => BadRequest(new { error = "Current password is incorrect" }),
            AuthResultStatus.Success => Ok(result.Response),
            _ => Problem()
        };
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await auth.LoginAsync(req);
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
        return result.Status switch
        {
            AuthResultStatus.Success => Ok(result.User),
            AuthResultStatus.InvalidCredentials => NotFound(),
            _ => DuplicateConflict(result.Status),
        };
    }

    [HttpPost("refresh")]
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
