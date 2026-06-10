using System.Windows.Input;
using Dock.Model.ReactiveUI.Controls;
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

    public ICommand SaveLayoutAsNewCommand => App.SaveLayoutAsNewCommand;

    public ICommand SaveLayoutToExistingCommand => App.SaveLayoutToExistingCommand;

    public ICommand RemoveLayoutCommand => App.RemoveLayoutCommand;

    public ICommand RestoreDefaultLayoutCommand => App.RestoreDefaultLayoutCommand;
}
