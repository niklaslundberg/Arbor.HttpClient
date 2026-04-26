namespace Arbor.HttpClient.Core.HttpRequest;

public sealed record RequestHeader(string Name, string Value, bool IsEnabled = true);
