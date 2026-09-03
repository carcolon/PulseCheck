using System.IO.Pipes;
using Microsoft.Extensions.Hosting;

namespace PulseCheck.Agent;

public sealed class TrayPromptPipeServer(
    ILogger<TrayPromptPipeServer> logger,
    WindowsPromptService promptService) : BackgroundService
{
    private readonly SemaphoreSlim promptLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DiagnosticLog.Write($"Tray prompt pipe server starting on {PromptPipe.Name}.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                PromptPipe.Name,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
                await HandleConnectionAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write($"Tray prompt pipe server failed: {exception.Message}");
                logger.LogWarning(exception, "Tray prompt pipe server failed.");
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        Guid requestId = Guid.Empty;
        try
        {
            await promptLock.WaitAsync(stoppingToken);
            try
            {
                var request = await NamedPipeJsonTransport.ReadAsync(
                    pipe,
                    PromptPipeJsonContext.Default.PromptPipeRequest,
                    stoppingToken);
                requestId = request.RequestId;

                if (!string.Equals(request.MessageType, "showCampaignPrompt", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Unsupported prompt pipe message type: {request.MessageType}");
                }

                DiagnosticLog.Write($"Prompt pipe request received. Campaign={request.Campaign.Id}.");
                var result = await promptService.PromptAsync(
                    request.Campaign,
                    request.ForceResponseForPostponeLimit,
                    stoppingToken);

                await NamedPipeJsonTransport.WriteAsync(
                    pipe,
                    new PromptPipeResponse("promptResult", request.RequestId, result, null, DateTimeOffset.UtcNow),
                    PromptPipeJsonContext.Default.PromptPipeResponse,
                    stoppingToken);
            }
            finally
            {
                promptLock.Release();
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            DiagnosticLog.Write($"Prompt pipe request failed: {exception.Message}");
            logger.LogWarning(exception, "Prompt pipe request failed.");

            if (pipe.IsConnected)
            {
                try
                {
                    await NamedPipeJsonTransport.WriteAsync(
                        pipe,
                        new PromptPipeResponse("promptResult", requestId, null, exception.Message, DateTimeOffset.UtcNow),
                        PromptPipeJsonContext.Default.PromptPipeResponse,
                        CancellationToken.None);
                }
                catch
                {
                    // The service side may already have disconnected.
                }
            }
        }
    }
}
