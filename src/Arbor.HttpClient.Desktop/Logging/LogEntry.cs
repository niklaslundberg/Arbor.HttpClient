namespace Arbor.HttpClient.Desktop.Logging;

public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Message, string Tab);
