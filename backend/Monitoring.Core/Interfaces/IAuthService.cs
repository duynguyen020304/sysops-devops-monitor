using Monitoring.Core.DTOs;

namespace Monitoring.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task RevokeRefreshTokenAsync(string refreshToken);
    Task<UserWithRolesDto> CreateUserInWorkspaceAsync(Guid workspaceId, CreateUserRequest request, Guid grantedByUserId);
    string GenerateJwtToken(Core.Entities.User user, string roles, string permissions);
}
