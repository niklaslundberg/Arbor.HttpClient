using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

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
