using System;
using System.Reactive.Linq;
using Arbor.HttpClient.Desktop.Shared;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

public sealed partial class RequestHeaderViewModel : ReactiveViewModelBase
{
    [Reactive]
    private string _name = string.Empty;

    [Reactive]
    private string _value = string.Empty;

    [Reactive]
    private string _description = string.Empty;

    [Reactive]
    private bool _isEnabled = true;

    [Reactive]
    private bool _isInherited;

    public RequestHeaderViewModel()
    {
        this.WhenAnyValue(
                viewModel => viewModel.Name,
                viewModel => viewModel.Value,
                viewModel => viewModel.IsEnabled)
            .Skip(1)
            .Subscribe(_ => IsInherited = false);
    }
}
