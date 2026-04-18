using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

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
        App.RemoveHeaderCommand;

    // Proxy needed inside the RequestQueryParameters item-template (DataContext = RequestQueryParameterViewModel)
    public IRelayCommand<RequestQueryParameterViewModel?> RemoveQueryParameterCommand =>
        App.RemoveQueryParameterCommand;
}
