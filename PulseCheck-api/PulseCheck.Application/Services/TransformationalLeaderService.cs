using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;
using PulseCheck.Domain.Abstractions;
using PulseCheck.Domain.Entities;

namespace PulseCheck.Application.Services;

public sealed class TransformationalLeaderService(
    IPulseCheckUnitOfWork unitOfWork,
    IEmployeeOperationsProfileResolver employeeOperationsProfileResolver,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<TransformationalLeaderOptionsDto> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var leaders = await GetActiveLeadersAsync(cancellationToken);
        var operations = await employeeOperationsProfileResolver.GetOperationsAsync(cancellationToken);
        var assignments = await unitOfWork.GetTransformationalLeaderAssignmentsAsync(cancellationToken);
        var assignmentsBySolvoId = assignments.ToDictionary(item => item.SolvoId, StringComparer.OrdinalIgnoreCase);

        var operationList = operations
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToArray();

        var leaderDtos = leaders
            .GroupBy(item => item.SolvoId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.FullName)
            .ThenBy(item => item.SolvoId)
            .Select(item =>
            {
                assignmentsBySolvoId.TryGetValue(item.SolvoId, out var assignment);
                return ToDto(item, assignment);
            })
            .ToArray();

        return new TransformationalLeaderOptionsDto(operationList, leaderDtos);
    }

    public async Task<TransformationalLeaderCandidateDto> UpsertAssignmentAsync(
        UpsertTransformationalLeaderAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var solvoId = NormalizeRequiredPlainText(request.SolvoId, 80, "Solvo ID");
        var requestedOperations = TransformationalLeaderOperationScope.Normalize(
            request.Operations is { Count: > 0 }
                ? request.Operations
                : string.IsNullOrWhiteSpace(request.Operation) ? [] : [request.Operation]);
        if (requestedOperations.Count == 0)
        {
            throw new ArgumentException("Selecciona al menos una operacion.");
        }

        var leaders = await GetActiveLeadersAsync(cancellationToken);
        var leader = leaders.FirstOrDefault(item => string.Equals(item.SolvoId, solvoId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("El TL seleccionado no esta activo o no tiene job_title_code 1225.");

        var operations = await employeeOperationsProfileResolver.GetOperationsAsync(cancellationToken);
        var operationSet = operations
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalidOperation = requestedOperations.FirstOrDefault(operation => !operationSet.Contains(operation));
        if (!string.IsNullOrWhiteSpace(invalidOperation))
        {
            throw new InvalidOperationException($"La operacion seleccionada no existe en Wolfpack: {invalidOperation}.");
        }

        var now = dateTimeProvider.UtcNow;
        var primaryOperation = TransformationalLeaderOperationScope.Primary(requestedOperations);
        var operationsJson = TransformationalLeaderOperationScope.Serialize(requestedOperations);
        var assignment = await unitOfWork.GetTransformationalLeaderAssignmentBySolvoIdAsync(solvoId, cancellationToken);
        if (assignment is null)
        {
            assignment = new TransformationalLeaderAssignment
            {
                Id = Guid.NewGuid(),
                SolvoId = solvoId,
                Operation = primaryOperation,
                OperationsJson = operationsJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await unitOfWork.AddTransformationalLeaderAssignmentAsync(assignment, cancellationToken);
        }
        else
        {
            assignment.Operation = primaryOperation;
            assignment.OperationsJson = operationsJson;
            assignment.UpdatedAtUtc = now;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(leader, assignment);
    }

    public async Task<bool> DeleteAssignmentAsync(string solvoId, CancellationToken cancellationToken)
    {
        var normalizedSolvoId = NormalizeRequiredPlainText(solvoId, 80, "Solvo ID");
        var assignment = await unitOfWork.GetTransformationalLeaderAssignmentBySolvoIdAsync(normalizedSolvoId, cancellationToken);
        if (assignment is null)
        {
            return false;
        }

        await unitOfWork.RemoveTransformationalLeaderAssignmentAsync(assignment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TransformationalLeaderCandidateDto ToDto(
        TransformationalLeaderCandidate leader,
        TransformationalLeaderAssignment? assignment)
    {
        var assignedOperations = TransformationalLeaderOperationScope.Parse(assignment?.OperationsJson, assignment?.Operation);
        return new(
            leader.SolvoId,
            leader.FullName,
            leader.CorporateEmail,
            leader.JobTitleCode,
            leader.Status,
            leader.Operation,
            leader.Client,
            leader.Department,
            TransformationalLeaderOperationScope.Format(assignedOperations),
            assignedOperations,
            assignment?.UpdatedAtUtc);
    }

    private async Task<IReadOnlyList<TransformationalLeaderCandidate>> GetActiveLeadersAsync(CancellationToken cancellationToken)
    {
        var cachedLeaders = await unitOfWork.GetTransformationalLeaderCandidatesAsync(activeOnly: true, cancellationToken);
        if (cachedLeaders.Count > 0)
        {
            return cachedLeaders
                .Select(item => new TransformationalLeaderCandidate(
                    item.SolvoId,
                    item.FullName,
                    item.CorporateEmail,
                    item.JobTitleCode,
                    item.Status,
                    item.Operation,
                    item.Client,
                    item.Department))
                .ToArray();
        }

        return await employeeOperationsProfileResolver.GetTransformationalLeaderCandidatesAsync(cancellationToken);
    }

    private static string NormalizeRequiredPlainText(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException($"{fieldName} es requerido.");
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} no puede superar {maxLength} caracteres.");
        }

        return normalized;
    }
}

public sealed record UpsertTransformationalLeaderAssignmentRequest(
    string SolvoId,
    string? Operation,
    IReadOnlyList<string>? Operations);
