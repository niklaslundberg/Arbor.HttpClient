using System.Text.RegularExpressions;
using Arbor.HttpClient.Core.Environments;

namespace Arbor.HttpClient.Core.Variables;

public sealed class VariableResolver
{
    private static readonly Regex TokenPattern = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    public string Resolve(string input, IReadOnlyList<EnvironmentVariable> variables)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var lookup = variables
            .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);
        return TokenPattern.Replace(input, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return lookup.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }
}
