using PulseCheck.Application.Common;

namespace PulseCheck.Application.Ports;

public interface IEmployeeIdentityResolver
{
    Task<EmployeeIdentity?> ResolveAsync(RegisterDeviceRequest request, CancellationToken cancellationToken);
}

public sealed record EmployeeIdentity(
    string EntraObjectId,
    string EmployeeId,
    string UserPrincipalName,
    string Mail,
    string DisplayName,
    string Department);
