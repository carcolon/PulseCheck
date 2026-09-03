using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;

namespace PulseCheck.Infrastructure.Services;

public sealed class NullEmployeeOperationsProfileResolver : IEmployeeOperationsProfileResolver
{
    public Task<EmployeeOperationsProfile?> ResolveAsync(
        string employeeId,
        string? email,
        string? userPrincipalName,
        CancellationToken cancellationToken)
        => Task.FromResult<EmployeeOperationsProfile?>(null);

    public Task<IReadOnlyDictionary<string, EmployeeReportFields>> ResolveReportFieldsAsync(
        IReadOnlyCollection<string> employeeIds,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, EmployeeReportFields>>(
            new Dictionary<string, EmployeeReportFields>(StringComparer.OrdinalIgnoreCase));

    public Task<IReadOnlyList<string>> GetOperationsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> GetClientsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<TransformationalLeaderCandidate>> GetTransformationalLeaderCandidatesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TransformationalLeaderCandidate>>([]);

    public Task<FabricPeopleColumnsDiagnosticsDto?> GetPeopleColumnsDiagnosticsAsync(int sampleSize, CancellationToken cancellationToken)
        => Task.FromResult<FabricPeopleColumnsDiagnosticsDto?>(null);

    public Task<FabricEmployeeProfileDiagnosticsDto?> GetEmployeeProfileDiagnosticsByEmailAsync(string email, CancellationToken cancellationToken)
        => Task.FromResult<FabricEmployeeProfileDiagnosticsDto?>(null);
}
