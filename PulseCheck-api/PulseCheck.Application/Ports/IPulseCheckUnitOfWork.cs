using PulseCheck.Domain.Entities;

namespace PulseCheck.Application.Ports;

public interface IPulseCheckUnitOfWork
{
    Task<IReadOnlyList<Campaign>> GetCampaignsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Campaign>> GetActiveCampaignsAsync(CancellationToken cancellationToken);
    Task<Campaign?> GetCampaignByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CampaignExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken);
    Task<Device?> GetDeviceByDeviceIdAsync(string deviceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminUser>> GetAdminUsersAsync(CancellationToken cancellationToken);
    Task<AdminUser?> GetAdminUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<AdminUser?> GetAdminUserByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AdminSession?> GetAdminSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<TransformationalLeaderSession?> GetTransformationalLeaderSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<TransformationalLeaderSession?> GetTransformationalLeaderSessionByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransformationalLeaderSession>> GetActiveTransformationalLeaderSessionsBySolvoIdsAsync(
        IReadOnlyCollection<string> solvoIds,
        DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<TransformationalLeaderExportJob?> GetTransformationalLeaderExportJobByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransformationalLeaderExportJob>> GetVisibleTransformationalLeaderExportJobsBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
    Task<AgentCredential?> GetAgentCredentialByDeviceIdAsync(string deviceId, CancellationToken cancellationToken);
    Task<AgentCredential?> GetAgentCredentialByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<int> CountAdminUsersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PulseResponse>> GetResponsesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PulseResponse>> GetRecentResponsesAsync(int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeliveryLog>> GetDeliveryLogsSinceAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryLog>> GetRecentDeliveryLogsAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentActivityEvent>> GetRecentAgentActivityEventsAsync(
        int take,
        DateTimeOffset? fromUtc,
        string? eventType,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ClientInactivityAlertSetting>> GetClientInactivityAlertSettingsAsync(CancellationToken cancellationToken);
    Task<ClientInactivityAlertSetting?> GetClientInactivityAlertSettingByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClientInactivityAlertSetting>> GetEnabledClientInactivityAlertSettingsForScopeAsync(string client, string operation, CancellationToken cancellationToken);
    Task<bool> ClientInactivityAlertSettingExistsAsync(string client, string operation, int thresholdMinutes, Guid? exceptId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransformationalLeaderAssignment>> GetTransformationalLeaderAssignmentsAsync(CancellationToken cancellationToken);
    Task<TransformationalLeaderAssignment?> GetTransformationalLeaderAssignmentBySolvoIdAsync(string solvoId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransformationalLeaderCandidateCache>> GetTransformationalLeaderCandidatesAsync(
        bool activeOnly,
        CancellationToken cancellationToken);
    Task<bool> LockedSessionAlertNotificationExistsAsync(string deviceId, DateTimeOffset lockedAtUtc, int thresholdMinutes, CancellationToken cancellationToken);

    Task AddCampaignAsync(Campaign campaign, CancellationToken cancellationToken);
    Task RemoveCampaignAsync(Campaign campaign, CancellationToken cancellationToken);
    Task AddDeviceAsync(Device device, CancellationToken cancellationToken);
    Task AddAdminUserAsync(AdminUser user, CancellationToken cancellationToken);
    Task RemoveAdminUserAsync(AdminUser user, CancellationToken cancellationToken);
    Task AddAdminSessionAsync(AdminSession session, CancellationToken cancellationToken);
    Task AddTransformationalLeaderSessionAsync(TransformationalLeaderSession session, CancellationToken cancellationToken);
    Task AddTransformationalLeaderExportJobAsync(TransformationalLeaderExportJob exportJob, CancellationToken cancellationToken);
    Task AddAgentCredentialAsync(AgentCredential credential, CancellationToken cancellationToken);
    Task AddResponseAsync(PulseResponse response, CancellationToken cancellationToken);
    Task AddDeliveryLogAsync(DeliveryLog log, CancellationToken cancellationToken);
    Task AddAgentActivityEventAsync(AgentActivityEvent activityEvent, CancellationToken cancellationToken);
    Task AddClientInactivityAlertSettingAsync(ClientInactivityAlertSetting setting, CancellationToken cancellationToken);
    Task RemoveClientInactivityAlertSettingAsync(ClientInactivityAlertSetting setting, CancellationToken cancellationToken);
    Task AddTransformationalLeaderAssignmentAsync(TransformationalLeaderAssignment assignment, CancellationToken cancellationToken);
    Task AddTransformationalLeaderCandidateAsync(TransformationalLeaderCandidateCache candidate, CancellationToken cancellationToken);
    Task RemoveTransformationalLeaderAssignmentAsync(TransformationalLeaderAssignment assignment, CancellationToken cancellationToken);
    Task AddLockedSessionAlertNotificationAsync(LockedSessionAlertNotification notification, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
