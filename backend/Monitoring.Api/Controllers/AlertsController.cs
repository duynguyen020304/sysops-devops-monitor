using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Api.Filters;
using Monitoring.Core.DTOs;
using Monitoring.Core.Interfaces;

namespace Monitoring.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertsController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpGet]
    [RequirePermission("view_dashboards")]
    public async Task<ActionResult<PagedResult<AlertDto>>> GetAlerts([FromQuery] AlertListRequest request)
    {
        var workspaceId = GetWorkspaceId();
        var result = await _alertService.GetAlertsAsync(workspaceId, request);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("view_dashboards")]
    public async Task<ActionResult<AlertDto>> GetAlert(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        var alert = await _alertService.GetAlertByIdAsync(id, workspaceId);

        if (alert is null)
            return NotFound();

        return Ok(alert);
    }

    [HttpPost("rules")]
    [RequirePermission("configure_alerts")]
    public async Task<ActionResult<AlertRuleDto>> CreateRule([FromBody] CreateAlertRuleRequest request)
    {
        var workspaceId = GetWorkspaceId();
        var rule = await _alertService.CreateAlertRuleAsync(workspaceId, request);
        return CreatedAtAction(nameof(GetRules), new { id = rule.Id }, rule);
    }

    [HttpGet("rules")]
    [RequirePermission("view_dashboards")]
    public async Task<ActionResult<List<AlertRuleDto>>> GetRules()
    {
        var workspaceId = GetWorkspaceId();
        var rules = await _alertService.GetAlertRulesAsync(workspaceId);
        return Ok(rules);
    }

    [HttpPut("rules/{id:guid}")]
    [RequirePermission("configure_alerts")]
    public async Task<ActionResult<AlertRuleDto>> UpdateRule(Guid id, [FromBody] UpdateAlertRuleRequest request)
    {
        var workspaceId = GetWorkspaceId();

        try
        {
            var rule = await _alertService.UpdateAlertRuleAsync(id, workspaceId, request);
            return Ok(rule);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpDelete("rules/{id:guid}")]
    [RequirePermission("configure_alerts")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        var deleted = await _alertService.DeleteAlertRuleAsync(id, workspaceId);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id:guid}/acknowledge")]
    [RequirePermission("acknowledge_alerts")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        var userId = GetUserId();
        var result = await _alertService.AcknowledgeAlertAsync(id, userId, workspaceId);

        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id:guid}/resolve")]
    [RequirePermission("resolve_alerts")]
    public async Task<IActionResult> ResolveAlert(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        var userId = GetUserId();
        var result = await _alertService.ResolveAlertAsync(id, userId, workspaceId);

        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id:guid}/mute")]
    [RequirePermission("manage_alerts")]
    public async Task<IActionResult> MuteAlert(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        var result = await _alertService.MuteAlertAsync(id, workspaceId);

        if (!result)
            return NotFound();

        return NoContent();
    }

    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value
            ?? throw new UnauthorizedAccessException("WorkspaceId claim not found.");
        return Guid.Parse(claim);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID claim not found.");
        return Guid.Parse(claim);
    }
}
