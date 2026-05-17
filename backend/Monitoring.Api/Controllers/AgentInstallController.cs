using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/agent-install")]
public class AgentInstallController : ControllerBase
{
    private readonly MonitoringDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly Monitoring.Core.Interfaces.IAgentCleanupService _cleanupService;

    public AgentInstallController(MonitoringDbContext db, IConfiguration config, IWebHostEnvironment env, Monitoring.Core.Interfaces.IAgentCleanupService cleanupService)
    { _db = db; _config = config; _env = env; _cleanupService = cleanupService; }

    [HttpGet("tokens"), Authorize, RequirePermission("deploy_agents")]
    public async Task<IActionResult> ListTokens()
    {
        var user = await _db.Users.FindAsync(GetUserId());
        if (user is null) return Unauthorized();
        var tokens = await _db.AgentInstallTokens.Where(t => t.WorkspaceId == user.WorkspaceId && t.ArchivedAt == null).OrderByDescending(t => t.CreatedAt)
            .Select(t => new InstallTokenListResponse(t.Id, t.ServerName, t.CreatedAt, t.ExpiresAt, t.UsedAt, t.RevokedAt, GetTokenStatus(t))).ToListAsync();
        return Ok(tokens);
    }

    [HttpPost("tokens"), Authorize, RequirePermission("deploy_agents")]
    public async Task<IActionResult> GenerateToken([FromBody] GenerateInstallTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServerName)) return BadRequest(new { message = "Server name is required." });
        var user = await _db.Users.FindAsync(GetUserId());
        if (user is null) return Unauthorized();
        var now = DateTime.UtcNow;
        var token = GenerateSecureToken();
        var password = GeneratePlainPassword();
        var installToken = new AgentInstallToken { Id = Guid.NewGuid(), Token = token, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), ServerName = request.ServerName, CreatedBy = user.Id, CreatedAt = now, ExpiresAt = now.AddHours(1), WorkspaceId = user.WorkspaceId };
        _db.AgentInstallTokens.Add(installToken); await _db.SaveChangesAsync();
        var baseUrl = GetBaseUrl();
        return Ok(new InstallTokenResponse(installToken.Id, token, request.ServerName, $"{baseUrl}/api/agent-install/download?t={token}&pw={Uri.EscapeDataString(password)}", $"{baseUrl}/api/agent-install/page?t={token}", password, installToken.ExpiresAt, "active"));
    }

    [HttpPost("tokens/{id:guid}/revoke"), Authorize, RequirePermission("deploy_agents")]
    public async Task<IActionResult> RevokeToken(Guid id)
    {
        var user = await _db.Users.FindAsync(GetUserId()); if (user is null) return Unauthorized();
        var token = await _db.AgentInstallTokens.FirstOrDefaultAsync(t => t.Id == id && t.WorkspaceId == user.WorkspaceId);
        if (token is null) return NotFound(new { message = "Token not found." });
        if (token.RevokedAt is not null) return BadRequest(new { message = "Token already revoked." });
        if (token.UsedAt is not null) return BadRequest(new { message = "Token already used." });
        token.RevokedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); return Ok(new { message = "Token revoked." });
    }

    [HttpPost("tokens/cleanup"), Authorize, RequirePermission("deploy_agents")]
    public async Task<IActionResult> CleanupTokens()
    {
        var user = await _db.Users.FindAsync(GetUserId()); if (user is null) return Unauthorized();
        var archived = await _cleanupService.ArchiveStaleTokensAsync(user.WorkspaceId, null, "manual cleanup");
        return Ok(new CleanupResponse(archived));
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string? t, [FromQuery] string? pw)
    {
        if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(pw)) return BadRequest(new { message = "Token and password are required." });
        var installToken = await ValidateInstallTokenAsync(t); if (installToken is null) return NotFound(new { message = "Invalid or expired install token." });
        if (!BCrypt.Net.BCrypt.Verify(pw, installToken.PasswordHash)) return Unauthorized(new { message = "Invalid password." });
        return await ServeInstallScript(installToken.Token);
    }

    [HttpGet("page")]
    public async Task<IActionResult> DownloadPage([FromQuery] string? t)
    {
        if (string.IsNullOrEmpty(t)) return BadRequest(new { message = "Token is required." });
        var installToken = await _db.AgentInstallTokens.FirstOrDefaultAsync(x => x.Token == t);
        if (installToken is null) return NotFound("Invalid install token.");
        if (installToken.RevokedAt is not null) return NotFound("This install link has been revoked.");
        if (installToken.ExpiresAt < DateTime.UtcNow) return NotFound("This install link has expired.");
        if (installToken.UsedAt is not null) return NotFound("This install link has already been used.");
        var name = System.Web.HttpUtility.HtmlEncode(installToken.ServerName);
        var html = $@"<!DOCTYPE html><html lang=""en""><head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""><title>Download Monitoring Agent</title></head><body><h1>🔐 Monitoring Agent</h1><p>Server: {name}</p><form id=""form""><input type=""password"" id=""pw"" placeholder=""Password"" autofocus required><div id=""error"" style=""display:none;color:red""></div><button id=""btn"" type=""submit"">Download Installer</button></form><p>Link expires {installToken.ExpiresAt:yyyy-MM-dd HH:mm} UTC</p><script>document.getElementById('form').addEventListener('submit', async (e) => {{ e.preventDefault(); const btn = document.getElementById('btn'); const errEl = document.getElementById('error'); const pw = document.getElementById('pw').value; btn.disabled = true; btn.textContent = 'Verifying...'; errEl.style.display = 'none'; try {{ const res = await fetch('/api/agent-install/page/verify', {{ method: 'POST', headers: {{ 'Content-Type': 'application/json' }}, body: JSON.stringify({{ token: '{t}', password: pw }}) }}); if (!res.ok) {{ const data = await res.json(); throw new Error(data.message || 'Verification failed'); }} const data = await res.json(); window.location.href = data.downloadUrl; }} catch(err) {{ errEl.textContent = err.message; errEl.style.display = 'block'; btn.disabled = false; btn.textContent = 'Download Installer'; }} }});</script></body></html>";
        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpPost("page/verify")]
    public async Task<IActionResult> VerifyPassword([FromBody] VerifyPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.Password)) return BadRequest(new { message = "Token and password are required." });
        var installToken = await ValidateInstallTokenAsync(request.Token); if (installToken is null) return NotFound(new { message = "Invalid or expired install token." });
        if (!BCrypt.Net.BCrypt.Verify(request.Password, installToken.PasswordHash)) return Unauthorized(new { message = "Invalid password." });
        return Ok(new VerifyPasswordResponse($"{GetBaseUrl()}/api/agent-install/download?t={request.Token}&pw={Uri.EscapeDataString(request.Password)}"));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AgentRegisterRequest request, [FromHeader(Name = "X-Install-Token")] string? installTokenHeader)
    {
        if (string.IsNullOrEmpty(installTokenHeader)) return Unauthorized(new { message = "X-Install-Token header is required." });
        var installToken = await _db.AgentInstallTokens.FirstOrDefaultAsync(t => t.Token == installTokenHeader);
        if (installToken is null) return Unauthorized(new { message = "Invalid install token." });
        if (installToken.RevokedAt is not null) return Unauthorized(new { message = "Install token has been revoked." });
        if (installToken.ExpiresAt < DateTime.UtcNow) return Unauthorized(new { message = "Install token has expired." });
        if (installToken.UsedAt is not null) return BadRequest(new { message = "Install token has already been used." });
        var now = DateTime.UtcNow;
        installToken.UsedAt = now; var serverToken = GenerateSecureToken();
        var hostname = request.Hostname ?? installToken.ServerName;
        var machineId = string.IsNullOrWhiteSpace(request.MachineId) ? null : request.MachineId;
        var server = new Server { Id = Guid.NewGuid(), WorkspaceId = installToken.WorkspaceId, Hostname = hostname, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", OperatingSystem = $"{request.Os ?? "unknown"} {request.Arch ?? ""}".Trim(), AgentVersion = "1.0.0", ServerToken = serverToken, MachineId = machineId, Status = ServerStatus.Unknown, CreatedAt = now, UpdatedAt = now };
        installToken.ServerId = server.Id; _db.Servers.Add(server);
        _db.AgentInstallTokens.Update(installToken);
        await _db.SaveChangesAsync();
        await _cleanupService.ArchiveStaleTokensAsync(installToken.WorkspaceId, installToken.ServerName, "registered newer agent", installToken.Id);
        await _cleanupService.ArchiveStaleServerDuplicatesAsync(installToken.WorkspaceId, server.Id, hostname, machineId, "registered newer agent");
        await _db.SaveChangesAsync();
        var baseUrl = GetBaseUrl(); var apiUrl = baseUrl.Contains(":") ? baseUrl : $"{baseUrl.Replace("https://", "http://")}:5000";
        return Ok(new AgentRegisterResponse(server.Id, serverToken, apiUrl));
    }

    [HttpGet("agent-files")]
    public async Task<IActionResult> GetAgentFiles([FromQuery] string? t)
    {
        if (string.IsNullOrEmpty(t)) return BadRequest(new { message = "Token is required." });
        var installToken = await ValidateInstallTokenAsync(t);
        if (installToken is null) return NotFound(new { message = "Invalid or expired install token." });

        var agentDistPath = Path.Combine(_env.ContentRootPath, "..", "agent", "dist");
        var agentPackageJson = Path.Combine(_env.ContentRootPath, "..", "agent", "package.json");
        var agentLockFile = Path.Combine(_env.ContentRootPath, "..", "agent", "pnpm-lock.yaml");
        if (!Directory.Exists(agentDistPath)) return NotFound(new { message = "Agent files not found on server. Build the agent first." });

        var tempFile = Path.GetTempFileName();
        try
        {
            var stagingDir = Path.Combine(Path.GetTempPath(), $"agent-files-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDir);
            CopyDirectory(agentDistPath, Path.Combine(stagingDir, "dist"));
            if (System.IO.File.Exists(agentPackageJson)) System.IO.File.Copy(agentPackageJson, Path.Combine(stagingDir, "package.json"), true);
            if (System.IO.File.Exists(agentLockFile)) System.IO.File.Copy(agentLockFile, Path.Combine(stagingDir, "pnpm-lock.yaml"), true);
            var tarPath = tempFile + ".tar.gz";
            var psi = new System.Diagnostics.ProcessStartInfo { FileName = "tar", Arguments = $"-czf \"{tarPath}\" -C \"{stagingDir}\" .", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            var proc = System.Diagnostics.Process.Start(psi);
            await proc!.WaitForExitAsync();
            Directory.Delete(stagingDir, true);
            var bytes = await System.IO.File.ReadAllBytesAsync(tarPath);
            System.IO.File.Delete(tarPath);
            return File(bytes, "application/gzip", "monitoring-agent.tar.gz");
        }
        finally { if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile); }
    }

    private async Task<AgentInstallToken?> ValidateInstallTokenAsync(string token) { var installToken = await _db.AgentInstallTokens.FirstOrDefaultAsync(x => x.Token == token); return installToken is null || installToken.RevokedAt is not null || installToken.ExpiresAt < DateTime.UtcNow ? null : installToken; }
    private async Task<IActionResult> ServeInstallScript(string installToken) { var scriptPath = Path.Combine(_env.ContentRootPath, "..", "agent", "install.sh"); if (!System.IO.File.Exists(scriptPath)) return NotFound(new { message = "install.sh template not found on server." }); var script = await System.IO.File.ReadAllTextAsync(scriptPath); var baseUrl = GetBaseUrl(); script = script.Replace("BACKEND_URL=\"\"", $"BACKEND_URL=\"{baseUrl}\""); script = script.Replace("INSTALL_TOKEN=\"\"", $"INSTALL_TOKEN=\"{installToken}\""); return File(Encoding.UTF8.GetBytes(script), "text/x-shellscript", "install-agent.sh"); }
    private Guid GetUserId() { var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier); return claim is not null ? Guid.Parse(claim.Value) : Guid.Empty; }
    private string GetBaseUrl()
    {
        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var scheme = !string.IsNullOrWhiteSpace(forwardedProto) ? forwardedProto : Request.Scheme;
        return $"{scheme}://{Request.Host.Value}";
    }
    private static string GenerateSecureToken() { var bytes = new byte[24]; RandomNumberGenerator.Fill(bytes); return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", ""); }
    private static string GeneratePlainPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(16);
        foreach (var b in bytes) sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            System.IO.File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }
    private static string GetTokenStatus(AgentInstallToken t) { if (t.RevokedAt is not null) return "revoked"; if (t.UsedAt is not null) return "used"; if (t.ExpiresAt < DateTime.UtcNow) return "expired"; return "active"; }
}
