namespace Arbor.HttpClient.Core.Variables;

/// <summary>
/// Provides access to system (process) environment variables.
/// Abstracted to allow test isolation without depending on the real process environment.
/// </summary>
public interface ISystemEnvironmentVariableProvider
{
    /// <summary>Gets all system environment variables as a case-insensitive dictionary.</summary>
    IReadOnlyDictionary<string, string> GetAll();
}
