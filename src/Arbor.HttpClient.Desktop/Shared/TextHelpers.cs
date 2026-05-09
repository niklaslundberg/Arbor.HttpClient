using System;

namespace Arbor.HttpClient.Desktop.Shared;

/// <summary>
/// Text utility methods shared across UI components.
/// </summary>
internal static class TextHelpers
{
    /// <summary>
    /// Returns <paramref name="text"/> with all carriage-return and line-feed characters removed.
    /// Used to enforce single-line constraints on text fields that use <c>AvaloniaEdit.TextEditor</c>.
    /// </summary>
    internal static string StripNewlines(string text) =>
        text.Contains('\n') || text.Contains('\r')
            ? text.Replace("\r\n", string.Empty, StringComparison.Ordinal)
                  .Replace("\r", string.Empty, StringComparison.Ordinal)
                  .Replace("\n", string.Empty, StringComparison.Ordinal)
            : text;
}
