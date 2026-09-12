using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using PulseCheck.Api.Models;
using PulseCheck.Application.Common;
using PulseCheck.Application.Ports;

namespace PulseCheck.Api.Services;

public sealed class GraphEmployeeIdentityResolver(
    HttpClient httpClient,
    IOptions<PulseCheckOptions> options,
    ILogger<GraphEmployeeIdentityResolver> logger) : IEmployeeIdentityResolver
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EntraOptions _options = options.Value.Entra;
    private IConfidentialClientApplication? _clientApplication;

    public async Task<EmployeeIdentity?> ResolveAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        if (!_options.GraphEnabled ||
            string.IsNullOrWhiteSpace(_options.TenantId) ||
            string.IsNullOrWhiteSpace(_options.GraphClientId) ||
            string.IsNullOrWhiteSpace(_options.GraphClientSecret))
        {
            return null;
        }

        var userKey = ResolveUserKey(request);
        if (string.IsNullOrWhiteSpace(userKey))
        {
            return null;
        }

        try
        {
            return await GetUserByIdOrPrincipalAsync(userKey, cancellationToken)
                ?? await FindUserByMailOrPrincipalAsync(userKey, cancellationToken);
        }
        catch (MsalServiceException ex)
        {
            logger.LogWarning(ex, "Microsoft Graph token acquisition failed.");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Microsoft Graph user lookup failed for {UserKey}.", userKey);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Microsoft Graph returned an invalid user payload for {UserKey}.", userKey);
            return null;
        }
    }

    private async Task<EmployeeIdentity?> GetUserByIdOrPrincipalAsync(string userKey, CancellationToken cancellationToken)
    {
        var encodedUserKey = Uri.EscapeDataString(userKey);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/users/{encodedUserKey}?$select=id,displayName,userPrincipalName,mail,employeeId,department");

        using var response = await SendGraphAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return ToEmployeeIdentity(await JsonSerializer.DeserializeAsync<GraphUser>(stream, JsonOptions, cancellationToken));
    }

    private async Task<EmployeeIdentity?> FindUserByMailOrPrincipalAsync(string userKey, CancellationToken cancellationToken)
    {
        var escapedUserKey = userKey.Replace("'", "''", StringComparison.Ordinal);
        var filter = Uri.EscapeDataString($"mail eq '{escapedUserKey}' or userPrincipalName eq '{escapedUserKey}'");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/users?$filter={filter}&$select=id,displayName,userPrincipalName,mail,employeeId,department&$top=1");

        using var response = await SendGraphAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GraphUserCollection>(stream, JsonOptions, cancellationToken);
        return ToEmployeeIdentity(payload?.Value.FirstOrDefault());
    }

    private async Task<HttpResponseMessage> SendGraphAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await ClientApplication
            .AcquireTokenForClient(GraphScopes)
            .ExecuteAsync(cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private IConfidentialClientApplication ClientApplication =>
        _clientApplication ??= ConfidentialClientApplicationBuilder
            .Create(_options.GraphClientId)
            .WithClientSecret(_options.GraphClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_options.TenantId}")
            .Build();

    private static string? ResolveUserKey(RegisterDeviceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email.Contains('@', StringComparison.Ordinal))
        {
            return request.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.UserId) && request.UserId.Contains('@', StringComparison.Ordinal))
        {
            return request.UserId.Trim();
        }

        return null;
    }

    private static EmployeeIdentity? ToEmployeeIdentity(GraphUser? user)
    {
        if (user is null)
        {
            return null;
        }

        return new EmployeeIdentity(
            user.Id ?? string.Empty,
            user.EmployeeId ?? string.Empty,
            user.UserPrincipalName ?? string.Empty,
            user.Mail ?? string.Empty,
            user.DisplayName ?? string.Empty,
            user.Department ?? string.Empty);
    }

    private sealed record GraphUserCollection(IReadOnlyList<GraphUser> Value);

    private sealed record GraphUser(
        string? Id,
        string? DisplayName,
        string? UserPrincipalName,
        string? Mail,
        string? EmployeeId,
        string? Department);
}
