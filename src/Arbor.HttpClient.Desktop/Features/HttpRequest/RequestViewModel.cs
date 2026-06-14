using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using System.Reactive;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

public sealed class RequestViewModel : Document
{
    public RequestViewModel(IRequestPanelContext app)
    {
        App = app;
        Id = "request";
        Title = "Request";
    }

    public IRequestPanelContext App { get; }

    // Proxy needed inside the RequestHeaders item-template (DataContext = RequestHeaderViewModel)
    public ReactiveCommand<RequestHeaderViewModel?, Unit> RemoveHeaderCommand =>
        App.RequestEditor.RemoveHeaderCommand;

    // Proxy needed inside the RequestQueryParameters item-template (DataContext = RequestQueryParameterViewModel)
    public ReactiveCommand<RequestQueryParameterViewModel?, Unit> RemoveQueryParameterCommand =>
        App.RequestEditor.RemoveQueryParameterCommand;
}
