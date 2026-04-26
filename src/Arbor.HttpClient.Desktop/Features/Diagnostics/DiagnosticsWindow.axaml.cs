using Avalonia.Controls;

namespace Arbor.HttpClient.Desktop.Features.Diagnostics;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
