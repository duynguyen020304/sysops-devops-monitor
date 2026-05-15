using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Monitoring.Core.Interfaces;
using System.Security.Claims;

namespace Monitoring.Api.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var permissionService = context.HttpContext.RequestServices.GetService<IPermissionService>();
        if (permissionService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!await permissionService.HasPermissionAsync(userId, _permission))
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}
