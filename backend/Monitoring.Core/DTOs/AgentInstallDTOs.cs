namespace Monitoring.Core.DTOs;

public record GenerateInstallTokenRequest(string ServerName, string Password);

public record InstallTokenResponse(
    Guid Id,
    string Token,
    string ServerName,
    string DownloadUrl,
    string PageUrl,
    DateTime ExpiresAt,
    string Status
);

public record InstallTokenListResponse(
    Guid Id,
    string ServerName,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? UsedAt,
    DateTime? RevokedAt,
    string Status
);

public record VerifyPasswordRequest(string Token, string Password);

public record VerifyPasswordResponse(string DownloadUrl);

public record AgentRegisterRequest(string Hostname, string Os, string Arch);

public record AgentRegisterResponse(Guid ServerId, string ServerToken, string ApiUrl);
