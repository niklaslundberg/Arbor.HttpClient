using Arbor.HttpClient.Desktop.ViewModels;
using Avalonia.Controls;

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
            viewModel.ExitApplicationAction = Close;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Persist the layout snapshot BEFORE any window teardown so floating
        // window positions are captured while they are still alive.  Then close
        // the floating dock windows explicitly so Avalonia doesn't try to close
        // them again as owned windows, which would cause a NullReferenceException
        // inside the Dock factory cleanup.
        if (!e.Cancel && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PersistCurrentLayout();
            viewModel.CloseFloatingWindows();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
    }
}
