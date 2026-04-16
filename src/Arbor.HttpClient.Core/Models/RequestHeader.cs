namespace Arbor.HttpClient.Core.Models;

public sealed record RequestHeader(string Name, string Value, bool IsEnabled = true);
