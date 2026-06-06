using System.Globalization;
using System.Text;

namespace Arbor.HttpClient.Core.HttpRequest;

/// <summary>
/// Formats a request (from history, a collection, or the live composer) as a
/// portable single-line <c>curl</c> command. The output is intentionally
/// reusable across POSIX shells; values are always wrapped in single quotes and
/// any embedded single quote is escaped as <c>'\''</c> (the standard shell
/// idiom).
/// </summary>
public static class CurlFormatter
{
    /// <summary>
    /// Formats a request as a portable single-line <c>curl</c> command.
    /// </summary>
    /// <param name="method">HTTP method (e.g. <c>GET</c>, <c>POST</c>). Defaults to <c>GET</c> when blank.</param>
    /// <param name="url">Full request URL. Must not be empty or whitespace.</param>
    /// <param name="body">Optional request body appended via <c>--data-raw</c>.</param>
    /// <param name="headers">Optional enabled headers appended via <c>-H</c>.</param>
    /// <returns>A POSIX-compatible <c>curl</c> command string.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is null, empty, or whitespace.</exception>
    public static string Format(
        string method,
        string url,
        string? body = null,
        IReadOnlyList<RequestHeader>? headers = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required", nameof(url));
        }

        var effectiveMethod = string.IsNullOrWhiteSpace(method)
            ? "GET"
            : method.Trim().ToUpper(CultureInfo.InvariantCulture);

        var builder = new StringBuilder();
        builder.Append("curl -X ").Append(effectiveMethod).Append(' ').Append(ShellEscape(url));

        if (headers is { } enabledHeaders)
        {
            foreach (var header in enabledHeaders.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name)))
            {
                builder.Append(" -H ").Append(ShellEscape($"{header.Name}: {header.Value ?? string.Empty}"));
            }
        }

        if (!string.IsNullOrEmpty(body))
        {
            builder.Append(" --data-raw ").Append(ShellEscape(body));
        }

        return builder.ToString();
    }

    public static string Format(RequestHistoryEntry request, IReadOnlyList<RequestHeader>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Format(request.Method, request.Url, request.Body, headers);
    }

    private static string ShellEscape(string value)
    {
        // POSIX single-quoted strings: every character is literal except single
        // quote itself, which must be written as '\'' (close, escape, reopen).
        return "'" + value.Replace("'", "'\\''") + "'";
    }
}
