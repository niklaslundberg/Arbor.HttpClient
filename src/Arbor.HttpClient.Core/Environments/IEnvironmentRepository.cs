
namespace Arbor.HttpClient.Core.Environments;

public interface IEnvironmentRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<int> SaveAsync(string name, IReadOnlyList<EnvironmentVariable> variables, string? accentColor = null, bool showWarningBanner = false, CancellationToken cancellationToken = default);

    Task UpdateAsync(int environmentId, string name, IReadOnlyList<EnvironmentVariable> variables, string? accentColor = null, bool showWarningBanner = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequestEnvironment>> GetAllAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(int environmentId, CancellationToken cancellationToken = default);
}
