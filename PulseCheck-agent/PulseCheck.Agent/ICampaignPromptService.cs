namespace PulseCheck.Agent;

public interface ICampaignPromptService
{
    Task<PromptResult> PromptAsync(
        AgentCampaignConfiguration campaign,
        bool forceResponseForPostponeLimit,
        CancellationToken cancellationToken);
}
