using System.IO.Pipes;

namespace PulseCheck.Agent;

public sealed class PipeCampaignPromptService(ILogger<PipeCampaignPromptService> logger) : ICampaignPromptService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PromptTimeout = TimeSpan.FromMinutes(30);

    public async Task<PromptResult> PromptAsync(
        AgentCampaignConfiguration campaign,
        bool forceResponseForPostponeLimit,
        CancellationToken cancellationToken)
    {
        using var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCancellation.CancelAfter(ConnectTimeout);

        await using var pipe = new NamedPipeClientStream(
            ".",
            PromptPipe.Name,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(connectCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticLog.Write("Tray prompt pipe is unavailable.");
            logger.LogWarning("Tray prompt pipe is unavailable. Campaign {CampaignId} will be treated as dismissed.", campaign.Id);
            return new PromptResult(null, null);
        }

        var request = new PromptPipeRequest(
            "showCampaignPrompt",
            Guid.NewGuid(),
            campaign,
            forceResponseForPostponeLimit,
            DateTimeOffset.UtcNow);

        await NamedPipeJsonTransport.WriteAsync(
            pipe,
            request,
            PromptPipeJsonContext.Default.PromptPipeRequest,
            cancellationToken);

        using var promptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        promptCancellation.CancelAfter(PromptTimeout);

        var response = await NamedPipeJsonTransport.ReadAsync(
            pipe,
            PromptPipeJsonContext.Default.PromptPipeResponse,
            promptCancellation.Token);

        if (response.RequestId != request.RequestId)
        {
            throw new InvalidOperationException("Prompt pipe response did not match the request.");
        }

        if (!string.IsNullOrWhiteSpace(response.Error))
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Result ?? new PromptResult(null, null);
    }
}
