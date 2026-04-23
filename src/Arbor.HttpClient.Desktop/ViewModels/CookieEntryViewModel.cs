using System;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class CookieEntryViewModel : ViewModelBase
{
    [ObservableProperty]
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
    }
}
