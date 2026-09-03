using Microsoft.Extensions.Options;

namespace PulseCheck.Agent;

public sealed class Worker(
    ILogger<Worker> logger,
    PulseCheckApiClient apiClient,
    AgentIdentityResolver identityResolver,
    AgentRuntimeState runtimeState,
    LocalAgentStateStore stateStore,
    LocalQueueStore queueStore,
    AgentActivityQueueService activityQueue,
    SyncWakeSignal syncWakeSignal,
    ICampaignPromptService promptService,
    IOptions<AgentOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupIdentity = identityResolver.Resolve(options.Value);
        DiagnosticLog.Write($"Worker started for device {startupIdentity.DeviceId} against {options.Value.BaseUrl}.");
        logger.LogInformation(
            "PulseCheck agent started in background for device {DeviceId}, user {UserId}, email {Email}.",
            startupIdentity.DeviceId,
            startupIdentity.UserId,
            string.IsNullOrWhiteSpace(startupIdentity.Email) ? "not-resolved" : startupIdentity.Email);
        runtimeState.MarkStarted(startupIdentity);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var identity = identityResolver.Resolve(options.Value);
                runtimeState.MarkStarted(identity);
                await activityQueue.FlushAsync(stoppingToken);
                DiagnosticLog.Write("Starting sync cycle.");
                var configuration = await apiClient.SyncAsync(identity, stoppingToken);
                var state = await stateStore.ReadAsync(stoppingToken);
                var now = DateTimeOffset.Now;
                DiagnosticLog.Write($"Sync completed. Active campaigns: {configuration.ActiveCampaigns.Count}.");
                logger.LogInformation(
                    "Configuration fetched at {GeneratedAtUtc}. Active campaigns: {CampaignCount}",
                    configuration.GeneratedAtUtc,
                    configuration.ActiveCampaigns.Count);
                runtimeState.MarkSync(configuration.GeneratedAtUtc, configuration.ActiveCampaigns.Count);
                BackfillImmediateCampaignSignatures(configuration.ActiveCampaigns, state);
                runtimeState.MarkPendingResponses(HasPendingUnansweredCampaign(configuration.ActiveCampaigns, state));

                foreach (var campaign in configuration.ActiveCampaigns)
                {
                    if (campaign.Questions.Count == 0)
                    {
                        continue;
                    }

                    state.Campaigns.TryGetValue(campaign.Id, out var campaignState);
                    if (!CampaignScheduler.IsDue(campaign, campaignState, now))
                    {
                        continue;
                    }

                    DiagnosticLog.Write($"Campaign due: {campaign.Name} ({campaign.Id}).");
                    await apiClient.SubmitDeliveryLogAsync(
                        new DeliveryLogRequest(
                            campaign.Id,
                            identity.DeviceId,
                            identity.UserId,
                            identity.UserName,
                            identity.Email,
                            identity.Hostname,
                            "Prompted",
                            null,
                            0,
                            DateTimeOffset.UtcNow),
                        stoppingToken);
                    runtimeState.MarkPrompted(campaign.Name);

                    IReadOnlyList<PendingResponse>? promptResponses = null;
                    PromptResult? promptResult = null;
                    if (options.Value.SimulateResponses)
                    {
                        promptResponses = PendingResponse.CreateFrom(campaign, identity);
                    }
                    else
                    {
                        promptResult = await promptService.PromptAsync(
                            campaign,
                            campaignState?.PostponeCount >= 2,
                            stoppingToken);
                        if (promptResult.Answers is { Count: > 0 } promptAnswers)
                        {
                            var submissionId = Guid.NewGuid();
                            var answeredAtUtc = DateTimeOffset.UtcNow;
                            promptResponses = promptAnswers
                                .Select(item => new PendingResponse(
                                    campaign.Id,
                                    item.QuestionId,
                                    item.QuestionText,
                                    item.QuestionType,
                                    identity.UserId,
                                    identity.UserName,
                                    identity.Email,
                                    identity.Department,
                                    identity.DeviceId,
                                    identity.Hostname,
                                    item.NumericValue,
                                    item.TextValue,
                                    submissionId,
                                    answeredAtUtc))
                                .ToArray();
                        }
                    }

                    var currentState = campaignState ?? new CampaignLocalState();
                    var campaignSignature = CampaignScheduler.GetCampaignContentSignature(campaign);
                    currentState.LastPromptedAtUtc = DateTimeOffset.UtcNow;
                    currentState.LastPromptedCampaignSignature = campaignSignature;

                    if (promptResponses is { Count: > 0 })
                    {
                        foreach (var pendingResponse in promptResponses)
                        {
                            await queueStore.EnqueueAsync(pendingResponse, stoppingToken);
                        }

                        currentState.LastAnsweredAtUtc = promptResponses.Max(item => item.AnsweredAtUtc);
                        currentState.LastAnsweredCampaignSignature = campaignSignature;
                        currentState.PostponedUntilUtc = null;
                        currentState.PostponeCount = 0;
                        await apiClient.SubmitDeliveryLogAsync(
                            new DeliveryLogRequest(
                                campaign.Id,
                                identity.DeviceId,
                                identity.UserId,
                                identity.UserName,
                                identity.Email,
                                identity.Hostname,
                                "Answered",
                                null,
                                0,
                                currentState.LastAnsweredAtUtc.Value),
                            stoppingToken);
                        runtimeState.MarkAnswered(campaign.Name);
                    }
                    else
                    {
                        if (promptResult?.PostponeFor is TimeSpan postponeFor)
                        {
                            currentState.PostponedUntilUtc = DateTimeOffset.UtcNow.Add(postponeFor);
                            currentState.PostponeCount = Math.Min(currentState.PostponeCount + 1, 2);
                        }

                        await apiClient.SubmitDeliveryLogAsync(
                            new DeliveryLogRequest(
                                campaign.Id,
                                identity.DeviceId,
                                identity.UserId,
                                identity.UserName,
                                identity.Email,
                                identity.Hostname,
                                "Dismissed",
                                null,
                                0,
                                DateTimeOffset.UtcNow),
                            stoppingToken);
                        runtimeState.MarkIdle();
                    }

                    state.Campaigns[campaign.Id] = currentState;
                    runtimeState.MarkPendingResponses(HasPendingUnansweredCampaign(configuration.ActiveCampaigns, state));
                }

                var pending = await queueStore.ReadAllAsync(stoppingToken);
                if (pending.Count > 0)
                {
                    var remaining = new List<PendingResponse>();

                    foreach (var item in pending)
                    {
                        if (!IsValidPendingResponse(item))
                        {
                            DiagnosticLog.Write($"Dropped invalid queued response. Campaign={item.CampaignId}, Question={item.QuestionId}, Type={item.QuestionType}.");
                            logger.LogWarning(
                                "Dropping invalid queued response for campaign {CampaignId}. Question {QuestionId} has invalid payload.",
                                item.CampaignId,
                                item.QuestionId);
                            continue;
                        }

                        try
                        {
                            await apiClient.SubmitResponseAsync(item, stoppingToken);
                            DiagnosticLog.Write($"Response submitted. Campaign={item.CampaignId}, Question={item.QuestionId}, Type={item.QuestionType}.");
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
                        {
                            DiagnosticLog.Write($"Dropped queued response due to {ex.StatusCode}. Campaign={item.CampaignId}, Question={item.QuestionId}. Error={ex.Message}");
                            logger.LogWarning(
                                ex,
                                "Dropping queued response for campaign {CampaignId} due to non-retriable status {StatusCode}.",
                                item.CampaignId,
                                ex.StatusCode);
                        }
                        catch (Exception ex)
                        {
                            DiagnosticLog.Write($"Keeping queued response for retry. Campaign={item.CampaignId}, Question={item.QuestionId}. Error={ex.Message}");
                            logger.LogWarning(
                                ex,
                                "Keeping queued response for campaign {CampaignId} to retry later.",
                                item.CampaignId);
                            remaining.Add(item);
                        }
                    }

                    await queueStore.ReplaceAsync(remaining, stoppingToken);
                    logger.LogInformation(
                        "Processed {ProcessedCount} queued responses. Remaining: {RemainingCount}.",
                        pending.Count,
                        remaining.Count);
                }

                await stateStore.WriteAsync(state, stoppingToken);
                runtimeState.MarkIdle();
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write($"Agent cycle failed: {exception}");
                logger.LogWarning(exception, "Agent cycle failed. Will retry on next interval.");
                runtimeState.MarkError(exception.Message);
            }

            var wokeByRealtime = await syncWakeSignal.WaitForNextAsync(
                TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds),
                stoppingToken);

            if (wokeByRealtime)
            {
                DiagnosticLog.Write("Wake trigger: realtime signal.");
            }
            else
            {
                DiagnosticLog.Write("Wake trigger: polling timeout.");
            }
        }
    }

    private static bool IsValidPendingResponse(PendingResponse response)
    {
        if (string.Equals(response.QuestionType, "Text", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(response.QuestionType, "YesNo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(response.QuestionType, "Choice", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(response.TextValue);
        }

        return response.NumericValue.HasValue;
    }

    private static bool HasPendingUnansweredCampaign(
        IReadOnlyList<AgentCampaignConfiguration> activeCampaigns,
        AgentState state)
    {
        foreach (var campaign in activeCampaigns)
        {
            if (!state.Campaigns.TryGetValue(campaign.Id, out var campaignState) ||
                !CampaignScheduler.HasAnswerForCurrentCampaign(campaign, campaignState))
            {
                return true;
            }
        }

        return false;
    }

    private static void BackfillImmediateCampaignSignatures(
        IReadOnlyList<AgentCampaignConfiguration> activeCampaigns,
        AgentState state)
    {
        foreach (var campaign in activeCampaigns)
        {
            if (state.Campaigns.TryGetValue(campaign.Id, out var campaignState))
            {
                CampaignScheduler.BackfillImmediateCampaignSignature(campaign, campaignState);
            }
        }
    }
}
