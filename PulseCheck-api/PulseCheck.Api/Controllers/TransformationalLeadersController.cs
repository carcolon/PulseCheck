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
[Route("api/transformational-leaders")]
public sealed class TransformationalLeadersController(TransformationalLeaderService transformationalLeaderService) : ControllerBase
{
    [HttpGet("options")]
    public async Task<ActionResult<TransformationalLeaderOptionsDto>> GetOptions(CancellationToken cancellationToken)
    {
        var options = await transformationalLeaderService.GetOptionsAsync(cancellationToken);
        return Ok(options);
    }

    [HttpPut("assignments")]
    public async Task<ActionResult<TransformationalLeaderCandidateDto>> UpsertAssignment(
        [FromBody] UpsertTransformationalLeaderAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var leader = await transformationalLeaderService.UpsertAssignmentAsync(request, cancellationToken);
            return Ok(leader);
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

    [HttpDelete("assignments/{solvoId}")]
    public async Task<IActionResult> DeleteAssignment(string solvoId, CancellationToken cancellationToken)
    {
        var deleted = await transformationalLeaderService.DeleteAssignmentAsync(solvoId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
