using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Arbor.HttpClient.Desktop.Logging;

/// <summary>
/// Serilog sink that stores the last <see cref="Capacity"/> log events in memory
/// and raises <see cref="EntryAdded"/> when a new one arrives.
/// Thread-safe.
/// </summary>
public sealed class InMemorySink : ILogEventSink, IDisposable
{
    private const int Capacity = 1000;

    private readonly MessageTemplateTextFormatter _formatter = new("{Message:lj}{NewLine}{Exception}", null);
    private readonly Queue<LogEntry> _entries = new();
    private readonly object _lock = new();

    public event EventHandler<LogEntry>? EntryAdded;

    public IReadOnlyList<LogEntry> GetSnapshot()
    {
        lock (_lock)
        {
            return [.. _entries];
        }
    }

    public void Emit(LogEvent logEvent)
    {
        using var writer = new System.IO.StringWriter();
        _formatter.Format(logEvent, writer);
        var message = writer.ToString().TrimEnd();

        var entry = new LogEntry(
            logEvent.Timestamp,
            logEvent.Level.ToString(),
            message);

        lock (_lock)
        {
            if (_entries.Count >= Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }

        EntryAdded?.Invoke(this, entry);
    }

    public void Dispose()
    {
        // Nothing to release
    }
}
