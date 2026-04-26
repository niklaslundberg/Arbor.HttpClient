using Avalonia.Controls;

namespace Arbor.HttpClient.Desktop.Features.About;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
