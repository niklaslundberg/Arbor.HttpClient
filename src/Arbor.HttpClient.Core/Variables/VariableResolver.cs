using System.Globalization;
using System.Text.RegularExpressions;
using Arbor.HttpClient.Core.Environments;

namespace Arbor.HttpClient.Core.Variables;

public sealed class VariableResolver
{
    /// <summary>The prefix used to reference system environment variables, e.g. <c>{{env:PATH}}</c>.</summary>
    public const string EnvPrefix = "env:";
    /// <summary>The prefix used to reference computed values, e.g. <c>{{c:TimeStampUtc:yyyyMMdd}}</c>.</summary>
    public const string ComputedPrefix = "c:";

    private const string TimeStampLocalName = "TimeStampLocal";
    private const string TimeStampUtcName = "TimeStampUtc";
    private const string DefaultTimeStampFormat = "o";
    private const string InvalidTimeStampFormatText = "invalidTimeStampFormat";

    private static readonly Regex TokenPattern = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    private readonly ISystemEnvironmentVariableProvider _environmentVariableProvider;

    /// <summary>Initialises the resolver using the real process environment variables.</summary>
    public VariableResolver() : this(new SystemEnvironmentVariableProvider())
    {
    }

    /// <summary>Initialises the resolver with the supplied environment variable provider (allows test injection).</summary>
    public VariableResolver(ISystemEnvironmentVariableProvider environmentVariableProvider)
    {
        ArgumentNullException.ThrowIfNull(environmentVariableProvider);
        _environmentVariableProvider = environmentVariableProvider;
    }

    public string Resolve(string input, IReadOnlyList<EnvironmentVariable> variables)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var lookup = variables
            .Where(v => !v.IsExpired)
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

            if (key.StartsWith(ComputedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var computedValue = key[ComputedPrefix.Length..].Trim();
                var formatSeparator = computedValue.IndexOf(':');
                var computedName = formatSeparator >= 0
                    ? computedValue[..formatSeparator].Trim()
                    : computedValue;
                var dateTimeFormat = formatSeparator >= 0
                    ? computedValue[(formatSeparator + 1)..].Trim()
                    : DefaultTimeStampFormat;

                if (string.IsNullOrWhiteSpace(dateTimeFormat))
                {
                    dateTimeFormat = DefaultTimeStampFormat;
                }

                DateTimeOffset? timestamp = computedName switch
                {
                    TimeStampLocalName => DateTimeOffset.Now,
                    TimeStampUtcName => DateTimeOffset.UtcNow,
                    _ => null
                };

                if (timestamp is null)
                {
                    return string.Empty;
                }

                try
                {
                    return timestamp.Value.ToString(dateTimeFormat, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    return InvalidTimeStampFormatText;
                }
            }

            return lookup.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }
}
