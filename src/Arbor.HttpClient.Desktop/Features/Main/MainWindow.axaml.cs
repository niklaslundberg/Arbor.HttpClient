using Arbor.HttpClient.Desktop.Features.About;
using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Localization;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Dock.Controls.ProportionalStackPanel;
using Dock.Model.Core;

namespace Arbor.HttpClient.Desktop.Features.Main;

public partial class MainWindow : Window
{
    private bool _isCloseConfirmationInProgress;
    private bool _allowCloseWithPendingInheritedHeaders;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
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
        if (!e.Cancel
            && !_allowCloseWithPendingInheritedHeaders
            && !_isCloseConfirmationInProgress
            && DataContext is MainWindowViewModel viewModel
            && viewModel.HasPendingCollectionInheritedHeadersAutoSave)
        {
            e.Cancel = true;
            _isCloseConfirmationInProgress = true;
            _ = ConfirmCloseWithPendingInheritedHeadersAsync(viewModel);
            return;
        }

        // Persist the layout snapshot BEFORE any window teardown so floating
        // window positions are captured while they are still alive.  Then close
        // the floating dock windows explicitly so Avalonia doesn't try to close
        // them again as owned windows, which would cause a NullReferenceException
        // inside the Dock factory cleanup.
        if (!e.Cancel && DataContext is MainWindowViewModel currentViewModel)
        {
            // Walk the visual tree and read the actual rendered proportions from
            // ProportionalStackPanel.ProportionProperty, then write them back to
            // the model dockables.  This is the reliable source of truth for
            // proportion values after user-initiated splitter drags.
            SyncDockProportionsFromVisuals();

            // Record window geometry so it can be included in the saved snapshot.
            currentViewModel.SetWindowGeometry(Width, Height, Position.X, Position.Y);

            currentViewModel.PersistCurrentLayout();
            currentViewModel.CloseFloatingWindows();
        }

        base.OnClosing(e);
    }

    private async Task ConfirmCloseWithPendingInheritedHeadersAsync(MainWindowViewModel viewModel)
    {
        try
        {
            var shouldClose = await ShowPendingInheritedHeadersWarningAsync();
            if (!shouldClose)
            {
                return;
            }

            await viewModel.FlushPendingCollectionInheritedHeadersAutoSaveAsync();
            _allowCloseWithPendingInheritedHeaders = true;
            Close();
        }
        finally
        {
            _isCloseConfirmationInProgress = false;
            _allowCloseWithPendingInheritedHeaders = false;
        }
    }

    private async Task<bool> ShowPendingInheritedHeadersWarningAsync()
    {
        var messageWindow = new Window
        {
            Width = 460,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = Strings.PendingInheritedHeadersCloseWarningTitle,
            Content = new Grid
            {
                Margin = new Avalonia.Thickness(16),
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = Strings.PendingInheritedHeadersCloseWarningMessage,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Margin = new Avalonia.Thickness(0, 16, 0, 0),
                        [Grid.RowProperty] = 1,
                        Children =
                        {
                            new Button
                            {
                                Content = Strings.PendingInheritedHeadersCloseWarningCancel,
                                MinWidth = 96
                            },
                            new Button
                            {
                                Content = Strings.PendingInheritedHeadersCloseWarningClose,
                                MinWidth = 96
                            }
                        }
                    }
                }
            }
        };

        var buttonPanel = ((StackPanel)((Grid)messageWindow.Content!).Children[1]);
        var cancelButton = (Button)buttonPanel.Children[0];
        var closeButton = (Button)buttonPanel.Children[1];

        cancelButton.Click += (_, _) => messageWindow.Close(false);
        closeButton.Click += (_, _) => messageWindow.Close(true);

        return await messageWindow.ShowDialog<bool>(this);
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
        foreach (var control in this.GetVisualDescendants()
                     .OfType<Control>()
                     .Where(c => c is { DataContext: IDockable, Parent: ProportionalStackPanel }))
        {
            var dockable = (IDockable)control.DataContext!;
            var proportion = ProportionalStackPanel.GetProportion(control);
            if (!double.IsFinite(proportion) || proportion <= 0)
            {
                continue;
            }

            dockable.Proportion = proportion;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}
