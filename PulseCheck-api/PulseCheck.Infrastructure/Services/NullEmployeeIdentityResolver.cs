using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;

namespace PulseCheck.Infrastructure.Services;

public sealed class NullEmployeeIdentityResolver : IEmployeeIdentityResolver
{
    public Task<EmployeeIdentity?> ResolveAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
        => Task.FromResult<EmployeeIdentity?>(null);
}
