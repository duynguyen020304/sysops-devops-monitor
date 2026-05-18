using Microsoft.EntityFrameworkCore;
using Monitoring.Core.Entities;

namespace Monitoring.Infrastructure.Data;

public class MonitoringDbContext : DbContext
{
    public MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : base(options) { }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<WorkflowLog> WorkflowLogs => Set<WorkflowLog>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<AgentInstallToken> AgentInstallTokens => Set<AgentInstallToken>();
    public DbSet<AgentUpdateRelease> AgentUpdateReleases => Set<AgentUpdateRelease>();
    public DbSet<AgentUpdateAssignment> AgentUpdateAssignments => Set<AgentUpdateAssignment>();
    public DbSet<AgentUpdateEvent> AgentUpdateEvents => Set<AgentUpdateEvent>();
    public DbSet<PM2Process> PM2Processes => Set<PM2Process>();
    public DbSet<PM2Log> PM2Logs => Set<PM2Log>();
    public DbSet<SystemdService> SystemdServices => Set<SystemdService>();
    public DbSet<SystemdLog> SystemdLogs => Set<SystemdLog>();
    public DbSet<ServerMetric> ServerMetrics => Set<ServerMetric>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
            e.HasMany(x => x.UserRoles)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Role
        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.NormalizedName).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.NormalizedName).IsUnique();
            e.HasMany(x => x.RolePermissions)
                .WithOne(x => x.Role)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.UserRoles)
                .WithOne(x => x.Role)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Permission
        modelBuilder.Entity<Permission>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.NormalizedName).IsRequired().HasMaxLength(100);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(256);
            e.HasIndex(x => x.NormalizedName).IsUnique();
            e.HasMany(x => x.RolePermissions)
                .WithOne(x => x.Permission)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RolePermission
        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        });

        // UserRoleEntity
        modelBuilder.Entity<UserRoleEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
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
            e.HasIndex(x => new { x.WorkflowRunId, x.LineNumber, x.Id });
            e.HasIndex(x => new { x.WorkflowRunId, x.LineNumber }).IsUnique();
        });

        // Server
        modelBuilder.Entity<Server>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Hostname).IsRequired().HasMaxLength(256);
            e.Property(x => x.IpAddress).IsRequired().HasMaxLength(45);
            e.Property(x => x.OperatingSystem).IsRequired().HasMaxLength(128);
            e.Property(x => x.AgentVersion).IsRequired().HasMaxLength(32);
            e.Property(x => x.AgentBuildId).HasMaxLength(128);
            e.Property(x => x.AgentCapabilitiesJson);
            e.Property(x => x.AgentUpdateStatus).HasMaxLength(64);
            e.Property(x => x.ServerToken).HasMaxLength(128);
            e.Property(x => x.MachineId).HasMaxLength(256);
            e.Property(x => x.ArchiveReason).HasMaxLength(256);
            e.Property(x => x.SystemdRefreshRequestedAt);
            e.Property(x => x.SystemdLastRefreshedAt);
            e.Property(x => x.SshUsername).HasMaxLength(128);
            e.Property(x => x.SshPort).HasDefaultValue(22);
            e.Property(x => x.SshPrivateKeyPath).HasMaxLength(512);
            e.HasIndex(x => x.WorkspaceId);
            e.HasIndex(x => new { x.WorkspaceId, x.Hostname, x.ArchivedAt });
            e.HasIndex(x => new { x.WorkspaceId, x.MachineId, x.ArchivedAt });
        });

        // AgentInstallToken
        modelBuilder.Entity<AgentInstallToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).IsRequired().HasMaxLength(64);
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.ServerName).IsRequired().HasMaxLength(256);
            e.Property(x => x.ArchiveReason).HasMaxLength(256);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.WorkspaceId);
            e.HasIndex(x => new { x.WorkspaceId, x.ServerName, x.ArchivedAt });
        });

        // AgentUpdateRelease
        modelBuilder.Entity<AgentUpdateRelease>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).IsRequired().HasMaxLength(32);
            e.Property(x => x.BuildId).IsRequired().HasMaxLength(128);
            e.Property(x => x.Channel).IsRequired().HasMaxLength(64);
            e.Property(x => x.GitSha).HasMaxLength(64);
            e.Property(x => x.ManifestJson).IsRequired();
            e.Property(x => x.ManifestSignature).IsRequired();
            e.Property(x => x.PublicKeyId).IsRequired().HasMaxLength(128);
            e.Property(x => x.ArtifactPath).IsRequired().HasMaxLength(512);
            e.Property(x => x.ArtifactSha256).HasMaxLength(128);
            e.HasIndex(x => new { x.WorkspaceId, x.Channel, x.IsActive });
        });

        // AgentUpdateAssignment
        modelBuilder.Entity<AgentUpdateAssignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).IsRequired().HasMaxLength(64);
            e.Property(x => x.FromVersion).HasMaxLength(32);
            e.Property(x => x.FromBuildId).HasMaxLength(128);
            e.Property(x => x.ErrorMessage).HasMaxLength(1024);
            e.Property(x => x.LastFailureCode).HasMaxLength(128);
            e.HasIndex(x => new { x.ServerId, x.Status });
            e.HasIndex(x => new { x.ServerId, x.ReleaseId, x.Status });
            e.HasOne(x => x.Server).WithMany().HasForeignKey(x => x.ServerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Release).WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        });

        // AgentUpdateEvent
        modelBuilder.Entity<AgentUpdateEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(64);
            e.Property(x => x.Message).HasMaxLength(1024);
            e.HasIndex(x => new { x.ServerId, x.Timestamp });
            e.HasIndex(x => x.AssignmentId);
            e.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.SetNull);
        });

        // PM2Process
        modelBuilder.Entity<PM2Process>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.ExecutionMode).IsRequired().HasMaxLength(32);
            e.Property(x => x.NodeVersion).IsRequired().HasMaxLength(32);
            e.HasIndex(x => x.ServerId);
            e.HasIndex(x => new { x.ServerId, x.Name }).IsUnique();
        });

        // PM2Log
        modelBuilder.Entity<PM2Log>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Level).IsRequired().HasMaxLength(32);
            e.Property(x => x.Message).IsRequired();
            e.Property(x => x.RawMessage).IsRequired();
            e.Property(x => x.Fingerprint).IsRequired().HasMaxLength(128);
            e.HasIndex(x => x.ServerId);
            e.HasIndex(x => x.ProcessId);
            e.HasIndex(x => x.Fingerprint).IsUnique();
        });

        // SystemdService
        modelBuilder.Entity<SystemdService>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(256);
            e.Property(x => x.LoadState).IsRequired().HasMaxLength(64);
            e.Property(x => x.ActiveState).IsRequired().HasMaxLength(64);
            e.Property(x => x.SubState).IsRequired().HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(512);
            e.Property(x => x.FragmentPath).HasMaxLength(512);
            e.HasIndex(x => new { x.ServerId, x.Name }).IsUnique();
            e.HasOne(x => x.Server)
                .WithMany()
                .HasForeignKey(x => x.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SystemdLog
        modelBuilder.Entity<SystemdLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Level).IsRequired().HasMaxLength(32);
            e.Property(x => x.Message).IsRequired();
            e.Property(x => x.RawJson).IsRequired();
            e.Property(x => x.Cursor).HasMaxLength(256);
            e.Property(x => x.BootId).HasMaxLength(128);
            e.Property(x => x.Fingerprint).IsRequired().HasMaxLength(128);
            e.HasIndex(x => new { x.ServerId, x.Timestamp });
            e.HasIndex(x => new { x.ServiceId, x.Timestamp });
            e.HasIndex(x => x.Fingerprint).IsUnique();
            e.HasIndex(x => x.Cursor);
            e.HasOne(x => x.Service)
                .WithMany(x => x.Logs)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).IsRequired().HasMaxLength(256);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
