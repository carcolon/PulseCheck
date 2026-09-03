using PulseCheck.Application.Common;

namespace PulseCheck.Application.Ports;

public interface INotificationPublisher
{
    Task PublishCampaignCreatedAsync(CampaignDto campaign, CancellationToken cancellationToken);
    Task PublishCampaignUpdatedAsync(CampaignDto campaign, CancellationToken cancellationToken);
    Task PublishResponseReceivedAsync(PulseResponseDto response, CancellationToken cancellationToken);
    Task PublishDeviceHeartbeatAsync(DeviceHeartbeatDto device, CancellationToken cancellationToken);
    Task PublishDeliveryLogAsync(DeliveryLogDto log, CancellationToken cancellationToken);
    Task PublishAgentActivityAsync(AgentActivityEventDto activityEvent, CancellationToken cancellationToken);
    Task PublishLiveEventAsync(LiveEventDto liveEvent, CancellationToken cancellationToken);
}
