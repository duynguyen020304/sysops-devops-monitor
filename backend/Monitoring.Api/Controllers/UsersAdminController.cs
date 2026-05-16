using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly MonitoringDbContext _db;
    private readonly IAuthService _authService;

    public UsersController(MonitoringDbContext db, IAuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    [HttpGet]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<List<UserWithRolesDto>>> GetUsers()
    {
        var workspaceId = GetWorkspaceId();

        var users = await _db.Users
            .Where(u => u.WorkspaceId == workspaceId)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Status,
                u.CreatedAt
            })
            .ToListAsync();

        var result = new List<UserWithRolesDto>();
        foreach (var u in users)
        {
            var roles = await _db.UserRoles
                .Where(ur => ur.UserId == u.Id)
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
                .ToListAsync();

            var permissions = await _db.UserRoles
                .Where(ur => ur.UserId == u.Id)
                .Join(_db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
                .Join(_db.Permissions, permId => permId, p => p.Id, (_, p) => p.Name)
                .Distinct()
                .ToListAsync();

            result.Add(new UserWithRolesDto(u.Id, u.Name, u.Email, roles, permissions, u.Status.ToString(), u.CreatedAt));
        }

        return Ok(result);
    }

    [HttpPost]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<UserWithRolesDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var workspaceId = GetWorkspaceId();
            var grantedByUserId = GetUserId();
            var result = await _authService.CreateUserInWorkspaceAsync(workspaceId, request, grantedByUserId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<UserWithRolesDto>> GetUser(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
            return NotFound();

        var roles = await _db.UserRoles
            .Where(ur => ur.UserId == id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .ToListAsync();

        var permissions = await _db.UserRoles
            .Where(ur => ur.UserId == id)
            .Join(_db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
            .Join(_db.Permissions, permId => permId, p => p.Id, (_, p) => p.Name)
            .Distinct()
            .ToListAsync();

        return Ok(new UserWithRolesDto(user.Id, user.Name, user.Email, roles, permissions, user.Status.ToString(), user.CreatedAt));
    }

    [HttpPost("{id:guid}/roles")]
    [RequirePermission("manage_users")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
            return NotFound();

        var role = await _db.Roles.FindAsync(request.RoleId);
        if (role is null)
            return BadRequest(new { message = "Role not found." });

        if (await _db.UserRoles.AnyAsync(ur => ur.UserId == id && ur.RoleId == request.RoleId))
            return Ok(new { message = "User already has this role." });

        var userId = GetUserId();

        _db.UserRoles.Add(new UserRoleEntity
        {
            Id = Guid.NewGuid(),
            UserId = id,
            RoleId = request.RoleId,
            GrantedByUserId = userId,
            GrantedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Role assigned successfully." });
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission("manage_users")]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId)
    {
        var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == id && ur.RoleId == roleId);
        if (userRole is null)
            return NotFound();

        _db.UserRoles.Remove(userRole);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Role removed successfully." });
    }

    [HttpGet("{id:guid}/permissions")]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<List<string>>> GetUserPermissions(Guid id)
    {
        var permissions = await _db.UserRoles
            .Where(ur => ur.UserId == id)
            .Join(_db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
            .Join(_db.Permissions, permId => permId, p => p.Id, (_, p) => p.Name)
            .Distinct()
            .ToListAsync();

        return Ok(permissions);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("manage_users")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (user.WorkspaceId != workspaceId)
            return Forbid();

        // Prevent self-deletion
        var currentUserId = GetUserId();
        if (user.Id == currentUserId)
            return BadRequest(new { message = "Cannot delete your own account." });

        // Remove user roles
        var userRoles = await _db.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
        _db.UserRoles.RemoveRange(userRoles);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID claim not found.");
        return Guid.Parse(claim);
    }
}
