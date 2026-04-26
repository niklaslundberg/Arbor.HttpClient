using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Arbor.HttpClient.Desktop.Features.Main;

namespace Arbor.HttpClient.Desktop.Features.Layout;

public sealed class LayoutManagementViewModel : Tool
{
    public LayoutManagementViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "layout-management";
        Title = "Layout";
    }

    public MainWindowViewModel App { get; }

    public IRelayCommand SaveLayoutAsNewCommand => App.SaveLayoutAsNewCommand;

    public IRelayCommand<string?> SaveLayoutToExistingCommand => App.SaveLayoutToExistingCommand;

    public IRelayCommand<string?> RemoveLayoutCommand => App.RemoveLayoutCommand;

    public IRelayCommand RestoreDefaultLayoutCommand => App.RestoreDefaultLayoutCommand;
}
