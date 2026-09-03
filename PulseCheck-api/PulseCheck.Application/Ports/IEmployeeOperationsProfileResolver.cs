using PulseCheck.Application.Common;

namespace PulseCheck.Application.Ports;

public interface IEmployeeOperationsProfileResolver
{
    Task<EmployeeOperationsProfile?> ResolveAsync(
        string employeeId,
        string? email,
        string? userPrincipalName,
        CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, EmployeeReportFields>> ResolveReportFieldsAsync(
        IReadOnlyCollection<string> employeeIds,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetOperationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetClientsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TransformationalLeaderCandidate>> GetTransformationalLeaderCandidatesAsync(CancellationToken cancellationToken);
    Task<FabricPeopleColumnsDiagnosticsDto?> GetPeopleColumnsDiagnosticsAsync(int sampleSize, CancellationToken cancellationToken);
    Task<FabricEmployeeProfileDiagnosticsDto?> GetEmployeeProfileDiagnosticsByEmailAsync(string email, CancellationToken cancellationToken);
}

public sealed record EmployeeOperationsProfile(
    string SolvoId,
    string Operation,
    string Status,
    string LeaderSolvoId,
    string LeaderFullName,
    string LeaderCorporateEmail,
    string ClientCode,
    string Client,
    string DepartmentCode,
    string Department);

public sealed record EmployeeReportFields(
    string PayrollCompany,
    string Country,
    string InternalEmployeeCategory,
    string JobTitle);

public sealed record TransformationalLeaderCandidate(
    string SolvoId,
    string FullName,
    string CorporateEmail,
    string JobTitleCode,
    string Status,
    string Operation,
    string Client,
    string Department);
