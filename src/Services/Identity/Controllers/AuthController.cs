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
            AuthResultStatus.EmailAlreadyRegistered => Conflict(new { error = "Email already registered" }),
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
            AuthResultStatus.EmailAlreadyRegistered => Conflict(new { error = "Email already registered" }),
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
