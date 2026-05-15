using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Core.DTOs;
using Monitoring.Core.Interfaces;
using System.Security.Claims;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RepositoriesController : ControllerBase
{
    private readonly IGitHubService _gitHubService;

    public RepositoriesController(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RepositoryDto>>> GetRepositories()
    {
        var workspaceId = GetWorkspaceId();
        var repositories = await _gitHubService.GetRepositoriesAsync(workspaceId);

        var dtos = repositories.Select(r => new RepositoryDto(
            Id: r.Id,
            Owner: r.Owner,
            Name: r.Name,
            FullName: r.FullName,
            DefaultBranch: r.DefaultBranch,
            Visibility: r.Visibility,
            CreatedAt: r.CreatedAt
        )).ToList();

        return Ok(dtos);
    }

    [HttpPost]
    public async Task<ActionResult<RepositoryDto>> ConnectRepository([FromBody] ConnectRepositoryRequest request)
    {
        try
        {
            var workspaceId = GetWorkspaceId();
            var repository = await _gitHubService.ConnectRepositoryAsync(
                workspaceId, request.GitHubToken, request.Owner, request.Name);

            var dto = new RepositoryDto(
                Id: repository.Id,
                Owner: repository.Owner,
                Name: repository.Name,
                FullName: repository.FullName,
                DefaultBranch: repository.DefaultBranch,
                Visibility: repository.Visibility,
                CreatedAt: repository.CreatedAt
            );

            return CreatedAtAction(nameof(GetRepository), new { id = repository.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return BadRequest(new { message = "Failed to connect to GitHub. Check your token and repository details." });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RepositoryDto>> GetRepository(Guid id)
    {
        var repository = await _gitHubService.GetRepositoryAsync(id);
        if (repository is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (repository.WorkspaceId != workspaceId)
            return Forbid();

        var dto = new RepositoryDto(
            Id: repository.Id,
            Owner: repository.Owner,
            Name: repository.Name,
            FullName: repository.FullName,
            DefaultBranch: repository.DefaultBranch,
            Visibility: repository.Visibility,
            CreatedAt: repository.CreatedAt
        );

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DisconnectRepository(Guid id)
    {
        var repository = await _gitHubService.GetRepositoryAsync(id);
        if (repository is null)
            return NotFound();

        var workspaceId = GetWorkspaceId();
        if (repository.WorkspaceId != workspaceId)
            return Forbid();

        await _gitHubService.DisconnectRepositoryAsync(id);
        return NoContent();
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }
}
