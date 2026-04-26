namespace Arbor.HttpClient.Core.Environments;

public sealed record RequestEnvironment(int Id, string Name, IReadOnlyList<EnvironmentVariable> Variables);
