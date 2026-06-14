using Dock.Model.ReactiveUI.Controls;

namespace Arbor.HttpClient.Desktop.Features.Logging;

public sealed class LogPanelViewModel : Tool
{
    public LogPanelViewModel(LogWindowViewModel logs)
    {
        Logs = logs;
        Id = "log-panel";
        Title = "Logs";
    }

    /// <summary>The log feature view model this dockable surfaces; bound directly by <c>LogPanelView</c>.</summary>
    public LogWindowViewModel Logs { get; }
}
