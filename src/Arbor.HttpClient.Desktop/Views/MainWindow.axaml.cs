using System.Linq;
using Arbor.HttpClient.Desktop.ViewModels;

namespace Arbor.HttpClient.Desktop.Views;

public partial class MainWindow : Avalonia.Controls.Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.StorageProvider = StorageProvider;
            viewModel.OpenLogWindowAction = OpenLogWindow;
        }
    }

    private void OpenLogWindow()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
        {
            var existing = lifetime.Windows.OfType<LogWindow>().FirstOrDefault();
            if (existing is not null)
            {
                existing.Activate();
                return;
            }
        }

        new LogWindow { DataContext = vm.LogWindowViewModel }.Show(this);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
    }
}

