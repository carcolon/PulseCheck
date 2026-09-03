using Microsoft.EntityFrameworkCore;
using PulseCheck.Application.Ports;
using PulseCheck.Domain.Entities;
using PulseCheck.Domain.Enums;

namespace PulseCheck.Infrastructure.Persistence;

public sealed class PulseCheckUnitOfWork(PulseCheckDbContext dbContext) : IPulseCheckUnitOfWork
{
    public async Task<IReadOnlyList<Campaign>> GetCampaignsAsync(CancellationToken cancellationToken)
        => await dbContext.Campaigns.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Campaign>> GetActiveCampaignsAsync(CancellationToken cancellationToken)
        => await dbContext.Campaigns
            .Where(item => item.Status == CampaignStatus.Active && item.DeletedAtUtc == null)
            .ToListAsync(cancellationToken);

    public Task<Campaign?> GetCampaignByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.Campaigns.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> CampaignExistsAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.Campaigns.AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken)
        => await dbContext.Devices.ToListAsync(cancellationToken);

    public Task<Device?> GetDeviceByDeviceIdAsync(string deviceId, CancellationToken cancellationToken)
        => dbContext.Devices.FirstOrDefaultAsync(item => item.DeviceId == deviceId, cancellationToken);

    public async Task<IReadOnlyList<AdminUser>> GetAdminUsersAsync(CancellationToken cancellationToken)
        => await dbContext.AdminUsers.ToListAsync(cancellationToken);

    public Task<AdminUser?> GetAdminUserByEmailAsync(string email, CancellationToken cancellationToken)
        => dbContext.AdminUsers.FirstOrDefaultAsync(item => item.Email == email.Trim().ToLowerInvariant(), cancellationToken);

    public Task<AdminUser?> GetAdminUserByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.AdminUsers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<AdminSession?> GetAdminSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => dbContext.AdminSessions
            .Include(item => item.AdminUser)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

    public Task<TransformationalLeaderSession?> GetTransformationalLeaderSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => dbContext.TransformationalLeaderSessions.FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

    public Task<TransformationalLeaderSession?> GetTransformationalLeaderSessionByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.TransformationalLeaderSessions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TransformationalLeaderSession>> GetActiveTransformationalLeaderSessionsBySolvoIdsAsync(
        IReadOnlyCollection<string> solvoIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedSolvoIds = solvoIds
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedSolvoIds.Count == 0)
        {
            return [];
        }

        var sessions = await dbContext.TransformationalLeaderSessions
            .Where(item => item.RevokedAtUtc == null && item.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        return sessions
            .Where(item => normalizedSolvoIds.Contains(item.SolvoId.Trim()))
            .ToArray();
    }

    public Task<AgentCredential?> GetAgentCredentialByDeviceIdAsync(string deviceId, CancellationToken cancellationToken)
        => dbContext.AgentCredentials.FirstOrDefaultAsync(item => item.DeviceId == deviceId.Trim(), cancellationToken);

    public Task<AgentCredential?> GetAgentCredentialByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => dbContext.AgentCredentials.FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

    public Task<TransformationalLeaderExportJob?> GetTransformationalLeaderExportJobByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.TransformationalLeaderExportJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TransformationalLeaderExportJob>> GetVisibleTransformationalLeaderExportJobsBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
        => await dbContext.TransformationalLeaderExportJobs
            .Where(item => item.SessionId == sessionId && item.DismissedAtUtc == null && item.DownloadedAtUtc == null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

    public Task<int> CountAdminUsersAsync(CancellationToken cancellationToken)
        => dbContext.AdminUsers.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<PulseResponse>> GetResponsesAsync(CancellationToken cancellationToken)
        => await dbContext.Responses.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PulseResponse>> GetRecentResponsesAsync(int take, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlite())
        {
            var responses = await dbContext.Responses.ToListAsync(cancellationToken);
            return responses
                .OrderByDescending(item => item.AnsweredAtUtc)
                .Take(take)
                .ToList();
        }

        return await dbContext.Responses
            .OrderByDescending(item => item.AnsweredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeliveryLog>> GetDeliveryLogsSinceAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlite())
        {
            var logs = await dbContext.DeliveryLogs.ToListAsync(cancellationToken);
            return logs
                .Where(item => item.PromptedAtUtc >= fromUtc)
                .OrderByDescending(item => item.PromptedAtUtc)
                .ToList();
        }

        return await dbContext.DeliveryLogs
            .Where(item => item.PromptedAtUtc >= fromUtc)
            .OrderByDescending(item => item.PromptedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeliveryLog>> GetRecentDeliveryLogsAsync(int take, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlite())
        {
            var logs = await dbContext.DeliveryLogs
                .Include(item => item.Campaign)
                .ToListAsync(cancellationToken);

            return logs
                .OrderByDescending(item => item.PromptedAtUtc)
                .Take(take)
                .ToList();
        }

        return await dbContext.DeliveryLogs
            .Include(item => item.Campaign)
            .OrderByDescending(item => item.PromptedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentActivityEvent>> GetRecentAgentActivityEventsAsync(
        int take,
        DateTimeOffset? fromUtc,
        string? eventType,
        CancellationToken cancellationToken)
    {
        IQueryable<AgentActivityEvent> query = dbContext.AgentActivityEvents;

        if (fromUtc is not null)
        {
            query = query.Where(item => item.OccurredAtUtc >= fromUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(item => item.EventType == eventType);
        }

        if (dbContext.Database.IsSqlite())
        {
            var events = await query.ToListAsync(cancellationToken);
            return events
                .OrderByDescending(item => item.OccurredAtUtc)
                .Take(take)
                .ToList();
        }

        return await query
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClientInactivityAlertSetting>> GetClientInactivityAlertSettingsAsync(CancellationToken cancellationToken)
        => await dbContext.ClientInactivityAlertSettings
            .OrderBy(item => item.Client)
            .ThenBy(item => item.Operation)
            .ToListAsync(cancellationToken);

    public Task<ClientInactivityAlertSetting?> GetClientInactivityAlertSettingByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.ClientInactivityAlertSettings.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ClientInactivityAlertSetting>> GetEnabledClientInactivityAlertSettingsForScopeAsync(string client, string operation, CancellationToken cancellationToken)
    {
        var normalizedClient = client.Trim().ToLower();
        var normalizedOperation = operation.Trim().ToLower();
        return await dbContext.ClientInactivityAlertSettings
            .Where(item => item.IsEnabled &&
                           (item.Client == string.Empty || item.Client.ToLower() == normalizedClient) &&
                           (item.Operation == string.Empty || item.Operation.ToLower() == normalizedOperation))
            .OrderBy(item => item.AlertThresholdMinutes)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ClientInactivityAlertSettingExistsAsync(
        string client,
        string operation,
        int thresholdMinutes,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var normalizedClient = client.Trim().ToLower();
        var normalizedOperation = operation.Trim().ToLower();
        return dbContext.ClientInactivityAlertSettings.AnyAsync(
            item => item.Client.ToLower() == normalizedClient &&
                    item.Operation.ToLower() == normalizedOperation &&
                    item.AlertThresholdMinutes == thresholdMinutes &&
                    (!exceptId.HasValue || item.Id != exceptId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<TransformationalLeaderAssignment>> GetTransformationalLeaderAssignmentsAsync(CancellationToken cancellationToken)
        => await dbContext.TransformationalLeaderAssignments
            .OrderBy(item => item.Operation)
            .ThenBy(item => item.SolvoId)
            .ToListAsync(cancellationToken);

    public Task<TransformationalLeaderAssignment?> GetTransformationalLeaderAssignmentBySolvoIdAsync(string solvoId, CancellationToken cancellationToken)
        => dbContext.TransformationalLeaderAssignments.FirstOrDefaultAsync(item => item.SolvoId == solvoId.Trim(), cancellationToken);

    public async Task<IReadOnlyList<TransformationalLeaderCandidateCache>> GetTransformationalLeaderCandidatesAsync(
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TransformationalLeaderCandidates.AsQueryable();
        if (activeOnly)
        {
            query = query.Where(item => item.IsActive);
        }

        return await query
            .OrderBy(item => item.FullName)
            .ThenBy(item => item.SolvoId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> LockedSessionAlertNotificationExistsAsync(
        string deviceId,
        DateTimeOffset lockedAtUtc,
        int thresholdMinutes,
        CancellationToken cancellationToken)
        => dbContext.LockedSessionAlertNotifications.AnyAsync(
            item => item.DeviceId == deviceId.Trim() &&
                    item.LockedAtUtc == lockedAtUtc &&
                    item.ThresholdMinutes == thresholdMinutes,
            cancellationToken);

    public Task AddCampaignAsync(Campaign campaign, CancellationToken cancellationToken)
        => dbContext.Campaigns.AddAsync(campaign, cancellationToken).AsTask();

    public Task RemoveCampaignAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        dbContext.Campaigns.Remove(campaign);
        return Task.CompletedTask;
    }

    public Task AddDeviceAsync(Device device, CancellationToken cancellationToken)
        => dbContext.Devices.AddAsync(device, cancellationToken).AsTask();

    public Task AddAdminUserAsync(AdminUser user, CancellationToken cancellationToken)
        => dbContext.AdminUsers.AddAsync(user, cancellationToken).AsTask();

    public Task RemoveAdminUserAsync(AdminUser user, CancellationToken cancellationToken)
    {
        dbContext.AdminUsers.Remove(user);
        return Task.CompletedTask;
    }

    public Task AddAdminSessionAsync(AdminSession session, CancellationToken cancellationToken)
        => dbContext.AdminSessions.AddAsync(session, cancellationToken).AsTask();

    public Task AddTransformationalLeaderSessionAsync(TransformationalLeaderSession session, CancellationToken cancellationToken)
        => dbContext.TransformationalLeaderSessions.AddAsync(session, cancellationToken).AsTask();

    public Task AddTransformationalLeaderExportJobAsync(TransformationalLeaderExportJob exportJob, CancellationToken cancellationToken)
        => dbContext.TransformationalLeaderExportJobs.AddAsync(exportJob, cancellationToken).AsTask();

    public Task AddAgentCredentialAsync(AgentCredential credential, CancellationToken cancellationToken)
        => dbContext.AgentCredentials.AddAsync(credential, cancellationToken).AsTask();

    public Task AddResponseAsync(PulseResponse response, CancellationToken cancellationToken)
        => dbContext.Responses.AddAsync(response, cancellationToken).AsTask();

    public Task AddDeliveryLogAsync(DeliveryLog log, CancellationToken cancellationToken)
        => dbContext.DeliveryLogs.AddAsync(log, cancellationToken).AsTask();

    public Task AddAgentActivityEventAsync(AgentActivityEvent activityEvent, CancellationToken cancellationToken)
        => dbContext.AgentActivityEvents.AddAsync(activityEvent, cancellationToken).AsTask();

    public Task AddClientInactivityAlertSettingAsync(ClientInactivityAlertSetting setting, CancellationToken cancellationToken)
        => dbContext.ClientInactivityAlertSettings.AddAsync(setting, cancellationToken).AsTask();

    public Task RemoveClientInactivityAlertSettingAsync(ClientInactivityAlertSetting setting, CancellationToken cancellationToken)
    {
        dbContext.ClientInactivityAlertSettings.Remove(setting);
        return Task.CompletedTask;
    }

    public Task AddTransformationalLeaderAssignmentAsync(TransformationalLeaderAssignment assignment, CancellationToken cancellationToken)
        => dbContext.TransformationalLeaderAssignments.AddAsync(assignment, cancellationToken).AsTask();

    public Task AddTransformationalLeaderCandidateAsync(TransformationalLeaderCandidateCache candidate, CancellationToken cancellationToken)
        => dbContext.TransformationalLeaderCandidates.AddAsync(candidate, cancellationToken).AsTask();

    public Task RemoveTransformationalLeaderAssignmentAsync(TransformationalLeaderAssignment assignment, CancellationToken cancellationToken)
    {
        dbContext.TransformationalLeaderAssignments.Remove(assignment);
        return Task.CompletedTask;
    }

    public Task AddLockedSessionAlertNotificationAsync(LockedSessionAlertNotification notification, CancellationToken cancellationToken)
        => dbContext.LockedSessionAlertNotifications.AddAsync(notification, cancellationToken).AsTask();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
