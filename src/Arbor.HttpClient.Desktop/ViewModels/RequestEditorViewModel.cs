using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Arbor.HttpClient.Desktop.ViewModels;

/// <summary>
/// View model for the request editor panel. Owns all state and logic required to
/// compose an HTTP request: URL, method, headers, query parameters, auth, body,
/// content-type, and the live request preview. Intentionally has no dependency on
/// the Avalonia UI thread or on any other feature view model, so it can be
/// constructed and tested without the headless Avalonia session.
/// </summary>
public sealed partial class RequestEditorViewModel : ViewModelBase
{
    private readonly VariableResolver _variableResolver;
    private readonly Func<IReadOnlyList<EnvironmentVariable>> _getActiveVariables;
    private readonly ILogger _logger;
    private readonly Action? _onOptionsAffectingPropertyChanged;
    private bool _isUpdatingRequestUrlFromQueryParameters;
    private bool _isUpdatingQueryParametersFromRequestUrl;

    private const string VariableTokenStart = "{{";

    // ── Auth mode constants ───────────────────────────────────────────────────
    public const string AuthNoneOption = "None";
    public const string AuthBearerOption = "Bearer Token";
    public const string AuthBasicOption = "Basic";
    public const string AuthApiKeyOption = "API Key";
    public const string AuthOAuth2ClientCredentialsOption = "OAuth 2 (Client Credentials)";

    // ── Content-type constants ────────────────────────────────────────────────
    public const string NoneContentTypeOption = "(none)";
    public const string CustomContentTypeOption = "Custom...";

    // ── Observable properties ─────────────────────────────────────────────────

    [ObservableProperty]
    private string _requestName = "Sample Request";

    [ObservableProperty]
    private string _selectedMethod = "GET";

    [ObservableProperty]
    private string _requestUrl = "https://postman-echo.com/get?hello=world";

    [ObservableProperty]
    private string _requestPreview = string.Empty;

    [ObservableProperty]
    private string _requestBody = string.Empty;

    [ObservableProperty]
    private bool _followRedirectsForRequest = true;

    [ObservableProperty]
    private string _selectedHttpVersionOption = "1.1";

    [ObservableProperty]
    private string _selectedContentTypeOption = NoneContentTypeOption;

    [ObservableProperty]
    private string _customContentType = string.Empty;

    [ObservableProperty]
    private string _selectedAuthModeOption = AuthNoneOption;

    [ObservableProperty]
    private string _authBearerToken = string.Empty;

    [ObservableProperty]
    private string _authBasicUsername = string.Empty;

    [ObservableProperty]
    private string _authBasicPassword = string.Empty;

    [ObservableProperty]
    private string _authApiKey = string.Empty;

    [ObservableProperty]
    private string _authOAuth2AccessToken = string.Empty;

    [ObservableProperty]
    private bool _isRequestHeadersPreviewVisible;

    // ── Derived bool properties ───────────────────────────────────────────────

    public bool IsBearerAuthMode => SelectedAuthModeOption == AuthBearerOption;
    public bool IsBasicAuthMode => SelectedAuthModeOption == AuthBasicOption;
    public bool IsApiKeyAuthMode => SelectedAuthModeOption == AuthApiKeyOption;
    public bool IsOAuth2ClientCredentialsAuthMode => SelectedAuthModeOption == AuthOAuth2ClientCredentialsOption;
    public bool IsCustomContentType => SelectedContentTypeOption == CustomContentTypeOption;

    public string RequestHeadersPreviewLabel =>
        IsRequestHeadersPreviewVisible ? "▼ Preview" : "▶ Preview";

    /// <summary>The actual Content-Type header value to send (empty string = no header).</summary>
    public string ContentType
    {
        get
        {
            if (string.IsNullOrEmpty(SelectedContentTypeOption) || SelectedContentTypeOption == NoneContentTypeOption)
            {
                return string.Empty;
            }

            return IsCustomContentType ? CustomContentType : SelectedContentTypeOption;
        }
    }

    // ── Settable defaults (synced from MainWindowViewModel when options change) ─
    public string DefaultContentType { get; set; } = "application/json";

    // ── Static option lists ───────────────────────────────────────────────────

    public IReadOnlyList<string> Methods { get; } = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public IReadOnlyList<string> HttpVersionOptions { get; } = ["1.0", "1.1", "2.0", "3.0"];

    public IReadOnlyList<string> AuthModeOptions { get; } =
    [
        AuthNoneOption,
        AuthBearerOption,
        AuthBasicOption,
        AuthApiKeyOption,
        AuthOAuth2ClientCredentialsOption
    ];

    public IReadOnlyList<string> ContentTypeOptions { get; } =
    [
        NoneContentTypeOption,
        "application/json",
        "application/xml",
        "text/plain",
        "text/html",
        "application/x-www-form-urlencoded",
        "multipart/form-data",
        CustomContentTypeOption
    ];

    // ── Observable collections ────────────────────────────────────────────────

    public ObservableCollection<RequestHeaderViewModel> RequestHeaders { get; }
    public ObservableCollection<RequestQueryParameterViewModel> RequestQueryParameters { get; }
    public ObservableCollection<string> RequestHeadersPreview { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddHeader() => RequestHeaders.Add(new RequestHeaderViewModel());

    [RelayCommand]
    private void RemoveHeader(RequestHeaderViewModel? header)
    {
        if (header is not null)
        {
            RequestHeaders.Remove(header);
        }
    }

    [RelayCommand]
    private void AddQueryParameter() => RequestQueryParameters.Add(new RequestQueryParameterViewModel());

    [RelayCommand]
    private void RemoveQueryParameter(RequestQueryParameterViewModel? parameter)
    {
        if (parameter is not null)
        {
            RequestQueryParameters.Remove(parameter);
        }
    }

    [RelayCommand]
    private void ToggleRequestHeadersPreview() =>
        IsRequestHeadersPreviewVisible = !IsRequestHeadersPreviewVisible;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="variableResolver">Resolves <c>{{variable}}</c> tokens in URL/body/headers.</param>
    /// <param name="getActiveVariables">
    /// Callback that returns the current active environment variables. Typically
    /// implemented as a lambda that reads <c>MainWindowViewModel.ActiveEnvironmentVariables</c>.
    /// </param>
    /// <param name="logger">Optional Serilog logger.</param>
    /// <param name="onOptionsAffectingPropertyChanged">
    /// Invoked when a property that should trigger an options auto-save changes (e.g. HTTP version).
    /// </param>
    public RequestEditorViewModel(
        VariableResolver variableResolver,
        Func<IReadOnlyList<EnvironmentVariable>> getActiveVariables,
        ILogger? logger = null,
        Action? onOptionsAffectingPropertyChanged = null)
    {
        _variableResolver = variableResolver;
        _getActiveVariables = getActiveVariables;
        _logger = logger ?? Log.Logger;
        _onOptionsAffectingPropertyChanged = onOptionsAffectingPropertyChanged;

        RequestHeaders = [];
        RequestQueryParameters = [];

        RequestHeaders.CollectionChanged += OnRequestHeadersCollectionChanged;
        RequestQueryParameters.CollectionChanged += OnRequestQueryParametersCollectionChanged;
    }

    // ── Property-change hooks ─────────────────────────────────────────────────

    partial void OnSelectedMethodChanged(string value) => RefreshRequestPreview();

    partial void OnSelectedHttpVersionOptionChanged(string value)
    {
        _logger.Information("Selected HTTP version changed to {Value}", value);
        RefreshRequestPreview();
        _onOptionsAffectingPropertyChanged?.Invoke();
    }

    partial void OnRequestBodyChanged(string value) => RefreshRequestDerivedViews();

    partial void OnRequestUrlChanged(string value)
    {
        if (_isUpdatingRequestUrlFromQueryParameters)
        {
            return;
        }

        SyncQueryParametersFromRequestUrl(value);
        RefreshRequestPreview();
    }

    partial void OnSelectedContentTypeOptionChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomContentType));
        OnPropertyChanged(nameof(ContentType));
        RefreshRequestDerivedViews();
    }

    partial void OnSelectedAuthModeOptionChanged(string value)
    {
        OnPropertyChanged(nameof(IsBearerAuthMode));
        OnPropertyChanged(nameof(IsBasicAuthMode));
        OnPropertyChanged(nameof(IsApiKeyAuthMode));
        OnPropertyChanged(nameof(IsOAuth2ClientCredentialsAuthMode));
        RefreshRequestDerivedViews();
    }

    partial void OnAuthBearerTokenChanged(string value)
    {
        if (IsBearerAuthMode)
        {
            RefreshRequestDerivedViews();
        }
    }

    partial void OnAuthBasicUsernameChanged(string value)
    {
        if (IsBasicAuthMode)
        {
            RefreshRequestDerivedViews();
        }
    }

    partial void OnAuthBasicPasswordChanged(string value)
    {
        if (IsBasicAuthMode)
        {
            RefreshRequestDerivedViews();
        }
    }

    partial void OnAuthApiKeyChanged(string value)
    {
        if (IsApiKeyAuthMode)
        {
            RefreshRequestDerivedViews();
        }
    }

    partial void OnAuthOAuth2AccessTokenChanged(string value)
    {
        if (IsOAuth2ClientCredentialsAuthMode)
        {
            RefreshRequestDerivedViews();
        }
    }

    partial void OnCustomContentTypeChanged(string value)
    {
        if (IsCustomContentType)
        {
            OnPropertyChanged(nameof(ContentType));
            RefreshRequestDerivedViews();
        }
    }

    partial void OnIsRequestHeadersPreviewVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(RequestHeadersPreviewLabel));

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the request preview text. Called externally when the active environment changes.
    /// </summary>
    public void RefreshRequestPreview()
    {
        var variables = GetResolvedVariables();
        var resolvedUrl = _variableResolver.Resolve(RequestUrl, variables);
        var resolvedBody = _variableResolver.Resolve(RequestBody, variables);
        var previewHeaders = BuildResolvedHeaders(variables, resolvedBody);

        var requestLine = $"{SelectedMethod} {resolvedUrl} HTTP/{SelectedHttpVersionOption}";
        var headerLines = previewHeaders.Select(h => $"{h.Name}: {h.Value}");

        var builder = new StringBuilder();
        builder.AppendLine(requestLine);
        foreach (var line in headerLines)
        {
            builder.AppendLine(line);
        }

        builder.AppendLine();
        builder.Append(resolvedBody);

        RequestPreview = builder.ToString();
    }

    /// <summary>
    /// Builds the <see cref="HttpRequestDraft"/> representing the current editor state,
    /// ready to be passed to <see cref="HttpRequestService.SendAsync"/>.
    /// </summary>
    public HttpRequestDraft BuildDraft()
    {
        var variables = GetResolvedVariables();
        var resolvedUrl = _variableResolver.Resolve(RequestUrl, variables);
        var resolvedBody = _variableResolver.Resolve(RequestBody, variables);
        var headers = BuildResolvedHeaders(variables, resolvedBody);

        return new HttpRequestDraft(
            RequestName,
            SelectedMethod,
            resolvedUrl,
            resolvedBody,
            headers,
            ParseHttpVersion(SelectedHttpVersionOption),
            FollowRedirectsForRequest);
    }

    /// <summary>
    /// Returns the resolved variables from the active environment.
    /// Exposed for external callers that need variable resolution (e.g. collection base URL).
    /// </summary>
    public IReadOnlyList<EnvironmentVariable> GetResolvedVariables() =>
        _getActiveVariables()
            .Where(v => v.IsEnabled && !string.IsNullOrWhiteSpace(v.Name))
            .ToList();

    // ── Private helpers ───────────────────────────────────────────────────────

    private void RefreshRequestDerivedViews()
    {
        UpdateRequestHeadersPreview();
        RefreshRequestPreview();
    }

    private void UpdateRequestHeadersPreview()
    {
        RequestHeadersPreview.Clear();

        var effectiveContentType = ResolveContentType(RequestBody);
        if (!string.IsNullOrEmpty(effectiveContentType))
        {
            RequestHeadersPreview.Add($"Content-Type: {effectiveContentType}");
        }

        var authHeader = BuildAuthHeader(value => value);
        if (authHeader is not null)
        {
            RequestHeadersPreview.Add($"{authHeader.Name}: {authHeader.Value}");
        }

        foreach (var h in RequestHeaders.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name)))
        {
            if (authHeader is not null && string.Equals(h.Name, authHeader.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RequestHeadersPreview.Add($"{h.Name}: {h.Value}");
        }
    }

    private List<RequestHeader> BuildResolvedHeaders(IReadOnlyList<EnvironmentVariable> variables, string resolvedBody)
    {
        var headers = RequestHeaders
            .Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name))
            .Select(h => new RequestHeader(
                _variableResolver.Resolve(h.Name, variables),
                _variableResolver.Resolve(h.Value, variables)))
            .ToList();

        var authHeader = BuildAuthHeader(value => _variableResolver.Resolve(value, variables));
        if (authHeader is not null)
        {
            headers.RemoveAll(header => string.Equals(header.Name, authHeader.Name, StringComparison.OrdinalIgnoreCase));
            headers.Insert(0, authHeader);
        }

        var effectiveContentType = ResolveContentType(resolvedBody);
        if (!string.IsNullOrEmpty(effectiveContentType))
        {
            headers.Insert(0, new RequestHeader("Content-Type", effectiveContentType));
        }

        return headers;
    }

    private RequestHeader? BuildAuthHeader(Func<string, string> resolve)
    {
        return SelectedAuthModeOption switch
        {
            AuthBearerOption when !string.IsNullOrWhiteSpace(AuthBearerToken) =>
                new RequestHeader("Authorization", $"Bearer {resolve(AuthBearerToken)}"),

            AuthBasicOption when !string.IsNullOrWhiteSpace(AuthBasicUsername) || !string.IsNullOrWhiteSpace(AuthBasicPassword) =>
                new RequestHeader(
                    "Authorization",
                    $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{resolve(AuthBasicUsername)}:{resolve(AuthBasicPassword)}"))}"),

            AuthApiKeyOption when !string.IsNullOrWhiteSpace(AuthApiKey) =>
                new RequestHeader("Authorization", $"ApiKey {resolve(AuthApiKey)}"),

            AuthOAuth2ClientCredentialsOption when !string.IsNullOrWhiteSpace(AuthOAuth2AccessToken) =>
                new RequestHeader("Authorization", $"Bearer {resolve(AuthOAuth2AccessToken)}"),

            _ => null
        };
    }

    private string ResolveContentType(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(ContentType))
        {
            return ContentType;
        }

        return DefaultContentType;
    }

    private void SyncQueryParametersFromRequestUrl(string url)
    {
        if (_isUpdatingQueryParametersFromRequestUrl)
        {
            return;
        }

        _isUpdatingQueryParametersFromRequestUrl = true;
        try
        {
            var query = ExtractQuery(url);
            RequestQueryParameters.Clear();

            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            foreach (var segment in query.Split('&', StringSplitOptions.None))
            {
                if (segment.Length == 0)
                {
                    continue;
                }

                var equalsIndex = segment.IndexOf('=');
                var rawKey = equalsIndex >= 0 ? segment[..equalsIndex] : segment;
                var rawValue = equalsIndex >= 0 ? segment[(equalsIndex + 1)..] : string.Empty;

                RequestQueryParameters.Add(new RequestQueryParameterViewModel
                {
                    Key = DecodeQueryComponent(rawKey),
                    Value = DecodeQueryComponent(rawValue),
                    IsEnabled = true
                });
            }
        }
        finally
        {
            _isUpdatingQueryParametersFromRequestUrl = false;
        }
    }

    private void SyncRequestUrlFromQueryParameters()
    {
        if (_isUpdatingQueryParametersFromRequestUrl)
        {
            return;
        }

        var (prefix, _, fragment) = SplitUrl(RequestUrl);

        var query = string.Join("&", RequestQueryParameters
            .Where(param => param.IsEnabled && !string.IsNullOrWhiteSpace(param.Key))
            .Select(param => $"{EncodeQueryComponent(param.Key)}={EncodeQueryComponent(param.Value ?? string.Empty)}"));

        var updatedUrl = BuildUrl(prefix, query, fragment);

        if (string.Equals(updatedUrl, RequestUrl, StringComparison.Ordinal))
        {
            return;
        }

        _isUpdatingRequestUrlFromQueryParameters = true;
        try
        {
            RequestUrl = updatedUrl;
        }
        finally
        {
            _isUpdatingRequestUrlFromQueryParameters = false;
        }
    }

    private void OnRequestHeadersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (RequestHeaderViewModel h in e.NewItems)
            {
                h.PropertyChanged += OnRequestHeaderPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (RequestHeaderViewModel h in e.OldItems)
            {
                h.PropertyChanged -= OnRequestHeaderPropertyChanged;
            }
        }

        RefreshRequestDerivedViews();
    }

    private void OnRequestHeaderPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RefreshRequestDerivedViews();

    private void OnRequestQueryParametersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (RequestQueryParameterViewModel parameter in e.NewItems)
            {
                parameter.PropertyChanged += OnRequestQueryParameterPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (RequestQueryParameterViewModel parameter in e.OldItems)
            {
                parameter.PropertyChanged -= OnRequestQueryParameterPropertyChanged;
            }
        }

        SyncRequestUrlFromQueryParameters();
        RefreshRequestPreview();
    }

    private void OnRequestQueryParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SyncRequestUrlFromQueryParameters();
        RefreshRequestPreview();
    }

    // ── Static URL helpers ────────────────────────────────────────────────────

    private static (string Prefix, string Query, string Fragment) SplitUrl(string url)
    {
        var fragmentIndex = url.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : string.Empty;
        var urlWithoutFragment = fragmentIndex >= 0 ? url[..fragmentIndex] : url;

        var queryIndex = urlWithoutFragment.IndexOf('?');
        if (queryIndex < 0)
        {
            return (urlWithoutFragment, string.Empty, fragment);
        }

        var prefix = urlWithoutFragment[..queryIndex];
        var query = queryIndex + 1 < urlWithoutFragment.Length ? urlWithoutFragment[(queryIndex + 1)..] : string.Empty;
        return (prefix, query, fragment);
    }

    private static string ExtractQuery(string url) => SplitUrl(url).Query;

    private static string BuildUrl(string prefix, string query, string fragment)
    {
        var queryPart = string.IsNullOrEmpty(query) ? string.Empty : $"?{query}";
        return $"{prefix}{queryPart}{fragment}";
    }

    private static string DecodeQueryComponent(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains('%'))
        {
            return value;
        }

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch
        {
            return value;
        }
    }

    private static string EncodeQueryComponent(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Contains(VariableTokenStart, StringComparison.Ordinal))
        {
            return value;
        }

        return Uri.EscapeDataString(value);
    }

    private static Version ParseHttpVersion(string value) => value switch
    {
        "1.0" => global::System.Net.HttpVersion.Version10,
        "1.1" => global::System.Net.HttpVersion.Version11,
        "2.0" => global::System.Net.HttpVersion.Version20,
        "3.0" => global::System.Net.HttpVersion.Version30,
        _ => global::System.Net.HttpVersion.Version11
    };
}
