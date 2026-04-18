using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed class OptionsViewModel : Tool
{
    public OptionsViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "options";
        Title = "Options";
    }

    public MainWindowViewModel App { get; }
}
