using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.Options;
using Avalonia.Controls;

namespace Arbor.HttpClient.Desktop.Features.Options;

public partial class OptionsWindow : Window
{
    public OptionsWindow()
    {
        InitializeComponent();
        CategoryTree.SelectionChanged += OnCategoryTreeSelectionChanged;
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void OnCategoryTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CategoryTree.SelectedItem is TreeViewItem { Tag: string tag } &&
            DataContext is MainWindowViewModel vm)
        {
            vm.OptionsPanel.SelectedOptionsPage = tag;
        }
    }
}
