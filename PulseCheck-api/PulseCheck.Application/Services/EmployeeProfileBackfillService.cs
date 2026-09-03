using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Application.Services;

public sealed class EmployeeProfileBackfillService(
    IPulseCheckUnitOfWork unitOfWork,
    IEmployeeIdentityResolver employeeIdentityResolver,
    IEmployeeOperationsProfileResolver employeeOperationsProfileResolver)
{
    public async Task<EmployeeProfileBackfillResult> BackfillAsync(
        int maxDevicesPerRun,
        bool updateHistoricalResponses,
        CancellationToken cancellationToken)
    {
        var devices = await unitOfWork.GetDevicesAsync(cancellationToken);
        var candidateDevices = devices
            .Where(NeedsProfileBackfill)
            .OrderByDescending(item => item.LastSeenAtUtc)
            .Take(Math.Max(1, maxDevicesPerRun))
            .ToArray();

        var enrichedDevices = 0;
        foreach (var device in candidateDevices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await EnrichDeviceAsync(device, cancellationToken))
            {
                enrichedDevices++;
            }
        }

        var updatedResponses = updateHistoricalResponses
            ? await BackfillResponsesFromDevicesAsync(devices, cancellationToken)
            : 0;

        if (enrichedDevices > 0 || updatedResponses > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new EmployeeProfileBackfillResult(candidateDevices.Length, enrichedDevices, updatedResponses);
    }

    private async Task<bool> EnrichDeviceAsync(Device device, CancellationToken cancellationToken)
    {
        var before = CaptureProfile(device);
        var request = new RegisterDeviceRequest(
            device.DeviceId,
            device.Hostname,
            device.UserId,
            device.UserName,
            device.Email,
            device.Department,
            device.OperatingSystem,
            device.AgentVersion);

        var resolvedIdentity = await employeeIdentityResolver.ResolveAsync(request, cancellationToken);
        device.UserName = ResolveValue(resolvedIdentity?.DisplayName, device.UserName);
        device.Email = ResolveValue(resolvedIdentity?.Mail, device.Email);
        device.EmployeeId = ResolveValue(resolvedIdentity?.EmployeeId, device.EmployeeId);
        device.EntraObjectId = ResolveValue(resolvedIdentity?.EntraObjectId, device.EntraObjectId);
        device.UserPrincipalName = ResolveValue(resolvedIdentity?.UserPrincipalName, device.UserPrincipalName);
        device.Department = ResolveValue(resolvedIdentity?.Department, device.Department);

        var operationsProfile = await employeeOperationsProfileResolver.ResolveAsync(device.EmployeeId, cancellationToken);
        if (operationsProfile is not null)
        {
            device.Operation = ResolveValue(operationsProfile.Operation, device.Operation);
            device.EmployeeStatus = ResolveValue(operationsProfile.Status, device.EmployeeStatus);
            device.LeaderSolvoId = ResolveValue(operationsProfile.LeaderSolvoId, device.LeaderSolvoId);
            device.LeaderFullName = ResolveValue(operationsProfile.LeaderFullName, device.LeaderFullName);
            device.LeaderCorporateEmail = ResolveValue(operationsProfile.LeaderCorporateEmail, device.LeaderCorporateEmail);
            device.Department = ResolveValue(operationsProfile.Operation, device.Department);
        }

        return before != CaptureProfile(device);
    }

    private async Task<int> BackfillResponsesFromDevicesAsync(
        IReadOnlyList<Device> devices,
        CancellationToken cancellationToken)
    {
        var responses = await unitOfWork.GetResponsesAsync(cancellationToken);
        var devicesByDeviceId = devices
            .Where(HasProfile)
            .GroupBy(item => item.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.OrderByDescending(device => device.LastSeenAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var devicesByEmail = devices
            .Where(item => HasProfile(item) && !string.IsNullOrWhiteSpace(item.Email))
            .GroupBy(item => item.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.OrderByDescending(device => device.LastSeenAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        foreach (var response in responses.Where(NeedsResponseProfileBackfill))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matchedDevice = ResolveDevice(response, devicesByDeviceId, devicesByEmail);
            if (matchedDevice is null)
            {
                continue;
            }

            var before = CaptureProfile(response);
            response.EmployeeId = ResolveValue(matchedDevice.EmployeeId, response.EmployeeId);
            response.EntraObjectId = ResolveValue(matchedDevice.EntraObjectId, response.EntraObjectId);
            response.UserPrincipalName = ResolveValue(matchedDevice.UserPrincipalName, response.UserPrincipalName);
            response.Operation = ResolveValue(matchedDevice.Operation, response.Operation);
            response.EmployeeStatus = ResolveValue(matchedDevice.EmployeeStatus, response.EmployeeStatus);
            response.LeaderSolvoId = ResolveValue(matchedDevice.LeaderSolvoId, response.LeaderSolvoId);
            response.LeaderFullName = ResolveValue(matchedDevice.LeaderFullName, response.LeaderFullName);
            response.LeaderCorporateEmail = ResolveValue(matchedDevice.LeaderCorporateEmail, response.LeaderCorporateEmail);
            response.Department = ResolveValue(matchedDevice.Operation, ResolveValue(matchedDevice.Department, response.Department));

            if (before != CaptureProfile(response))
            {
                updated++;
            }
        }

        return updated;
    }

    private static Device? ResolveDevice(
        PulseResponse response,
        IReadOnlyDictionary<string, Device> devicesByDeviceId,
        IReadOnlyDictionary<string, Device> devicesByEmail)
    {
        if (!string.IsNullOrWhiteSpace(response.DeviceId) &&
            devicesByDeviceId.TryGetValue(response.DeviceId, out var byDeviceId))
        {
            return byDeviceId;
        }

        if (!string.IsNullOrWhiteSpace(response.Email) &&
            devicesByEmail.TryGetValue(response.Email, out var byEmail))
        {
            return byEmail;
        }

        return null;
    }

    private static bool NeedsProfileBackfill(Device device)
        => !string.IsNullOrWhiteSpace(device.Email) &&
           (string.IsNullOrWhiteSpace(device.EmployeeId) ||
            string.IsNullOrWhiteSpace(device.Operation) ||
            string.IsNullOrWhiteSpace(device.LeaderCorporateEmail));

    private static bool NeedsResponseProfileBackfill(PulseResponse response)
        => string.IsNullOrWhiteSpace(response.EmployeeId) ||
           string.IsNullOrWhiteSpace(response.Operation) ||
           string.IsNullOrWhiteSpace(response.LeaderCorporateEmail);

    private static bool HasProfile(Device device)
        => !string.IsNullOrWhiteSpace(device.EmployeeId) ||
           !string.IsNullOrWhiteSpace(device.Operation) ||
           !string.IsNullOrWhiteSpace(device.LeaderCorporateEmail);

    private static EmployeeProfileSnapshot CaptureProfile(Device device) => new(
        device.EmployeeId,
        device.EntraObjectId,
        device.UserPrincipalName,
        device.Operation,
        device.EmployeeStatus,
        device.LeaderSolvoId,
        device.LeaderFullName,
        device.LeaderCorporateEmail,
        device.Department);

    private static EmployeeProfileSnapshot CaptureProfile(PulseResponse response) => new(
        response.EmployeeId,
        response.EntraObjectId,
        response.UserPrincipalName,
        response.Operation,
        response.EmployeeStatus,
        response.LeaderSolvoId,
        response.LeaderFullName,
        response.LeaderCorporateEmail,
        response.Department);

    private static string ResolveValue(string? preferred, string? fallback)
        => !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim()
            : fallback?.Trim() ?? string.Empty;

    private sealed record EmployeeProfileSnapshot(
        string EmployeeId,
        string EntraObjectId,
        string UserPrincipalName,
        string Operation,
        string EmployeeStatus,
        string LeaderSolvoId,
        string LeaderFullName,
        string LeaderCorporateEmail,
        string Department);
}

public sealed record EmployeeProfileBackfillResult(
    int CandidateDevices,
    int EnrichedDevices,
    int UpdatedHistoricalResponses);
