using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Auth;

public sealed class EntraAuthorizationCodeFlow(HttpClient httpClient, IOptions<PulseCheckOptions> options, IDataProtectionProvider dataProtectionProvider)
{
    private readonly EntraOptions _options = options.Value.Entra;
    private readonly string[] _allowedOrigins = options.Value.AllowedOrigins;
    private readonly IDataProtector _stateProtector = dataProtectionProvider.CreateProtector("PulseCheck.Entra.AuthorizationCodeState.v1");

    public bool IsEnabled =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.TenantId)
        && !string.IsNullOrWhiteSpace(_options.ApiClientId)
        && !string.IsNullOrWhiteSpace(_options.WebClientId);

    public Uri CreateAuthorizationUri(HttpResponse response, string redirectOrigin, string returnToPath)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("El acceso corporativo no esta configurado.");
        }

        var normalizedOrigin = NormalizeAllowedOrigin(redirectOrigin);
        var redirectUri = $"{normalizedOrigin}/auth/callback";
        var verifier = GenerateBase64Url(48);
        var challenge = Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.AuthorizationCodeLifetimeMinutes, 1, 30));
        var state = ProtectState(new EntraAuthorizationState(
            GenerateBase64Url(32),
            verifier,
            normalizedOrigin,
            redirectUri,
            NormalizeReturnToPath(returnToPath),
            expiresAtUtc));

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.WebClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["response_mode"] = "query",
            ["scope"] = BuildScopes(),
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account"
        };

        return new Uri(QueryHelpers.AddQueryString(
            $"https://login.microsoftonline.com/{WebUtility.UrlEncode(_options.TenantId)}/oauth2/v2.0/authorize",
            query));
    }

    public async Task<EntraAuthorizationCodeResult?> RedeemAuthorizationCodeAsync(
        HttpRequest request,
        HttpResponse response,
        string code,
        string state,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var protectedState = UnprotectState(state);
        if (protectedState is null ||
            protectedState.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            !string.Equals(protectedState.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            return null;
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.WebClientId,
            ["scope"] = BuildScopes(),
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = protectedState.CodeVerifier
        };

        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{WebUtility.UrlEncode(_options.TenantId)}/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        tokenRequest.Headers.TryAddWithoutValidation("Origin", protectedState.RedirectOrigin);

        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload?.AccessToken))
        {
            return null;
        }

        return new EntraAuthorizationCodeResult(payload.AccessToken, protectedState.ReturnToPath);
    }

    private string NormalizeAllowedOrigin(string redirectOrigin)
    {
        if (!Uri.TryCreate(redirectOrigin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !IsLocalHttp(uri)))
        {
            throw new InvalidOperationException("El origen de redireccion no esta permitido.");
        }

        var normalizedOrigin = uri.GetLeftPart(UriPartial.Authority);
        if (IsLocalHttp(uri) ||
            _allowedOrigins.Any(origin => string.Equals(origin.Trim().TrimEnd('/'), normalizedOrigin, StringComparison.OrdinalIgnoreCase)))
        {
            return normalizedOrigin;
        }

        throw new InvalidOperationException("El origen de redireccion no esta permitido.");
    }

    private static bool IsLocalHttp(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttp &&
           (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeReturnToPath(string returnToPath)
    {
        if (returnToPath.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(returnToPath, "/tl", StringComparison.OrdinalIgnoreCase))
        {
            return returnToPath;
        }

        return "/admin/overview";
    }

    private string ProtectState(EntraAuthorizationState state)
    {
        var protectedPayload = _stateProtector.Protect(System.Text.Json.JsonSerializer.Serialize(state));
        return Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(protectedPayload));
    }

    private EntraAuthorizationState? UnprotectState(string state)
    {
        try
        {
            var protectedPayload = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(state));
            return System.Text.Json.JsonSerializer.Deserialize<EntraAuthorizationState>(_stateProtector.Unprotect(protectedPayload));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private string BuildScopes()
        => $"openid profile email api://{_options.ApiClientId}/access_as_user";

    private static string GenerateBase64Url(int byteCount)
        => Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(byteCount));

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }
}

public sealed record EntraAuthorizationCodeResult(string AccessToken, string ReturnToPath);

public sealed record EntraAuthorizationState(
    string Nonce,
    string CodeVerifier,
    string RedirectOrigin,
    string RedirectUri,
    string ReturnToPath,
    DateTimeOffset ExpiresAtUtc);
