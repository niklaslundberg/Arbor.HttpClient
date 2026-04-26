using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Shared;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

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
    private string _requestUrl = "http://localhost:5000/echo";

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

    [ObservableProperty]
    private string _requestNotes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHttpRequest))]
    [NotifyPropertyChangedFor(nameof(IsGraphQlRequest))]
    [NotifyPropertyChangedFor(nameof(IsWebSocketRequest))]
    [NotifyPropertyChangedFor(nameof(IsSseRequest))]
    [NotifyPropertyChangedFor(nameof(IsGrpcRequest))]
    [NotifyPropertyChangedFor(nameof(IsStreamingRequest))]
    [NotifyPropertyChangedFor(nameof(SendButtonLabel))]
    private RequestType _selectedRequestType = RequestType.Http;

    // ── Derived bool properties ───────────────────────────────────────────────

    public bool IsHttpRequest => SelectedRequestType == RequestType.Http;
    public bool IsGraphQlRequest => SelectedRequestType == RequestType.GraphQL;
    public bool IsWebSocketRequest => SelectedRequestType == RequestType.WebSocket;
    public bool IsSseRequest => SelectedRequestType == RequestType.Sse;
    public bool IsGrpcRequest => SelectedRequestType == RequestType.GrpcUnary;
    public bool IsStreamingRequest => SelectedRequestType is RequestType.WebSocket or RequestType.Sse;

    /// <summary>Label shown on the primary action button; changes based on request type.</summary>
    public string SendButtonLabel => SelectedRequestType switch
    {
        RequestType.WebSocket or RequestType.Sse => "Connect",
        _ => "Send"
    };

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

    public IReadOnlyList<RequestType> RequestTypeOptions { get; } =
    [
        RequestType.Http,
        RequestType.GraphQL,
        RequestType.WebSocket,
        RequestType.Sse,
        RequestType.GrpcUnary
    ];

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
        if (header is { } h)
        {
            RequestHeaders.Remove(h);
        }
    }

    [RelayCommand]
    private void AddQueryParameter() => RequestQueryParameters.Add(new RequestQueryParameterViewModel());

    [RelayCommand]
    private void RemoveQueryParameter(RequestQueryParameterViewModel? parameter)
    {
        if (parameter is { } param)
        {
            RequestQueryParameters.Remove(param);
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
    /// Returns the URL with <c>{{variable}}</c> tokens resolved against the active environment.
    /// Used by non-HTTP protocols (GraphQL, WebSocket, SSE, gRPC) that need only the URL.
    /// </summary>
    public string GetResolvedUrl()
    {
        var variables = GetResolvedVariables();
        return _variableResolver.Resolve(RequestUrl, variables);
    }

    /// <summary>
    /// Returns the request headers with <c>{{variable}}</c> tokens resolved, ready to send
    /// with any protocol that supports custom HTTP headers.
    /// </summary>
    public IReadOnlyList<RequestHeader> GetResolvedHeaders()
    {
        var variables = GetResolvedVariables();
        return BuildResolvedHeaders(variables, string.Empty);
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
        if (authHeader is { } previewAuth)
        {
            RequestHeadersPreview.Add($"{previewAuth.Name}: {previewAuth.Value}");
        }

        foreach (var h in RequestHeaders.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name)))
        {
            if (authHeader is { } && string.Equals(h.Name, authHeader.Name, StringComparison.OrdinalIgnoreCase))
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
        if (authHeader is { } resolvedAuth)
        {
            headers.RemoveAll(header => string.Equals(header.Name, resolvedAuth.Name, StringComparison.OrdinalIgnoreCase));
            headers.Insert(0, resolvedAuth);
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
        if (e.NewItems is { } newHeaders)
        {
            foreach (RequestHeaderViewModel h in newHeaders)
            {
                h.PropertyChanged += OnRequestHeaderPropertyChanged;
            }
        }

        if (e.OldItems is { } oldHeaders)
        {
            foreach (RequestHeaderViewModel h in oldHeaders)
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
        if (e.NewItems is { } newParams)
        {
            foreach (RequestQueryParameterViewModel parameter in newParams)
            {
                parameter.PropertyChanged += OnRequestQueryParameterPropertyChanged;
            }
        }

        if (e.OldItems is { } oldParams)
        {
            foreach (RequestQueryParameterViewModel parameter in oldParams)
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
