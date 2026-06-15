using System.Windows.Input;
using Dock.Model.ReactiveUI.Controls;

namespace Arbor.HttpClient.Desktop.Features.Collections;

public sealed class LeftPanelViewModel : Tool
{
    public LeftPanelViewModel(ILeftPanelContext app)
    {
        App = app;
        Id = "left-panel";
        Title = "Explorer";
    }

    public ILeftPanelContext App { get; }

    // Proxy needed inside the ScheduledJobs item-template (DataContext = ScheduledJobViewModel)
    public ICommand RemoveScheduledJobCommand =>
        App.ScheduledJobsPanel.RemoveJobCommand;
}
