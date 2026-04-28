using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Testing.Fakes;

/// <summary>
/// A test double for <see cref="ISystemEnvironmentVariableProvider"/> that returns
/// a fixed dictionary of environment variables instead of reading from the real process environment.
/// </summary>
public sealed class FakeSystemEnvironmentVariableProvider : ISystemEnvironmentVariableProvider
{
    private readonly IReadOnlyDictionary<string, string> _variables;

    /// <summary>Initialises the provider with a predefined set of variables.</summary>
    public FakeSystemEnvironmentVariableProvider(IReadOnlyDictionary<string, string> variables)
    {
        _variables = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Initialises the provider with an empty variable set.</summary>
    public FakeSystemEnvironmentVariableProvider()
    {
        _variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> GetAll() => _variables;
}
