using Microsoft.EntityFrameworkCore;
using Monitoring.Core.Entities;

namespace Monitoring.Infrastructure.Data;

public class MonitoringDbContext : DbContext
{
    public MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : base(options) { }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<WorkflowLog> WorkflowLogs => Set<WorkflowLog>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<PM2Process> PM2Processes => Set<PM2Process>();
    public DbSet<PM2Log> PM2Logs => Set<PM2Log>();
    public DbSet<ServerMetric> ServerMetrics => Set<ServerMetric>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Workspace
        modelBuilder.Entity<Workspace>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.WorkspaceId);
        });

        // Repository
        modelBuilder.Entity<Repository>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Provider).IsRequired().HasMaxLength(64);
            e.Property(x => x.Owner).IsRequired().HasMaxLength(256);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(512);
            e.Property(x => x.DefaultBranch).IsRequired().HasMaxLength(256);
            e.Property(x => x.Visibility).IsRequired().HasMaxLength(32);
            e.HasIndex(x => x.WorkspaceId);
            e.HasIndex(x => x.GithubRepositoryId).IsUnique();
        });

        // WorkflowRun
        modelBuilder.Entity<WorkflowRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.WorkflowName).IsRequired().HasMaxLength(256);
            e.Property(x => x.Branch).IsRequired().HasMaxLength(256);
            e.Property(x => x.CommitSha).IsRequired().HasMaxLength(64);
            e.Property(x => x.CommitMessage).IsRequired();
            e.Property(x => x.Actor).IsRequired().HasMaxLength(256);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(64);
            e.Property(x => x.Status).IsRequired().HasMaxLength(64);
            e.Property(x => x.HtmlUrl).IsRequired();
            e.HasIndex(x => x.RepositoryId);
            e.HasIndex(x => x.GithubRunId).IsUnique();
        });

        // WorkflowLog
        modelBuilder.Entity<WorkflowLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.JobName).IsRequired().HasMaxLength(256);
            e.Property(x => x.StepName).IsRequired().HasMaxLength(256);
            e.Property(x => x.Level).IsRequired().HasMaxLength(32);
            e.Property(x => x.Message).IsRequired();
            e.Property(x => x.RawMessage).IsRequired();
            e.HasIndex(x => x.WorkflowRunId);
        });

        // Server
        modelBuilder.Entity<Server>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Hostname).IsRequired().HasMaxLength(256);
            e.Property(x => x.IpAddress).IsRequired().HasMaxLength(45);
            e.Property(x => x.OperatingSystem).IsRequired().HasMaxLength(128);
            e.Property(x => x.AgentVersion).IsRequired().HasMaxLength(32);
            e.HasIndex(x => x.WorkspaceId);
        });

        // PM2Process
        modelBuilder.Entity<PM2Process>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.ExecutionMode).IsRequired().HasMaxLength(32);
            e.Property(x => x.NodeVersion).IsRequired().HasMaxLength(32);
            e.HasIndex(x => x.ServerId);
        });

        // PM2Log
        modelBuilder.Entity<PM2Log>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Level).IsRequired().HasMaxLength(32);
            e.Property(x => x.Message).IsRequired();
            e.Property(x => x.RawMessage).IsRequired();
            e.HasIndex(x => x.ServerId);
            e.HasIndex(x => x.ProcessId);
        });

        // ServerMetric
        modelBuilder.Entity<ServerMetric>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ServerId, x.Timestamp });
        });

        // Alert
        modelBuilder.Entity<Alert>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(512);
            e.Property(x => x.Description).IsRequired();
            e.HasIndex(x => x.WorkspaceId);
            e.HasIndex(x => x.Status);
        });

        // AlertRule
        modelBuilder.Entity<AlertRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.ConditionType).IsRequired().HasMaxLength(128);
            e.HasIndex(x => x.WorkspaceId);
        });
    }
}
