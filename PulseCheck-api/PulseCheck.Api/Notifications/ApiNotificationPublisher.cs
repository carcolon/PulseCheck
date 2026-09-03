using Microsoft.AspNetCore.SignalR;
using PulseCheck.Api.Hubs;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;

namespace PulseCheck.Api.Notifications;

public sealed class ApiNotificationPublisher(
    IHubContext<NotificationHub> agentHubContext,
    IHubContext<AdminNotificationHub> adminHubContext) : INotificationPublisher
{
    public Task PublishCampaignCreatedAsync(CampaignDto campaign, CancellationToken cancellationToken)
        => Task.WhenAll(
            agentHubContext.Clients.All.SendAsync("campaignCreated", new { campaign.Id, campaign.UpdatedAtUtc }, cancellationToken),
            adminHubContext.Clients.All.SendAsync("campaignCreated", campaign, cancellationToken));

    public Task PublishCampaignUpdatedAsync(CampaignDto campaign, CancellationToken cancellationToken)
        => Task.WhenAll(
            agentHubContext.Clients.All.SendAsync("campaignUpdated", new { campaign.Id, campaign.UpdatedAtUtc }, cancellationToken),
            adminHubContext.Clients.All.SendAsync("campaignUpdated", campaign, cancellationToken));

    public Task PublishResponseReceivedAsync(PulseResponseDto response, CancellationToken cancellationToken)
        => PublishAdminOnlyAsync("responseReceived", response, cancellationToken);

    public Task PublishDeviceHeartbeatAsync(DeviceHeartbeatDto device, CancellationToken cancellationToken)
        => PublishAdminOnlyAsync("deviceHeartbeat", device, cancellationToken);

    public Task PublishDeliveryLogAsync(DeliveryLogDto log, CancellationToken cancellationToken)
        => PublishAdminOnlyAsync("deliveryLogReceived", log, cancellationToken);

    public Task PublishAgentActivityAsync(AgentActivityEventDto activityEvent, CancellationToken cancellationToken)
        => PublishAdminOnlyAsync("agentActivityReceived", activityEvent, cancellationToken);

    public Task PublishLiveEventAsync(LiveEventDto liveEvent, CancellationToken cancellationToken)
        => PublishAdminOnlyAsync("liveEvent", liveEvent, cancellationToken);

    private Task PublishAdminOnlyAsync(string methodName, object payload, CancellationToken cancellationToken)
        => adminHubContext.Clients.All.SendAsync(methodName, payload, cancellationToken);
}
