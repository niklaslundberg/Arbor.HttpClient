using Avalonia.Controls;
using Arbor.HttpClient.Desktop.ViewModels;

namespace Arbor.HttpClient.Desktop.Views;

public partial class OptionsView : UserControl
{
    public OptionsView()
    {
        InitializeComponent();
        CategoryTree.SelectionChanged += OnCategoryTreeSelectionChanged;
    }

    private void OnCategoryTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CategoryTree.SelectedItem is TreeViewItem { Tag: string tag } &&
            DataContext is OptionsViewModel vm)
        {
            vm.SelectedOptionsPage = tag;
        }
    }
}
