using Hangfire.Dashboard;
using PulseCheck.Application.Security;

namespace PulseCheck.Api.Auth;

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole(AdminRoles.Owner);
    }
}
