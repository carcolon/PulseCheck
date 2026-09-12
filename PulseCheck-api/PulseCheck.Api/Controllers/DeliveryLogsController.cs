using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[EnableRateLimiting("admin-api")]
[Route("api/delivery-logs")]
public sealed class DeliveryLogsController(PulseCheckService pulseCheckService) : ControllerBase
{
    [Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner,HRAdmin")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeliveryLogDto>>> GetLogs(CancellationToken cancellationToken)
    {
        var logs = await pulseCheckService.GetDeliveryLogsAsync(cancellationToken);
        return Ok(logs);
    }

    [Authorize(AuthenticationSchemes = AgentTokenAuthenticationDefaults.Scheme)]
    [HttpPost]
    public async Task<ActionResult<DeliveryLogDto>> CreateLog([FromBody] DeliveryLogRequest request, CancellationToken cancellationToken)
    {
        if (!RequestMatchesAuthenticatedDevice(request.DeviceId))
        {
            return Forbid();
        }

        var log = await pulseCheckService.CreateDeliveryLogAsync(request, cancellationToken);
        return log is null ? NotFound("Campaign was not found.") : Accepted(log);
    }

    private bool RequestMatchesAuthenticatedDevice(string deviceId)
    {
        var authenticatedDeviceId = User.FindFirst("device_id")?.Value;
        return !string.IsNullOrWhiteSpace(authenticatedDeviceId) &&
               string.Equals(authenticatedDeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
