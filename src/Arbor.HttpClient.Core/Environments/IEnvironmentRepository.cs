
namespace Arbor.HttpClient.Core.Environments;

/// <summary>
/// Defines persistence operations for named request environments and their variables.
/// </summary>
public interface IEnvironmentRepository
{
    /// <summary>Creates the underlying storage schema if it does not yet exist.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new environment and returns its generated identifier.
    /// </summary>
    /// <param name="name">Display name for the environment.</param>
    /// <param name="variables">Variables belonging to this environment.</param>
    /// <param name="accentColor">Optional hex accent color used to visually distinguish the environment.</param>
    /// <param name="showWarningBanner">When <see langword="true"/> a warning banner is shown while the environment is active.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The generated integer identifier of the saved environment.</returns>
    Task<int> SaveAsync(string name, IReadOnlyList<EnvironmentVariable> variables, string? accentColor = null, bool showWarningBanner = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites an existing environment identified by <paramref name="environmentId"/>.
    /// </summary>
    /// <param name="environmentId">Identifier of the environment to update.</param>
    /// <param name="name">Updated display name.</param>
    /// <param name="variables">Replacement variable list.</param>
    /// <param name="accentColor">Updated accent color.</param>
    /// <param name="showWarningBanner">Updated warning banner flag.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task UpdateAsync(int environmentId, string name, IReadOnlyList<EnvironmentVariable> variables, string? accentColor = null, bool showWarningBanner = false, CancellationToken cancellationToken = default);

    /// <summary>Returns all stored environments in insertion order.</summary>
    Task<IReadOnlyList<RequestEnvironment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Permanently removes the environment identified by <paramref name="environmentId"/>.</summary>
    Task DeleteAsync(int environmentId, CancellationToken cancellationToken = default);
}
