using Avalonia.Controls;
using Arbor.HttpClient.Desktop.ViewModels;

namespace Arbor.HttpClient.Desktop.Views;

public partial class LogWindow : Window
{
    private LogWindowViewModel? _viewModel;

    public LogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        _viewModel = DataContext as LogWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged += OnEntriesChanged;
        }
    }

    private void OnEntriesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel?.AutoScroll != true)
        {
            return;
        }

        var list = this.FindControl<ListBox>("LogList");
        if (list?.ItemsSource is System.Collections.IEnumerable items)
        {
            var last = items.Cast<object>().LastOrDefault();
            if (last is not null)
            {
                list.ScrollIntoView(last);
            }
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
        }

        base.OnClosed(e);
    }
}
