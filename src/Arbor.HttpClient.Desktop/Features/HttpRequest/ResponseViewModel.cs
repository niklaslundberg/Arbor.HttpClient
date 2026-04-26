using Dock.Model.Mvvm.Controls;
using Arbor.HttpClient.Desktop.Features.Main;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

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
