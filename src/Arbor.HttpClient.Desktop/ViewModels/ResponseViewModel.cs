using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed class ResponseViewModel : Document
{
    public ResponseViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "response";
        Title = "Response";
    }

    public MainWindowViewModel App { get; }
}
