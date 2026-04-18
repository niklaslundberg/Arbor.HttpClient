using Arbor.HttpClient.Desktop.ViewModels;
using Avalonia.Controls;
using System.Collections.Specialized;

namespace Arbor.HttpClient.Desktop.Views;

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
        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged -= OnScheduledEntriesChanged;
            _viewModel.HttpDiagnosticsEntries.CollectionChanged -= OnHttpDiagnosticsEntriesChanged;
            _viewModel.HttpRequestEntries.CollectionChanged -= OnHttpRequestEntriesChanged;
            _viewModel.DebugEntries.CollectionChanged -= OnDebugEntriesChanged;
        }

        _viewModel = (DataContext as LogPanelViewModel)?.App.LogWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged += OnScheduledEntriesChanged;
            _viewModel.HttpDiagnosticsEntries.CollectionChanged += OnHttpDiagnosticsEntriesChanged;
            _viewModel.HttpRequestEntries.CollectionChanged += OnHttpRequestEntriesChanged;
            _viewModel.DebugEntries.CollectionChanged += OnDebugEntriesChanged;
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
            if (last is not null)
            {
                list.ScrollIntoView(last);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged -= OnScheduledEntriesChanged;
            _viewModel.HttpDiagnosticsEntries.CollectionChanged -= OnHttpDiagnosticsEntriesChanged;
            _viewModel.HttpRequestEntries.CollectionChanged -= OnHttpRequestEntriesChanged;
            _viewModel.DebugEntries.CollectionChanged -= OnDebugEntriesChanged;
        }

        base.OnDetachedFromVisualTree(e);
    }
}
