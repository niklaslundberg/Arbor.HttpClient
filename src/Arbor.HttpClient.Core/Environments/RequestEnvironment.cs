namespace Arbor.HttpClient.Core.Environments;

public sealed record RequestEnvironment(
    int Id,
    string Name,
    IReadOnlyList<EnvironmentVariable> Variables,
    string? AccentColor = null,
    bool ShowWarningBanner = false);
