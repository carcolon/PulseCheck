using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace PulseCheck.Api.Auth;

public static class AdminSessionCookie
{
    public const string Name = "__Host-pulsecheck-admin-session";
    public const string CsrfCookieName = "__Host-pulsecheck-admin-csrf";
    public const string CsrfHeaderName = "X-PulseCheck-CSRF";

    public static string Append(HttpResponse response, string token, DateTimeOffset expiresAtUtc)
    {
        response.Cookies.Append(
            Name,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = expiresAtUtc
            });

        return AppendCsrf(response, expiresAtUtc);
    }

    public static string AppendCsrf(HttpResponse response, DateTimeOffset expiresAtUtc)
    {
        var csrfToken = GenerateCsrfToken();
        response.Cookies.Append(
            CsrfCookieName,
            csrfToken,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = expiresAtUtc
            });

        return csrfToken;
    }

    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(
            Name,
            new CookieOptions
            {
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/"
            });

        response.Cookies.Delete(
            CsrfCookieName,
            new CookieOptions
            {
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/"
            });
    }

    private static string GenerateCsrfToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
