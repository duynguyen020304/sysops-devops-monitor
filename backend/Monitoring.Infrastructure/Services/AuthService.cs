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
            Role = UserRole.Owner,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        workspace.OwnerUserId = user.Id;

        _db.Workspaces.Add(workspace);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.Status != UserStatus.Active)
            throw new UnauthorizedAccessException("Account is not active.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        // In a full implementation, we would validate the refresh token from Redis/DB.
        // For now, decode the user info from the refresh token and issue a new pair.
        var principal = GetPrincipalFromExpiredToken(request.RefreshToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        var user = await _db.Users.FindAsync(Guid.Parse(userIdClaim))
            ?? throw new UnauthorizedAccessException("User not found.");

        return BuildAuthResponse(user);
    }

    public Task RevokeRefreshTokenAsync(string refreshToken)
    {
        // Placeholder: In production, remove from Redis store
        return Task.CompletedTask;
    }

    public string GenerateJwtToken(User user)
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
            new(ClaimTypes.Role, user.Role.ToString()),
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

    private AuthResponse BuildAuthResponse(User user)
    {
        var token = GenerateJwtToken(user);
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
                Role: user.Role.ToString(),
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
