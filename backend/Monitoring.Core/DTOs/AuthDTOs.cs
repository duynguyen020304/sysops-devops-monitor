namespace Monitoring.Core.DTOs;

public record RegisterRequest(
    string Name,
    string Email,
    string Password
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponse(
    string Token,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid WorkspaceId
);

public record RefreshTokenRequest(
    string RefreshToken
);
