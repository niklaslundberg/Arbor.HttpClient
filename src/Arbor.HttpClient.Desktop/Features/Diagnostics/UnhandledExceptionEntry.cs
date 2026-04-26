namespace Arbor.HttpClient.Desktop.Features.Diagnostics;

/// <summary>Represents a single collected unhandled exception.</summary>
public sealed class UnhandledExceptionEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required DateTimeOffset Timestamp { get; init; }

    public required string ExceptionType { get; init; }

    public required string Message { get; init; }

    public required string StackTrace { get; init; }
}
