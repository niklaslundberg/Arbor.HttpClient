using System.Collections.Specialized;
using Arbor.HttpClient.Desktop.Features.Logging;
using Avalonia.Controls;

namespace Arbor.HttpClient.Desktop.Features.Logging;

public partial class LogPanelView : UserControl
{
    private LogWindowViewModel? _viewModel;

    public LogPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is { } prevVm)
        {
            prevVm.Entries.CollectionChanged -= OnScheduledEntriesChanged;
            prevVm.HttpDiagnosticsEntries.CollectionChanged -= OnHttpDiagnosticsEntriesChanged;
            prevVm.HttpRequestEntries.CollectionChanged -= OnHttpRequestEntriesChanged;
            prevVm.DebugEntries.CollectionChanged -= OnDebugEntriesChanged;
        }

        _viewModel = (DataContext as LogPanelViewModel)?.App.LogWindowViewModel;

        if (_viewModel is { } newVm)
        {
            newVm.Entries.CollectionChanged += OnScheduledEntriesChanged;
            newVm.HttpDiagnosticsEntries.CollectionChanged += OnHttpDiagnosticsEntriesChanged;
            newVm.HttpRequestEntries.CollectionChanged += OnHttpRequestEntriesChanged;
            newVm.DebugEntries.CollectionChanged += OnDebugEntriesChanged;
        }
    }

    private void OnScheduledEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToLast("ScheduledLogList");

    private void OnHttpDiagnosticsEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToLast("DiagnosticsLogList");

    private void OnHttpRequestEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToLast("RequestLogList");

    private void OnDebugEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToLast("DebugLogList");

    private void ScrollToLast(string listName)
    {
        if (_viewModel?.AutoScroll != true)
        {
            return;
        }

        var list = this.FindControl<ListBox>(listName);
        if (list?.ItemsSource is System.Collections.IEnumerable items)
        {
            var last = items.Cast<object>().LastOrDefault();
            if (last is { } lastItem)
            {
                list.ScrollIntoView(lastItem);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is { } detachedVm)
        {
            detachedVm.Entries.CollectionChanged -= OnScheduledEntriesChanged;
            detachedVm.HttpDiagnosticsEntries.CollectionChanged -= OnHttpDiagnosticsEntriesChanged;
            detachedVm.HttpRequestEntries.CollectionChanged -= OnHttpRequestEntriesChanged;
            detachedVm.DebugEntries.CollectionChanged -= OnDebugEntriesChanged;
        }

        base.OnDetachedFromVisualTree(e);
    }
}
