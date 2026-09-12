using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Auth;

public sealed class EntraAccessTokenValidator
{
    private readonly EntraOptions _options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;

    public EntraAccessTokenValidator(IOptions<PulseCheckOptions> options)
    {
        _options = options.Value.Entra;

        if (_options.Enabled
            && !string.IsNullOrWhiteSpace(_options.TenantId)
            && !string.IsNullOrWhiteSpace(_options.ApiClientId))
        {
            var metadataAddress = $"https://login.microsoftonline.com/{_options.TenantId}/v2.0/.well-known/openid-configuration";
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true });
        }
    }

    public bool IsEnabled =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.TenantId)
        && !string.IsNullOrWhiteSpace(_options.ApiClientId);

    public async Task<EntraValidatedPrincipal?> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(accessToken) || _configurationManager is null)
        {
            return null;
        }

        var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers =
            [
                $"https://login.microsoftonline.com/{_options.TenantId}/v2.0",
                $"https://login.microsoftonline.com/{_options.TenantId}/v2.0/",
                $"https://sts.windows.net/{_options.TenantId}/"
            ],
            ValidateAudience = true,
            ValidAudiences =
            [
                _options.ApiClientId,
                $"api://{_options.ApiClientId}"
            ],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        try
        {
            var principal = handler.ValidateToken(accessToken, validationParameters, out _);
            var scopes = principal.FindFirst("scp")?.Value
                ?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];

            if (!scopes.Contains("access_as_user", StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            var email = FirstClaim(principal, "preferred_username", "email", "upn", "unique_name");
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var displayName = FirstClaim(principal, "name", "preferred_username", "email") ?? email;
            var objectId = FirstClaim(principal, "oid", "sub");
            var tenantId = FirstClaim(principal, "tid");

            return new EntraValidatedPrincipal(email.Trim(), displayName.Trim(), objectId?.Trim(), tenantId?.Trim());
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? FirstClaim(System.Security.Claims.ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}

public sealed record EntraValidatedPrincipal(
    string Email,
    string DisplayName,
    string? ObjectId,
    string? TenantId);
