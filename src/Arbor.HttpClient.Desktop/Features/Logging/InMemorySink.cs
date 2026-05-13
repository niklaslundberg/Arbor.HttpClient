using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using System.Reactive.Subjects;

namespace Arbor.HttpClient.Desktop.Features.Logging;

/// <summary>
/// Serilog sink that stores the last <see cref="Capacity"/> log events in memory
/// and publishes new entries through both <see cref="EntryAddedObservable"/> and
/// <see cref="EntryAdded"/> for gradual migration.
/// Thread-safe.
/// </summary>
public sealed class InMemorySink : ILogEventSink, IDisposable
{
    private const int Capacity = 1000;

    private readonly MessageTemplateTextFormatter _formatter = new("{Message:lj}{NewLine}{Exception}", null);
    private readonly Queue<LogEntry> _entries = new();
    private readonly Subject<LogEntry> _entryAddedSubject = new();
    private readonly object _lock = new();

    public event EventHandler<LogEntry>? EntryAdded;
    public IObservable<LogEntry> EntryAddedObservable => _entryAddedSubject;

    public IReadOnlyList<LogEntry> GetSnapshot()
    {
        lock (_lock)
        {
            return [.. _entries];
        }
    }

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);
        var message = writer.ToString().TrimEnd();

        var entry = new LogEntry(
            logEvent.Timestamp,
            logEvent.Level.ToString(),
            message,
            GetTab(logEvent));

        lock (_lock)
        {
            if (_entries.Count >= Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }

        _entryAddedSubject.OnNext(entry);
        EntryAdded?.Invoke(this, entry);
    }

    public void Dispose()
    {
        _entryAddedSubject.OnCompleted();
        _entryAddedSubject.Dispose();
    }

    private static string GetTab(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("LogTab", out var tabProperty) &&
            tabProperty is ScalarValue { Value: string tabValue } &&
            !string.IsNullOrWhiteSpace(tabValue))
        {
            return tabValue;
        }

        return LogTab.Debug;
    }
}
