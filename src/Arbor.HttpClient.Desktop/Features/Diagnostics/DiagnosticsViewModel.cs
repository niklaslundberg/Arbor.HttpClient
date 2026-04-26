using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Arbor.HttpClient.Desktop.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arbor.HttpClient.Desktop.Features.Diagnostics;

public sealed partial class DiagnosticsViewModel : ViewModelBase
{
    private const string GitHubNewIssueBaseUrl =
        "https://github.com/niklaslundberg/Arbor.HttpClient/issues/new";

    private readonly UnhandledExceptionCollector _collector;

    public DiagnosticsViewModel(UnhandledExceptionCollector collector)
    {
        _collector = collector;
        Refresh();
    }

    public ObservableCollection<UnhandledExceptionEntryViewModel> Entries { get; } = [];

    [ObservableProperty]
    private bool _hasEntries;

    /// <summary>Refreshes the entries list from the collector.</summary>
    public void Refresh()
    {
        Entries.Clear();
        foreach (var entry in _collector.GetAll())
        {
            Entries.Add(new UnhandledExceptionEntryViewModel(entry, this));
        }

        HasEntries = Entries.Count > 0;
    }

    [RelayCommand]
    private void ClearAll()
    {
        _collector.Clear();
        Refresh();
    }

    internal void Dismiss(string id)
    {
        _collector.Remove(id);
        Refresh();
    }

    internal void ReportOnGitHub(UnhandledExceptionEntry entry)
    {
        var title = Uri.EscapeDataString(
            $"Unhandled exception: {entry.ExceptionType}");

        var body = BuildIssueBody(entry);
        var encodedBody = Uri.EscapeDataString(body);

        var url = $"{GitHubNewIssueBaseUrl}?title={title}&body={encodedBody}";

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException or PlatformNotSupportedException)
        {
            // No default browser or unsupported platform — silently ignore.
        }
    }

    private static string BuildIssueBody(UnhandledExceptionEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Unhandled exception report");
        sb.AppendLine();
        sb.AppendLine($"**Timestamp (UTC):** {entry.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Type:** `{entry.ExceptionType}`");
        sb.AppendLine($"**Message:** {entry.Message}");
        sb.AppendLine();
        sb.AppendLine("**Stack trace:**");
        sb.AppendLine("```");
        sb.AppendLine(entry.StackTrace);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*Reported automatically from the application diagnostics panel.*");
        return sb.ToString();
    }
}

/// <summary>Wraps a single <see cref="UnhandledExceptionEntry"/> for display in the diagnostics list.</summary>
public sealed partial class UnhandledExceptionEntryViewModel : ViewModelBase
{
    private readonly DiagnosticsViewModel _parent;

    public UnhandledExceptionEntryViewModel(UnhandledExceptionEntry entry, DiagnosticsViewModel parent)
    {
        Entry = entry;
        _parent = parent;
    }

    public UnhandledExceptionEntry Entry { get; }

    public string DisplayTimestamp => Entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string DisplayType => Entry.ExceptionType;

    public string DisplayMessage => Entry.Message;

    public string DisplayStackTrace => Entry.StackTrace;

    [RelayCommand]
    private void Dismiss() => _parent.Dismiss(Entry.Id);

    [RelayCommand]
    private void ReportOnGitHub() => _parent.ReportOnGitHub(Entry);
}
