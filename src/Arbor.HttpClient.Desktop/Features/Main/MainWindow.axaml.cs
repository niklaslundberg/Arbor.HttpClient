using Arbor.HttpClient.Desktop.Features.About;
using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.Main;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Dock.Controls.ProportionalStackPanel;
using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Main;

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
            viewModel.Clipboard = Clipboard;
            viewModel.ExitApplicationAction = Close;
            viewModel.OpenAboutWindowAction = () =>
            {
                var aboutWindow = new AboutWindow { DataContext = new AboutWindowViewModel() };
                _ = aboutWindow.ShowDialog(this);
            };
            viewModel.OpenDiagnosticsWindowAction = () =>
            {
                if (viewModel.UnhandledExceptionCollector is { } collector)
                {
                    var vm = new DiagnosticsViewModel(collector);
                    var diagnosticsWindow = new DiagnosticsWindow { DataContext = vm };
                    _ = diagnosticsWindow.ShowDialog(this);
                }
            };
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
            // Walk the visual tree and read the actual rendered proportions from
            // ProportionalStackPanel.ProportionProperty, then write them back to
            // the model dockables.  This is the reliable source of truth for
            // proportion values after user-initiated splitter drags.
            SyncDockProportionsFromVisuals();

            // Record window geometry so it can be included in the saved snapshot.
            viewModel.SetWindowGeometry(Width, Height, (int)Position.X, (int)Position.Y);

            viewModel.PersistCurrentLayout();
            viewModel.CloseFloatingWindows();
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Walks the visual tree and for every control that is a direct child of a
    /// <see cref="ProportionalStackPanel"/> and has an <see cref="IDockable"/> as its
    /// <see cref="Avalonia.StyledElement.DataContext"/>, reads the attached
    /// <see cref="ProportionalStackPanel.ProportionProperty"/> value and writes it back
    /// to <see cref="IDockable.Proportion"/>.
    /// <para>
    /// This is the reliable way to capture the user's actual splitter positions because
    /// the Avalonia binding (<c>TwoWay</c> by default for <see cref="ProportionalStackPanel.ProportionProperty"/>)
    /// may not always propagate visual changes back to the model in time for the closing handler.
    /// Reading directly from the visual layer guarantees the saved values reflect what the
    /// user actually sees on screen.
    /// </para>
    /// </summary>
    private void SyncDockProportionsFromVisuals()
    {
        foreach (var visual in this.GetVisualDescendants())
        {
            if (visual is not Control control) continue;
            if (control.DataContext is not IDockable dockable) continue;
            if (control.Parent is not ProportionalStackPanel) continue;

            var proportion = ProportionalStackPanel.GetProportion(control);
            if (double.IsNaN(proportion) || proportion <= 0) continue;

            dockable.Proportion = proportion;
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
    }
}
