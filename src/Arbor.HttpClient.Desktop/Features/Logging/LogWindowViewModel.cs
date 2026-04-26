using System.Collections.ObjectModel;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Shared;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arbor.HttpClient.Desktop.Features.Logging;

public sealed partial class LogWindowViewModel : ViewModelBase, IDisposable
{
    private const int MaxDisplayEntries = 1000;

    private readonly InMemorySink _sink;

    [ObservableProperty]
    private bool _autoScroll = true;

    public ObservableCollection<LogEntry> Entries { get; } = [];
    public ObservableCollection<LogEntry> HttpDiagnosticsEntries { get; } = [];
    public ObservableCollection<LogEntry> HttpRequestEntries { get; } = [];
    public ObservableCollection<LogEntry> DebugEntries { get; } = [];

    public LogWindowViewModel(InMemorySink sink)
    {
        _sink = sink;

        // Populate with existing entries
        foreach (var entry in _sink.GetSnapshot())
        {
            AddEntry(entry);
        }

        _sink.EntryAdded += OnEntryAdded;
    }

    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddEntry(entry);
        });
    }

    public void Dispose()
    {
        _sink.EntryAdded -= OnEntryAdded;
    }

    private void AddEntry(LogEntry entry)
    {
        switch (entry.Tab)
        {
            case LogTab.ScheduledLive:
                AddWithCapacity(Entries, entry);
                break;
            case LogTab.HttpDiagnostics:
                AddWithCapacity(HttpDiagnosticsEntries, entry);
                break;
            case LogTab.HttpRequests:
                AddWithCapacity(HttpRequestEntries, entry);
                break;
            case LogTab.Debug:
            default:
                AddWithCapacity(DebugEntries, entry);
                break;
        }
    }

    private static void AddWithCapacity(ObservableCollection<LogEntry> target, LogEntry entry)
    {
        if (target.Count >= MaxDisplayEntries)
        {
            target.RemoveAt(0);
        }

        target.Add(entry);
    }
}
