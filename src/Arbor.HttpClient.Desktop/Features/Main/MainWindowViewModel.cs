using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Arbor.HttpClient.Desktop.Demo;
using Arbor.HttpClient.Desktop.Features.Collections;
using Arbor.HttpClient.Desktop.Features.Cookies;
using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.Environments;
using Arbor.HttpClient.Desktop.Features.GraphQl;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.Options;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.Sse;
using Arbor.HttpClient.Desktop.Features.WebSocket;
using Arbor.HttpClient.Desktop.Shared;
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
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.OpenApiImport;
using Arbor.HttpClient.Core.ScheduledJobs;
using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Desktop.Features.Main;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly HttpRequestService _httpRequestService;
    private readonly IRequestHistoryRepository _requestHistoryRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IScheduledJobRepository _scheduledJobRepository;
    private readonly ScheduledJobService _scheduledJobService;
    private readonly LogWindowViewModel _logWindowViewModel;
    private readonly OpenApiImportService _openApiImportService;
    private readonly VariableResolver _variableResolver;
    private readonly ApplicationOptionsStore? _applicationOptionsStore;
    private readonly Action<ApplicationOptions>? _onApplicationOptionsChanged;
    private readonly ILogger _debugLogger;
    private readonly ILogger _httpRequestsLogger;
    private readonly global::System.Net.Http.HttpClient _protocolHttpClient = new();
    private RequestEditorViewModel _requestEditor = null!;
    private EnvironmentsViewModel _environmentsViewModel = null!;
    private OptionsViewModel _optionsViewModel = null!;
    private CookieJarViewModel _cookieJarViewModel = null!;
    private GraphQlViewModel _graphQlViewModel = null!;
    private WebSocketViewModel _webSocketViewModel = null!;
    private SseViewModel _sseViewModel = null!;
    private CancellationTokenSource? _streamingCts;
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
    private CancellationTokenSource? _optionsAutoSaveCts;
    private DockLayoutSnapshot? _defaultLayout;
    private byte[] _lastResponseBodyBytes = [];
    private readonly DraftPersistenceService? _draftPersistenceService;
    private CancellationTokenSource? _draftAutoSaveCts;
    private DraftState? _pendingDraft;
    private readonly DemoServer? _demoServer;
    private readonly UnhandledExceptionCollector? _unhandledExceptionCollector;

    // Needed for file picker – set by the view
    public IStorageProvider? StorageProvider { get; set; }

    // Needed for clipboard (e.g. "Copy as cURL" on history items) – set by the view
    public global::Avalonia.Input.Platform.IClipboard? Clipboard { get; set; }

    /// <summary>The collector used for unhandled exceptions; may be null when not configured.</summary>
    public UnhandledExceptionCollector? UnhandledExceptionCollector => _unhandledExceptionCollector;

    /// <summary>Dock layout root; bound to DockControl.Layout in MainWindow.</summary>
    public IRootDock? Layout { get; private set; }

    /// <summary>Dock factory; bound to DockControl.Factory in MainWindow.</summary>
    public IFactory? Factory => _dockFactory;

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
    private Collection? _selectedCollection;

    [ObservableProperty]
    private string _collectionSearchQuery = string.Empty;

    /// <summary>Sort order for collection requests: "Default" | "Name" | "Method" | "Path".</summary>
    [ObservableProperty]
    private string _collectionSortBy = "Default";

    /// <summary>
    /// Display mode for collection request entries:
    /// "NameAndPath" | "NameOnly" | "PathOnly" | "FullUrl".
    /// </summary>
    [ObservableProperty]
    private string _collectionDisplayMode = "NameAndPath";

    /// <summary>When true, requests are shown grouped by their top-level path segment.</summary>
    [ObservableProperty]
    private bool _isCollectionTreeView;

    [ObservableProperty]
    private string _newCollectionName = string.Empty;

    [ObservableProperty]
    private bool _isNewCollectionFormVisible;

    [ObservableProperty]
    private string? _selectedLayoutName;

    public const string SystemThemeOption = "System";
    public const string DarkThemeOption = "Dark";
    public const string LightThemeOption = "Light";
    public const int MinScheduledJobIntervalSeconds = 1;

    public IReadOnlyList<string> ThemeOptions { get; } =
    [
        SystemThemeOption,
        DarkThemeOption,
        LightThemeOption
    ];

    [ObservableProperty]
    private string _selectedThemeOption = SystemThemeOption;

    public IReadOnlyList<string> TlsVersionOptions { get; } =
    [
        "SystemDefault",
        "Tls10",
        "Tls11",
        "Tls12",
        "Tls13"
    ];

    private static readonly HashSet<string> InsecureTlsVersions = ["Tls10", "Tls11"];

    public bool IsInsecureTlsVersionSelected => InsecureTlsVersions.Contains(SelectedTlsVersionOption);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInsecureTlsVersionSelected))]
    private string _selectedTlsVersionOption = "SystemDefault";

    [ObservableProperty]
    private bool _followHttpRedirects = true;

    [ObservableProperty]
    private bool _enableHttpDiagnostics;

    [ObservableProperty]
    private string _defaultRequestUrl = "http://localhost:5000/echo";

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
    private bool _collectUnhandledExceptions;

    public double UiFontSize =>
        double.TryParse(UiFontSizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 13d;

    // Response headers panel (populated after each successful request)
    public ObservableCollection<string> ResponseHeaders { get; } = [];

    [ObservableProperty]
    private bool _hasResponseHeaders;

    /// <summary>
    /// True when a non-binary text response has been received and the response shortcuts
    /// toolbar (Copy body / Save as file / Copy as cURL) should be visible.
    /// </summary>
    [ObservableProperty]
    private bool _hasTextResponse;

    [ObservableProperty]
    private bool _hasDraftToRestore;

    [ObservableProperty]
    private string _draftRestoreMessage = string.Empty;

    /// <summary>Gets or sets the port the demo server listens on.</summary>
    [ObservableProperty]
    private int _demoServerPort = DemoServer.DefaultPort;

    /// <summary>True while the embedded demo server is running.</summary>
    [ObservableProperty]
    private bool _isDemoServerRunning;

    /// <summary>
    /// True when a collection request targeting the demo server was loaded but the
    /// server is not yet running.  Shows an inline banner offering to start it.
    /// </summary>
    [ObservableProperty]
    private bool _isDemoServerBannerVisible;

    partial void OnSelectedTlsVersionOptionChanged(string value)
    {
        LogAndQueueOptionsAutoSave("Selected TLS version changed to {TlsVersion}", value);
        if (InsecureTlsVersions.Contains(value))
        {
            _debugLogger.Warning("TLS version {TlsVersion} is cryptographically broken and should only be used for testing against legacy servers", value);
        }
    }

    partial void OnFollowHttpRedirectsChanged(bool value) =>
        LogAndQueueOptionsAutoSave("Follow redirects changed to {FollowRedirects}", value);

    partial void OnDefaultContentTypeChanged(string value)
    {
        _debugLogger.Information("Default content type changed to {ContentType}", value);
        _requestEditor.DefaultContentType = value;
        QueueOptionsAutoSave();
    }

    partial void OnUiFontSizeTextChanged(string value) =>
        OnUiFontSizeTextChangedCore();

    partial void OnUiFontFamilyChanged(string value)
    {
        if (Application.Current is { } currentApp)
        {
            currentApp.Resources["AppFontFamily"] = new FontFamily(value);
        }

        QueueOptionsAutoSave();
    }

    partial void OnEnableHttpDiagnosticsChanged(bool value) =>
        LogAndQueueOptionsAutoSave("HTTP diagnostics enabled changed to {IsEnabled}", value);

    partial void OnSelectedCollectionChanged(Collection? value)
    {
        CollectionItems.Clear();
        if (value is { } collection)
        {
            foreach (var r in collection.Requests)
            {
                CollectionItems.Add(new CollectionItemViewModel(r, collection.BaseUrl));
            }
        }

        ApplyCollectionFilter();
    }

    partial void OnCollectionSearchQueryChanged(string value) => ApplyCollectionFilter();

    partial void OnCollectionSortByChanged(string value) => ApplyCollectionFilter();

    partial void OnIsCollectionTreeViewChanged(bool value) => ApplyCollectionFilter();

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

    partial void OnCollectUnhandledExceptionsChanged(bool value)
    {
        if (_unhandledExceptionCollector is { } collector)
        {
            collector.IsCollecting = value;
        }

        QueueOptionsAutoSave();
    }

    partial void OnDemoServerPortChanged(int value) =>
        QueueOptionsAutoSave();

    private void OnUiFontSizeTextChangedCore()
    {
        OnPropertyChanged(nameof(UiFontSize));
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
        Action<ApplicationOptions>? onApplicationOptionsChanged = null,
        CookieContainer? cookieContainer = null,
        DraftPersistenceService? draftPersistenceService = null,
        DemoServer? demoServer = null,
        UnhandledExceptionCollector? unhandledExceptionCollector = null)
    {
        _httpRequestService = httpRequestService;
        _requestHistoryRepository = requestHistoryRepository;
        _collectionRepository = collectionRepository;
        _scheduledJobRepository = scheduledJobRepository;
        _scheduledJobService = scheduledJobService;
        _logWindowViewModel = logWindowViewModel;
        _openApiImportService = new OpenApiImportService();
        _variableResolver = new VariableResolver();
        _applicationOptionsStore = applicationOptionsStore;
        _onApplicationOptionsChanged = onApplicationOptionsChanged;
        _draftPersistenceService = draftPersistenceService;
        _demoServer = demoServer;
        _unhandledExceptionCollector = unhandledExceptionCollector;
        var appLogger = (logger ?? Log.Logger).ForContext<MainWindowViewModel>();
        _debugLogger = appLogger.ForContext("LogTab", Logging.LogTab.Debug);
        _httpRequestsLogger = appLogger.ForContext("LogTab", Logging.LogTab.HttpRequests);

        _requestEditor = new RequestEditorViewModel(
            _variableResolver,
            GetActiveVariablesForEditor,
            _debugLogger,
            QueueOptionsAutoSave);
        _environmentsViewModel = new EnvironmentsViewModel(
            environmentRepository,
            _requestEditor,
            () => StorageProvider,
            _debugLogger);
        _optionsViewModel = new OptionsViewModel(this);
        _cookieJarViewModel = new CookieJarViewModel(cookieContainer);
        _graphQlViewModel = new GraphQlViewModel(_protocolHttpClient, appLogger);
        _webSocketViewModel = new WebSocketViewModel(appLogger);
        _sseViewModel = new SseViewModel(_protocolHttpClient, appLogger);
        _environmentsViewModel.PropertyChanged += OnEnvironmentsViewModelPropertyChanged;
        _webSocketViewModel.PropertyChanged += OnStreamingViewModelPropertyChanged;
        _sseViewModel.PropertyChanged += OnStreamingViewModelPropertyChanged;
        _requestEditor.PropertyChanged += OnRequestEditorPropertyChanged;

        History = [];
        Collections = [];
        CollectionItems = [];
        FilteredCollectionItems = [];
        CollectionGroups = [];
        ScheduledJobs = [];
        SavedLayoutNames = [];

        SendRequestCommand = new AsyncRelayCommand(SendRequestAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);

        _dockFactory = new DockFactory(this, _environmentsViewModel, _optionsViewModel, _cookieJarViewModel);
        Layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(Layout);
        _defaultLayout = CaptureLayoutSnapshot();

        _suppressLayoutRestore = true;
        var options = initialOptions ?? new ApplicationOptions();
        ApplyOptions(options);
        ApplyLayoutOptions(options.Layouts);
        _suppressLayoutRestore = false;
        _requestEditor.RefreshRequestPreview();
    }

    public ObservableCollection<SavedRequest> History { get; }
    public ObservableCollection<Collection> Collections { get; }
    public ObservableCollection<CollectionItemViewModel> CollectionItems { get; }

    /// <summary>Filtered and sorted flat list of collection requests, bound in the Collections panel.</summary>
    public ObservableCollection<CollectionItemViewModel> FilteredCollectionItems { get; }

    /// <summary>
    /// Requests grouped by top-level path segment, used in the tree view.
    /// Each <see cref="CollectionGroupViewModel"/> holds an expandable set of items.
    /// </summary>
    public ObservableCollection<CollectionGroupViewModel> CollectionGroups { get; }
    public ObservableCollection<RequestEnvironment> Environments => _environmentsViewModel.Environments;
    public ObservableCollection<EnvironmentVariableViewModel> ActiveEnvironmentVariables => _environmentsViewModel.ActiveEnvironmentVariables;
    public ObservableCollection<ScheduledJobViewModel> ScheduledJobs { get; }
    public ObservableCollection<string> SavedLayoutNames { get; }
    public LogWindowViewModel LogWindowViewModel => _logWindowViewModel;
    public RequestEditorViewModel RequestEditor => _requestEditor;
    public EnvironmentsViewModel EnvironmentsPanel => _environmentsViewModel;
    public OptionsViewModel OptionsPanel => _optionsViewModel;
    public GraphQlViewModel GraphQlEditor => _graphQlViewModel;
    public WebSocketViewModel WebSocketSession => _webSocketViewModel;
    public SseViewModel SseSession => _sseViewModel;

    /// <summary>
    /// Label for the primary action button in the request composer.
    /// Shows "Send" for HTTP/GraphQL, "Connect"/"Disconnect" for streaming protocols.
    /// </summary>
    public string PrimaryActionLabel => _requestEditor.SelectedRequestType switch
    {
        RequestType.WebSocket => _webSocketViewModel.IsConnected ? "Disconnect" : "Connect",
        RequestType.Sse => _sseViewModel.IsConnected ? "Disconnect" : "Connect",
        _ => "Send"
    };

    public RequestEnvironment? ActiveEnvironment
    {
        get => _environmentsViewModel.ActiveEnvironment;
        set => _environmentsViewModel.ActiveEnvironment = value;
    }

    public bool IsEnvironmentPanelVisible
    {
        get => _environmentsViewModel.IsEnvironmentPanelVisible;
        set => _environmentsViewModel.IsEnvironmentPanelVisible = value;
    }

    public string NewEnvironmentName
    {
        get => _environmentsViewModel.NewEnvironmentName;
        set => _environmentsViewModel.NewEnvironmentName = value;
    }

    public IAsyncRelayCommand SendRequestCommand { get; }
    public IAsyncRelayCommand LoadHistoryCommand { get; }

    public IRelayCommand AddEnvironmentVariableCommand => _environmentsViewModel.AddEnvironmentVariableCommand;
    public IRelayCommand<EnvironmentVariableViewModel?> RemoveEnvironmentVariableCommand => _environmentsViewModel.RemoveEnvironmentVariableCommand;
    public IAsyncRelayCommand SaveEnvironmentCommand => _environmentsViewModel.SaveEnvironmentCommand;
    public IAsyncRelayCommand<RequestEnvironment?> DeleteEnvironmentCommand => _environmentsViewModel.DeleteEnvironmentCommand;
    public IRelayCommand<RequestEnvironment?> EditEnvironmentCommand => _environmentsViewModel.EditEnvironmentCommand;
    public IRelayCommand NewEnvironmentCommand => _environmentsViewModel.NewEnvironmentCommand;
    public IAsyncRelayCommand ExportEnvironmentsCommand => _environmentsViewModel.ExportEnvironmentsCommand;

    private void OnStreamingViewModelPropertyChanged(object? sender, global::System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WebSocketViewModel.IsConnected), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(SseViewModel.IsConnected), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(PrimaryActionLabel));
        }
    }

    private void OnRequestEditorPropertyChanged(object? sender, global::System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(RequestEditorViewModel.SelectedRequestType), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(PrimaryActionLabel));
        }
    }

    private void OnEnvironmentsViewModelPropertyChanged(object? sender, global::System.ComponentModel.PropertyChangedEventArgs e)    {
        if (string.Equals(e.PropertyName, nameof(EnvironmentsViewModel.ActiveEnvironment), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(ActiveEnvironment));
            return;
        }

        if (string.Equals(e.PropertyName, nameof(EnvironmentsViewModel.NewEnvironmentName), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(NewEnvironmentName));
            return;
        }

        if (string.Equals(e.PropertyName, nameof(EnvironmentsViewModel.IsEnvironmentPanelVisible), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(IsEnvironmentPanelVisible));
        }
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
                HttpVersion = _requestEditor.SelectedHttpVersionOption,
                TlsVersion = SelectedTlsVersionOption,
                EnableHttpDiagnostics = EnableHttpDiagnostics,
                DefaultContentType = DefaultContentType,
                FollowRedirects = FollowHttpRedirects,
                DefaultRequestUrl = DefaultRequestUrl,
                DemoServerPort = DemoServerPort
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
            Layouts = BuildLayoutOptions(),
            Diagnostics = new DiagnosticsOptions
            {
                CollectUnhandledExceptions = CollectUnhandledExceptions
            }
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
            _requestEditor.SelectedHttpVersionOption = options.Http.HttpVersion;
            SelectedTlsVersionOption = options.Http.TlsVersion;
            EnableHttpDiagnostics = options.Http.EnableHttpDiagnostics;
            FollowHttpRedirects = options.Http.FollowRedirects;
            DefaultRequestUrl = options.Http.DefaultRequestUrl;
            DefaultContentType = options.Http.DefaultContentType;
            _requestEditor.DefaultContentType = options.Http.DefaultContentType;
            UiFontFamily = options.Appearance.FontFamily;
            UiFontSizeText = options.Appearance.FontSize.ToString("0.##", CultureInfo.InvariantCulture);
            AutoStartScheduledJobsOnLaunch = options.ScheduledJobs.AutoStartOnLaunch;
            DefaultScheduledJobIntervalSeconds = options.ScheduledJobs.DefaultIntervalSeconds;
            DemoServerPort = options.Http.DemoServerPort;
            CollectUnhandledExceptions = options.Diagnostics.CollectUnhandledExceptions;

            if (_requestEditor.FollowRedirectsForRequest == previousDefaultFollowRedirects)
            {
                _requestEditor.FollowRedirectsForRequest = options.Http.FollowRedirects;
            }

            if (updateCurrentRequestUrl || string.IsNullOrWhiteSpace(_requestEditor.RequestUrl) || string.Equals(_requestEditor.RequestUrl, previousDefaultUrl, StringComparison.Ordinal))
            {
                _requestEditor.RequestUrl = options.Http.DefaultRequestUrl;
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

        if (layouts?.SavedLayouts is { } savedLayouts)
        {
            foreach (var namedLayout in savedLayouts)
            {
                if (!string.IsNullOrWhiteSpace(namedLayout.Name) && namedLayout.Layout is { } layout)
                {
                    _savedLayouts[namedLayout.Name] = layout;
                    SavedLayoutNames.Add(namedLayout.Name);
                }
            }
        }

        SelectedLayoutName = SavedLayoutNames.FirstOrDefault();
        ApplyLayoutSnapshot(layouts?.CurrentLayout);
        UpdateLayoutNameCounter();
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
        catch (JsonException)
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
        catch (XmlException)
        {
            formatted = string.Empty;
            return false;
        }
    }
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

    [RelayCommand]
    private void ShowHistoryTab()
    {
        LeftPanelTab = "History";
        ActivateLeftPanel();
    }

    [RelayCommand]
    private void ShowCollectionsTab()
    {
        LeftPanelTab = "Collections";
        ActivateLeftPanel();
    }

    [RelayCommand]
    private void ShowScheduledJobsTab()
    {
        LeftPanelTab = "ScheduledJobs";
        ActivateLeftPanel();
    }

    private void ActivateLeftPanel()
    {
        if (_dockFactory?.LeftToolDock is { } dock &&
            _dockFactory.LeftPanelViewModel is { } leftPanelVm)
        {
            dock.ActiveDockable = leftPanelVm;
        }
    }

    /// <summary>Set by the view layer to close the main window.</summary>
    public Action? ExitApplicationAction { get; set; }

    /// <summary>Set by the view layer to open the About window.</summary>
    public Action? OpenAboutWindowAction { get; set; }

    [RelayCommand]
    private void OpenAbout() => OpenAboutWindowAction?.Invoke();

    /// <summary>Set by the view layer to open the Diagnostics window.</summary>
    public Action? OpenDiagnosticsWindowAction { get; set; }

    [RelayCommand]
    private void OpenDiagnostics() => OpenDiagnosticsWindowAction?.Invoke();

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
    private void OpenCookieJar()
    {
        if (_dockFactory?.LeftToolDock is { } dock &&
            _dockFactory.CookieJarViewModel is { } cookieJarVm)
        {
            cookieJarVm.RefreshCookies();
            dock.ActiveDockable = cookieJarVm;
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
    private void OpenEnvironments()
    {
        IsEnvironmentPanelVisible = true;
        if (_dockFactory?.LeftToolDock is { } dock &&
            _dockFactory.EnvironmentsViewModel is { } environmentsVm)
        {
            dock.ActiveDockable = environmentsVm;
        }
    }

    [RelayCommand]
    private void OpenLayoutPanel()
    {
        if (_dockFactory?.LeftToolDock is { } dock &&
            _dockFactory.LayoutManagementViewModel is { } layoutVm)
        {
            dock.ActiveDockable = layoutVm;
        }
    }

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
        if (_defaultLayout is { } defaultLayout)
        {
            ApplyLayoutSnapshot(defaultLayout);
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
    private void LoadCollectionRequest(CollectionItemViewModel? item) => LoadCollectionRequestCore(item);

    internal void LoadCollectionRequestCore(CollectionItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        // Detect special protocol methods used in demo collections.
        _requestEditor.SelectedRequestType = item.Method switch
        {
            "WS" or "WSS" => RequestType.WebSocket,
            "SSE" => RequestType.Sse,
            _ => RequestType.Http
        };

        if (_requestEditor.SelectedRequestType == RequestType.Http)
        {
            _requestEditor.SelectedMethod = item.Method;
        }

        var collectionBaseUrl = SelectedCollection?.BaseUrl;
        var activeEnv = ActiveEnvironment;

        var baseUrl = activeEnv is { }
            ? _variableResolver.Resolve(collectionBaseUrl ?? string.Empty, _requestEditor.GetResolvedVariables())
            : (collectionBaseUrl ?? string.Empty);

        // If item.Path is already an absolute web URL (http/https/ws/wss), use it directly; otherwise prefix
        // with the collection base URL.  We check the scheme explicitly rather than relying on UriKind.Absolute
        // because Uri.TryCreate("/ws", UriKind.Absolute, …) returns true on Linux (the path is resolved as a
        // file:// URI), which would silently strip the base URL.
        var resolvedUrl = Uri.TryCreate(item.Path, UriKind.Absolute, out var parsedUri) && parsedUri is { }
                          && parsedUri.Scheme is "http" or "https" or "ws" or "wss"
            ? item.Path
            : baseUrl.TrimEnd('/') + item.Path;

        // For WebSocket requests, rewrite http:// → ws:// and https:// → wss://.
        if (_requestEditor.SelectedRequestType == RequestType.WebSocket)
        {
            resolvedUrl = resolvedUrl
                .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
        }

        _requestEditor.RequestUrl = resolvedUrl;
        _requestEditor.RequestName = item.Name;
        _requestEditor.RequestNotes = item.Notes ?? string.Empty;

        if (item.Method is "POST" or "PUT" or "PATCH")
        {
            _requestEditor.RequestBody = "{}";
        }
        else
        {
            _requestEditor.RequestBody = string.Empty;
        }

        // Show a banner if this request targets the local demo server and it is not running.
        IsDemoServerBannerVisible = _demoServer is { } server
            && !server.IsRunning
            && IsDemoServerUrl(resolvedUrl, server.Port);
    }

    /// <summary>Starts the embedded demo server on <see cref="DemoServerPort"/>.</summary>
    [RelayCommand]
    private async Task StartDemoServerAsync()
    {
        if (_demoServer is null || _demoServer.IsRunning)
        {
            return;
        }

        try
        {
            await _demoServer.StartAsync(DemoServerPort);
            IsDemoServerRunning = true;
            IsDemoServerBannerVisible = false;
            _debugLogger.Information("Demo server started on port {Port}", DemoServerPort);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to start demo server: {ex.Message}";
            _debugLogger.Error(ex, "Failed to start demo server on port {Port}", DemoServerPort);
        }
    }

    /// <summary>Stops the embedded demo server.</summary>
    [RelayCommand]
    private async Task StopDemoServerAsync()
    {
        if (_demoServer is null || !_demoServer.IsRunning)
        {
            return;
        }

        await _demoServer.StopAsync();
        IsDemoServerRunning = false;
        _debugLogger.Information("Demo server stopped");
    }

    /// <summary>Dismisses the "demo server not running" banner without starting the server.</summary>
    [RelayCommand]
    private void DismissDemoServerBanner() => IsDemoServerBannerVisible = false;

    [RelayCommand]
    private void ShowNewCollectionForm()
    {
        NewCollectionName = string.Empty;
        IsNewCollectionFormVisible = true;
    }

    [RelayCommand]
    private void CancelNewCollection()
    {
        NewCollectionName = string.Empty;
        IsNewCollectionFormVisible = false;
    }

    [RelayCommand]
    private async Task CreateCollectionAsync()
    {
        var name = NewCollectionName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var id = await _collectionRepository.SaveAsync(name, null, null, []);
        IsNewCollectionFormVisible = false;
        NewCollectionName = string.Empty;

        await LoadCollectionsAsync();
        SelectedCollection = Collections.FirstOrDefault(c => c.Id == id);
        LeftPanelTab = "Collections";
        _debugLogger.Information("Created new collection {CollectionName}", name);
    }

    [RelayCommand]
    private async Task AddRequestToCollectionAsync()
    {
        if (SelectedCollection is not { } collection)
        {
            return;
        }

        var draft = _requestEditor.BuildDraft();

        var baseUrl = collection.BaseUrl?.TrimEnd('/');
        string path;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            path = draft.Url;
        }
        else if (draft.Url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
        {
            path = draft.Url[baseUrl.Length..];
        }
        else if (Uri.TryCreate(draft.Url, UriKind.Absolute, out var absoluteUri))
        {
            path = absoluteUri.PathAndQuery + absoluteUri.Fragment;
        }
        else
        {
            path = draft.Url;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        var newRequest = new CollectionRequest(
            string.IsNullOrWhiteSpace(draft.Name) ? draft.Method + " " + path : draft.Name,
            draft.Method,
            path,
            null);

        var updatedRequests = collection.Requests.Append(newRequest).ToList();
        await _collectionRepository.UpdateAsync(
            collection.Id,
            collection.Name,
            collection.SourcePath,
            collection.BaseUrl,
            updatedRequests);

        await LoadCollectionsAsync();
        SelectedCollection = Collections.FirstOrDefault(c => c.Id == collection.Id);
        _debugLogger.Information("Added request {RequestName} to collection {CollectionName}", newRequest.Name, collection.Name);
    }

    [RelayCommand]
    private void SetCollectionSortBy(string? sortBy) =>
        CollectionSortBy = sortBy ?? "Default";

    [RelayCommand]
    private void SetCollectionDisplayMode(string? mode) =>
        CollectionDisplayMode = mode ?? "NameAndPath";

    [RelayCommand]
    private void ToggleCollectionTreeView() =>
        IsCollectionTreeView = !IsCollectionTreeView;

    private void ApplyCollectionFilter()
    {
        var items = CollectionItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(CollectionSearchQuery))
        {
            items = items.Where(item =>
                item.Name.Contains(CollectionSearchQuery, StringComparison.OrdinalIgnoreCase)
                || item.Path.Contains(CollectionSearchQuery, StringComparison.OrdinalIgnoreCase)
                || item.Method.Contains(CollectionSearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        items = CollectionSortBy switch
        {
            "Name" => items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            "Method" => items.OrderBy(i => i.Method, StringComparer.OrdinalIgnoreCase),
            "Path" => items.OrderBy(i => i.Path, StringComparer.OrdinalIgnoreCase),
            _ => items
        };

        var filteredList = items.ToList();

        FilteredCollectionItems.Clear();
        foreach (var item in filteredList)
        {
            FilteredCollectionItems.Add(item);
        }

        // Preserve expansion state keyed by GroupKey so user-collapsed groups survive filter/sort changes.
        var previousExpanded = CollectionGroups.ToDictionary(
            g => g.GroupKey,
            g => g.IsExpanded,
            StringComparer.OrdinalIgnoreCase);

        CollectionGroups.Clear();
        foreach (var group in filteredList
            .GroupBy(i => i.GroupKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var groupVm = new CollectionGroupViewModel(group.Key, group.ToList());
            if (previousExpanded.TryGetValue(group.Key, out var wasExpanded))
            {
                groupVm.IsExpanded = wasExpanded;
            }

            CollectionGroups.Add(groupVm);
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

    // CommunityToolkit.Mvvm strips the "Async" suffix when generating command properties,
    // so the XAML binding target is OpenRequestBodyInExternalEditorCommand (not OpenRequestBodyInExternalEditorAsyncCommand).
    [RelayCommand]
    private async Task OpenRequestBodyInExternalEditorAsync()
    {
        _requestBodyWatcher?.Dispose();
        _requestBodyWatcher = null;

        var path = await WriteTempFileAsync("arbor-request", _requestEditor.RequestBody);

        var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnRequestBodyFileChanged;
        _requestBodyWatcher = watcher;

        OpenWithShell(path);
    }

    // CommunityToolkit.Mvvm strips the "Async" suffix when generating command properties,
    // so the XAML binding target is OpenResponseBodyInExternalEditorCommand (not OpenResponseBodyInExternalEditorAsyncCommand).
    [RelayCommand]
    private async Task OpenResponseBodyInExternalEditorAsync()
    {
        var path = await WriteTempFileAsync("arbor-response", ResponseBody);
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

    /// <summary>
    /// Copies the current (pretty-printed) response body text to the clipboard.
    /// No-op when the clipboard is unavailable or the response body is empty.
    /// </summary>
    [RelayCommand]
    private async Task CopyResponseBodyAsync()
    {
        if (Clipboard is null || string.IsNullOrEmpty(ResponseBody))
        {
            return;
        }

        await Clipboard.SetTextAsync(ResponseBody);
    }

    /// <summary>
    /// Opens a save-file dialog and writes the raw response body to the chosen path.
    /// The suggested file extension is derived from the response <c>Content-Type</c>.
    /// No-op when the storage provider is unavailable or the response body is empty.
    /// </summary>
    [RelayCommand]
    private async Task SaveResponseBodyAsFileAsync()
    {
        if (StorageProvider is null || string.IsNullOrEmpty(RawResponseBody))
        {
            return;
        }

        var extension = !string.IsNullOrWhiteSpace(ResponseContentType)
            ? ExtensionFromContentType(ResponseContentType)
            : DetectExtensionFromContent(RawResponseBody);

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Response Body",
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

        await File.WriteAllTextAsync(file.Path.LocalPath, RawResponseBody, Encoding.UTF8);
    }

    /// <summary>
    /// Copies the current request (as configured in the request editor) to the
    /// clipboard formatted as a single-line <c>curl</c> command.
    /// No-op when the clipboard is unavailable.
    /// </summary>
    [RelayCommand]
    private async Task CopyCurrentRequestAsCurlAsync()
    {
        if (Clipboard is null)
        {
            return;
        }

        var draft = _requestEditor.BuildDraft();
        var command = CurlFormatter.Format(draft.Method, draft.Url, draft.Body, draft.Headers);
        await Clipboard.SetTextAsync(command);
    }

    public void Dispose()
    {
        _environmentsViewModel.PropertyChanged -= OnEnvironmentsViewModelPropertyChanged;
        _webSocketViewModel.PropertyChanged -= OnStreamingViewModelPropertyChanged;
        _sseViewModel.PropertyChanged -= OnStreamingViewModelPropertyChanged;
        _requestEditor.PropertyChanged -= OnRequestEditorPropertyChanged;
        _environmentsViewModel.Dispose();
        _optionsAutoSaveCts?.Cancel();
        _optionsAutoSaveCts?.Dispose();
        _draftAutoSaveCts?.Cancel();
        _draftAutoSaveCts?.Dispose();
        _draftPersistenceService?.ClearDraft();
        _scheduledJobService.Dispose();
        _requestBodyWatcher?.Dispose();
        _requestBodyWatcher = null;
        _streamingCts?.Cancel();
        _streamingCts?.Dispose();
        _webSocketViewModel.Dispose();
        _sseViewModel.Dispose();
        _protocolHttpClient.Dispose();

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
            _environmentsViewModel.LoadEnvironmentsAsync(cancellationToken),
            LoadScheduledJobsAsync(cancellationToken)).ConfigureAwait(false);

        await SeedDemoDataAsync(cancellationToken).ConfigureAwait(false);

        var savedDraft = _draftPersistenceService is { } draftService
            ? await draftService.LoadDraftAsync(cancellationToken).ConfigureAwait(false)
            : null;
        if (savedDraft is { } draft)
        {
            _pendingDraft = draft;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                HasDraftToRestore = true;
                DraftRestoreMessage =
                    $"An unsaved draft from {draft.SavedAt.LocalDateTime:g} was found. Restore it?";
            });
            // Auto-save is deferred until the user dismisses the banner to avoid
            // overwriting the pending draft before they can restore it.
        }
        else
        {
            StartDraftAutoSave();
        }
    }

    /// <summary>
    /// Seeds the "Localhost Demo" collection and "Demo (localhost)" environment the first
    /// time the application starts.  Subsequent starts are no-ops (the data already exists).
    /// </summary>
    private async Task SeedDemoDataAsync(CancellationToken cancellationToken = default)
    {
        const string demoCollectionName = "Localhost Demo";
        const string demoEnvironmentName = "Demo (localhost)";

        var existingCollections = await _collectionRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (!existingCollections.Any(c => string.Equals(c.Name, demoCollectionName, StringComparison.Ordinal)))
        {
            var demoRequests = new List<CollectionRequest>
            {
                new("Echo GET", "GET", "/echo",
                    "Simple HTTP GET — returns request info as JSON when no body is present."),
                new("Echo POST", "POST", "/echo",
                    "HTTP POST — your JSON body is echoed back in the response."),
                new("Echo PUT", "PUT", "/echo",
                    "HTTP PUT — your JSON body is echoed back in the response."),
                new("Echo DELETE", "DELETE", "/echo",
                    "HTTP DELETE — returns request info as JSON."),
                new("Server status", "GET", "/status",
                    "Returns a JSON summary of the demo server (version, port, endpoints)."),
                new("Server-Sent Events", "SSE", "/sse",
                    "Streams five numbered SSE events, one every 500 ms. Request type is set to SSE automatically."),
                new("WebSocket echo", "WS", "/ws",
                    "WebSocket echo server — every message you send is reflected back. Request type is set to WebSocket automatically.")
            };

            var newId = await _collectionRepository.SaveAsync(
                demoCollectionName,
                null,
                "{{baseUrl}}",
                demoRequests,
                cancellationToken).ConfigureAwait(false);

            // Reload collections and select the newly seeded demo collection.
            await LoadCollectionsAsync(cancellationToken).ConfigureAwait(false);
            SelectedCollection = Collections.FirstOrDefault(c => c.Id == newId);
            LeftPanelTab = "Collections";

            _debugLogger.Information("Seeded demo collection '{Name}'", demoCollectionName);
        }

        var existingEnvironments = await _environmentsViewModel.GetAllEnvironmentsAsync(cancellationToken).ConfigureAwait(false);
        if (!existingEnvironments.Any(e => string.Equals(e.Name, demoEnvironmentName, StringComparison.Ordinal)))
        {
            await _environmentsViewModel.SeedEnvironmentAsync(
                demoEnvironmentName,
                [new EnvironmentVariable("baseUrl", $"http://localhost:{DemoServerPort}", true)],
                cancellationToken).ConfigureAwait(false);

            _debugLogger.Information("Seeded demo environment '{Name}'", demoEnvironmentName);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="url"/> targets the local demo
    /// server at the given <paramref name="port"/>.
    /// </summary>
    private static bool IsDemoServerUrl(string url, int port) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal))
        && uri.Port == port;

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
                await Dispatcher.UIThread.InvokeAsync(() => _requestEditor.RequestBody = content);
            }
            catch
            {
                // ignore transient read errors while the editor is still writing
            }
        });
    }

    private async Task<string> WriteTempFileAsync(string prefix, string content, CancellationToken cancellationToken = default)
    {
        var ext = !string.IsNullOrEmpty(_requestEditor.ContentType)
            ? ExtensionFromContentType(_requestEditor.ContentType)
            : DetectExtensionFromContent(content);
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}{ext}");
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
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
        catch (Exception ex)
        {
            _debugLogger.Warning(ex, "Failed to persist layout options");
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
        if (root.Windows is { } windows)
        {
            foreach (var window in windows)
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

        if (dockable is IDock dock && dock.VisibleDockables is { } dockables)
        {
            foreach (var child in dockables)
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
                if (primary is { })
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
        if (activeDockable is { } active)
        {
            dock.ActiveDockable = active;
        }
    }

    private static T? FindDockById<T>(IDockable dockable, string id) where T : class, IDockable
    {
        if (dockable is T foundDockable && string.Equals(foundDockable.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return foundDockable;
        }

        if (dockable is IDock dock && dock.VisibleDockables is { } childDockables)
        {
            foreach (var child in childDockables)
            {
                var childDock = FindDockById<T>(child, id);
                if (childDock is { } found)
                {
                    return found;
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
        ErrorMessage = string.Empty;

        switch (_requestEditor.SelectedRequestType)
        {
            case RequestType.GraphQL:
                await SendGraphQlRequestAsync();
                break;

            case RequestType.WebSocket:
                await ToggleWebSocketConnectionAsync();
                break;

            case RequestType.Sse:
                await ToggleSseConnectionAsync();
                break;

            case RequestType.GrpcUnary:
                ErrorMessage = "gRPC support requires a .proto file import. This feature is under development.";
                break;

            default:
                await SendHttpRequestAsync();
                break;
        }
    }

    private async Task SendHttpRequestAsync()
    {
        try
        {
            var draft = _requestEditor.BuildDraft();
            _httpRequestsLogger.Information("Manual request started: {Method} {Url}", draft.Method, draft.Url);

            var response = await _httpRequestService.SendAsync(draft);

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}";
            ResponseStatusCode = response.StatusCode;
            ResponseTimeDisplay = FormatElapsedMilliseconds(response.ElapsedMilliseconds);
            ResponseSizeDisplay = FormatByteSize(response.BodyBytes?.LongLength ?? 0);
            _lastResponseBodyBytes = response.BodyBytes ?? [];
            RawResponseBody = response.Body;

            ResponseHeaders.Clear();
            foreach (var (name, value) in response.Headers)
            {
                ResponseHeaders.Add($"{name}: {value}");
            }

            HasResponseHeaders = ResponseHeaders.Count > 0;
            UpdateResponsePresentation(response.Body, response.Headers);
            HasTextResponse = HasResponseHeaders && !IsBinaryResponse;
            _httpRequestsLogger.Information("Manual request completed: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);

            _cookieJarViewModel.RefreshCookies();
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

    private async Task SendGraphQlRequestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = _requestEditor.GetResolvedUrl();
            var headers = _requestEditor.GetResolvedHeaders();

            _httpRequestsLogger.Information("GraphQL request started: {Url}", url);

            var response = await _graphQlViewModel.SendQueryAsync(url, headers, cancellationToken);

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}";
            ResponseStatusCode = response.StatusCode;
            ResponseTimeDisplay = FormatElapsedMilliseconds(response.ElapsedMilliseconds);
            ResponseSizeDisplay = FormatByteSize(response.BodyBytes?.LongLength ?? 0);
            _lastResponseBodyBytes = response.BodyBytes ?? [];
            RawResponseBody = response.Body;

            ResponseHeaders.Clear();
            foreach (var (name, value) in response.Headers)
            {
                ResponseHeaders.Add($"{name}: {value}");
            }

            HasResponseHeaders = ResponseHeaders.Count > 0;
            UpdateResponsePresentation(response.Body, response.Headers);
            HasTextResponse = HasResponseHeaders && !IsBinaryResponse;
            _httpRequestsLogger.Information("GraphQL request completed: {StatusCode}", response.StatusCode);

            _cookieJarViewModel.RefreshCookies();
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            _httpRequestsLogger.Error(exception, "GraphQL request failed");
            ErrorMessage = exception.Message;
            ResponseStatusCode = 0;
            ResponseTimeDisplay = string.Empty;
            ResponseSizeDisplay = string.Empty;
        }
    }

    private async Task ToggleWebSocketConnectionAsync()
    {
        if (_webSocketViewModel.IsConnected)
        {
            await _webSocketViewModel.DisconnectCommand.ExecuteAsync(null);
            _streamingCts?.Cancel();
            _streamingCts = null;
            return;
        }

        var url = _requestEditor.GetResolvedUrl();
        var headers = _requestEditor.GetResolvedHeaders();

        _streamingCts?.Dispose();
        _streamingCts = new CancellationTokenSource();

        // Fire-and-forget: the receive loop runs until the connection is closed or the token is cancelled.
        // The WebSocketViewModel updates IsConnected on the UI thread via Dispatcher.UIThread.Post.
        _ = _webSocketViewModel.ConnectAsync(url, headers, _streamingCts.Token);
    }

    private Task ToggleSseConnectionAsync()
    {
        if (_sseViewModel.IsConnected)
        {
            _sseViewModel.DisconnectCommand.Execute(null);
            _streamingCts?.Cancel();
            _streamingCts = null;
            return Task.CompletedTask;
        }

        var url = _requestEditor.GetResolvedUrl();
        var headers = _requestEditor.GetResolvedHeaders();

        _streamingCts?.Dispose();
        _streamingCts = new CancellationTokenSource();

        // Fire-and-forget: the SSE loop runs until the stream ends or the token is cancelled.
        // The SseViewModel updates IsConnected on the UI thread via Dispatcher.UIThread.Post.
        _ = _sseViewModel.ConnectAsync(url, headers, _streamingCts.Token);
        return Task.CompletedTask;
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
                _scheduledJobService.Start(config, vm.IsWebViewEnabled ? vm.HandleResponse : null);
                vm.IsRunning = true;
            }
        }
    }

    private IReadOnlyList<EnvironmentVariable> GetActiveVariablesForEditor() =>
        _environmentsViewModel.GetActiveVariablesForEditor();

    [RelayCommand]
    private async Task RestoreDraftAsync()
    {
        var draft = _pendingDraft
            ?? (_draftPersistenceService is { } ds
                ? await ds.LoadDraftAsync().ConfigureAwait(false)
                : null);
        if (draft is { } pendingDraft)
        {
            await Dispatcher.UIThread.InvokeAsync(() => DraftPersistenceService.RestoreToEditor(pendingDraft, _requestEditor));
        }

        _pendingDraft = null;
        HasDraftToRestore = false;
        StartDraftAutoSave();
    }

    [RelayCommand]
    private void DiscardDraft()
    {
        _pendingDraft = null;
        _draftPersistenceService?.ClearDraft();
        HasDraftToRestore = false;
        StartDraftAutoSave();
    }

    private void StartDraftAutoSave()
    {
        if (_draftPersistenceService is null)
        {
            return;
        }

        _draftAutoSaveCts?.Cancel();
        _draftAutoSaveCts?.Dispose();
        _draftAutoSaveCts = new CancellationTokenSource();
        _ = RunDraftAutoSaveAsync(_draftAutoSaveCts.Token);
    }

    private async Task RunDraftAutoSaveAsync(CancellationToken cancellationToken)
    {
        if (_draftPersistenceService is null)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var state = await Dispatcher.UIThread.InvokeAsync(
                        () => DraftPersistenceService.CaptureFromEditor(_requestEditor));
                    cancellationToken.ThrowIfCancellationRequested();
                    await _draftPersistenceService.SaveDraftAsync(state, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _debugLogger.Warning(ex, "Auto-save draft failed");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown — timer cancelled.
        }
    }
}
