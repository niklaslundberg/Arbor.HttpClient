using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Arbor.HttpClient.Desktop.Features.Main;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

public sealed class RequestViewModel : Document
{
    public RequestViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "request";
        Title = "Request";
    }

    public MainWindowViewModel App { get; }

    // Proxy needed inside the RequestHeaders item-template (DataContext = RequestHeaderViewModel)
    public IRelayCommand<RequestHeaderViewModel?> RemoveHeaderCommand =>
        App.RequestEditor.RemoveHeaderCommand;

    // Proxy needed inside the RequestQueryParameters item-template (DataContext = RequestQueryParameterViewModel)
    public IRelayCommand<RequestQueryParameterViewModel?> RemoveQueryParameterCommand =>
        App.RequestEditor.RemoveQueryParameterCommand;
}
