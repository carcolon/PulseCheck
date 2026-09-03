using System.Net;
using System.Text.RegularExpressions;

namespace PulseCheck.Application.Security;

public static partial class PlainTextSecurity
{
    public static bool TryNormalize(string? value, int maxLength, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        normalized = NormalizeWhitespace(value.Trim());
        if (normalized.Length == 0 || normalized.Length > maxLength)
        {
            return false;
        }

        return !ContainsUnsafeMarkup(normalized);
    }

    private static bool ContainsUnsafeMarkup(string value)
    {
        var decoded = WebUtility.HtmlDecode(value);
        if (decoded.Contains('<') || decoded.Contains('>'))
        {
            return true;
        }

        return UnsafeHtmlPattern().IsMatch(decoded);
    }

    private static string NormalizeWhitespace(string value)
        => WhitespacePattern().Replace(value, " ");

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"(?i)\b(?:javascript|vbscript|data:text/html)\s*:|\bon[a-z]+\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeHtmlPattern();
}
