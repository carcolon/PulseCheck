namespace PulseCheck.Agent;

public sealed record AgentRuntimeIdentity(
    string UserId,
    string UserName,
    string Email,
    string Department,
    string DeviceId,
    string Hostname,
    string OperatingSystem,
    string AgentVersion)
{
    public static AgentRuntimeIdentity FromResponse(PendingResponse response)
        => new(
            response.UserId,
            response.UserName,
            response.Email,
            response.Department,
            response.DeviceId,
            response.Hostname,
            string.Empty,
            string.Empty);

    public static AgentRuntimeIdentity FromDeliveryLog(DeliveryLogRequest request)
        => new(
            request.UserId,
            request.UserName,
            request.Email,
            string.Empty,
            request.DeviceId,
            request.Hostname,
            string.Empty,
            string.Empty);

    public static AgentRuntimeIdentity FromActivity(AgentActivityEventRequest request)
        => new(
            request.UserId,
            request.UserName,
            request.Email,
            request.Department,
            request.DeviceId,
            request.Hostname,
            string.Empty,
            string.Empty);
}
