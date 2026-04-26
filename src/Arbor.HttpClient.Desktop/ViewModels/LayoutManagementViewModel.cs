using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class LayoutManagementViewModel : Tool
{
    public LayoutManagementViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "layout-management";
        Title = "Layout";
    }

    public MainWindowViewModel App { get; }

    [RelayCommand]
    private void SaveLayoutAsNew() => App.SaveLayoutAsNewCommand.Execute(null);

    [RelayCommand]
    private void SaveLayoutToExisting() => App.SaveLayoutToExistingCommand.Execute(App.SelectedLayoutName);

    [RelayCommand]
    private void RemoveLayout() => App.RemoveLayoutCommand.Execute(App.SelectedLayoutName);

    [RelayCommand]
    private void RestoreDefaultLayout() => App.RestoreDefaultLayoutCommand.Execute(null);
}
