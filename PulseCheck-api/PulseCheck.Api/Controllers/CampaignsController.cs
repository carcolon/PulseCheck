using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulseCheck.Api.Auth;
using PulseCheck.Application.Common;
using PulseCheck.Application.Services;

namespace PulseCheck.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AdminTokenAuthenticationDefaults.Scheme, Roles = "Owner,HRAdmin")]
[EnableRateLimiting("admin-api")]
[Route("api/[controller]")]
public sealed class CampaignsController(PulseCheckService pulseCheckService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CampaignDto>>> GetCampaigns(
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var campaigns = await pulseCheckService.GetCampaignsAsync(includeDeleted, cancellationToken);
        return Ok(campaigns);
    }

    [HttpGet("audience-options")]
    public async Task<ActionResult<CampaignAudienceOptionsDto>> GetAudienceOptions(CancellationToken cancellationToken)
    {
        var options = await pulseCheckService.GetCampaignAudienceOptionsAsync(cancellationToken);
        return Ok(options);
    }

    [HttpPost]
    public async Task<ActionResult<CampaignDto>> CreateCampaign([FromBody] CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var campaign = await pulseCheckService.CreateCampaignAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetCampaigns), new { id = campaign.Id }, campaign);
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

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<CampaignDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateCampaignStatusRequest request,
        CancellationToken cancellationToken)
    {
        var campaign = await pulseCheckService.UpdateCampaignStatusAsync(id, request, cancellationToken);
        return campaign is null ? NotFound() : Ok(campaign);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> UpdateCampaign(
        Guid id,
        [FromBody] UpdateCampaignRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var campaign = await pulseCheckService.UpdateCampaignAsync(id, request, cancellationToken);
            return campaign is null ? NotFound() : Ok(campaign);
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
    public async Task<IActionResult> DeleteCampaign(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await pulseCheckService.DeleteCampaignAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
