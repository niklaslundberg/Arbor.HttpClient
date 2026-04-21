namespace Arbor.HttpClient.Core.Models;

public sealed record EnvironmentVariable(string Name, string Value, bool IsEnabled = true);
