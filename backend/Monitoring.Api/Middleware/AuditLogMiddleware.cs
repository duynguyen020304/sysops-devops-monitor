using System.Diagnostics;
using System.Security.Claims;

namespace Monitoring.Api.Middleware;

public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;

    private static readonly HashSet<string> AuditedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "DELETE", "PATCH"
    };

    private static readonly HashSet<string> SensitivePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/register",
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/repositories",
        "/api/servers/register",
        "/api/alerts/rules",
        "/api/alerts"
    };

    public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userRole = context.User.FindFirstValue(ClaimTypes.Role);
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        await _next(context);

        sw.Stop();

        _logger.LogInformation(
            "Audit: {Method} {Path} -> {StatusCode} by User:{UserId} Role:{Role} in {Elapsed}ms",
            method, path, context.Response.StatusCode, userId, userRole, sw.ElapsedMilliseconds);
    }

    private static bool ShouldAudit(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        if (string.IsNullOrEmpty(path)) return false;

        // Audit all mutations
        if (AuditedMethods.Contains(method)) return true;

        // Audit GET to sensitive paths
        if (SensitivePaths.Any(s => path.StartsWith(s, StringComparison.OrdinalIgnoreCase))) return true;

        return false;
    }
}
