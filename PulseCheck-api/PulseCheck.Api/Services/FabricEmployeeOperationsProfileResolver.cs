using System.Data;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PulseCheck.Api.Models;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;

namespace PulseCheck.Api.Services;

public sealed class FabricEmployeeOperationsProfileResolver(
    IOptions<PulseCheckOptions> options,
    ILogger<FabricEmployeeOperationsProfileResolver> logger) : IEmployeeOperationsProfileResolver
{
    private static readonly TokenRequestContext FabricTokenRequest = new(["https://database.windows.net/.default"]);
    private readonly EntraOptions _entraOptions = options.Value.Entra;
    private readonly FabricOptions _fabricOptions = options.Value.Fabric;
    private ClientSecretCredential? _credential;

    public async Task<EmployeeOperationsProfile?> ResolveAsync(
        string employeeId,
        string? email,
        string? userPrincipalName,
        CancellationToken cancellationToken)
    {
        var normalizedEmployeeId = employeeId.Trim();
        var normalizedEmail = email?.Trim() ?? string.Empty;
        var normalizedUserPrincipalName = userPrincipalName?.Trim() ?? string.Empty;

        if (!IsConfigured() ||
            (string.IsNullOrWhiteSpace(normalizedEmployeeId) &&
             string.IsNullOrWhiteSpace(normalizedEmail) &&
             string.IsNullOrWhiteSpace(normalizedUserPrincipalName)))
        {
            return null;
        }

        try
        {
            var token = await Credential.GetTokenAsync(FabricTokenRequest, cancellationToken);
            await using var connection = new SqlConnection(BuildConnectionString())
            {
                AccessToken = token.Token
            };

            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
            command.CommandText = BuildQuery();
            command.Parameters.Add("@solvoId", SqlDbType.NVarChar, 80).Value = normalizedEmployeeId;
            command.Parameters.Add("@email", SqlDbType.NVarChar, 180).Value = normalizedEmail;
            command.Parameters.Add("@userPrincipalName", SqlDbType.NVarChar, 180).Value = normalizedUserPrincipalName;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new EmployeeOperationsProfile(
                ReadString(reader, "solvo_id"),
                ReadString(reader, "operation"),
                ReadString(reader, "status"),
                ReadString(reader, "leader_solvo_id"),
                ReadString(reader, "leader_full_name"),
                ReadString(reader, "leader_corporate_email"),
                ReadString(reader, "client_code"),
                ReadString(reader, "client"),
                ReadString(reader, "department_code"),
                ReadString(reader, "department"));
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or AuthenticationFailedException)
        {
            logger.LogWarning(
                ex,
                "Fabric employee profile lookup failed for employee id {EmployeeId}, email {Email}, user principal name {UserPrincipalName}.",
                employeeId,
                email,
                userPrincipalName);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetOperationsAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return [];
        }

        try
        {
            var token = await Credential.GetTokenAsync(FabricTokenRequest, cancellationToken);
            await using var connection = new SqlConnection(BuildConnectionString())
            {
                AccessToken = token.Token
            };

            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
            command.CommandText = """
                SELECT DISTINCT CAST([operation] AS nvarchar(180)) AS [operation]
                FROM [gld].[wolfpack_without_salary]
                WHERE [operation] IS NOT NULL
                  AND LTRIM(RTRIM(CAST([operation] AS nvarchar(180)))) <> N''
                ORDER BY CAST([operation] AS nvarchar(180));
                """;

            var operations = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var operation = ReadString(reader, "operation");
                if (!string.IsNullOrWhiteSpace(operation))
                {
                    operations.Add(operation);
                }
            }

            return operations
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToArray();
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or AuthenticationFailedException)
        {
            logger.LogWarning(ex, "Fabric operation list lookup failed.");
            return [];
        }
    }

    public async Task<FabricEmployeeProfileDiagnosticsDto?> GetEmployeeProfileDiagnosticsByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Email is required.");
        }

        if (!IsConfigured())
        {
            return new FabricEmployeeProfileDiagnosticsDto(
                DateTimeOffset.UtcNow,
                "[gld].[wolfpack_without_salary]",
                normalizedEmail,
                false,
                0,
                null,
                []);
        }

        try
        {
            var token = await Credential.GetTokenAsync(FabricTokenRequest, cancellationToken);
            await using var connection = new SqlConnection(BuildConnectionString())
            {
                AccessToken = token.Token
            };

            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
            command.CommandText = BuildEmployeeProfileDiagnosticsQuery();
            command.Parameters.Add("@email", SqlDbType.NVarChar, 180).Value = normalizedEmail;

            var rows = new List<FabricEmployeeProfileDiagnosticRowDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new FabricEmployeeProfileDiagnosticRowDto(
                    ReadString(reader, "solvo_id"),
                    ReadString(reader, "full_name"),
                    ReadString(reader, "corporate_email"),
                    ReadString(reader, "user_principal_name"),
                    ReadString(reader, "job_title_code"),
                    ReadString(reader, "status"),
                    ReadString(reader, "operation"),
                    ReadString(reader, "client_code"),
                    ReadString(reader, "client"),
                    ReadString(reader, "department_code"),
                    ReadString(reader, "department"),
                    ReadString(reader, "leader_solvo_id"),
                    ReadString(reader, "leader_full_name"),
                    ReadString(reader, "leader_corporate_email")));
            }

            return new FabricEmployeeProfileDiagnosticsDto(
                DateTimeOffset.UtcNow,
                "[gld].[wolfpack_without_salary]",
                normalizedEmail,
                true,
                rows.Count,
                rows.FirstOrDefault(),
                rows);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or AuthenticationFailedException)
        {
            logger.LogWarning(ex, "Fabric employee profile diagnostics lookup failed for email {Email}.", normalizedEmail);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetClientsAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return [];
        }

        try
        {
            var token = await Credential.GetTokenAsync(FabricTokenRequest, cancellationToken);
            await using var connection = new SqlConnection(BuildConnectionString())
            {
                AccessToken = token.Token
            };

            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
            command.CommandText = """
                SELECT DISTINCT CAST([client] AS nvarchar(180)) AS [client]
                FROM [gld].[wolfpack_without_salary]
                WHERE [client] IS NOT NULL
                  AND LTRIM(RTRIM(CAST([client] AS nvarchar(180)))) <> N''
                ORDER BY CAST([client] AS nvarchar(180));
                """;

            var clients = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var client = ReadString(reader, "client");
                if (!string.IsNullOrWhiteSpace(client))
                {
                    clients.Add(client);
                }
            }

            return clients
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToArray();
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or AuthenticationFailedException)
        {
            logger.LogWarning(ex, "Fabric client list lookup failed.");
            return [];
        }
    }

    public async Task<IReadOnlyList<TransformationalLeaderCandidate>> GetTransformationalLeaderCandidatesAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return [];
        }

        try
        {
            var token = await Credential.GetTokenAsync(FabricTokenRequest, cancellationToken);
            await using var connection = new SqlConnection(BuildConnectionString())
            {
                AccessToken = token.Token
            };

            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
            command.CommandText = """
                SELECT
                    CAST([solvo_id] AS nvarchar(80)) AS [solvo_id],
                    CAST([full_name] AS nvarchar(180)) AS [full_name],
                    CAST([corporate_email] AS nvarchar(180)) AS [corporate_email],
                    CAST([job_title_code] AS nvarchar(80)) AS [job_title_code],
                    CAST([status] AS nvarchar(80)) AS [status],
                    CAST([operation] AS nvarchar(180)) AS [operation],
                    CAST([client] AS nvarchar(180)) AS [client],
                    CAST([department] AS nvarchar(180)) AS [department]
                FROM [gld].[wolfpack_without_salary]
                WHERE LTRIM(RTRIM(CAST([job_title_code] AS nvarchar(80)))) = N'1225'
                  AND LTRIM(RTRIM(CAST([status] AS nvarchar(80)))) = N'Active'
                ORDER BY CAST([full_name] AS nvarchar(180)), CAST([solvo_id] AS nvarchar(80));
                """;

            var leaders = new List<TransformationalLeaderCandidate>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var solvoId = ReadString(reader, "solvo_id");
                if (string.IsNullOrWhiteSpace(solvoId))
                {
                    continue;
                }

                leaders.Add(new TransformationalLeaderCandidate(
                    solvoId,
                    ReadString(reader, "full_name"),
                    ReadString(reader, "corporate_email"),
                    ReadString(reader, "job_title_code"),
                    ReadString(reader, "status"),
                    ReadString(reader, "operation"),
                    ReadString(reader, "client"),
                    ReadString(reader, "department")));
            }

            return leaders;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or AuthenticationFailedException)
        {
            logger.LogWarning(ex, "Fabric transformational leader candidate lookup failed.");
            return [];
        }
    }

    public async Task<FabricPeopleColumnsDiagnosticsDto?> GetPeopleColumnsDiagnosticsAsync(
        int sampleSize,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return null;
        }

        var safeSampleSize = Math.Clamp(sampleSize, 0, 50);

        try
        {
            var token = await Credential.GetTokenAsync(FabricTokenRequest, cancellationToken);
            await using var connection = new SqlConnection(BuildConnectionString())
            {
                AccessToken = token.Token
            };

            await connection.OpenAsync(cancellationToken);
            var totalRows = await ReadTotalRowsAsync(connection, cancellationToken);
            var diagnostics = new List<FabricPeopleColumnDiagnosticsDto>();

            foreach (var columnName in PeopleDiagnosticsColumns)
            {
                diagnostics.Add(await ReadColumnDiagnosticsAsync(connection, columnName, safeSampleSize, cancellationToken));
            }

            return new FabricPeopleColumnsDiagnosticsDto(
                DateTimeOffset.UtcNow,
                "[gld].[wolfpack_without_salary]",
                totalRows,
                diagnostics);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or AuthenticationFailedException)
        {
            logger.LogWarning(ex, "Fabric people column diagnostics lookup failed.");
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, EmployeeReportFields>> ResolveReportFieldsAsync(
        IReadOnlyCollection<string> employeeIds,
        CancellationToken cancellationToken)
    {
        var normalizedEmployeeIds = employeeIds
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!IsConfigured() || normalizedEmployeeIds.Length == 0)
        {
            return new Dictionary<string, EmployeeReportFields>(StringComparer.OrdinalIgnoreCase);
        }

        var lookup = new Dictionary<string, EmployeeReportFields>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var token = await Credential.GetTokenAsync(FabricTokenRequest, cancellationToken);
            await using var connection = new SqlConnection(BuildConnectionString())
            {
                AccessToken = token.Token
            };

            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
            command.CommandText = BuildReportFieldsQuery();
            command.Parameters.Add("@employeeIdsJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(normalizedEmployeeIds);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var solvoId = ReadString(reader, "solvo_id");
                if (string.IsNullOrWhiteSpace(solvoId) || lookup.ContainsKey(solvoId))
                {
                    continue;
                }

                lookup[solvoId] = new EmployeeReportFields(
                    ReadString(reader, "payroll_company"),
                    ReadString(reader, "country"),
                    ReadString(reader, "internal_employee_category"),
                    ReadString(reader, "job_title"));
            }

            return lookup;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or AuthenticationFailedException)
        {
            logger.LogWarning(ex, "Fabric employee report fields lookup failed.");
            return lookup;
        }
    }

    private ClientSecretCredential Credential =>
        _credential ??= new ClientSecretCredential(
            _entraOptions.TenantId,
            _entraOptions.GraphClientId,
            _entraOptions.GraphClientSecret);

    private bool IsConfigured()
        => _fabricOptions.Enabled &&
           !string.IsNullOrWhiteSpace(_fabricOptions.Endpoint) &&
           !string.IsNullOrWhiteSpace(_fabricOptions.Database) &&
           !string.IsNullOrWhiteSpace(_entraOptions.TenantId) &&
           !string.IsNullOrWhiteSpace(_entraOptions.GraphClientId) &&
           !string.IsNullOrWhiteSpace(_entraOptions.GraphClientSecret);

    private string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _fabricOptions.Endpoint,
            InitialCatalog = _fabricOptions.Database,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30
        };

        return builder.ConnectionString;
    }

    private string BuildQuery()
    {
        return """
            SELECT TOP (1)
                CAST(employee.[solvo_id] AS nvarchar(80)) AS [solvo_id],
                CAST(employee.[operation] AS nvarchar(180)) AS [operation],
                CAST(employee.[status] AS nvarchar(80)) AS [status],
                CAST(employee.[leader_solvo_id] AS nvarchar(80)) AS [leader_solvo_id],
                CAST(employee.[leader_full_name] AS nvarchar(180)) AS [leader_full_name],
                CAST(leader.[corporate_email] AS nvarchar(180)) AS [leader_corporate_email],
                CAST(employee.[client_code] AS nvarchar(80)) AS [client_code],
                CAST(employee.[client] AS nvarchar(180)) AS [client],
                CAST(employee.[department_code] AS nvarchar(80)) AS [department_code],
                CAST(employee.[department] AS nvarchar(180)) AS [department]
            FROM [gld].[wolfpack_without_salary] employee
            OUTER APPLY (
                SELECT TOP (1) leaderLookup.[corporate_email]
                FROM [gld].[wolfpack_without_salary] leaderLookup
                WHERE LTRIM(RTRIM(CAST(leaderLookup.[solvo_id] AS nvarchar(80)))) =
                      LTRIM(RTRIM(CAST(employee.[leader_solvo_id] AS nvarchar(80))))
            ) leader
            WHERE (LTRIM(RTRIM(@solvoId)) <> N''
                    AND LTRIM(RTRIM(CAST(employee.[solvo_id] AS nvarchar(80)))) = LTRIM(RTRIM(@solvoId)))
               OR (LTRIM(RTRIM(@email)) <> N''
                    AND LOWER(LTRIM(RTRIM(CAST(employee.[corporate_email] AS nvarchar(180))))) = LOWER(LTRIM(RTRIM(@email))))
               OR (LTRIM(RTRIM(@userPrincipalName)) <> N''
                    AND LOWER(LTRIM(RTRIM(CAST(employee.[corporate_email] AS nvarchar(180))))) = LOWER(LTRIM(RTRIM(@userPrincipalName))))
            ORDER BY
                CASE
                    WHEN LTRIM(RTRIM(@solvoId)) <> N''
                     AND LTRIM(RTRIM(CAST(employee.[solvo_id] AS nvarchar(80)))) = LTRIM(RTRIM(@solvoId)) THEN 0
                    ELSE 1
                END,
                CASE
                    WHEN LTRIM(RTRIM(CAST(employee.[status] AS nvarchar(80)))) = N'Active' THEN 0
                    ELSE 1
                END,
                CASE
                    WHEN employee.[operation] IS NOT NULL
                     AND LTRIM(RTRIM(CAST(employee.[operation] AS nvarchar(180)))) <> N'' THEN 0
                    ELSE 1
                END,
                CAST(employee.[solvo_id] AS nvarchar(80));
            """;
    }

    private string BuildReportFieldsQuery()
    {
        return """
            SELECT
                CAST(employee.[solvo_id] AS nvarchar(80)) AS [solvo_id],
                CAST(employee.[payroll_company] AS nvarchar(180)) AS [payroll_company],
                CAST(employee.[country] AS nvarchar(120)) AS [country],
                CAST(employee.[internal_employee_category] AS nvarchar(180)) AS [internal_employee_category],
                CAST(employee.[job_title] AS nvarchar(180)) AS [job_title]
            FROM [gld].[wolfpack_without_salary] employee
            INNER JOIN OPENJSON(@employeeIdsJson) employeeIds
                ON LTRIM(RTRIM(CAST(employee.[solvo_id] AS nvarchar(80)))) =
                   LTRIM(RTRIM(CAST(employeeIds.[value] AS nvarchar(80))));
            """;
    }

    private string BuildEmployeeProfileDiagnosticsQuery()
    {
        return """
            SELECT
                CAST(employee.[solvo_id] AS nvarchar(80)) AS [solvo_id],
                CAST(employee.[full_name] AS nvarchar(180)) AS [full_name],
                CAST(employee.[corporate_email] AS nvarchar(180)) AS [corporate_email],
                CAST(employee.[corporate_email] AS nvarchar(180)) AS [user_principal_name],
                CAST(employee.[job_title_code] AS nvarchar(80)) AS [job_title_code],
                CAST(employee.[status] AS nvarchar(80)) AS [status],
                CAST(employee.[operation] AS nvarchar(180)) AS [operation],
                CAST(employee.[client_code] AS nvarchar(80)) AS [client_code],
                CAST(employee.[client] AS nvarchar(180)) AS [client],
                CAST(employee.[department_code] AS nvarchar(80)) AS [department_code],
                CAST(employee.[department] AS nvarchar(180)) AS [department],
                CAST(employee.[leader_solvo_id] AS nvarchar(80)) AS [leader_solvo_id],
                CAST(employee.[leader_full_name] AS nvarchar(180)) AS [leader_full_name],
                CAST(leader.[corporate_email] AS nvarchar(180)) AS [leader_corporate_email]
            FROM [gld].[wolfpack_without_salary] employee
            OUTER APPLY (
                SELECT TOP (1) leaderLookup.[corporate_email]
                FROM [gld].[wolfpack_without_salary] leaderLookup
                WHERE LTRIM(RTRIM(CAST(leaderLookup.[solvo_id] AS nvarchar(80)))) =
                      LTRIM(RTRIM(CAST(employee.[leader_solvo_id] AS nvarchar(80))))
            ) leader
            WHERE LOWER(LTRIM(RTRIM(CAST(employee.[corporate_email] AS nvarchar(180))))) = LOWER(LTRIM(RTRIM(@email)))
            ORDER BY
                CASE
                    WHEN LTRIM(RTRIM(CAST(employee.[status] AS nvarchar(80)))) = N'Active' THEN 0
                    ELSE 1
                END,
                CASE
                    WHEN employee.[operation] IS NOT NULL
                     AND LTRIM(RTRIM(CAST(employee.[operation] AS nvarchar(180)))) <> N'' THEN 0
                    ELSE 1
                END,
                CAST(employee.[solvo_id] AS nvarchar(80));
            """;
    }

    private static readonly string[] PeopleDiagnosticsColumns =
    [
        "client_code",
        "client",
        "department_code",
        "department"
    ];

    private async Task<long> ReadTotalRowsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
        command.CommandText = "SELECT COUNT_BIG(1) FROM [gld].[wolfpack_without_salary];";

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar);
    }

    private async Task<FabricPeopleColumnDiagnosticsDto> ReadColumnDiagnosticsAsync(
        SqlConnection connection,
        string columnName,
        int sampleSize,
        CancellationToken cancellationToken)
    {
        await using var countsCommand = connection.CreateCommand();
        countsCommand.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
        countsCommand.CommandText = columnName switch
        {
            "client_code" => """
                SELECT
                    COUNT_BIG(CASE WHEN [client_code] IS NOT NULL
                        AND LTRIM(RTRIM(CAST([client_code] AS nvarchar(4000)))) <> N'' THEN 1 END) AS [non_empty_rows],
                    COUNT_BIG(DISTINCT NULLIF(LTRIM(RTRIM(CAST([client_code] AS nvarchar(4000)))), N'')) AS [distinct_values]
                FROM [gld].[wolfpack_without_salary];
                """,
            "client" => """
                SELECT
                    COUNT_BIG(CASE WHEN [client] IS NOT NULL
                        AND LTRIM(RTRIM(CAST([client] AS nvarchar(4000)))) <> N'' THEN 1 END) AS [non_empty_rows],
                    COUNT_BIG(DISTINCT NULLIF(LTRIM(RTRIM(CAST([client] AS nvarchar(4000)))), N'')) AS [distinct_values]
                FROM [gld].[wolfpack_without_salary];
                """,
            "department_code" => """
                SELECT
                    COUNT_BIG(CASE WHEN [department_code] IS NOT NULL
                        AND LTRIM(RTRIM(CAST([department_code] AS nvarchar(4000)))) <> N'' THEN 1 END) AS [non_empty_rows],
                    COUNT_BIG(DISTINCT NULLIF(LTRIM(RTRIM(CAST([department_code] AS nvarchar(4000)))), N'')) AS [distinct_values]
                FROM [gld].[wolfpack_without_salary];
                """,
            "department" => """
                SELECT
                    COUNT_BIG(CASE WHEN [department] IS NOT NULL
                        AND LTRIM(RTRIM(CAST([department] AS nvarchar(4000)))) <> N'' THEN 1 END) AS [non_empty_rows],
                    COUNT_BIG(DISTINCT NULLIF(LTRIM(RTRIM(CAST([department] AS nvarchar(4000)))), N'')) AS [distinct_values]
                FROM [gld].[wolfpack_without_salary];
                """,
            _ => throw new InvalidOperationException("Unsupported Fabric diagnostics column.")
        };

        long nonEmptyRows = 0;
        long distinctValues = 0;
        await using (var reader = await countsCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                nonEmptyRows = Convert.ToInt64(reader["non_empty_rows"]);
                distinctValues = Convert.ToInt64(reader["distinct_values"]);
            }
        }

        var sampleValues = sampleSize == 0
            ? []
            : await ReadColumnSampleValuesAsync(connection, columnName, sampleSize, cancellationToken);

        return new FabricPeopleColumnDiagnosticsDto(columnName, nonEmptyRows, distinctValues, sampleValues);
    }

    private async Task<IReadOnlyList<string>> ReadColumnSampleValuesAsync(
        SqlConnection connection,
        string columnName,
        int sampleSize,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = Math.Max(5, _fabricOptions.CommandTimeoutSeconds);
        command.CommandText = columnName switch
        {
            "client_code" => """
                SELECT TOP (@sampleSize)
                    LTRIM(RTRIM(CAST([client_code] AS nvarchar(4000)))) AS [value]
                FROM [gld].[wolfpack_without_salary]
                WHERE [client_code] IS NOT NULL
                  AND LTRIM(RTRIM(CAST([client_code] AS nvarchar(4000)))) <> N''
                GROUP BY LTRIM(RTRIM(CAST([client_code] AS nvarchar(4000))))
                ORDER BY LTRIM(RTRIM(CAST([client_code] AS nvarchar(4000))));
                """,
            "client" => """
                SELECT TOP (@sampleSize)
                    LTRIM(RTRIM(CAST([client] AS nvarchar(4000)))) AS [value]
                FROM [gld].[wolfpack_without_salary]
                WHERE [client] IS NOT NULL
                  AND LTRIM(RTRIM(CAST([client] AS nvarchar(4000)))) <> N''
                GROUP BY LTRIM(RTRIM(CAST([client] AS nvarchar(4000))))
                ORDER BY LTRIM(RTRIM(CAST([client] AS nvarchar(4000))));
                """,
            "department_code" => """
                SELECT TOP (@sampleSize)
                    LTRIM(RTRIM(CAST([department_code] AS nvarchar(4000)))) AS [value]
                FROM [gld].[wolfpack_without_salary]
                WHERE [department_code] IS NOT NULL
                  AND LTRIM(RTRIM(CAST([department_code] AS nvarchar(4000)))) <> N''
                GROUP BY LTRIM(RTRIM(CAST([department_code] AS nvarchar(4000))))
                ORDER BY LTRIM(RTRIM(CAST([department_code] AS nvarchar(4000))));
                """,
            "department" => """
                SELECT TOP (@sampleSize)
                    LTRIM(RTRIM(CAST([department] AS nvarchar(4000)))) AS [value]
                FROM [gld].[wolfpack_without_salary]
                WHERE [department] IS NOT NULL
                  AND LTRIM(RTRIM(CAST([department] AS nvarchar(4000)))) <> N''
                GROUP BY LTRIM(RTRIM(CAST([department] AS nvarchar(4000))))
                ORDER BY LTRIM(RTRIM(CAST([department] AS nvarchar(4000))));
                """,
            _ => throw new InvalidOperationException("Unsupported Fabric diagnostics column.")
        };
        command.Parameters.Add("@sampleSize", SqlDbType.Int).Value = sampleSize;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(ReadString(reader, "value"));
        }

        return values;
    }

    private static string ReadString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? string.Empty;
    }
}
