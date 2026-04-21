using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed class EnvironmentsViewModel : Tool
{
    public EnvironmentsViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "environments";
        Title = "Environments";
    }

    public MainWindowViewModel App { get; }
}
