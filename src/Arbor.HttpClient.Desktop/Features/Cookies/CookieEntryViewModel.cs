using System;
using System.Net;
using System.Reactive.Linq;
using Arbor.HttpClient.Desktop.Shared;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.Cookies;

public sealed partial class CookieEntryViewModel : ReactiveViewModelBase
{
    [Reactive]
    private string _value;

    public string Name { get; }
    public string Domain { get; }
    public string Path { get; }
    public string Expires { get; }

    internal Cookie Cookie { get; }

    public CookieEntryViewModel(Cookie cookie)
    {
        Cookie = cookie;
        Name = cookie.Name;
        Domain = cookie.Domain;
        Path = cookie.Path;
        _value = cookie.Value ?? string.Empty;
        Expires = cookie.Expires == DateTime.MinValue
            ? "Session"
            : cookie.Expires.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        this.WhenAnyValue(viewModel => viewModel.Value)
            .Skip(1)
            .Subscribe(value => Cookie.Value = value);
    }
}
