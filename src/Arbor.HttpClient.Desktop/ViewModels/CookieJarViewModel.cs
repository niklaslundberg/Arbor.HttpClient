using System;
using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class CookieJarViewModel : Tool
{
    private readonly CookieContainer _cookieContainer;

    public CookieJarViewModel(CookieContainer? cookieContainer = null)
    {
        _cookieContainer = cookieContainer ?? new CookieContainer();
        Id = "cookie-jar";
        Title = "Cookie Jar";
        Cookies = [];
        RefreshCookies();
    }

    public ObservableCollection<CookieEntryViewModel> Cookies { get; }

    [ObservableProperty]
    private string _newCookieName = string.Empty;

    [ObservableProperty]
    private string _newCookieValue = string.Empty;

    [ObservableProperty]
    private string _newCookieDomain = string.Empty;

    [RelayCommand]
    private void Refresh() => RefreshCookies();

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var entry in Cookies)
        {
            entry.Cookie.Expires = DateTime.UtcNow.AddDays(-1);
        }

        RefreshCookies();
    }

    [RelayCommand]
    private void Remove(CookieEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.Cookie.Expires = DateTime.UtcNow.AddDays(-1);
        RefreshCookies();
    }

    [RelayCommand]
    private void AddCookie()
    {
        var name = NewCookieName.Trim();
        var domain = NewCookieDomain.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(domain))
        {
            return;
        }

        var cookie = new Cookie(name, NewCookieValue.Trim(), "/", domain);
        _cookieContainer.Add(cookie);

        NewCookieName = string.Empty;
        NewCookieValue = string.Empty;
        NewCookieDomain = string.Empty;

        RefreshCookies();
    }

    public void RefreshCookies()
    {
        Cookies.Clear();
        foreach (Cookie cookie in _cookieContainer.GetAllCookies())
        {
            if (!cookie.Expired)
            {
                Cookies.Add(new CookieEntryViewModel(cookie));
            }
        }
    }
}
