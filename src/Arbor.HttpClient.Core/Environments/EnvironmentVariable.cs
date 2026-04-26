namespace Arbor.HttpClient.Core.Environments;

public sealed record EnvironmentVariable(string Name, string Value, bool IsEnabled = true);
