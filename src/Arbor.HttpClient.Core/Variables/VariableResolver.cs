using System.Text.RegularExpressions;
using Arbor.HttpClient.Core.Environments;

namespace Arbor.HttpClient.Core.Variables;

public sealed class VariableResolver
{
    /// <summary>The prefix used to reference system environment variables, e.g. <c>{{env:PATH}}</c>.</summary>
    public const string EnvPrefix = "env:";

    private static readonly Regex TokenPattern = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    private readonly ISystemEnvironmentVariableProvider _environmentVariableProvider;

    /// <summary>Initialises the resolver using the real process environment variables.</summary>
    public VariableResolver() : this(new SystemEnvironmentVariableProvider())
    {
    }

    /// <summary>Initialises the resolver with the supplied environment variable provider (allows test injection).</summary>
    public VariableResolver(ISystemEnvironmentVariableProvider environmentVariableProvider)
    {
        _environmentVariableProvider = environmentVariableProvider;
    }

    public string Resolve(string input, IReadOnlyList<EnvironmentVariable> variables)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var lookup = variables
            .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

        IReadOnlyDictionary<string, string>? envLookup = null;

        return TokenPattern.Replace(input, match =>
        {
            var key = match.Groups[1].Value.Trim();

            if (key.StartsWith(EnvPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var envKey = key[EnvPrefix.Length..].Trim();
                envLookup ??= _environmentVariableProvider.GetAll();
                return envLookup.TryGetValue(envKey, out var envValue) ? envValue : string.Empty;
            }

            return lookup.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }
}
