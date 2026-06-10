using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
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
public sealed partial class RequestEditorViewModel : ReactiveViewModelBase
{
    private readonly VariableResolver _variableResolver;
    private readonly Func<IReadOnlyList<EnvironmentVariable>> _getActiveVariables;
    private readonly ILogger _logger;
    private readonly Action? _onOptionsAffectingPropertyChanged;
    private bool _isUpdatingRequestUrlFromQueryParameters;
    private bool _isUpdatingQueryParametersFromRequestUrl;
    private int _suppressRefreshDepth;
    // RequestEditorViewModel state is mutated on the UI thread; this cache is intentionally unsynchronized.
    private string _cachedFormattedBody = string.Empty;
    private string _cachedFormattingResolvedBody = string.Empty;
    private string _cachedFormattingMediaType = string.Empty;
    private bool _cachedFormattingUseIndentation;
    private bool _hasCachedFormattedBody;

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
    public const string DefaultTlsVersionOverrideOption = "(default)";

    // ── Observable properties ─────────────────────────────────────────────────

    [Reactive]
    private string _requestName = "Sample Request";

    [Reactive]
    private string _selectedMethod = "GET";

    [Reactive]
    private string _requestUrl = "http://localhost:5000/echo";

    [Reactive]
    private string _requestPreview = string.Empty;

    [Reactive]
    private string _requestBody = string.Empty;

    [Reactive]
    private string _requestTimeoutSecondsText = string.Empty;

    [Reactive]
    private bool _followRedirectsForRequest = true;

    [Reactive]
    private bool _validateUrlBeforeSend = true;

    [Reactive]
    private bool _prettyPrintRequestBody;

    [Reactive]
    private bool _prettyPrintRequestBodyUseIndentation = true;

    [Reactive]
    private bool _showRequestPreview = true;

    [Reactive]
    private bool _ignoreCertificateValidationForRequest;

    [Reactive]
    private string _selectedTlsVersionOverrideOption = DefaultTlsVersionOverrideOption;

    [Reactive]
    private string _selectedHttpVersionOption = "1.1";

    [Reactive]
    private string _selectedContentTypeOption = NoneContentTypeOption;

    [Reactive]
    private string _customContentType = string.Empty;

    [Reactive]
    private string _selectedAuthModeOption = AuthNoneOption;

    [Reactive]
    private string _authBearerToken = string.Empty;

    [Reactive]
    private string _authBasicUsername = string.Empty;

    [Reactive]
    private string _authBasicPassword = string.Empty;

    [Reactive]
    private string _authApiKey = string.Empty;

    [Reactive]
    private string _authOAuth2AccessToken = string.Empty;

    [Reactive]
    private bool _isRequestHeadersPreviewVisible;

    [Reactive]
    private string _requestNotes = string.Empty;

    [Reactive]
    private RequestType _selectedRequestType = RequestType.Http;

    // ── Derived properties (ObservableAsPropertyHelper, see constructor) ──────

    private readonly ObservableAsPropertyHelper<bool> _isHttpRequest;
    private readonly ObservableAsPropertyHelper<bool> _isRequestPreviewPanelVisible;
    private readonly ObservableAsPropertyHelper<bool> _isGraphQlRequest;
    private readonly ObservableAsPropertyHelper<bool> _isWebSocketRequest;
    private readonly ObservableAsPropertyHelper<bool> _isSseRequest;
    private readonly ObservableAsPropertyHelper<bool> _isGrpcRequest;
    private readonly ObservableAsPropertyHelper<bool> _isStreamingRequest;
    private readonly ObservableAsPropertyHelper<string> _sendButtonLabel;
    private readonly ObservableAsPropertyHelper<bool> _isBearerAuthMode;
    private readonly ObservableAsPropertyHelper<bool> _isBasicAuthMode;
    private readonly ObservableAsPropertyHelper<bool> _isApiKeyAuthMode;
    private readonly ObservableAsPropertyHelper<bool> _isOAuth2ClientCredentialsAuthMode;
    private readonly ObservableAsPropertyHelper<bool> _isCustomContentType;
    private readonly ObservableAsPropertyHelper<string> _requestHeadersPreviewLabel;
    private readonly ObservableAsPropertyHelper<string> _contentType;

    public bool IsHttpRequest => _isHttpRequest.Value;
    public bool IsRequestPreviewPanelVisible => _isRequestPreviewPanelVisible.Value;
    public bool IsGraphQlRequest => _isGraphQlRequest.Value;
    public bool IsWebSocketRequest => _isWebSocketRequest.Value;
    public bool IsSseRequest => _isSseRequest.Value;
    public bool IsGrpcRequest => _isGrpcRequest.Value;
    public bool IsStreamingRequest => _isStreamingRequest.Value;

    /// <summary>Label shown on the primary action button; changes based on request type.</summary>
    public string SendButtonLabel => _sendButtonLabel.Value;

    public bool IsBearerAuthMode => _isBearerAuthMode.Value;
    public bool IsBasicAuthMode => _isBasicAuthMode.Value;
    public bool IsApiKeyAuthMode => _isApiKeyAuthMode.Value;
    public bool IsOAuth2ClientCredentialsAuthMode => _isOAuth2ClientCredentialsAuthMode.Value;
    public bool IsCustomContentType => _isCustomContentType.Value;

    public string RequestHeadersPreviewLabel => _requestHeadersPreviewLabel.Value;

    /// <summary>The actual Content-Type header value to send (empty string = no header).</summary>
    public string ContentType => _contentType.Value;

    private static string ComputeContentType(string selectedOption, string customContentType)
    {
        if (string.IsNullOrEmpty(selectedOption) || selectedOption == NoneContentTypeOption)
        {
            return string.Empty;
        }

        return selectedOption == CustomContentTypeOption ? customContentType : selectedOption;
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

    public IReadOnlyList<string> TlsVersionOverrideOptions { get; } =
    [
        DefaultTlsVersionOverrideOption,
        "SystemDefault",
        "Tls10",
        "Tls11",
        "Tls12",
        "Tls13"
    ];

    public IReadOnlyList<string> WellKnownHeaderNames { get; } =
    [
        "Accept",
        "Accept-Charset",
        "Accept-Encoding",
        "Accept-Language",
        "Authorization",
        "Cache-Control",
        "Connection",
        "Content-Disposition",
        "Content-Encoding",
        "Content-Length",
        "Content-Type",
        "Cookie",
        "Host",
        "If-Match",
        "If-Modified-Since",
        "If-None-Match",
        "Origin",
        "Pragma",
        "Range",
        "Referer",
        "User-Agent"
    ];

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

    [ReactiveCommand]
    private void AddHeader() => EnsurePlaceholderHeader();

    [ReactiveCommand]
    private void RemoveHeader(RequestHeaderViewModel? header)
    {
        if (header is { } h)
        {
            RequestHeaders.Remove(h);
            EnsurePlaceholderHeader();
        }
    }

    [ReactiveCommand]
    private void AddQueryParameter() => EnsurePlaceholderQueryParameter();

    [ReactiveCommand]
    private void RemoveQueryParameter(RequestQueryParameterViewModel? parameter)
    {
        if (parameter is { } param)
        {
            RequestQueryParameters.Remove(param);
            EnsurePlaceholderQueryParameter();
        }
    }

    [ReactiveCommand]
    private void ToggleRequestHeadersPreview() =>
        IsRequestHeadersPreviewVisible = !IsRequestHeadersPreviewVisible;

    [ReactiveCommand]
    private void PrettyPrintRequestBodySource()
    {
        if (TryFormatRequestBody(RequestBody, out var formattedBody))
        {
            RequestBody = formattedBody;
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a request editor that resolves environment variables, tracks editable request state,
    /// and produces preview/send-ready request drafts.
    /// </summary>
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

        var requestType = this.WhenAnyValue(viewModel => viewModel.SelectedRequestType);
        _isHttpRequest = requestType
            .Select(type => type == RequestType.Http)
            .ToProperty(this, viewModel => viewModel.IsHttpRequest);
        _isGraphQlRequest = requestType
            .Select(type => type == RequestType.GraphQL)
            .ToProperty(this, viewModel => viewModel.IsGraphQlRequest);
        _isWebSocketRequest = requestType
            .Select(type => type == RequestType.WebSocket)
            .ToProperty(this, viewModel => viewModel.IsWebSocketRequest);
        _isSseRequest = requestType
            .Select(type => type == RequestType.Sse)
            .ToProperty(this, viewModel => viewModel.IsSseRequest);
        _isGrpcRequest = requestType
            .Select(type => type == RequestType.GrpcUnary)
            .ToProperty(this, viewModel => viewModel.IsGrpcRequest);
        _isStreamingRequest = requestType
            .Select(type => type is RequestType.WebSocket or RequestType.Sse)
            .ToProperty(this, viewModel => viewModel.IsStreamingRequest);
        _sendButtonLabel = requestType
            .Select(type => type is RequestType.WebSocket or RequestType.Sse ? "Connect" : "Send")
            .ToProperty(this, viewModel => viewModel.SendButtonLabel);
        _isRequestPreviewPanelVisible = this
            .WhenAnyValue(
                viewModel => viewModel.SelectedRequestType,
                viewModel => viewModel.ShowRequestPreview,
                (type, showPreview) => type == RequestType.Http && showPreview)
            .ToProperty(this, viewModel => viewModel.IsRequestPreviewPanelVisible);

        var authMode = this.WhenAnyValue(viewModel => viewModel.SelectedAuthModeOption);
        _isBearerAuthMode = authMode
            .Select(mode => mode == AuthBearerOption)
            .ToProperty(this, viewModel => viewModel.IsBearerAuthMode);
        _isBasicAuthMode = authMode
            .Select(mode => mode == AuthBasicOption)
            .ToProperty(this, viewModel => viewModel.IsBasicAuthMode);
        _isApiKeyAuthMode = authMode
            .Select(mode => mode == AuthApiKeyOption)
            .ToProperty(this, viewModel => viewModel.IsApiKeyAuthMode);
        _isOAuth2ClientCredentialsAuthMode = authMode
            .Select(mode => mode == AuthOAuth2ClientCredentialsOption)
            .ToProperty(this, viewModel => viewModel.IsOAuth2ClientCredentialsAuthMode);

        _isCustomContentType = this
            .WhenAnyValue(viewModel => viewModel.SelectedContentTypeOption)
            .Select(option => option == CustomContentTypeOption)
            .ToProperty(this, viewModel => viewModel.IsCustomContentType);
        _contentType = this
            .WhenAnyValue(
                viewModel => viewModel.SelectedContentTypeOption,
                viewModel => viewModel.CustomContentType,
                ComputeContentType)
            .ToProperty(this, viewModel => viewModel.ContentType);
        _requestHeadersPreviewLabel = this
            .WhenAnyValue(viewModel => viewModel.IsRequestHeadersPreviewVisible)
            .Select(visible => visible ? "▼ Preview" : "▶ Preview")
            .ToProperty(this, viewModel => viewModel.RequestHeadersPreviewLabel);

        RequestHeaders = [];
        RequestQueryParameters = [];

        // Add initial placeholder rows directly, before hooking up the collection-changed
        // event handlers. This avoids triggering the full refresh chain (which calls
        // _getActiveVariables) while the parent ViewModel may still be initializing.
        var headerPlaceholder = new RequestHeaderViewModel { IsEnabled = false };
        headerPlaceholder.PropertyChanged += OnRequestHeaderPropertyChanged;
        RequestHeaders.Add(headerPlaceholder);

        var queryParamPlaceholder = new RequestQueryParameterViewModel { IsEnabled = false };
        queryParamPlaceholder.PropertyChanged += OnRequestQueryParameterPropertyChanged;
        RequestQueryParameters.Add(queryParamPlaceholder);

        RequestHeaders.CollectionChanged += OnRequestHeadersCollectionChanged;
        RequestQueryParameters.CollectionChanged += OnRequestQueryParametersCollectionChanged;

        RegisterPropertyChangeSubscriptions();
    }

    private void RegisterPropertyChangeSubscriptions()
    {
        this.WhenAnyValue(viewModel => viewModel.SelectedMethod)
            .Skip(1)
            .Subscribe(_ => RefreshRequestPreview())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.SelectedHttpVersionOption)
            .Skip(1)
            .Subscribe(_ => ApplySelectedHttpVersionOption())
            .DisposeWith(Disposables);

        this.WhenAnyValue(
                viewModel => viewModel.RequestBody,
                viewModel => viewModel.PrettyPrintRequestBody)
            .Skip(1)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.PrettyPrintRequestBodyUseIndentation)
            .Skip(1)
            .Where(_ => PrettyPrintRequestBody)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.RequestUrl)
            .Skip(1)
            .Subscribe(_ => ApplyRequestUrlChanged())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.SelectedContentTypeOption)
            .Skip(1)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.SelectedAuthModeOption)
            .Skip(1)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.AuthBearerToken)
            .Skip(1)
            .Where(_ => IsBearerAuthMode)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(
                viewModel => viewModel.AuthBasicUsername,
                viewModel => viewModel.AuthBasicPassword)
            .Skip(1)
            .Where(_ => IsBasicAuthMode)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.AuthApiKey)
            .Skip(1)
            .Where(_ => IsApiKeyAuthMode)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.AuthOAuth2AccessToken)
            .Skip(1)
            .Where(_ => IsOAuth2ClientCredentialsAuthMode)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.CustomContentType)
            .Skip(1)
            .Where(_ => IsCustomContentType)
            .Subscribe(_ => RefreshRequestDerivedViews())
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.RequestTimeoutSecondsText)
            .Skip(1)
            .Subscribe(_ => ApplyRequestTimeoutSecondsText())
            .DisposeWith(Disposables);
    }

    private void ApplySelectedHttpVersionOption()
    {
        _logger.Information("Selected HTTP version changed to {Value}", SelectedHttpVersionOption);
        RefreshRequestPreview();
        _onOptionsAffectingPropertyChanged?.Invoke();
    }

    private void ApplyRequestUrlChanged()
    {
        if (_isUpdatingRequestUrlFromQueryParameters)
        {
            return;
        }

        SyncQueryParametersFromRequestUrl(RequestUrl);
        RefreshRequestPreview();
    }

    private void ApplyRequestTimeoutSecondsText()
    {
        if (string.IsNullOrEmpty(RequestTimeoutSecondsText))
        {
            return;
        }

        if (!RequestTimeoutSecondsText.All(char.IsDigit))
        {
            RequestTimeoutSecondsText = string.Empty;
            return;
        }

        var normalized = int.TryParse(RequestTimeoutSecondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Min(parsed, 100).ToString(CultureInfo.InvariantCulture)
            : "100";

        if (!string.Equals(RequestTimeoutSecondsText, normalized, StringComparison.Ordinal))
        {
            RequestTimeoutSecondsText = normalized;
        }
    }

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Suppresses all preview/derived-view refreshes until <see cref="EndBulkUpdate"/>
    /// is called or the returned handle is disposed. Exactly one refresh fires at the end
    /// of the suppression window to bring derived state up to date.
    /// Use this when setting multiple properties in bulk (e.g. loading a collection request
    /// or switching the active tab) to avoid an O(n) chain of expensive formatting calls.
    /// <para>
    /// <strong>Threading:</strong> Must be called on the UI thread. All properties of
    /// <see cref="RequestEditorViewModel"/> are mutated on the UI thread only.
    /// </para>
    /// </summary>
    public BulkUpdateHandle BeginBulkUpdate()
    {
        _suppressRefreshDepth++;
        return new BulkUpdateHandle(this);
    }

    /// <summary>
    /// Ends the suppression window started by <see cref="BeginBulkUpdate"/> and fires one
    /// deferred refresh when the depth reaches zero. Handles nested suppression windows:
    /// if <see cref="BeginBulkUpdate"/> was called N times, the refresh fires only after
    /// the N-th matching <see cref="EndBulkUpdate"/> call.
    /// Must be called on the UI thread.
    /// </summary>
    public void EndBulkUpdate()
    {
        if (--_suppressRefreshDepth <= 0)
        {
            _suppressRefreshDepth = 0;
            RefreshRequestDerivedViews();
        }
    }

    /// <summary>
    /// Opaque handle returned by <see cref="BeginBulkUpdate"/>. Dispose to call
    /// <see cref="EndBulkUpdate"/> automatically via a <c>using</c> statement.
    /// Safe to dispose in its default state (no-op when <c>_vm</c> is null).
    /// </summary>
    public readonly struct BulkUpdateHandle : IDisposable
    {
        private readonly RequestEditorViewModel? _vm;
        internal BulkUpdateHandle(RequestEditorViewModel vm) => _vm = vm;

        public void Dispose() => _vm?.EndBulkUpdate();
    }

    /// <summary>
    /// Refreshes the request preview text. Called externally when the active environment changes.
    /// </summary>
    public void RefreshRequestPreview()
    {
        if (_suppressRefreshDepth > 0)
        {
            return;
        }

        var variables = GetResolvedVariables();
        var resolvedUrl = _variableResolver.Resolve(RequestUrl, variables);
        var resolvedBody = GetResolvedRequestBodyForPreviewAndSend(variables);
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
    /// Builds the <see cref="ResolvedHttpRequestDraft"/> representing the current editor state,
    /// ready to be passed to <see cref="HttpRequestService.SendAsync"/>.
    /// </summary>
    public ResolvedHttpRequestDraft BuildResolvedHttpRequestDraft()
    {
        var variables = GetResolvedVariables();
        var resolvedUrl = _variableResolver.Resolve(RequestUrl, variables);
        var resolvedBody = GetResolvedRequestBodyForPreviewAndSend(variables);
        var headers = BuildResolvedHeaders(variables, resolvedBody);
        int? timeoutSeconds = null;
        if (!string.IsNullOrWhiteSpace(RequestTimeoutSecondsText))
        {
            if (!int.TryParse(RequestTimeoutSecondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedTimeoutSeconds)
                || parsedTimeoutSeconds < 0
                || parsedTimeoutSeconds > 100)
            {
                throw new InvalidDataException("Request timeout must be a whole number between 0 and 100 seconds.");
            }

            timeoutSeconds = parsedTimeoutSeconds;
        }

        return new ResolvedHttpRequestDraft(
            RequestName,
            SelectedMethod,
            resolvedUrl,
            resolvedBody,
            headers,
            ParseHttpVersion(SelectedHttpVersionOption),
            FollowRedirectsForRequest,
            // null = "use default" (validate certificates); true = bypass validation.
            // false is never passed — disabled maps to null so the factory sees no override.
            IgnoreCertificateValidationForRequest ? true : null,
            timeoutSeconds,
            string.Equals(SelectedTlsVersionOverrideOption, DefaultTlsVersionOverrideOption, StringComparison.Ordinal)
                ? null
                : SelectedTlsVersionOverrideOption);
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
        if (_suppressRefreshDepth > 0)
        {
            return;
        }

        UpdateRequestHeadersPreview();
        RefreshRequestPreview();
    }

    private string GetResolvedRequestBodyForPreviewAndSend(IReadOnlyList<EnvironmentVariable> variables)
    {
        var resolvedBody = _variableResolver.Resolve(RequestBody, variables);
        if (!PrettyPrintRequestBody)
        {
            return resolvedBody;
        }

        var mediaType = HttpContentTypeHelper.NormalizeMediaType(ResolveContentType(resolvedBody));
        if (!HttpContentTypeHelper.IsJsonMediaType(mediaType) && !HttpContentTypeHelper.IsXmlMediaType(mediaType))
        {
            return resolvedBody;
        }

        if (_hasCachedFormattedBody
            && string.Equals(_cachedFormattingResolvedBody, resolvedBody, StringComparison.Ordinal)
            && string.Equals(_cachedFormattingMediaType, mediaType, StringComparison.Ordinal)
            && _cachedFormattingUseIndentation == PrettyPrintRequestBodyUseIndentation)
        {
            return _cachedFormattedBody;
        }

        if (!TryFormatRequestBodyByMediaType(resolvedBody, mediaType, PrettyPrintRequestBodyUseIndentation, out var formattedBody))
        {
            return resolvedBody;
        }

        _cachedFormattedBody = formattedBody;
        _cachedFormattingResolvedBody = resolvedBody;
        _cachedFormattingMediaType = mediaType;
        _cachedFormattingUseIndentation = PrettyPrintRequestBodyUseIndentation;
        _hasCachedFormattedBody = true;
        return formattedBody;
    }

    private bool TryFormatRequestBody(string requestBody, out string formattedBody)
    {
        formattedBody = requestBody;
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return false;
        }

        var mediaType = HttpContentTypeHelper.NormalizeMediaType(ResolveContentType(requestBody));
        return TryFormatRequestBodyByMediaType(requestBody, mediaType, PrettyPrintRequestBodyUseIndentation, out formattedBody);
    }

    private static bool TryFormatRequestBodyByMediaType(string requestBody, string mediaType, bool useIndentation, out string formattedBody)
    {
        if (HttpContentTypeHelper.IsJsonMediaType(mediaType))
        {
            return TryFormatJsonBody(requestBody, useIndentation, out formattedBody);
        }

        if (HttpContentTypeHelper.IsXmlMediaType(mediaType))
        {
            return TryFormatXmlBody(requestBody, useIndentation, out formattedBody);
        }

        formattedBody = string.Empty;
        return false;
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
            headers.RemoveAll(h => string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));
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

        var query = ExtractQuery(url);
        if (string.IsNullOrEmpty(query)
            && RequestQueryParameters.Count == 1
            && string.IsNullOrWhiteSpace(RequestQueryParameters[0].Key)
            && string.IsNullOrWhiteSpace(RequestQueryParameters[0].Value)
            && !RequestQueryParameters[0].IsEnabled)
        {
            return;
        }

        _isUpdatingQueryParametersFromRequestUrl = true;
        try
        {
            RequestQueryParameters.Clear();

            if (!string.IsNullOrEmpty(query))
            {
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

            EnsurePlaceholderQueryParameter();
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

    private void OnRequestHeaderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is RequestHeaderViewModel header)
        {
            switch (e.PropertyName)
            {
                case nameof(RequestHeaderViewModel.Name):
                    if (header.IsEnabled && string.IsNullOrWhiteSpace(header.Name))
                    {
                        header.IsEnabled = false;
                        return;
                    }

                    if (!header.IsEnabled && !string.IsNullOrWhiteSpace(header.Name))
                    {
                        header.IsEnabled = true;
                    }

                    if (ReferenceEquals(RequestHeaders[^1], header) && !string.IsNullOrWhiteSpace(header.Name))
                    {
                        EnsurePlaceholderHeader();
                    }

                    break;

                case nameof(RequestHeaderViewModel.IsEnabled):
                    if (header.IsEnabled && string.IsNullOrWhiteSpace(header.Name))
                    {
                        header.IsEnabled = false;
                        return;
                    }

                    if (ReferenceEquals(RequestHeaders[^1], header))
                    {
                        EnsurePlaceholderHeader();
                    }

                    break;
            }
        }

        RefreshRequestDerivedViews();
    }

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
        if (sender is RequestQueryParameterViewModel param)
        {
            switch (e.PropertyName)
            {
                case nameof(RequestQueryParameterViewModel.Key):
                    if (param.IsEnabled && string.IsNullOrWhiteSpace(param.Key))
                    {
                        param.IsEnabled = false;
                        return;
                    }

                    if (!param.IsEnabled && !string.IsNullOrWhiteSpace(param.Key))
                    {
                        param.IsEnabled = true;
                    }

                    if (ReferenceEquals(RequestQueryParameters[^1], param) && !string.IsNullOrWhiteSpace(param.Key))
                    {
                        EnsurePlaceholderQueryParameter();
                    }

                    break;

                case nameof(RequestQueryParameterViewModel.IsEnabled):
                    if (param.IsEnabled && string.IsNullOrWhiteSpace(param.Key))
                    {
                        param.IsEnabled = false;
                        return;
                    }

                    if (ReferenceEquals(RequestQueryParameters[^1], param))
                    {
                        EnsurePlaceholderQueryParameter();
                    }

                    break;
            }
        }

        SyncRequestUrlFromQueryParameters();
        RefreshRequestPreview();
    }

    // ── Placeholder row helpers ────────────────────────────────────────────────

    private void EnsurePlaceholderHeader()
    {
        if (RequestHeaders.Count == 0
            || !string.IsNullOrWhiteSpace(RequestHeaders[^1].Name)
            || RequestHeaders[^1].IsEnabled)
        {
            RequestHeaders.Add(new RequestHeaderViewModel { IsEnabled = false });
        }
    }

    private void EnsurePlaceholderQueryParameter()
    {
        if (RequestQueryParameters.Count == 0
            || !string.IsNullOrWhiteSpace(RequestQueryParameters[^1].Key)
            || RequestQueryParameters[^1].IsEnabled)
        {
            RequestQueryParameters.Add(new RequestQueryParameterViewModel { IsEnabled = false });
        }
    }

    /// <summary>
    /// Ensures a placeholder (empty, disabled) row exists at the end of both the headers and
    /// query parameters collections. Call this after programmatically loading rows (e.g. from a
    /// saved draft or collection import) to restore the always-visible new-row UX.
    /// </summary>
    public void EnsurePlaceholderRows()
    {
        EnsurePlaceholderHeader();
        EnsurePlaceholderQueryParameter();
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

    private static bool TryFormatJsonBody(string requestBody, bool useIndentation, out string formattedBody)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(requestBody);
            formattedBody = JsonSerializer.Serialize(jsonDocument.RootElement, new JsonSerializerOptions
            {
                WriteIndented = useIndentation,
                // Always use LF so output is consistent across platforms (Windows JsonSerializer defaults to CRLF).
                NewLine = "\n",
                // Request preview/body editors treat this as plain text; payload semantics should not be altered by escaping.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            return true;
        }
        catch (JsonException)
        {
            formattedBody = string.Empty;
            return false;
        }
    }

    private static bool TryFormatXmlBody(string requestBody, bool useIndentation, out string formattedBody)
    {
        try
        {
            var document = XDocument.Parse(requestBody);
            formattedBody = useIndentation
                ? document.ToString()
                : document.ToString(SaveOptions.DisableFormatting);
            return true;
        }
        catch (XmlException)
        {
            formattedBody = string.Empty;
            return false;
        }
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
        "1.0" => System.Net.HttpVersion.Version10,
        "1.1" => System.Net.HttpVersion.Version11,
        "2.0" => System.Net.HttpVersion.Version20,
        "3.0" => System.Net.HttpVersion.Version30,
        _ => System.Net.HttpVersion.Version11
    };
}
