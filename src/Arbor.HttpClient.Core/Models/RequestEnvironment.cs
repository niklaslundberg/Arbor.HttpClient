namespace Arbor.HttpClient.Core.Models;

public sealed record RequestEnvironment(int Id, string Name, IReadOnlyList<EnvironmentVariable> Variables);
