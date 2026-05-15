namespace Monitoring.Core.DTOs;

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    DateTime CreatedAt
);

public record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    List<string> Permissions,
    DateTime CreatedAt
);

public record CreateRoleRequest(
    string Name,
    string? Description,
    List<string> PermissionNames
);

public record UpdateRoleRequest(
    string? Name,
    string? Description,
    List<string>? PermissionNames
);

public record PermissionDto(
    Guid Id,
    string Name,
    string? Category,
    string? Description
);

public record UserWithRolesDto(
    Guid Id,
    string Name,
    string Email,
    List<string> Roles,
    List<string> Permissions,
    string Status,
    DateTime CreatedAt
);

public record AssignRoleRequest(
    Guid RoleId
);
