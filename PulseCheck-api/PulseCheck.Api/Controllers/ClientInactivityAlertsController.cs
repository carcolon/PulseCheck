using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner")]
[EnableRateLimiting("admin-api")]
[Route("api/client-inactivity-alerts")]
public sealed class ClientInactivityAlertsController(PulseCheckService pulseCheckService) : ControllerBase
{
    [HttpGet("options")]
    public async Task<ActionResult<ClientInactivityAlertOptionsDto>> GetOptions(CancellationToken cancellationToken)
    {
        var options = await pulseCheckService.GetClientInactivityAlertOptionsAsync(cancellationToken);
        return Ok(options);
    }

    [HttpPut]
    public async Task<ActionResult<ClientInactivityAlertSettingDto>> Upsert(
        [FromBody] UpsertClientInactivityAlertSettingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var setting = await pulseCheckService.UpsertClientInactivityAlertSettingAsync(request, cancellationToken);
            return Ok(setting);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await pulseCheckService.DeleteClientInactivityAlertSettingAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
