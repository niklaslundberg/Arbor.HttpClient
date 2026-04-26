using Dock.Model.Mvvm.Controls;
using Arbor.HttpClient.Desktop.Features.Main;

namespace Arbor.HttpClient.Desktop.Features.Logging;

public sealed class LogPanelViewModel : Tool
{
    public LogPanelViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "log-panel";
        Title = "Logs";
    }

    public MainWindowViewModel App { get; }
}
