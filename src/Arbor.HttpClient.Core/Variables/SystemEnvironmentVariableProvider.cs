using System.Collections;

namespace Arbor.HttpClient.Core.Variables;

/// <summary>
/// Returns system (process) environment variables from <see cref="Environment.GetEnvironmentVariables"/>.
/// </summary>
public sealed class SystemEnvironmentVariableProvider : ISystemEnvironmentVariableProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> GetAll()
    {
        var envVars = Environment.GetEnvironmentVariables();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in envVars)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                result[key] = value;
            }
        }

        return result;
    }
}
