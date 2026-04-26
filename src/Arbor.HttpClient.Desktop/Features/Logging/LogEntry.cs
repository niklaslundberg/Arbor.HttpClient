namespace Arbor.HttpClient.Desktop.Features.Logging;

public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Message, string Tab);
