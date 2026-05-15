using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Monitoring.Core.Entities;
using Monitoring.Core.Enums;
using Monitoring.Core.Interfaces;
using Monitoring.Infrastructure.BackgroundServices;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Services;
using Monitoring.Api.Middleware;

// Load .env file (searches up from current directory)
DotNetEnv.Env.Load(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"));

var builder = WebApplication.CreateBuilder(args);

// Build connection string from individual DB_ env vars if CONNECTION_STRING not set
var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
    var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
    var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "monitoring";
    var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
    var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
    connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass}";
}
// Override the config value so GetConnectionString picks it up
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

// EF Core PostgreSQL
builder.Services.AddDbContext<MonitoringDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// DI registration
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IServerService, ServerService>();
builder.Services.AddScoped<IDeployService, DeployService>();
builder.Services.AddScoped<IPM2Service, PM2Service>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddSingleton<ILogMaskingService, LogMaskingService>();
builder.Services.AddScoped<IAlertService, AlertService>();

// HttpClient for GitHub API
builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri("https://api.github.com");
    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("MonitoringPlatform", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
});

// Background services
builder.Services.AddHostedService<GitHubSyncService>();
builder.Services.AddHostedService<MetricAggregationService>();
builder.Services.AddHostedService<LogRetentionService>();
builder.Services.AddHostedService<AlertEvaluationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
    await db.Database.MigrateAsync();

    // Seed RBAC data (permissions, roles, super admin)
    await RbacSeeder.SeedAsync(db, builder.Configuration);

    // Seed default alert rules if none exist
    if (!await db.AlertRules.AnyAsync())
    {
        var defaultWorkspace = await db.Workspaces.FirstOrDefaultAsync();
        var workspaceId = defaultWorkspace?.Id ?? Guid.Empty;

        db.AlertRules.AddRange(
            // GitHub Actions
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Workflow Failed", SourceType = AlertSourceType.GitHubActions, ConditionType = "workflow_failure", Threshold = 1, TimeWindowSeconds = 300, Severity = AlertSeverity.Warning, IsEnabled = true, CooldownSeconds = 300 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Deployment Failed", SourceType = AlertSourceType.GitHubActions, ConditionType = "deployment_failure", Threshold = 1, TimeWindowSeconds = 300, Severity = AlertSeverity.Critical, IsEnabled = true, CooldownSeconds = 300 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Duration Too Long", SourceType = AlertSourceType.GitHubActions, ConditionType = "workflow_duration", Threshold = 2, TimeWindowSeconds = 3600, Severity = AlertSeverity.Warning, IsEnabled = true, CooldownSeconds = 600 },
            // PM2
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Process Stopped", SourceType = AlertSourceType.PM2, ConditionType = "process_stopped", Threshold = 1, TimeWindowSeconds = 60, Severity = AlertSeverity.Critical, IsEnabled = true, CooldownSeconds = 60 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Process Errored", SourceType = AlertSourceType.PM2, ConditionType = "process_errored", Threshold = 1, TimeWindowSeconds = 60, Severity = AlertSeverity.Critical, IsEnabled = true, CooldownSeconds = 60 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "High Restart Count", SourceType = AlertSourceType.PM2, ConditionType = "restart_count", Threshold = 3, TimeWindowSeconds = 600, Severity = AlertSeverity.Warning, IsEnabled = true, CooldownSeconds = 600 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "High Memory (PM2)", SourceType = AlertSourceType.PM2, ConditionType = "memory_usage", Threshold = 512, TimeWindowSeconds = 300, Severity = AlertSeverity.Warning, IsEnabled = true, CooldownSeconds = 300 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "High CPU (PM2)", SourceType = AlertSourceType.PM2, ConditionType = "cpu_usage", Threshold = 80, TimeWindowSeconds = 300, Severity = AlertSeverity.Warning, IsEnabled = true, CooldownSeconds = 300 },
            // Server
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "High CPU", SourceType = AlertSourceType.Server, ConditionType = "cpu_usage", Threshold = 85, TimeWindowSeconds = 600, Severity = AlertSeverity.Warning, IsEnabled = true, CooldownSeconds = 600 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Critical CPU", SourceType = AlertSourceType.Server, ConditionType = "cpu_usage", Threshold = 95, TimeWindowSeconds = 300, Severity = AlertSeverity.Critical, IsEnabled = true, CooldownSeconds = 300 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "High RAM", SourceType = AlertSourceType.Server, ConditionType = "memory_usage", Threshold = 85, TimeWindowSeconds = 600, Severity = AlertSeverity.Warning, IsEnabled = true, CooldownSeconds = 600 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Critical RAM", SourceType = AlertSourceType.Server, ConditionType = "memory_usage", Threshold = 95, TimeWindowSeconds = 300, Severity = AlertSeverity.Critical, IsEnabled = true, CooldownSeconds = 300 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "High Disk", SourceType = AlertSourceType.Server, ConditionType = "disk_usage", Threshold = 80, TimeWindowSeconds = 0, Severity = AlertSeverity.Warning, IsEnabled = true, CooldownSeconds = 3600 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Critical Disk", SourceType = AlertSourceType.Server, ConditionType = "disk_usage", Threshold = 90, TimeWindowSeconds = 0, Severity = AlertSeverity.Critical, IsEnabled = true, CooldownSeconds = 3600 },
            new AlertRule { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Agent Offline", SourceType = AlertSourceType.Server, ConditionType = "heartbeat_miss", Threshold = 120, TimeWindowSeconds = 120, Severity = AlertSeverity.Critical, IsEnabled = true, CooldownSeconds = 300 }
        );
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseMiddleware<AuditLogMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
