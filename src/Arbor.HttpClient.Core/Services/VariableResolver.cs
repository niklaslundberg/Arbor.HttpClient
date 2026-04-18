using System.Text.RegularExpressions;
using Arbor.HttpClient.Core.Models;

namespace Arbor.HttpClient.Core.Services;

public sealed class VariableResolver
{
    private static readonly Regex TokenPattern = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    public string Resolve(string input, IReadOnlyList<EnvironmentVariable> variables)
    {
        if (string.IsNullOrEmpty(input) || variables.Count == 0)
        {
            return input;
        }

        return TokenPattern.Replace(input, match =>
        {
            var key = match.Groups[1].Value.Trim();
            foreach (var variable in variables)
            {
                if (string.Equals(variable.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return variable.Value;
                }
            }

            return match.Value;
        });
    }
}
