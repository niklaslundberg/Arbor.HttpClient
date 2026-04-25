using System;
using Arbor.HttpClient.Desktop.ViewModels;
using Avalonia.Interactivity;

namespace Arbor.HttpClient.Desktop.Views;

public partial class LeftPanelView : Avalonia.Controls.UserControl
{
    public LeftPanelView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens a <see cref="WebViewWindow"/> for the scheduled job whose
    /// "Web view" button was clicked.  The window subscribes to the VM so
    /// it refreshes automatically on every completed scheduled tick.
    /// </summary>
    private void OnViewInAppClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.Button { DataContext: ScheduledJobViewModel vm })
        {
            return;
        }

        if (!Uri.TryCreate(vm.Url, UriKind.Absolute, out var uri))
        {
            return;
        }

        var win = new WebViewWindow
        {
            Title = string.IsNullOrWhiteSpace(vm.Name)
                ? "Web View"
                : $"Web View — {vm.Name}"
        };
        win.SubscribeToJob(vm);
        // Show first so the window and its NativeWebView are fully initialised
        // (OnOpened wires up the controls), then navigate.
        win.Show();
        win.Navigate(uri);
    }
}

