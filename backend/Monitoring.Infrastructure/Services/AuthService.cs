using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly MonitoringDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(MonitoringDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser is not null)
            throw new InvalidOperationException("A user with this email already exists.");

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = $"{request.Name}'s Workspace",
            OwnerUserId = Guid.NewGuid(), // will be updated after user creation
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        workspace.OwnerUserId = user.Id;

        // Assign Owner role to the registering user
        var ownerRole = await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "OWNER");
        if (ownerRole is not null)
        {
            _db.UserRoles.Add(new UserRoleEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = ownerRole.Id,
                GrantedAt = DateTime.UtcNow
            });
        }

        _db.Workspaces.Add(workspace);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("Account is not active.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = GetPrincipalFromExpiredToken(request.RefreshToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        var user = await _db.Users.FindAsync(Guid.Parse(userIdClaim))
            ?? throw new UnauthorizedAccessException("User not found.");

        return await BuildAuthResponseAsync(user);
    }

    public Task RevokeRefreshTokenAsync(string refreshToken)
    {
        return Task.CompletedTask;
    }

    public string GenerateJwtToken(User user, string roles, string permissions)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, roles),
            new("permissions", permissions),
            new("WorkspaceId", user.WorkspaceId.ToString())
        };

        var expiresMinutes = int.Parse(jwtSettings["ExpiresInMinutes"] ?? "60");

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user)
    {
        // Load user's roles and permissions from DB
        var userRoles = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .ToListAsync();

        var userPermissions = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(_db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionId)
            .Join(_db.Permissions, permId => permId, p => p.Id, (_, p) => p.Name)
            .Distinct()
            .ToListAsync();

        var rolesStr = string.Join(",", userRoles);
        var permsStr = string.Join(",", userPermissions);

        var token = GenerateJwtToken(user, rolesStr, permsStr);
        var refreshToken = GenerateRefreshToken();
        var expiresMinutes = int.Parse(_config.GetSection("JwtSettings")["ExpiresInMinutes"] ?? "60");

        return new AuthResponse(
            Token: token,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(expiresMinutes),
            User: new UserDto(
                Id: user.Id,
                Name: user.Name,
                Email: user.Email,
                Roles: userRoles,
                Permissions: userPermissions,
                WorkspaceId: user.WorkspaceId
            )
        );
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false, // allow expired tokens
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, validationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        return principal;
    }
}
