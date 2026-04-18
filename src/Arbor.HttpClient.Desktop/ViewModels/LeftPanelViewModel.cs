using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed class LeftPanelViewModel : Tool
{
    public LeftPanelViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "left-panel";
        Title = "Explorer";
    }

    public MainWindowViewModel App { get; }

    // Proxy needed inside the ScheduledJobs item-template (DataContext = ScheduledJobViewModel)
    public IAsyncRelayCommand<ScheduledJobViewModel?> RemoveScheduledJobCommand =>
        App.RemoveScheduledJobCommand;
}
