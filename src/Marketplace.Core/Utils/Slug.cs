using System.Text;
using System.Text.RegularExpressions;

namespace Marketplace.Core.Utils;

public static class Slug
{
    public static string From(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalized = input.Trim().ToLowerInvariant();

        normalized = normalized
            .Replace("і", "i")
            .Replace("ї", "yi")
            .Replace("є", "ye")
            .Replace("ґ", "g");

        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", "");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        normalized = normalized.Replace(" ", "-");
        normalized = Regex.Replace(normalized, @"-+", "-").Trim('-');

        return normalized;
    }
}
