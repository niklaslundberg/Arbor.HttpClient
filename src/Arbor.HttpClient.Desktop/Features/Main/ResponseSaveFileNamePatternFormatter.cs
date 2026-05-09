using System.Globalization;
using System.Text.RegularExpressions;

namespace Arbor.HttpClient.Desktop.Features.Main;

internal static partial class ResponseSaveFileNamePatternFormatter
{
    internal const string DefaultPattern = "{collectionName}-{requestPath}-{requestName}-{timestamp:yyyy-MM-dd HH.mm.ss}{contentTypeExtension}";

    private static readonly HashSet<string> SupportedTokens =
    [
        "collectionName",
        "requestPath",
        "requestName",
        "timestamp",
        "timestampUtc",
        "extension",
        "contentTypeExtension"
    ];

    private static readonly HashSet<char> AdditionalInvalidFileNameChars = [':', '*', '?', '"', '<', '>', '|'];

    public static bool TryFormat(
        string pattern,
        string collectionName,
        string requestPath,
        string requestName,
        string extension,
        DateTimeOffset timestamp,
        out string fileName,
        out string validationError)
    {
        if (!TryValidatePattern(pattern, out validationError))
        {
            fileName = string.Empty;
            return false;
        }

        var normalizedExtension = string.IsNullOrWhiteSpace(extension) ? ".txt" : extension;
        if (!normalizedExtension.StartsWith(".", StringComparison.Ordinal))
        {
            normalizedExtension = $".{normalizedExtension}";
        }

        var raw = TokenRegex().Replace(pattern, match =>
        {
            var token = match.Groups["name"].Value;
            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

            return token switch
            {
                "collectionName" => collectionName,
                "requestPath" => requestPath,
                "requestName" => requestName,
                "extension" or "contentTypeExtension" => normalizedExtension,
                "timestamp" => string.IsNullOrWhiteSpace(format)
                    ? timestamp.ToLocalTime().ToString(CultureInfo.InvariantCulture)
                    : timestamp.ToLocalTime().ToString(format, CultureInfo.InvariantCulture),
                "timestampUtc" => string.IsNullOrWhiteSpace(format)
                    ? timestamp.ToUniversalTime().ToString(CultureInfo.InvariantCulture)
                    : timestamp.ToUniversalTime().ToString(format, CultureInfo.InvariantCulture),
                _ => string.Empty
            };
        });

        var normalized = NormalizeFileName(raw);
        if (!Path.HasExtension(normalized))
        {
            normalized += normalizedExtension;
        }

        fileName = normalized;
        validationError = string.Empty;
        return true;
    }

    public static bool TryValidatePattern(string pattern, out string error)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            error = "File name pattern cannot be empty.";
            return false;
        }

        var stripped = TokenRegex().Replace(pattern, string.Empty);
        if (stripped.Contains('{', StringComparison.Ordinal) || stripped.Contains('}', StringComparison.Ordinal))
        {
            error = "File name pattern contains unmatched braces.";
            return false;
        }

        foreach (Match match in TokenRegex().Matches(pattern))
        {
            var token = match.Groups["name"].Value;
            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

            if (!SupportedTokens.Contains(token))
            {
                error = $"Unsupported token '{{{token}}}' in file name pattern.";
                return false;
            }

            if (token is not ("timestamp" or "timestampUtc") && !string.IsNullOrWhiteSpace(format))
            {
                error = $"Token '{{{token}}}' does not support a format specifier.";
                return false;
            }

            if ((token is "timestamp" or "timestampUtc") && !string.IsNullOrWhiteSpace(format))
            {
                try
                {
                    _ = DateTimeOffset.UtcNow.ToString(format, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    error = $"Invalid timestamp format '{format}'.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private static string NormalizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "response";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var normalized = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            normalized[i] = invalid.Contains(c) || AdditionalInvalidFileNameChars.Contains(c) || c is '/' or '\\' ? '_' : c;
        }

        var trimmed = new string(normalized).Trim(' ', '.');
        return string.IsNullOrEmpty(trimmed) ? "response" : trimmed;
    }

    [GeneratedRegex(@"\{(?<name>[a-zA-Z]+)(:(?<format>[^{}]+))?\}")]
    private static partial Regex TokenRegex();
}
