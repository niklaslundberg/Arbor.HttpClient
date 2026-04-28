using System;
using System.Collections.Generic;
using System.Linq;
using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Desktop.Features.Variables;

public static class VariableCompletionEngine
{
    private static readonly string EnvPrefix = VariableResolver.EnvPrefix;

    public static bool TryGetContext(string text, int caretOffset, out VariableCompletionContext context)
    {
        context = default;
        if (string.IsNullOrEmpty(text) || caretOffset < 0 || caretOffset > text.Length)
        {
            return false;
        }

        var beforeCaret = text[..caretOffset];
        var tokenStart = beforeCaret.LastIndexOf("{{", StringComparison.Ordinal);
        if (tokenStart < 0)
        {
            return false;
        }

        var lastTokenEnd = beforeCaret.LastIndexOf("}}", StringComparison.Ordinal);
        if (lastTokenEnd > tokenStart)
        {
            return false;
        }

        var prefixStart = tokenStart + 2;
        var rawPrefix = text[prefixStart..caretOffset];
        if (rawPrefix.IndexOfAny(['{', '}', '\r', '\n']) >= 0)
        {
            return false;
        }

        // Detect whether we are inside an {{env:...}} token.
        if (rawPrefix.StartsWith(EnvPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var envNameStart = prefixStart + EnvPrefix.Length;
            var envNamePrefix = text[envNameStart..caretOffset];
            context = new VariableCompletionContext(envNameStart, envNamePrefix, IsEnvVariable: true);
            return true;
        }

        context = new VariableCompletionContext(prefixStart, rawPrefix, IsEnvVariable: false);
        return true;
    }

    public static List<string> GetSuggestions(IEnumerable<string> variableNames, string prefix)
    {
        var effectivePrefix = prefix ?? string.Empty;
        return variableNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => name.StartsWith(effectivePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string BuildInsertionText(string fullText, int endOffset, string variableName)
    {
        var hasClosingBraces =
            endOffset + 1 < fullText.Length &&
            fullText[endOffset] == '}' &&
            fullText[endOffset + 1] == '}';

        if (!hasClosingBraces)
        {
            hasClosingBraces = HasClosingBracesLaterInCurrentToken(fullText, endOffset);
        }

        return hasClosingBraces ? variableName : string.Concat(variableName, "}}");
    }

    private static bool HasClosingBracesLaterInCurrentToken(string fullText, int endOffset)
    {
        if (endOffset < 0 || endOffset >= fullText.Length)
        {
            return false;
        }

        var closingOffset = fullText.IndexOf("}}", endOffset, StringComparison.Ordinal);
        if (closingOffset < 0)
        {
            return false;
        }

        for (var index = endOffset; index < closingOffset; index++)
        {
            var ch = fullText[index];
            if (ch is '{' or '}' or '\r' or '\n')
            {
                return false;
            }
        }

        return true;
    }
}

public readonly record struct VariableCompletionContext(int ReplaceStartOffset, string Prefix, bool IsEnvVariable);
