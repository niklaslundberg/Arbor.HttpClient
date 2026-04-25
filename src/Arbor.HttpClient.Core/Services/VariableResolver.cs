using System.Text.RegularExpressions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Services;

public sealed class VariableResolver
{
    private static readonly Regex TokenPattern = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    public string Resolve(string input, IReadOnlyList<EnvironmentVariable> variables)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var lookup = variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase);
        return TokenPattern.Replace(input, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return lookup.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }
}
