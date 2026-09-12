namespace PulseCheck.Application.Security;

public static class AdminRoles
{
    public const string Owner = "Owner";
    public const string HRAdmin = "HRAdmin";
    public const string WorkforceAdmin = "WorkforceAdmin";

    private static readonly string[] OperationalRoles = [HRAdmin, WorkforceAdmin];

    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [HRAdmin];
        }

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(part, Owner, StringComparison.OrdinalIgnoreCase))
            {
                return [Owner];
            }

            if (string.Equals(part, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                roles.Add(HRAdmin);
                roles.Add(WorkforceAdmin);
                continue;
            }

            if (string.Equals(part, HRAdmin, StringComparison.OrdinalIgnoreCase))
            {
                roles.Add(HRAdmin);
                continue;
            }

            if (string.Equals(part, WorkforceAdmin, StringComparison.OrdinalIgnoreCase))
            {
                roles.Add(WorkforceAdmin);
            }
        }

        return roles.Count == 0
            ? [HRAdmin]
            : OperationalRoles.Where(roles.Contains).ToArray();
    }

    public static string NormalizeForStorage(string? value)
        => Serialize(Parse(value));

    public static string NormalizeForStorage(IEnumerable<string>? values)
        => Serialize(values is null ? [] : values.SelectMany(Parse));

    public static string Serialize(IEnumerable<string> values)
    {
        var roles = values.ToArray();
        if (roles.Any(role => string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase)))
        {
            return Owner;
        }

        var normalized = OperationalRoles
            .Where(role => roles.Any(input => string.Equals(input, role, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return normalized.Length == 0 ? HRAdmin : string.Join(",", normalized);
    }

    public static bool IsOwner(string? value)
        => Parse(value).Any(role => string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase));
}
