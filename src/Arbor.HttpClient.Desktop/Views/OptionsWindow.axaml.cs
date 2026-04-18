using Avalonia.Controls;
using Arbor.HttpClient.Desktop.ViewModels;

namespace Arbor.HttpClient.Desktop.Views;

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
            vm.SelectedOptionsPage = tag;
        }
    }
}
