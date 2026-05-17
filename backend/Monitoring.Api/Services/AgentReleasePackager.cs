namespace Monitoring.Api.Services;

public static class AgentReleasePackager
{
    public static async Task CreateTarAsync(string distPath, string packageJsonPath, string lockPath, string artifactPath)
    {
        var stagingDir = Path.Combine(Path.GetTempPath(), $"agent-release-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);
        CopyDirectory(distPath, Path.Combine(stagingDir, "dist"));
        File.Copy(packageJsonPath, Path.Combine(stagingDir, "package.json"), true);
        if (File.Exists(lockPath)) File.Copy(lockPath, Path.Combine(stagingDir, "pnpm-lock.yaml"), true);
        var psi = new System.Diagnostics.ProcessStartInfo { FileName = "tar", Arguments = $"-czf \"{artifactPath}\" -C \"{stagingDir}\" .", UseShellExecute = false, RedirectStandardError = true };
        var proc = System.Diagnostics.Process.Start(psi)!;
        await proc.WaitForExitAsync();
        var err = await proc.StandardError.ReadToEndAsync();
        Directory.Delete(stagingDir, true);
        if (proc.ExitCode != 0) throw new InvalidOperationException("Failed to package agent artifact: " + err);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir)) File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir)) CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }
}
