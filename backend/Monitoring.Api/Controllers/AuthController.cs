using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Core.DTOs;
using Monitoring.Core.Interfaces;
using System.Security.Claims;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Headers["X-Refresh-Token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(refreshToken);
        }
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<UserDto> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        var rolesStr = User.FindFirst(ClaimTypes.Role)?.Value;
        var permissionsStr = User.FindFirst("permissions")?.Value;
        var workspaceId = User.FindFirst("WorkspaceId")?.Value;

        if (userId is null || email is null || name is null || rolesStr is null || workspaceId is null)
            return Unauthorized();

        var roles = rolesStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        var permissions = (permissionsStr ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        return Ok(new UserDto(
            Id: Guid.Parse(userId),
            Name: name,
            Email: email,
            Roles: roles,
            Permissions: permissions,
            WorkspaceId: Guid.Parse(workspaceId)
        ));
    }
}
