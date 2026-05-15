using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly MonitoringDbContext _db;

    public RolesController(MonitoringDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var roles = await _db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.IsSystem, r.CreatedAt))
            .ToListAsync();

        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<RoleDetailDto>> GetRole(Guid id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role is null)
            return NotFound();

        var permissions = await _db.RolePermissions
            .Where(rp => rp.RoleId == id)
            .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Name)
            .ToListAsync();

        return Ok(new RoleDetailDto(role.Id, role.Name, role.Description, role.IsSystem, permissions, role.CreatedAt));
    }

    [HttpGet("permissions")]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<List<PermissionDto>>> GetAllPermissions()
    {
        var permissions = await _db.Permissions
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .Select(p => new PermissionDto(p.Id, p.Name, p.Category, p.Description))
            .ToListAsync();

        return Ok(permissions);
    }

    [HttpPost]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<RoleDetailDto>> CreateRole([FromBody] CreateRoleRequest request)
    {
        var normalizedName = request.Name.Trim().ToUpperInvariant();

        if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalizedName))
            return Conflict(new { message = $"Role '{request.Name}' already exists." });

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            Description = request.Description,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var permissions = await _db.Permissions
            .Where(p => request.PermissionNames.Select(n => n.ToUpperInvariant()).Contains(p.NormalizedName))
            .ToListAsync();

        foreach (var perm in permissions)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                PermissionId = perm.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetRole), new { id = role.Id },
            new RoleDetailDto(role.Id, role.Name, role.Description, role.IsSystem,
                permissions.Select(p => p.Name).ToList(), role.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("manage_users")]
    public async Task<ActionResult<RoleDetailDto>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role is null)
            return NotFound();

        if (role.IsSystem)
            return BadRequest(new { message = "Cannot modify system roles." });

        if (request.Name is not null)
        {
            var normalizedName = request.Name.Trim().ToUpperInvariant();
            if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalizedName && r.Id != id))
                return Conflict(new { message = $"Role '{request.Name}' already exists." });

            role.Name = request.Name.Trim();
            role.NormalizedName = normalizedName;
        }

        if (request.Description is not null)
            role.Description = request.Description;

        role.UpdatedAt = DateTime.UtcNow;

        if (request.PermissionNames is not null)
        {
            // Remove existing permissions
            var existingRPs = await _db.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync();
            _db.RolePermissions.RemoveRange(existingRPs);

            // Add new permissions
            var permissions = await _db.Permissions
                .Where(p => request.PermissionNames.Select(n => n.ToUpperInvariant()).Contains(p.NormalizedName))
                .ToListAsync();

            foreach (var perm in permissions)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = id,
                    PermissionId = perm.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();

        var updatedPerms = await _db.RolePermissions
            .Where(rp => rp.RoleId == id)
            .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Name)
            .ToListAsync();

        return Ok(new RoleDetailDto(role.Id, role.Name, role.Description, role.IsSystem, updatedPerms, role.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("manage_users")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role is null)
            return NotFound();

        if (role.IsSystem)
            return BadRequest(new { message = "Cannot delete system roles." });

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
