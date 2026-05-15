using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.Data;

namespace Monitoring.Infrastructure.Services;

public class DeployService : IDeployService
{
    private readonly MonitoringDbContext _db;
    private readonly IConfiguration _config;

    public DeployService(MonitoringDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<DeployResult> DeployAgentAsync(Guid serverId, CancellationToken ct = default)
    {
        var server = await _db.Servers.FirstOrDefaultAsync(s => s.Id == serverId, ct);
        if (server is null)
            return new DeployResult(false, string.Empty, "Server not found.");

        var user = string.IsNullOrWhiteSpace(server.SshUsername) ? "root" : server.SshUsername;
        var port = server.SshPort > 0 ? server.SshPort : 22;
        var key = server.SshPrivateKeyPath;
        var agentPath = _config["DeploySettings:AgentBuildPath"] ?? "agent";
        var remotePath = _config["DeploySettings:AgentRemotePath"] ?? "/opt/monitoring-agent";
        var apiUrl = _config["DeploySettings:AgentApiUrl"] ?? "http://localhost:5000";
        var token = _config["DeploySettings:AgentToken"] ?? _config["AgentSettings:Token"] ?? string.Empty;
        var sshOptions = BuildSshOptions(port, key);
        var target = $"{user}@{server.IpAddress}";
        var output = new StringBuilder();

        var steps = new[]
        {
            ("pnpm", $"--dir {Quote(agentPath)} build"),
            ("ssh", $"{sshOptions} {target} {Quote($"mkdir -p {remotePath}")}"),
            ("rsync", $"-az -e {Quote($"ssh {sshOptions}")} {Quote(Path.Combine(agentPath, "dist") + Path.DirectorySeparatorChar)} {Quote(Path.Combine(agentPath, "package.json"))} {Quote(Path.Combine(agentPath, "pnpm-lock.yaml"))} {target}:{Quote(remotePath + "/")}"),
            ("ssh", $"{sshOptions} {target} {Quote($"cd {remotePath} && pnpm install --prod")}"),
            ("ssh", $"{sshOptions} {target} {Quote($"cat > {remotePath}/.env <<'ENV'\nAGENT_API_URL={apiUrl}\nAGENT_SERVER_ID={server.Id}\nAGENT_SERVER_TOKEN={token}\nENV")}"),
            ("ssh", $"{sshOptions} {target} {Quote($"cd {remotePath} && (pm2 delete monitoring-agent 2>/dev/null || true) && pm2 start dist/index.js --name monitoring-agent")}")
        };

        foreach (var (fileName, args) in steps)
        {
            var result = await RunProcessAsync(fileName, args, ct);
            output.AppendLine($"$ {fileName} {args}");
            output.Append(result.Stdout);
            output.Append(result.Stderr);
            if (result.ExitCode != 0)
                return new DeployResult(false, output.ToString(), $"Command failed: {fileName} (exit {result.ExitCode})");
        }

        return new DeployResult(true, output.ToString(), null);
    }

    private static string BuildSshOptions(int port, string? key)
    {
        var options = $"-p {port} -o StrictHostKeyChecking=accept-new";
        if (!string.IsNullOrWhiteSpace(key))
            options += $" -i {Quote(key)}";
        return options;
    }

    private static async Task<(string Stdout, string Stderr, int ExitCode)> RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (await stdoutTask, await stderrTask, process.ExitCode);
    }

    private static string Quote(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}";
}
