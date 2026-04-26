using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.Features.Collections;

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
