using System.Collections.ObjectModel;
using Arbor.HttpClient.Desktop.Logging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class LogWindowViewModel : ViewModelBase, IDisposable
{
    private const int MaxDisplayEntries = 1000;

    private readonly InMemorySink _sink;

    [ObservableProperty]
    private bool _autoScroll = true;

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public LogWindowViewModel(InMemorySink sink)
    {
        _sink = sink;

        // Populate with existing entries
        foreach (var entry in _sink.GetSnapshot())
        {
            Entries.Add(entry);
        }

        _sink.EntryAdded += OnEntryAdded;
    }

    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Entries.Count >= MaxDisplayEntries)
            {
                Entries.RemoveAt(0);
            }

            Entries.Add(entry);
        });
    }

    public void Dispose()
    {
        _sink.EntryAdded -= OnEntryAdded;
    }
}
