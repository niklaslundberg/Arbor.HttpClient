using Arbor.HttpClient.Desktop.Shared;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

public sealed partial class RequestQueryParameterViewModel : ReactiveViewModelBase
{
    [Reactive]
    private string _key = string.Empty;

    [Reactive]
    private string _value = string.Empty;

    [Reactive]
    private string _description = string.Empty;

    [Reactive]
    private bool _isEnabled = true;
}
