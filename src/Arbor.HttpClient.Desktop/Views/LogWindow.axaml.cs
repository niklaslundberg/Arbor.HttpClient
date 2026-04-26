using Arbor.HttpClient.Desktop.ViewModels;
using Avalonia.Controls;

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

        if (_viewModel is { } vm)
        {
            vm.Entries.CollectionChanged += OnEntriesChanged;
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
            if (last is { } lastItem)
            {
                list.ScrollIntoView(lastItem);
            }
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        if (_viewModel is { } oldVm)
        {
            oldVm.Entries.CollectionChanged -= OnEntriesChanged;
        }

        base.OnClosed(e);
    }
}
