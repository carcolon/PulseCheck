using System.Text.Json;

namespace PulseCheck.Application.Services;

public static class TransformationalLeaderOperationScope
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> Parse(string? operationsJson, string? fallbackOperation)
    {
        var operations = new List<string>();
        if (!string.IsNullOrWhiteSpace(operationsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<IReadOnlyList<string>>(operationsJson, JsonOptions);
                if (parsed is not null)
                {
                    operations.AddRange(parsed);
                }
            }
            catch (JsonException)
            {
                // Fall back to the legacy single operation field.
            }
        }

        if (operations.Count == 0 && !string.IsNullOrWhiteSpace(fallbackOperation))
        {
            operations.Add(fallbackOperation);
        }

        return Normalize(operations);
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string?> operations)
        => operations
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToArray();

    public static string Serialize(IEnumerable<string> operations)
        => JsonSerializer.Serialize(Normalize(operations), JsonOptions);

    public static string Format(IEnumerable<string> operations)
        => string.Join(", ", Normalize(operations));

    public static string Primary(IEnumerable<string> operations)
        => Normalize(operations).FirstOrDefault() ?? string.Empty;

    public static bool IncludesAllOperations(IEnumerable<string> operations)
        => Normalize(operations).Any(IsAllOperationsScope);

    public static bool Contains(IEnumerable<string> operations, string operation)
        => Normalize(operations).Any(item => item.Equals(operation.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool IsAllOperationsScope(string operation)
        => operation.Equals(TransformationalLeaderAuthService.OwnerOperationScope, StringComparison.OrdinalIgnoreCase);
}
