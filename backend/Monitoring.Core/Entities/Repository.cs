using Monitoring.Core.Enums;

namespace Monitoring.Core.Entities;

public class Repository
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Provider { get; set; }
    public required string Owner { get; set; }
    public required string Name { get; set; }
    public required string FullName { get; set; }
    public required string DefaultBranch { get; set; }
    public required string Visibility { get; set; }
    public long GithubRepositoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AccessToken { get; set; }
    public DateTime UpdatedAt { get; set; }
}
