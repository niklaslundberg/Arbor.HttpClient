using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Serilog;

namespace Arbor.HttpClient.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly HttpRequestService _httpRequestService;
    private readonly IRequestHistoryRepository _requestHistoryRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IScheduledJobRepository _scheduledJobRepository;
    private readonly ScheduledJobService _scheduledJobService;
    private readonly LogWindowViewModel _logWindowViewModel;
    private readonly OpenApiImportService _openApiImportService;
    private readonly VariableResolver _variableResolver;
    private readonly ApplicationOptionsStore? _applicationOptionsStore;
    private readonly Action<ApplicationOptions>? _onApplicationOptionsChanged;
    private readonly ILogger _debugLogger;
    private readonly ILogger _httpRequestsLogger;
    private readonly List<string> _tempFiles = [];
    private readonly List<SavedRequest> _allHistory = [];
    private FileSystemWatcher? _requestBodyWatcher;
    private int _requestBodyReadPending;
    private DockFactory? _dockFactory;
    private ApplicationOptions _applicationOptions = new();
    private readonly Dictionary<string, DockLayoutSnapshot> _savedLayouts = new(StringComparer.OrdinalIgnoreCase);
    private int _layoutNameCounter = 1;
    private bool _suppressLayoutRestore;
    private bool _suppressOptionsAutoSave;
    private bool _suppressEnvironmentAutoSave;
    private bool _isSavingEnvironment;
    private CancellationTokenSource? _optionsAutoSaveCts;
    private CancellationTokenSource? _environmentAutoSaveCts;
    private DockLayoutSnapshot? _defaultLayout;
    private bool _isUpdatingRequestUrlFromQueryParameters;
    private bool _isUpdatingQueryParametersFromRequestUrl;
    private byte[] _lastResponseBodyBytes = Array.Empty<byte>();

    // Needed for file picker – set by the view
    public IStorageProvider? StorageProvider { get; set; }

    // Needed for clipboard (e.g. "Copy as cURL" on history items) – set by the view
    public global::Avalonia.Input.Platform.IClipboard? Clipboard { get; set; }

    /// <summary>Dock layout root; bound to DockControl.Layout in MainWindow.</summary>
    public IRootDock? Layout { get; private set; }

    /// <summary>Dock factory; bound to DockControl.Factory in MainWindow.</summary>
    public IFactory? Factory => _dockFactory;

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
    private string _responseStatus = string.Empty;

    /// <summary>
    /// Numeric HTTP status code for the last completed response (0 when no response yet
    /// or the request failed before receiving one). Used by the UI to color-code the
    /// response status by family (1xx/2xx/3xx/4xx/5xx).
    /// </summary>
    [ObservableProperty]
    private int _responseStatusCode;

    /// <summary>
    /// Human-readable elapsed time for the last response, e.g. "142 ms" or "1.23 s".
    /// Empty when no response has been received.
    /// </summary>
    [ObservableProperty]
    private string _responseTimeDisplay = string.Empty;

    /// <summary>
    /// Human-readable size of the last response body, e.g. "512 B", "1.3 KB", "4.7 MB".
    /// Empty when no response has been received.
    /// </summary>
    [ObservableProperty]
    private string _responseSizeDisplay = string.Empty;

    [ObservableProperty]
    private string _responseBody = string.Empty;

    [ObservableProperty]
    private string _rawResponseBody = string.Empty;

    [ObservableProperty]
    private string _responseBodyTabLabel = "Body";

    [ObservableProperty]
    private string _responseContentType = string.Empty;

    [ObservableProperty]
    private bool _isBinaryResponse;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _historySearchQuery = string.Empty;

    [ObservableProperty]
    private string _leftPanelTab = "History"; // "History" | "Collections"

    [ObservableProperty]
    private RequestEnvironment? _activeEnvironment;

    [ObservableProperty]
    private bool _isEnvironmentPanelVisible;

    [ObservableProperty]
    private string _newEnvironmentName = string.Empty;

    [ObservableProperty]
    private Collection? _selectedCollection;

    [ObservableProperty]
    private bool _isLayoutPanelVisible;

    [ObservableProperty]
    private string? _selectedLayoutName;

    public const string SystemThemeOption = "System";
    public const string DarkThemeOption = "Dark";
    public const string LightThemeOption = "Light";
    public const int MinScheduledJobIntervalSeconds = 1;
    private const string VariableTokenStart = "{{";
    private const string OptionsPageBreadcrumbSeparator = " \u203a  ";

    public IReadOnlyList<string> ThemeOptions { get; } =
    [
        SystemThemeOption,
        DarkThemeOption,
        LightThemeOption
    ];

    [ObservableProperty]
    private string _selectedThemeOption = SystemThemeOption;

    public IReadOnlyList<string> HttpVersionOptions { get; } =
    [
        "1.0",
        "1.1",
        "2.0",
        "3.0"
    ];

    [ObservableProperty]
    private string _selectedHttpVersionOption = "1.1";

    public IReadOnlyList<string> TlsVersionOptions { get; } =
    [
        "SystemDefault",
        "Tls10",
        "Tls11",
        "Tls12",
        "Tls13"
    ];

    [ObservableProperty]
    private string _selectedTlsVersionOption = "SystemDefault";

    [ObservableProperty]
    private bool _followHttpRedirects = true;

    [ObservableProperty]
    private bool _enableHttpDiagnostics;

    [ObservableProperty]
    private string _defaultRequestUrl = "https://postman-echo.com/get?hello=world";

    [ObservableProperty]
    private string _defaultContentType = "application/json";

    public IReadOnlyList<string> FontFamilyOptions { get; } =
    [
        "Cascadia Code,Consolas,Menlo,monospace",
        "Consolas,Menlo,monospace",
        "JetBrains Mono,Cascadia Code,Consolas,monospace"
    ];

    [ObservableProperty]
    private string _uiFontFamily = "Cascadia Code,Consolas,Menlo,monospace";

    [ObservableProperty]
    private string _uiFontSizeText = "13";

    [ObservableProperty]
    private bool _autoStartScheduledJobsOnLaunch = true;

    [ObservableProperty]
    private int _defaultScheduledJobIntervalSeconds = 60;

    [ObservableProperty]
    private string _selectedOptionsPage = "HTTP";

    partial void OnSelectedOptionsPageChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedOptionsPageTitle));
        OnPropertyChanged(nameof(SelectedOptionsPageBreadcrumb));
    }

    public string SelectedOptionsPageTitle => SelectedOptionsPage switch
    {
        "HTTP" => "HTTP",
        "ScheduledJobs" => "Scheduled Jobs",
        "LookAndFeel" => "Look & Feel",
        _ => SelectedOptionsPage
    };

    public string SelectedOptionsPageBreadcrumb => $"Options{OptionsPageBreadcrumbSeparator}{SelectedOptionsPageTitle}";

    public double UiFontSize =>
        double.TryParse(UiFontSizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 13d;

    // Content-Type selector
    public const string AuthNoneOption = "None";
    public const string AuthBearerOption = "Bearer Token";
    public const string AuthBasicOption = "Basic";
    public const string AuthApiKeyOption = "API Key";
    public const string AuthOAuth2ClientCredentialsOption = "OAuth 2 (Client Credentials)";
    public const string NoneContentTypeOption = "(none)";
    public const string CustomContentTypeOption = "Custom...";

    public IReadOnlyList<string> AuthModeOptions { get; } =
    [
        AuthNoneOption,
        AuthBearerOption,
        AuthBasicOption,
        AuthApiKeyOption,
        AuthOAuth2ClientCredentialsOption
    ];

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

    public bool IsBearerAuthMode => SelectedAuthModeOption == AuthBearerOption;
    public bool IsBasicAuthMode => SelectedAuthModeOption == AuthBasicOption;
    public bool IsApiKeyAuthMode => SelectedAuthModeOption == AuthApiKeyOption;
    public bool IsOAuth2ClientCredentialsAuthMode => SelectedAuthModeOption == AuthOAuth2ClientCredentialsOption;

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

    [ObservableProperty]
    private string _selectedContentTypeOption = NoneContentTypeOption;

    [ObservableProperty]
    private string _customContentType = string.Empty;

    public bool IsCustomContentType => SelectedContentTypeOption == CustomContentTypeOption;

    /// <summary>The actual Content-Type value to send (empty string means no Content-Type header).</summary>
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

    // Request headers preview panel (collapsible, live-updating)
    [ObservableProperty]
    private bool _isRequestHeadersPreviewVisible;

    public string RequestHeadersPreviewLabel =>
        IsRequestHeadersPreviewVisible ? "▼ Preview" : "▶ Preview";

    partial void OnIsRequestHeadersPreviewVisibleChanged(bool value) =>
        OnPropertyChanged(nameof(RequestHeadersPreviewLabel));

    public ObservableCollection<string> RequestHeadersPreview { get; } = [];

    // Response headers panel (populated after each successful request)
    public ObservableCollection<string> ResponseHeaders { get; } = [];

    [ObservableProperty]
    private bool _hasResponseHeaders;

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

    partial void OnSelectedMethodChanged(string value) =>
        RefreshRequestPreview();

    partial void OnSelectedHttpVersionOptionChanged(string value) =>
        LogRefreshRequestPreviewAndQueueOptionsAutoSave("Selected HTTP version", value);

    partial void OnSelectedTlsVersionOptionChanged(string value) =>
        LogAndQueueOptionsAutoSave("Selected TLS version changed to {TlsVersion}", value);

    partial void OnFollowHttpRedirectsChanged(bool value) =>
        LogAndQueueOptionsAutoSave("Follow redirects changed to {FollowRedirects}", value);

    partial void OnRequestBodyChanged(string value) =>
        RefreshRequestDerivedViews();

    partial void OnDefaultContentTypeChanged(string value)
    {
        _debugLogger.Information("Default content type changed to {ContentType}", value);
        RefreshRequestDerivedViews();
        QueueOptionsAutoSave();
    }

    partial void OnRequestUrlChanged(string value)
    {
        if (_isUpdatingRequestUrlFromQueryParameters)
        {
            return;
        }

        SyncQueryParametersFromRequestUrl(value);
        RefreshRequestPreview();
    }

    partial void OnUiFontSizeTextChanged(string value) =>
        OnUiFontSizeTextChangedCore();

    partial void OnUiFontFamilyChanged(string value)
    {
        if (Application.Current is not null)
        {
            Application.Current.Resources["AppFontFamily"] = new FontFamily(value);
        }

        QueueOptionsAutoSave();
    }

    partial void OnEnableHttpDiagnosticsChanged(bool value) =>
        LogAndQueueOptionsAutoSave("HTTP diagnostics enabled changed to {IsEnabled}", value);

    partial void OnSelectedCollectionChanged(Collection? value)
    {
        CollectionItems.Clear();
        if (value is not null)
        {
            foreach (var r in value.Requests)
            {
                CollectionItems.Add(new CollectionItemViewModel(r));
            }
        }
    }

    partial void OnSelectedThemeOptionChanged(string value)
    {
        _debugLogger.Information("Theme changed to {Theme}", value);

        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = value switch
        {
            DarkThemeOption => ThemeVariant.Dark,
            LightThemeOption => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };

        QueueOptionsAutoSave();
    }

    partial void OnDefaultRequestUrlChanged(string value) =>
        QueueOptionsAutoSave();

    partial void OnAutoStartScheduledJobsOnLaunchChanged(bool value) =>
        QueueOptionsAutoSave();

    partial void OnDefaultScheduledJobIntervalSecondsChanged(int value) =>
        QueueOptionsAutoSave();

    partial void OnNewEnvironmentNameChanged(string value) =>
        QueueEnvironmentAutoSave();

    private void OnUiFontSizeTextChangedCore()
    {
        OnPropertyChanged(nameof(UiFontSize));
        QueueOptionsAutoSave();
    }

    private void LogRefreshRequestPreviewAndQueueOptionsAutoSave(string operation, string value)
    {
        _debugLogger.Information("{Operation} changed to {Value}", operation, value);
        RefreshRequestPreview();
        QueueOptionsAutoSave();
    }

    private void LogAndQueueOptionsAutoSave<T>(string messageTemplate, T value)
    {
        _debugLogger.Information(messageTemplate, value);
        QueueOptionsAutoSave();
    }

    public MainWindowViewModel(
        HttpRequestService httpRequestService,
        IRequestHistoryRepository requestHistoryRepository,
        ICollectionRepository collectionRepository,
        IEnvironmentRepository environmentRepository,
        IScheduledJobRepository scheduledJobRepository,
        ScheduledJobService scheduledJobService,
        LogWindowViewModel logWindowViewModel,
        ILogger? logger = null,
        ApplicationOptionsStore? applicationOptionsStore = null,
        ApplicationOptions? initialOptions = null,
        Action<ApplicationOptions>? onApplicationOptionsChanged = null)
    {
        _httpRequestService = httpRequestService;
        _requestHistoryRepository = requestHistoryRepository;
        _collectionRepository = collectionRepository;
        _environmentRepository = environmentRepository;
        _scheduledJobRepository = scheduledJobRepository;
        _scheduledJobService = scheduledJobService;
        _logWindowViewModel = logWindowViewModel;
        _openApiImportService = new OpenApiImportService();
        _variableResolver = new VariableResolver();
        _applicationOptionsStore = applicationOptionsStore;
        _onApplicationOptionsChanged = onApplicationOptionsChanged;
        var appLogger = (logger ?? Log.Logger).ForContext<MainWindowViewModel>();
        _debugLogger = appLogger.ForContext("LogTab", Logging.LogTab.Debug);
        _httpRequestsLogger = appLogger.ForContext("LogTab", Logging.LogTab.HttpRequests);

        Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
        History = [];
        Collections = [];
        CollectionItems = [];
        Environments = [];
        ActiveEnvironmentVariables = [];
        RequestHeaders = [];
        RequestQueryParameters = [];
        ScheduledJobs = [];
        SavedLayoutNames = [];

        RequestHeaders.CollectionChanged += OnRequestHeadersCollectionChanged;
        RequestQueryParameters.CollectionChanged += OnRequestQueryParametersCollectionChanged;
        ActiveEnvironmentVariables.CollectionChanged += OnActiveEnvironmentVariablesCollectionChanged;

        SendRequestCommand = new AsyncRelayCommand(SendRequestAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);

        _dockFactory = new DockFactory(this);
        Layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(Layout);
        _defaultLayout = CaptureLayoutSnapshot();

        _suppressLayoutRestore = true;
        var options = initialOptions ?? new ApplicationOptions();
        ApplyOptions(options);
        ApplyLayoutOptions(options.Layouts);
        _suppressLayoutRestore = false;
        SyncQueryParametersFromRequestUrl(RequestUrl);
        RefreshRequestPreview();
    }

    public IReadOnlyList<string> Methods { get; }

    public ObservableCollection<SavedRequest> History { get; }
    public ObservableCollection<Collection> Collections { get; }
    public ObservableCollection<CollectionItemViewModel> CollectionItems { get; }
    public ObservableCollection<RequestEnvironment> Environments { get; }
    public ObservableCollection<EnvironmentVariableViewModel> ActiveEnvironmentVariables { get; }
    public ObservableCollection<RequestHeaderViewModel> RequestHeaders { get; }
    public ObservableCollection<RequestQueryParameterViewModel> RequestQueryParameters { get; }
    public ObservableCollection<ScheduledJobViewModel> ScheduledJobs { get; }
    public ObservableCollection<string> SavedLayoutNames { get; }
    public LogWindowViewModel LogWindowViewModel => _logWindowViewModel;

    public IAsyncRelayCommand SendRequestCommand { get; }
    public IAsyncRelayCommand LoadHistoryCommand { get; }

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

    private void OnActiveEnvironmentVariablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (EnvironmentVariableViewModel variable in e.NewItems)
            {
                variable.PropertyChanged += OnActiveEnvironmentVariablePropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (EnvironmentVariableViewModel variable in e.OldItems)
            {
                variable.PropertyChanged -= OnActiveEnvironmentVariablePropertyChanged;
            }
        }

        RefreshRequestPreview();
        QueueEnvironmentAutoSave();
    }

    private void OnActiveEnvironmentVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshRequestPreview();
        QueueEnvironmentAutoSave();
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

    private void RefreshRequestDerivedViews()
    {
        UpdateRequestHeadersPreview();
        RefreshRequestPreview();
    }

    private ApplicationOptions BuildOptionsFromCurrentState()
    {
        if (!double.TryParse(UiFontSizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize))
        {
            throw new InvalidDataException("Font size must be a number.");
        }

        var options = new ApplicationOptions
        {
            Http = new HttpOptions
            {
                HttpVersion = SelectedHttpVersionOption,
                TlsVersion = SelectedTlsVersionOption,
                EnableHttpDiagnostics = EnableHttpDiagnostics,
                DefaultContentType = DefaultContentType,
                FollowRedirects = FollowHttpRedirects,
                DefaultRequestUrl = DefaultRequestUrl
            },
            Appearance = new AppearanceOptions
            {
                Theme = SelectedThemeOption,
                FontFamily = UiFontFamily,
                FontSize = fontSize
            },
            ScheduledJobs = new ScheduledJobsOptions
            {
                AutoStartOnLaunch = AutoStartScheduledJobsOnLaunch,
                DefaultIntervalSeconds = Math.Max(1, DefaultScheduledJobIntervalSeconds)
            },
            Layouts = BuildLayoutOptions()
        };

        ApplicationOptionsStore.Validate(options);
        return options;
    }

    private void ApplyOptions(ApplicationOptions options, bool updateCurrentRequestUrl = true)
    {
        var previousDefaultFollowRedirects = _applicationOptions.Http.FollowRedirects;
        var previousDefaultUrl = _applicationOptions.Http.DefaultRequestUrl;
        _applicationOptions = options;

        var previousSuppressOptionsAutoSave = _suppressOptionsAutoSave;
        _suppressOptionsAutoSave = true;

        try
        {
            SelectedThemeOption = options.Appearance.Theme;
            SelectedHttpVersionOption = options.Http.HttpVersion;
            SelectedTlsVersionOption = options.Http.TlsVersion;
            EnableHttpDiagnostics = options.Http.EnableHttpDiagnostics;
            FollowHttpRedirects = options.Http.FollowRedirects;
            DefaultRequestUrl = options.Http.DefaultRequestUrl;
            DefaultContentType = options.Http.DefaultContentType;
            UiFontFamily = options.Appearance.FontFamily;
            UiFontSizeText = options.Appearance.FontSize.ToString("0.##", CultureInfo.InvariantCulture);
            AutoStartScheduledJobsOnLaunch = options.ScheduledJobs.AutoStartOnLaunch;
            DefaultScheduledJobIntervalSeconds = options.ScheduledJobs.DefaultIntervalSeconds;

            if (FollowRedirectsForRequest == previousDefaultFollowRedirects)
            {
                FollowRedirectsForRequest = options.Http.FollowRedirects;
            }

            if (updateCurrentRequestUrl || string.IsNullOrWhiteSpace(RequestUrl) || string.Equals(RequestUrl, previousDefaultUrl, StringComparison.Ordinal))
            {
                RequestUrl = options.Http.DefaultRequestUrl;
            }
        }
        finally
        {
            _suppressOptionsAutoSave = previousSuppressOptionsAutoSave;
        }
    }

    private void ApplyLayoutOptions(LayoutOptions? layouts)
    {
        _savedLayouts.Clear();
        SavedLayoutNames.Clear();

        if (layouts?.SavedLayouts is not null)
        {
            foreach (var namedLayout in layouts.SavedLayouts)
            {
                if (!string.IsNullOrWhiteSpace(namedLayout.Name) && namedLayout.Layout is not null)
                {
                    _savedLayouts[namedLayout.Name] = namedLayout.Layout;
                    SavedLayoutNames.Add(namedLayout.Name);
                }
            }
        }

        SelectedLayoutName = SavedLayoutNames.FirstOrDefault();
        ApplyLayoutSnapshot(layouts?.CurrentLayout);
        UpdateLayoutNameCounter();
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

    private void RefreshRequestPreview()
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

    private List<EnvironmentVariable> GetResolvedVariables() =>
        ActiveEnvironmentVariables
            .Where(v => !string.IsNullOrWhiteSpace(v.Name))
            .Select(v => new EnvironmentVariable(v.Name, v.Value))
            .ToList();

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

    private void UpdateResponsePresentation(string responseBody, IReadOnlyList<(string Name, string Value)> headers)
    {
        ResponseContentType = GetResponseContentType(headers);
        var mediaType = NormalizeMediaType(ResponseContentType);
        var isJson = IsJsonMediaType(mediaType);
        var isXml = IsXmlMediaType(mediaType);
        var isHtml = IsHtmlMediaType(mediaType);

        IsBinaryResponse = IsBinaryMediaType(mediaType);
        ResponseBodyTabLabel = isJson ? "JSON" : isXml ? "XML" : "Body";

        if (IsBinaryResponse)
        {
            ResponseBody = $"Binary response ({mediaType}). Use \"Save and Open\" to inspect the content.";
            RawResponseBody = ResponseBody;
            return;
        }

        if (isJson && TryFormatJson(responseBody, out var formattedJson))
        {
            ResponseBody = formattedJson;
            return;
        }

        if ((isXml || isHtml) && TryFormatXml(responseBody, out var formattedXml))
        {
            ResponseBody = formattedXml;
            return;
        }

        ResponseBody = responseBody;
    }

    private static string GetResponseContentType(IReadOnlyList<(string Name, string Value)> headers) =>
        headers.FirstOrDefault(h => string.Equals(h.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

    private static string NormalizeMediaType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var semicolonIndex = contentType.IndexOf(';');
        var mediaType = semicolonIndex >= 0 ? contentType[..semicolonIndex] : contentType;
        return mediaType.Trim().ToLowerInvariant();
    }

    private static bool IsJsonMediaType(string mediaType) =>
        mediaType == "application/json" || mediaType.EndsWith("+json", StringComparison.Ordinal);

    private static bool IsXmlMediaType(string mediaType) =>
        mediaType is "application/xml" or "text/xml" || mediaType.EndsWith("+xml", StringComparison.Ordinal);

    private static bool IsHtmlMediaType(string mediaType) =>
        mediaType == "text/html";

    private static bool IsBinaryMediaType(string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        if (mediaType.StartsWith("text/", StringComparison.Ordinal) || IsJsonMediaType(mediaType) || IsXmlMediaType(mediaType) || IsHtmlMediaType(mediaType))
        {
            return false;
        }

        return mediaType.StartsWith("image/", StringComparison.Ordinal)
               || mediaType.StartsWith("audio/", StringComparison.Ordinal)
               || mediaType.StartsWith("video/", StringComparison.Ordinal)
               || mediaType is "application/octet-stream"
               || mediaType is "application/zip"
               || mediaType is "application/pdf"
               || mediaType is "application/msword"
               || mediaType is "application/vnd.ms-excel"
               || mediaType is "application/vnd.ms-powerpoint"
               || mediaType.StartsWith("application/vnd.openxmlformats-officedocument.", StringComparison.Ordinal);
    }

    private static bool TryFormatJson(string input, out string formatted)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(input);
            formatted = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch
        {
            formatted = string.Empty;
            return false;
        }
    }

    private static bool TryFormatXml(string input, out string formatted)
    {
        try
        {
            var document = XDocument.Parse(input, LoadOptions.PreserveWhitespace);
            using var writer = new StringWriter();
            using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings
            {
                OmitXmlDeclaration = false,
                Indent = true,
                NewLineOnAttributes = false
            });
            document.Save(xmlWriter);
            xmlWriter.Flush();
            formatted = writer.ToString();
            return true;
        }
        catch
        {
            formatted = string.Empty;
            return false;
        }
    }


    private static Version ParseHttpVersion(string value) => value switch
    {
        "1.0" => global::System.Net.HttpVersion.Version10,
        "1.1" => global::System.Net.HttpVersion.Version11,
        "2.0" => global::System.Net.HttpVersion.Version20,
        "3.0" => global::System.Net.HttpVersion.Version30,
        _ => global::System.Net.HttpVersion.Version11
    };

    [RelayCommand]
    private void AddScheduledJob()
    {
        var vm = new ScheduledJobViewModel(_scheduledJobRepository, _scheduledJobService)
        {
            IntervalSeconds = Math.Max(MinScheduledJobIntervalSeconds, DefaultScheduledJobIntervalSeconds),
            FollowRedirects = FollowHttpRedirects
        };
        ScheduledJobs.Add(vm);
        LeftPanelTab = "ScheduledJobs";
    }

    [RelayCommand]
    private async Task RemoveScheduledJobAsync(ScheduledJobViewModel? job)
    {
        if (job is null)
        {
            return;
        }

        _scheduledJobService.Stop(job.Id);
        if (job.Id != 0)
        {
            await _scheduledJobRepository.DeleteAsync(job.Id);
        }

        ScheduledJobs.Remove(job);
    }

    partial void OnHistorySearchQueryChanged(string value) => ApplyHistoryFilter(value);

    partial void OnActiveEnvironmentChanged(RequestEnvironment? value)
    {
        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            ActiveEnvironmentVariables.Clear();
            if (value is not null)
            {
                foreach (var v in value.Variables)
                {
                    ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel(v.Name, v.Value));
                }
            }
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
        }

        RefreshRequestPreview();
    }

    [RelayCommand]
    private void ShowHistoryTab() => LeftPanelTab = "History";

    [RelayCommand]
    private void ShowCollectionsTab() => LeftPanelTab = "Collections";

    [RelayCommand]
    private void ShowScheduledJobsTab() => LeftPanelTab = "ScheduledJobs";

    /// <summary>Set by the view layer to close the main window.</summary>
    public Action? ExitApplicationAction { get; set; }

    [RelayCommand]
    private void OpenLogWindow()
    {
        if (_dockFactory?.LeftToolDock is { } dock &&
            _dockFactory.LogPanelViewModel is { } logPanelVm)
        {
            dock.ActiveDockable = logPanelVm;
        }
    }

    [RelayCommand]
    private void ExitApplication() => ExitApplicationAction?.Invoke();

    [RelayCommand]
    private void OpenOptions()
    {
        if (_dockFactory?.LeftToolDock is { } dock &&
            _dockFactory.OptionsViewModel is { } optVm)
        {
            dock.ActiveDockable = optVm;
        }
    }

    [RelayCommand]
    private void ToggleEnvironmentPanel() => IsEnvironmentPanelVisible = !IsEnvironmentPanelVisible;

    [RelayCommand]
    private void ToggleLayoutPanel() => IsLayoutPanelVisible = !IsLayoutPanelVisible;

    partial void OnSelectedLayoutNameChanged(string? value)
    {
        if (_suppressLayoutRestore || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (_savedLayouts.TryGetValue(value, out var snapshot))
        {
            ApplyLayoutSnapshot(snapshot);
            PersistLayoutOptions();
        }
    }

    [RelayCommand]
    private void SaveLayoutAsNew()
    {
        var layoutSnapshot = CaptureLayoutSnapshot();
        if (layoutSnapshot is null)
        {
            return;
        }

        var name = GenerateNextLayoutName();
        _savedLayouts[name] = layoutSnapshot;
        _suppressLayoutRestore = true;
        RefreshSavedLayoutNames();
        SelectedLayoutName = name;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [RelayCommand]
    private void SaveLayoutToExisting(string? layoutName)
    {
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return;
        }

        var layoutSnapshot = CaptureLayoutSnapshot();
        if (layoutSnapshot is null)
        {
            return;
        }

        _savedLayouts[layoutName] = layoutSnapshot;
        _suppressLayoutRestore = true;
        RefreshSavedLayoutNames();
        SelectedLayoutName = layoutName;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [RelayCommand]
    private void RestoreDefaultLayout()
    {
        if (_defaultLayout is not null)
        {
            ApplyLayoutSnapshot(_defaultLayout);
            _suppressLayoutRestore = true;
            SelectedLayoutName = null;
            _suppressLayoutRestore = false;
            PersistLayoutOptions();
        }
    }

    [RelayCommand]
    private void RemoveLayout(string? layoutName)
    {
        if (string.IsNullOrWhiteSpace(layoutName))
        {
            return;
        }

        if (_savedLayouts.Remove(layoutName))
        {
            _suppressLayoutRestore = true;
            RefreshSavedLayoutNames();
            if (string.Equals(SelectedLayoutName, layoutName, StringComparison.OrdinalIgnoreCase))
            {
                SelectedLayoutName = SavedLayoutNames.FirstOrDefault();
            }
            _suppressLayoutRestore = false;
            PersistLayoutOptions();
        }
    }

    [RelayCommand]
    private void LoadCollectionRequest(CollectionItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedMethod = item.Method;

        var baseUrl = ActiveEnvironment is not null
            ? _variableResolver.Resolve(SelectedCollection?.BaseUrl ?? string.Empty, GetResolvedVariables())
            : (SelectedCollection?.BaseUrl ?? string.Empty);

        RequestUrl = baseUrl.TrimEnd('/') + item.Path;
        RequestName = item.Name;

        if (item.Method is "POST" or "PUT" or "PATCH")
        {
            RequestBody = "{}";
        }
        else
        {
            RequestBody = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ImportCollectionAsync()
    {
        if (StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import OpenAPI Specification",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("OpenAPI Specification")
                {
                    Patterns = ["*.json", "*.yaml", "*.yml"]
                }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            await using var stream = await files[0].OpenReadAsync();
            var collection = _openApiImportService.Import(stream, files[0].Path.LocalPath);
            var id = await _collectionRepository.SaveAsync(
                collection.Name,
                collection.SourcePath,
                collection.BaseUrl,
                collection.Requests);

            await LoadCollectionsAsync();
            SelectedCollection = Collections.FirstOrDefault(c => c.Id == id);
            LeftPanelTab = "Collections";
            _debugLogger.Information("Imported collection {CollectionName} from {Path}", collection.Name, files[0].Path.LocalPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteCollectionAsync(Collection? collection)
    {
        if (collection is null)
        {
            return;
        }

        await _collectionRepository.DeleteAsync(collection.Id);
        _debugLogger.Information("Deleted collection {CollectionName}", collection.Name);
        if (SelectedCollection?.Id == collection.Id)
        {
            SelectedCollection = null;
        }

        await LoadCollectionsAsync();
    }

    [RelayCommand]
    private void SaveOptions()
    {
        try
        {
            ErrorMessage = string.Empty;
            var options = BuildOptionsFromCurrentState();
            _applicationOptionsStore?.Save(options);
            ApplyOptions(options, updateCurrentRequestUrl: false);
            _onApplicationOptionsChanged?.Invoke(options);
            _debugLogger.Information("Saved application options");
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Options could not be saved: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportOptionsAsync()
    {
        if (StorageProvider is null)
        {
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            var options = BuildOptionsFromCurrentState();
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Options",
                SuggestedFileName = "arbor-options.json",
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON")
                    {
                        Patterns = ["*.json"]
                    }
                ]
            });

            if (file is null)
            {
                return;
            }

            _applicationOptionsStore?.Export(file.Path.LocalPath, options);
            _debugLogger.Information("Exported options to {Path}", file.Path.LocalPath);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Options export failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportOptionsAsync()
    {
        if (StorageProvider is null || _applicationOptionsStore is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Options",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            var options = _applicationOptionsStore.Import(files[0].Path.LocalPath);
            _applicationOptionsStore.Save(options);
            ApplyOptions(options, updateCurrentRequestUrl: false);
            _onApplicationOptionsChanged?.Invoke(options);
            _debugLogger.Information("Imported options from {Path}", files[0].Path.LocalPath);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Options import failed: {exception.Message}";
        }
    }

    [RelayCommand]
    private void AddEnvironmentVariable()
    {
        ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel(string.Empty, string.Empty));
        _debugLogger.Information("Added environment variable placeholder");
    }

    [RelayCommand]
    private void RemoveEnvironmentVariable(EnvironmentVariableViewModel? variable)
    {
        if (variable is not null)
        {
            ActiveEnvironmentVariables.Remove(variable);
            _debugLogger.Information("Removed environment variable {VariableName}", variable.Name);
        }
    }

    [RelayCommand]
    private Task SaveEnvironmentAsync() => SaveEnvironmentCoreAsync(closeEnvironmentPanel: true);

    private async Task SaveEnvironmentCoreAsync(bool closeEnvironmentPanel)
    {
        if (string.IsNullOrWhiteSpace(NewEnvironmentName))
        {
            return;
        }

        if (_isSavingEnvironment)
        {
            return;
        }

        _isSavingEnvironment = true;
        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
        var variables = ActiveEnvironmentVariables
            .Where(v => !string.IsNullOrWhiteSpace(v.Name))
            .Select(v => new EnvironmentVariable(v.Name, v.Value))
            .ToList();

        int? newEnvId = null;
        if (ActiveEnvironment is not null)
        {
            await _environmentRepository.UpdateAsync(ActiveEnvironment.Id, NewEnvironmentName, variables);
            _debugLogger.Information("Updated environment {EnvironmentName}", NewEnvironmentName);
        }
        else
        {
            newEnvId = await _environmentRepository.SaveAsync(NewEnvironmentName, variables);
            _debugLogger.Information("Created environment {EnvironmentName}", NewEnvironmentName);
        }

        await LoadEnvironmentsAsync();

        if (newEnvId.HasValue)
        {
            ActiveEnvironment = Environments.FirstOrDefault(e => e.Id == newEnvId.Value);
        }

        if (closeEnvironmentPanel)
        {
            IsEnvironmentPanelVisible = false;
        }
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
            _isSavingEnvironment = false;
        }
    }

    [RelayCommand]
    private async Task DeleteEnvironmentAsync(RequestEnvironment? environment)
    {
        if (environment is null)
        {
            return;
        }

        await _environmentRepository.DeleteAsync(environment.Id);
        _debugLogger.Information("Deleted environment {EnvironmentName}", environment.Name);
        if (ActiveEnvironment?.Id == environment.Id)
        {
            ActiveEnvironment = null;
        }

        await LoadEnvironmentsAsync();
    }

    [RelayCommand]
    private void EditEnvironment(RequestEnvironment? environment)
    {
        if (environment is null)
        {
            return;
        }

        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            ActiveEnvironment = environment;
            NewEnvironmentName = environment.Name;
            _debugLogger.Information("Editing environment {EnvironmentName}", environment.Name);
            ActiveEnvironmentVariables.Clear();
            foreach (var v in environment.Variables)
            {
                ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel(v.Name, v.Value));
            }

            IsEnvironmentPanelVisible = true;
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
        }
    }

    [RelayCommand]
    private void NewEnvironment()
    {
        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            ActiveEnvironment = null;
            NewEnvironmentName = string.Empty;
            ActiveEnvironmentVariables.Clear();
            IsEnvironmentPanelVisible = true;
            _debugLogger.Information("Creating new environment");
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
        }
    }

    [RelayCommand]
    private void OpenRequestBodyInExternalEditor()
    {
        _requestBodyWatcher?.Dispose();
        _requestBodyWatcher = null;

        var path = WriteTempFile("arbor-request", RequestBody);

        var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnRequestBodyFileChanged;
        _requestBodyWatcher = watcher;

        OpenWithShell(path);
    }

    [RelayCommand]
    private void OpenResponseBodyInExternalEditor()
    {
        var path = WriteTempFile("arbor-response", ResponseBody);
        OpenWithShell(path);
    }

    [RelayCommand]
    private async Task SaveBinaryResponseAndOpenAsync()
    {
        if (!IsBinaryResponse || _lastResponseBodyBytes.Length == 0 || StorageProvider is null)
        {
            return;
        }

        var extension = ExtensionFromContentType(ResponseContentType);

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Response",
            SuggestedFileName = $"response{extension}",
            FileTypeChoices =
            [
                new FilePickerFileType("Response file")
                {
                    Patterns = [$"*{extension}"]
                }
            ]
        });

        if (file is null)
        {
            return;
        }

        await File.WriteAllBytesAsync(file.Path.LocalPath, _lastResponseBodyBytes);
        OpenWithShell(file.Path.LocalPath);
    }

    /// <summary>
    /// Copies the given history item to the clipboard formatted as a single-line
    /// <c>curl</c> command. Matches the "Copy as cURL" action available in
    /// Hoppscotch, Insomnia, and the browser devtools network panel.
    /// No-op when the clipboard or request is unavailable.
    /// </summary>
    [RelayCommand]
    private async Task CopyHistoryItemAsCurlAsync(SavedRequest? request)
    {
        if (request is null || Clipboard is null)
        {
            return;
        }

        var command = CurlFormatter.Format(request);
        await Clipboard.SetTextAsync(command);
    }

    public void Dispose()
    {
        _optionsAutoSaveCts?.Cancel();
        _optionsAutoSaveCts?.Dispose();
        _environmentAutoSaveCts?.Cancel();
        _environmentAutoSaveCts?.Dispose();
        _scheduledJobService.Dispose();
        _requestBodyWatcher?.Dispose();
        _requestBodyWatcher = null;

        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); }
            catch { /* best-effort cleanup */ }
        }
    }

    public void PersistCurrentLayout() => PersistLayoutOptions();

    /// <summary>
    /// Returns a snapshot of the current layout options (including floating windows).
    /// Exposed for testing the save/restore cycle without a real ApplicationOptionsStore.
    /// </summary>
    public LayoutOptions CaptureCurrentLayout() => BuildLayoutOptions();

    /// <summary>
    /// Closes all floating dock windows via the factory so they are removed from
    /// <see cref="Layout"/>.Windows before the main window tears down.
    /// Call this from <c>OnClosing</c> (after <see cref="PersistCurrentLayout"/>) so
    /// positions are captured before teardown and no NPE occurs when Avalonia later
    /// tries to close the already-gone owned windows.
    /// </summary>
    public void CloseFloatingWindows()
    {
        if (Layout?.Windows is not { Count: > 0 } || _dockFactory is null)
        {
            return;
        }

        // Take a snapshot of the list: RemoveWindow modifies it during iteration.
        // RemoveWindow sets window.Owner = null after the first call, so any
        // re-entrant call from HostWindow.Closed is a safe no-op.
        foreach (var win in Layout.Windows.ToList())
        {
            _dockFactory.RemoveWindow(win);
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            LoadHistoryAsync(cancellationToken),
            LoadCollectionsAsync(cancellationToken),
            LoadEnvironmentsAsync(cancellationToken),
            LoadScheduledJobsAsync(cancellationToken)).ConfigureAwait(false);
    }

    private void OnRequestBodyFileChanged(object sender, FileSystemEventArgs e)
    {
        if (Interlocked.Exchange(ref _requestBodyReadPending, 1) == 1)
        {
            return;
        }

        Task.Run(async () =>
        {
            await Task.Delay(200).ConfigureAwait(false);
            Interlocked.Exchange(ref _requestBodyReadPending, 0);
            try
            {
                var content = await File.ReadAllTextAsync(e.FullPath).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() => RequestBody = content);
            }
            catch
            {
                // ignore transient read errors while the editor is still writing
            }
        });
    }

    private string WriteTempFile(string prefix, string content)
    {
        var ext = !string.IsNullOrEmpty(ContentType)
            ? ExtensionFromContentType(ContentType)
            : DetectExtensionFromContent(content);
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}{ext}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    internal static string ExtensionFromContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return ".txt";
        }

        var mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return mediaType switch
        {
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            "text/html" => ".html",
            "application/pdf" => ".pdf",
            "application/zip" => ".zip",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            _ => ".txt"
        };
    }

    internal static string DetectExtensionFromContent(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return ".json";
        }

        if (trimmed.StartsWith('<'))
        {
            return ".xml";
        }

        return ".txt";
    }

    private static void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // no associated application – silently ignore
        }
    }

    private LayoutOptions BuildLayoutOptions() =>
        new()
        {
            CurrentLayout = CaptureLayoutSnapshot(),
            SavedLayouts = _savedLayouts
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => new NamedDockLayout
                {
                    Name = item.Key,
                    Layout = item.Value
                })
                .ToList()
        };

    private void PersistLayoutOptions()
    {
        if (_applicationOptionsStore is null)
        {
            return;
        }

        try
        {
            var updatedOptions = new ApplicationOptions
            {
                Http = _applicationOptions.Http,
                Appearance = _applicationOptions.Appearance,
                ScheduledJobs = _applicationOptions.ScheduledJobs,
                Layouts = BuildLayoutOptions()
            };

            _applicationOptionsStore.Save(updatedOptions);
            _applicationOptions = updatedOptions;
        }
        catch
        {
            // best-effort persistence for implicit layout save
        }
    }

    private DockLayoutSnapshot? CaptureLayoutSnapshot()
    {
        var root = Layout;
        if (root is null)
        {
            return null;
        }

        var leftToolDock = FindDockById<ToolDock>(root, "left-tool-dock");
        var documentDock = FindDockById<DocumentDock>(root, "document-dock");
        if (leftToolDock is null || documentDock is null)
        {
            return null;
        }

        var floatingWindows = new List<FloatingWindowSnapshot>();
        if (root.Windows is not null)
        {
            foreach (var window in root.Windows)
            {
                var floatRoot = window.Layout;
                if (floatRoot is null)
                {
                    continue;
                }

                var ids = new List<string>();
                CollectDockableIds(floatRoot, ids);

                floatingWindows.Add(new FloatingWindowSnapshot
                {
                    X = window.X,
                    Y = window.Y,
                    Width = window.Width > 0 ? window.Width : 300,
                    Height = window.Height > 0 ? window.Height : 400,
                    DockableIds = ids,
                    ActiveDockableId = floatRoot.ActiveDockable?.Id
                });
            }
        }

        return new DockLayoutSnapshot
        {
            LeftToolProportion = leftToolDock.Proportion,
            DocumentProportion = documentDock.Proportion,
            ActiveToolDockableId = leftToolDock.ActiveDockable?.Id,
            ActiveDocumentDockableId = documentDock.ActiveDockable?.Id,
            LeftToolDockableOrder = GetDockableOrder(leftToolDock.VisibleDockables),
            DocumentDockableOrder = GetDockableOrder(documentDock.VisibleDockables),
            FloatingWindows = floatingWindows
        };
    }

    private static void CollectDockableIds(IDockable dockable, ICollection<string> ids)
    {
        if (!string.IsNullOrWhiteSpace(dockable.Id))
        {
            ids.Add(dockable.Id);
        }

        if (dockable is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (var child in dock.VisibleDockables)
            {
                CollectDockableIds(child, ids);
            }
        }
    }

    private void ApplyLayoutSnapshot(DockLayoutSnapshot? snapshot)
    {
        if (snapshot is null || Layout is null || _dockFactory is null)
        {
            return;
        }

        // When floating windows are present in the current session the dockables
        // inside them have been moved OUT of leftToolDock/documentDock.  Closing
        // those windows via CloseWindow permanently destroys the dockables, so
        // FindDockById would never find them again for re-floating.  The safest
        // fix is to recreate the entire dock layout so all dockables start in
        // their known home positions before we apply the snapshot.
        if (Layout.Windows is { Count: > 0 })
        {
            Layout = _dockFactory.CreateLayout();
            _dockFactory.InitLayout(Layout);
            OnPropertyChanged(nameof(Layout));
        }

        var leftToolDock = FindDockById<ToolDock>(Layout, "left-tool-dock");
        var documentDock = FindDockById<DocumentDock>(Layout, "document-dock");
        if (leftToolDock is null || documentDock is null)
        {
            return;
        }

        if (snapshot.LeftToolProportion > 0)
        {
            leftToolDock.Proportion = snapshot.LeftToolProportion;
        }

        if (snapshot.DocumentProportion > 0)
        {
            documentDock.Proportion = snapshot.DocumentProportion;
        }

        ApplyDockOrder(leftToolDock, snapshot.LeftToolDockableOrder);
        ApplyDockOrder(documentDock, snapshot.DocumentDockableOrder);
        SetActiveDockable(leftToolDock, snapshot.ActiveToolDockableId);
        SetActiveDockable(documentDock, snapshot.ActiveDocumentDockableId);

        // Restore floating windows
        foreach (var fw in snapshot.FloatingWindows)
        {
            if (fw.DockableIds.Count == 0)
            {
                continue;
            }

            // Find the first dockable to float (it creates the floating window)
            IDockable? primary = null;
            foreach (var id in fw.DockableIds)
            {
                primary = FindDockById<IDockable>(leftToolDock, id)
                       ?? FindDockById<IDockable>(documentDock, id);
                if (primary is not null)
                {
                    break;
                }
            }

            if (primary is null)
            {
                continue;
            }

            var countBefore = Layout.Windows?.Count ?? 0;
            _dockFactory.FloatDockable(primary);

            // FloatDockable may not have created a window (e.g. if already floating), so guard
            if (Layout.Windows is null || Layout.Windows.Count <= countBefore)
            {
                continue;
            }

            var floatWin = Layout.Windows[^1];
            floatWin.X = fw.X;
            floatWin.Y = fw.Y;
            floatWin.Width = fw.Width;
            floatWin.Height = fw.Height;

            // Move remaining dockables into the same floating window
            if (floatWin.Layout is IDock floatDock)
            {
                for (var i = 1; i < fw.DockableIds.Count; i++)
                {
                    var extra = FindDockById<IDockable>(leftToolDock, fw.DockableIds[i])
                             ?? FindDockById<IDockable>(documentDock, fw.DockableIds[i]);
                    if (extra?.Owner is IDock sourceOwner)
                    {
                        _dockFactory.MoveDockable(sourceOwner, floatDock, extra, null);
                    }
                }

                SetActiveDockable(floatDock, fw.ActiveDockableId);
            }
        }
    }

    private static void ApplyDockOrder(IDock dock, IReadOnlyList<string> orderedDockableIds)
    {
        var visibleDockables = dock.VisibleDockables;
        if (visibleDockables is null || orderedDockableIds.Count == 0)
        {
            return;
        }

        var byId = visibleDockables
            .Where(d => !string.IsNullOrWhiteSpace(d.Id))
            .ToDictionary(d => d.Id!, StringComparer.OrdinalIgnoreCase);

        var reordered = new List<IDockable>(visibleDockables.Count);
        foreach (var id in orderedDockableIds)
        {
            if (byId.TryGetValue(id, out var dockable) && !reordered.Contains(dockable))
            {
                reordered.Add(dockable);
            }
        }

        foreach (var dockable in visibleDockables)
        {
            if (!reordered.Contains(dockable))
            {
                reordered.Add(dockable);
            }
        }

        visibleDockables.Clear();
        foreach (var dockable in reordered)
        {
            visibleDockables.Add(dockable);
        }
    }

    private static void SetActiveDockable(IDock dock, string? dockableId)
    {
        if (string.IsNullOrWhiteSpace(dockableId) || dock.VisibleDockables is null)
        {
            return;
        }

        var activeDockable = dock.VisibleDockables.FirstOrDefault(item => string.Equals(item.Id, dockableId, StringComparison.OrdinalIgnoreCase));
        if (activeDockable is not null)
        {
            dock.ActiveDockable = activeDockable;
        }
    }

    private static T? FindDockById<T>(IDockable dockable, string id) where T : class, IDockable
    {
        if (dockable is T foundDockable && string.Equals(foundDockable.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return foundDockable;
        }

        if (dockable is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (var child in dock.VisibleDockables)
            {
                var childDock = FindDockById<T>(child, id);
                if (childDock is not null)
                {
                    return childDock;
                }
            }
        }

        return null;
    }

    private static List<string> GetDockableOrder(IList<IDockable>? dockables) =>
        dockables?
            .Select(d => d.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList() ?? [];

    private string GenerateNextLayoutName()
    {
        UpdateLayoutNameCounter();
        return $"Layout {_layoutNameCounter++}";
    }

    private void RefreshSavedLayoutNames()
    {
        var names = _savedLayouts.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        SavedLayoutNames.Clear();
        foreach (var name in names)
        {
            SavedLayoutNames.Add(name);
        }
    }

    private void UpdateLayoutNameCounter()
    {
        while (_savedLayouts.ContainsKey($"Layout {_layoutNameCounter}"))
        {
            _layoutNameCounter++;
        }
    }

    private void ApplyHistoryFilter(string query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allHistory
            : _allHistory
                .Where(item =>
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Url.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Method.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // Remove items no longer in the filtered set
        for (var i = History.Count - 1; i >= 0; i--)
        {
            if (!filtered.Contains(History[i]))
            {
                History.RemoveAt(i);
            }
        }

        // Append items that are missing, maintaining order
        for (var i = 0; i < filtered.Count; i++)
        {
            if (i >= History.Count || !ReferenceEquals(History[i], filtered[i]))
            {
                History.Insert(i, filtered[i]);
            }
        }
    }

    private async Task SendRequestAsync()
    {
        try
        {
            ErrorMessage = string.Empty;

            var variables = GetResolvedVariables();
            var resolvedUrl = _variableResolver.Resolve(RequestUrl, variables);
            var resolvedBody = _variableResolver.Resolve(RequestBody, variables);
            var headers = BuildResolvedHeaders(variables, resolvedBody);

            _httpRequestsLogger.Information("Manual request started: {Method} {Url}", SelectedMethod, resolvedUrl);

            var response = await _httpRequestService.SendAsync(
                new HttpRequestDraft(
                    RequestName,
                    SelectedMethod,
                    resolvedUrl,
                    resolvedBody,
                    headers,
                    ParseHttpVersion(SelectedHttpVersionOption),
                    FollowRedirectsForRequest));

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}";
            ResponseStatusCode = response.StatusCode;
            ResponseTimeDisplay = FormatElapsedMilliseconds(response.ElapsedMilliseconds);
            ResponseSizeDisplay = FormatByteSize(response.BodyBytes?.LongLength ?? 0);
            _lastResponseBodyBytes = response.BodyBytes ?? Array.Empty<byte>();
            RawResponseBody = response.Body;

            ResponseHeaders.Clear();
            foreach (var (name, value) in response.Headers)
            {
                ResponseHeaders.Add($"{name}: {value}");
            }

            HasResponseHeaders = ResponseHeaders.Count > 0;
            UpdateResponsePresentation(response.Body, response.Headers);
            _httpRequestsLogger.Information("Manual request completed: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);

            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            _httpRequestsLogger.Error(exception, "Manual request failed");
            ErrorMessage = exception.Message;
            ResponseStatusCode = 0;
            ResponseTimeDisplay = string.Empty;
            ResponseSizeDisplay = string.Empty;
        }
    }

    public static string FormatElapsedMilliseconds(double milliseconds)
    {
        if (milliseconds < 0)
        {
            milliseconds = 0;
        }

        if (milliseconds < 1000)
        {
            return $"{Math.Round(milliseconds)} ms";
        }

        var seconds = milliseconds / 1000.0;
        return seconds < 60
            ? $"{seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} s"
            : $"{((long)seconds) / 60} min {((long)seconds) % 60} s";
    }

    public static string FormatByteSize(long byteCount)
    {
        if (byteCount < 0)
        {
            byteCount = 0;
        }

        const double kilobyte = 1024.0;
        const double megabyte = kilobyte * 1024.0;
        const double gigabyte = megabyte * 1024.0;

        if (byteCount < kilobyte)
        {
            return $"{byteCount} B";
        }
        if (byteCount < megabyte)
        {
            return $"{(byteCount / kilobyte).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} KB";
        }
        if (byteCount < gigabyte)
        {
            return $"{(byteCount / megabyte).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} MB";
        }
        return $"{(byteCount / gigabyte).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} GB";
    }

    private async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        var requests = (await _requestHistoryRepository.GetRecentAsync(100, cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        _allHistory.Clear();
        _allHistory.AddRange(requests);

        ApplyHistoryFilter(HistorySearchQuery);
    }

    private async Task LoadCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _collectionRepository.GetAllAsync(cancellationToken);

        Collections.Clear();
        foreach (var c in all)
        {
            Collections.Add(c);
        }
    }

    private async Task LoadEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _environmentRepository.GetAllAsync(cancellationToken);

        var previousId = ActiveEnvironment?.Id;

        var previousSuppressEnvironmentAutoSave = _suppressEnvironmentAutoSave;
        _suppressEnvironmentAutoSave = true;
        try
        {
            // Explicitly null out ActiveEnvironment before clearing the collection so that
            // the ComboBox TwoWay binding cannot write null back after we restore below.
            ActiveEnvironment = null;

            Environments.Clear();
            foreach (var e in all)
            {
                Environments.Add(e);
            }

            if (previousId.HasValue)
            {
                ActiveEnvironment = Environments.FirstOrDefault(e => e.Id == previousId.Value);
            }
        }
        finally
        {
            _suppressEnvironmentAutoSave = previousSuppressEnvironmentAutoSave;
        }
    }

    private void QueueOptionsAutoSave()
    {
        if (_suppressOptionsAutoSave || _applicationOptionsStore is null)
        {
            return;
        }

        _optionsAutoSaveCts?.Cancel();
        _optionsAutoSaveCts?.Dispose();
        _optionsAutoSaveCts = new CancellationTokenSource();
        _ = TriggerOptionsAutoSaveAsync(_optionsAutoSaveCts.Token);
    }

    private async Task TriggerOptionsAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(SaveOptions);
        }
        catch (OperationCanceledException)
        {
            // Debounced auto-save was superseded by a newer edit.
        }
    }

    private void QueueEnvironmentAutoSave()
    {
        if (_suppressEnvironmentAutoSave ||
            _isSavingEnvironment ||
            !IsEnvironmentPanelVisible ||
            string.IsNullOrWhiteSpace(NewEnvironmentName) ||
            ActiveEnvironmentVariables.Any(variable => string.IsNullOrWhiteSpace(variable.Name)))
        {
            return;
        }

        _environmentAutoSaveCts?.Cancel();
        _environmentAutoSaveCts?.Dispose();
        _environmentAutoSaveCts = new CancellationTokenSource();
        _ = TriggerEnvironmentAutoSaveAsync(_environmentAutoSaveCts.Token);
    }

    private async Task TriggerEnvironmentAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(async () => await SaveEnvironmentCoreAsync(closeEnvironmentPanel: false));
        }
        catch (OperationCanceledException)
        {
            // Debounced auto-save was superseded by a newer edit.
        }
    }

    private async Task LoadScheduledJobsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _scheduledJobRepository.GetAllAsync(cancellationToken);

        ScheduledJobs.Clear();
        foreach (var config in all)
        {
            var vm = ScheduledJobViewModel.FromConfig(config, _scheduledJobRepository, _scheduledJobService, FollowHttpRedirects);
            ScheduledJobs.Add(vm);

            if (AutoStartScheduledJobsOnLaunch && config.AutoStart && !_scheduledJobService.IsRunning(config.Id))
            {
                _scheduledJobService.Start(config);
                vm.IsRunning = true;
            }
        }
    }
}
