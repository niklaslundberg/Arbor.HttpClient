using System;
using System.ComponentModel;
using System.Globalization;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.WebView;
using Avalonia.Controls;
using Avalonia.Input;

namespace Arbor.HttpClient.Desktop.Features.WebView;

/// <summary>
/// A lightweight window that embeds a platform-native web browser via
/// <c>NativeWebView</c> (WebView2 on Windows, WebKit on macOS, WebKitGTK on Linux).
/// Wire up to a <see cref="ScheduledJobViewModel"/> via <see cref="SubscribeToJob"/>
/// so the view refreshes automatically on every completed scheduled tick.
/// </summary>
public partial class WebViewWindow : Window
{
    private NativeWebView? _webView;
    private Button? _backButton;
    private Button? _forwardButton;
    private Button? _refreshButton;
    private Button? _goButton;
    private TextBox? _urlBox;
    private ScheduledJobViewModel? _subscribedVm;

    public WebViewWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _webView = this.FindControl<NativeWebView>("WebView");
        _backButton = this.FindControl<Button>("BackButton");
        _forwardButton = this.FindControl<Button>("ForwardButton");
        _urlBox = this.FindControl<TextBox>("UrlBox");
        _refreshButton = this.FindControl<Button>("RefreshButton");
        _goButton = this.FindControl<Button>("GoButton");

        if (_backButton is { } back)
        {
            back.Click += OnBackButtonClick;
        }

        if (_forwardButton is { } forward)
        {
            forward.Click += OnForwardButtonClick;
        }

        if (_refreshButton is { } refresh)
        {
            refresh.Click += OnRefreshButtonClick;
        }

        if (_goButton is { } go)
        {
            go.Click += OnGoButtonClick;
        }

        if (_urlBox is { } box)
        {
            box.KeyDown += OnUrlBoxKeyDown;
        }

        if (_webView is { } wv)
        {
            wv.NavigationStarted += OnNavigationStarted;
            wv.NavigationCompleted += OnNavigationCompleted;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_subscribedVm is { } vm)
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm = null;
        }

        if (_backButton is { } back)
        {
            back.Click -= OnBackButtonClick;
        }

        if (_forwardButton is { } forward)
        {
            forward.Click -= OnForwardButtonClick;
        }

        if (_refreshButton is { } refresh)
        {
            refresh.Click -= OnRefreshButtonClick;
        }

        if (_goButton is { } go)
        {
            go.Click -= OnGoButtonClick;
        }

        if (_urlBox is { } box)
        {
            box.KeyDown -= OnUrlBoxKeyDown;
        }

        if (_webView is { } wv)
        {
            wv.NavigationStarted -= OnNavigationStarted;
            wv.NavigationCompleted -= OnNavigationCompleted;
        }

        base.OnClosed(e);
    }

    /// <summary>
    /// Navigates the embedded browser to <paramref name="uri"/> and updates the URL bar.
    /// Safe to call before the window is shown — navigation is deferred until
    /// <see cref="OnOpened"/> has initialised the <c>NativeWebView</c>.
    /// </summary>
    public void Navigate(Uri uri)
    {
        if (_urlBox is { } box)
        {
            box.Text = uri.ToString();
        }

        _webView?.Navigate(uri);
    }

    /// <summary>
    /// Subscribes to <paramref name="vm"/> so that each time a new scheduled
    /// response is received the browser automatically refreshes to the job URL.
    /// The subscription is torn down when this window is closed.
    /// Call at most once per window instance.
    /// </summary>
    public void SubscribeToJob(ScheduledJobViewModel vm)
    {
        _subscribedVm = vm;
        vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnBackButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _webView?.GoBack();

    private void OnForwardButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _webView?.GoForward();

    private void OnRefreshButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _webView?.Refresh();

    private void OnGoButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TryNavigateFromUrlBox();

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScheduledJobViewModel.LastResponseStatus)
            && _subscribedVm is { } vm
            && Uri.TryCreate(vm.Url, UriKind.Absolute, out var uri))
        {
            // HandleResponse already marshals to the UI thread via Dispatcher.UIThread,
            // so this callback is already on the UI thread — no extra Post needed.
            Navigate(BuildNoCacheUri(uri));
        }
    }

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (_urlBox is { } box && e.Request is { } uri)
        {
            box.Text = uri.ToString();
        }
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (_backButton is { } back)
        {
            back.IsEnabled = _webView?.CanGoBack ?? false;
        }

        if (_forwardButton is { } forward)
        {
            forward.IsEnabled = _webView?.CanGoForward ?? false;
        }
    }

    private void OnUrlBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            TryNavigateFromUrlBox();
        }
    }

    private void TryNavigateFromUrlBox()
    {
        var text = _urlBox?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Prepend https:// if the user omitted the scheme.
        var raw = text.Trim();
        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            raw = "https://" + raw;
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            Navigate(uri);
        }
    }

    internal static Uri BuildNoCacheUri(Uri uri, long? cacheBustValue = null)
    {
        var cacheBust = cacheBustValue ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var existingQuery = string.IsNullOrEmpty(uri.Query) ? string.Empty : uri.Query[1..];
        var cacheBustSegment = $"arborNoCache={cacheBust.ToString(CultureInfo.InvariantCulture)}";
        var combinedQuery = string.IsNullOrEmpty(existingQuery)
            ? cacheBustSegment
            : $"{existingQuery}&{cacheBustSegment}";
        var uriBuilder = new UriBuilder(uri)
        {
            Query = combinedQuery
        };
        return uriBuilder.Uri;
    }
}
