using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Arbor.HttpClient.Desktop.Demo;
using Arbor.HttpClient.Desktop.Features.Collections;
using Arbor.HttpClient.Desktop.Features.Cookies;
using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.Demo;
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
using Arbor.HttpClient.Desktop.Features.Streaming;
using Arbor.HttpClient.Desktop.Localization;
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
using Arbor.HttpClient.Core.Scripting;
using Arbor.HttpClient.Core.Variables;
using Arbor.HttpClient.Desktop.Features.Scripting;

namespace Arbor.HttpClient.Desktop.Features.Main;

public partial class MainWindowViewModel : ViewModelBase, IDisposable, IResponseActionsContext
{
    private readonly HttpRequestService _httpRequestService;
    private HttpRequestWorkflow _httpRequestWorkflow = null!;
    private ManualHttpRequestCoordinator _manualHttpRequestCoordinator = null!;
    private readonly HttpResponseProjectionWorkflow _httpResponseProjectionWorkflow = new();
    private readonly IRequestHistoryRepository _requestHistoryRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IScheduledJobRepository _scheduledJobRepository;
    private CollectionsWorkflow _collectionsWorkflow = null!;
    private CollectionsManagementCoordinator _collectionsManagementCoordinator = null!;
    private readonly ScheduledJobService _scheduledJobService;
    private readonly LogWindowViewModel _logWindowViewModel;
    private readonly OpenApiImportService _openApiImportService;
    private readonly VariableResolver _variableResolver;
    private readonly ApplicationOptionsStore? _applicationOptionsStore;
    private readonly Action<ApplicationOptions>? _onApplicationOptionsChanged;
    private readonly ILogger _debugLogger;
    private readonly ILogger _httpRequestsLogger;
    private readonly System.Net.Http.HttpClient _protocolHttpClient = new();
    private RequestEditorViewModel _requestEditor = null!;
    private EnvironmentsViewModel _environmentsViewModel = null!;
    private OptionsViewModel _optionsViewModel = null!;
    private CookieJarViewModel _cookieJarViewModel = null!;
    private GraphQlViewModel _graphQlViewModel = null!;
    private GraphQlRequestWorkflow _graphQlRequestWorkflow = null!;
    private ManualGraphQlRequestCoordinator _manualGraphQlRequestCoordinator = null!;
    private DemoDataWorkflow _demoDataWorkflow = null!;
    private DemoServerLifecycleCoordinator _demoServerLifecycleCoordinator = null!;
    private StreamingConnectionWorkflow _streamingConnectionWorkflow = null!;
    private WebSocketViewModel _webSocketViewModel = null!;
    private SseViewModel _sseViewModel = null!;
    private CancellationTokenSource? _streamingCts;
    private readonly List<string> _tempFiles = [];
    private readonly List<RequestHistoryEntry> _allHistory = [];
    private FileSystemWatcher? _requestBodyWatcher;
    private CancellationTokenSource? _fileWatcherCts;
    private int _requestBodyReadPending;
    private readonly Subject<string> _historyFilterRequestedSubject = new();
    private readonly CompositeDisposable _historyFilterDisposables = new();
    private readonly Subject<string> _collectionSearchFilterRequestedSubject = new();
    private readonly CompositeDisposable _collectionSearchFilterDisposables = new();
    private DockFactory? _dockFactory;
    private readonly LayoutWorkflow _layoutWorkflow = new();
    private readonly LayoutTreeWorkflow _layoutTreeWorkflow = new();
    private ApplicationOptions _applicationOptions = new();
    private readonly Dictionary<string, DockLayoutSnapshot> _savedLayouts = new(StringComparer.OrdinalIgnoreCase);
    private int _layoutNameCounter = 1;
    private bool _suppressLayoutRestore;
    private bool _suppressOptionsAutoSave;
    private readonly Subject<Unit> _optionsAutoSaveRequestedSubject = new();
    private readonly CompositeDisposable _optionsAutoSaveDisposables = new();
    private bool _suppressCollectionInheritedHeadersAutoSave;
    private bool _suppressCollectionInheritedHeadersLivePreviewSync;
    private readonly Subject<CollectionInheritedHeadersAutoSaveSnapshot> _collectionInheritedHeadersAutoSaveRequestedSubject = new();
    private readonly CompositeDisposable _collectionInheritedHeadersAutoSaveDisposables = new();
    private readonly CompositeDisposable _crossFeatureDisposables = new();
    private Task? _collectionInheritedHeadersAutoSaveTask;
    private int _collectionInheritedHeadersAutoSaveVersion;
    private bool _hasPendingCollectionInheritedHeadersAutoSave;
    private CollectionInheritedHeadersAutoSaveSnapshot? _pendingCollectionInheritedHeadersAutoSaveSnapshot;
    private DockLayoutSnapshot? _defaultLayout;
    private byte[] _lastResponseBodyBytes = [];
    private readonly DraftPersistenceService? _draftPersistenceService;
    private CancellationTokenSource? _draftAutoSaveCts;
    private RequestEditorSnapshot? _pendingDraft;
    private readonly DemoServer? _demoServer;
    private readonly UnhandledExceptionCollector? _unhandledExceptionCollector;
    private readonly IScriptRunner _scriptRunner = new RoslynScriptRunner();
    private readonly ScriptViewModel _scriptViewModel = new();
    private readonly ResponseActionsViewModel _responseActions = null!;
    private const string DefaultContentTypeCustomOption = "Custom...";

    private static readonly HashSet<string> AutoSaveOnlyOptionPropertyNames = new(StringComparer.Ordinal)
    {
        nameof(DefaultRequestUrl),
        nameof(ResponseSaveDefaultFolder),
        nameof(AutoStartScheduledJobsOnLaunch),
        nameof(DefaultScheduledJobIntervalSeconds),
        nameof(DemoServerPort),
        nameof(DemoServerHttpsPort),
        nameof(IsDemoServerHttpEnabled),
        nameof(IsDemoServerHttpsEnabled)
    };

    [ObservableProperty]
    private RequestTabViewModel? _activeRequestTab;

    // Cached DockTree from the last explicit layout capture (set during PersistCurrentLayout,
    // SaveLayoutAsNew, SaveLayoutToExisting, and window close).  Reused for auto-saves triggered
    // by non-layout property changes (TLS version, font, etc.) so that CaptureDockNode is NOT
    // called during the auto-save debounce chain and cannot reset the debounce timer.
    private DockTreeNode? _cachedDockTree;

    // Window geometry captured just before close — included in the next PersistCurrentLayout call.
    private double _windowWidthAtClose;
    private double _windowHeightAtClose;
    private int _windowXAtClose;
    private int _windowYAtClose;
    private bool _windowPositionCaptured;

    // Startup layout snapshot: re-applied after the window opens so that the PSP
    // re-measures with the saved proportions once all visual bindings are in place.
    private DockLayoutSnapshot? _startupLayoutSnapshot;

    private sealed record CollectionInheritedHeadersAutoSaveSnapshot(
        int CollectionId,
        string CollectionName,
        string? CollectionSourcePath,
        string? CollectionBaseUrl,
        IReadOnlyList<CollectionRequest> CollectionRequests,
        IReadOnlyList<RequestHeader>? InheritedHeaders);

    // Needed for file picker – set by the view
    public IStorageProvider? StorageProvider { get; set; }

    // Needed for clipboard (e.g. "Copy as cURL" on history items) – set by the view
    public IClipboard? Clipboard { get; set; }

    public bool HasPendingCollectionInheritedHeadersAutoSave => _hasPendingCollectionInheritedHeadersAutoSave;

    /// <summary>
    /// Exposes the extracted response-actions coordinator.
    /// XAML bindings currently resolve commands via delegation on <see cref="MainWindowViewModel"/>
    /// (Phase 2). In a future Phase 3 cleanup the bindings can point directly to this property.
    /// </summary>
    public ResponseActionsViewModel ResponseActions => _responseActions;

    // ── IResponseActionsContext explicit implementations ──────────────────────

    IReadOnlyList<string> IResponseActionsContext.ResponseHeaders => ResponseHeaders;

    byte[] IResponseActionsContext.GetLastResponseBodyBytes() => _lastResponseBodyBytes;

    string IResponseActionsContext.SelectedCollectionName => SelectedCollection?.Name ?? string.Empty;

    string IResponseActionsContext.RequestEditorResolvedUrl => _requestEditor.GetResolvedUrl();

    string IResponseActionsContext.RequestEditorRequestName => _requestEditor.RequestName;

    string IResponseActionsContext.RequestEditorContentType => _requestEditor.ContentType;

    ResolvedHttpRequestDraft IResponseActionsContext.BuildResolvedHttpRequestDraft() => _requestEditor.BuildResolvedHttpRequestDraft();

    void IResponseActionsContext.RecordTempFile(string path) => _tempFiles.Add(path);

    void IResponseActionsContext.SetResponseSaveFileNamePatternValidationError(string error) =>
        ResponseSaveFileNamePatternValidationError = error;

    // ─────────────────────────────────────────────────────────────────────────

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
    private string _responseRawText = string.Empty;

    [ObservableProperty]
    private int _selectedResponseTabIndex;

    [ObservableProperty]
    private bool _isResponseWebViewAvailable;

    [ObservableProperty]
    private string _responseWebViewUri = "about:blank";

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
    private string _renameCollectionName = string.Empty;

    [ObservableProperty]
    private bool _isRenameCollectionFormVisible;

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
    private bool _showRequestPreviewByDefault = true;

    [ObservableProperty]
    private bool _enableHttpDiagnostics;

    [ObservableProperty]
    private string _defaultRequestUrl = "http://localhost:5000/echo";

    [ObservableProperty]
    private string _defaultContentType = "application/json";

    public IReadOnlyList<string> DefaultContentTypeOptions { get; } =
    [
        "application/json",
        "application/xml",
        "text/plain",
        "text/html",
        "application/x-www-form-urlencoded",
        "multipart/form-data",
        DefaultContentTypeCustomOption
    ];

    public bool IsCustomDefaultContentType =>
        string.Equals(SelectedDefaultContentTypeOption, DefaultContentTypeCustomOption, StringComparison.Ordinal);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomDefaultContentType))]
    private string _selectedDefaultContentTypeOption = "application/json";

    [ObservableProperty]
    private string _customDefaultContentType = string.Empty;

    [ObservableProperty]
    private string _responseSaveDefaultFolder = string.Empty;

    [ObservableProperty]
    private string _responseSaveFileNamePattern = ResponseSaveFileNamePatternFormatter.DefaultPattern;

    [ObservableProperty]
    private string _responseSaveFileNamePatternValidationError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequestTimeoutDefaultWatermark))]
    private int _defaultRequestTimeoutSeconds = 100;

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

    public string RequestTimeoutDefaultWatermark =>
        $"{Strings.RequestTimeoutDefaultWatermark} ({DefaultRequestTimeoutSeconds})";

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

    /// <summary>Gets or sets the HTTP port the demo server listens on.</summary>
    [ObservableProperty]
    private int _demoServerPort = DemoServer.DefaultPort;

    /// <summary>Gets or sets the HTTPS port the demo server listens on.</summary>
    [ObservableProperty]
    private int _demoServerHttpsPort = DemoServer.DefaultHttpsPort;

    /// <summary>Gets or sets whether the demo server HTTP endpoint is enabled.</summary>
    [ObservableProperty]
    private bool _isDemoServerHttpEnabled = true;

    /// <summary>Gets or sets whether the demo server HTTPS endpoint is enabled.</summary>
    [ObservableProperty]
    private bool _isDemoServerHttpsEnabled;

    /// <summary>True while the embedded demo server is running.</summary>
    [ObservableProperty]
    private bool _isDemoServerRunning;

    /// <summary>
    /// True when a collection request targeting the demo server was loaded but the
    /// server is not yet running.  Shows an inline banner offering to start it.
    /// </summary>
    [ObservableProperty]
    private bool _isDemoServerBannerVisible;

    [RelayCommand]
    private async Task SelectResponseSaveDefaultFolderAsync()
    {
        if (StorageProvider is null)
        {
            return;
        }

        var suggestedStartLocation = await GetResponseSaveSuggestedStartLocationAsync();
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.OptionsResponseSaveDefaultFolder,
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStartLocation
        });

        if (folders.Count == 0)
        {
            return;
        }

        ResponseSaveDefaultFolder = folders[0].Path.LocalPath;
    }

    private void ApplySelectedCollection()
    {
        IsRenameCollectionFormVisible = false;
        RenameCollectionName = string.Empty;

        var previousSuppressCollectionInheritedHeadersAutoSave = _suppressCollectionInheritedHeadersAutoSave;
        _suppressCollectionInheritedHeadersAutoSave = true;
        try
        {
            CollectionItems.Clear();
            CollectionInheritedHeaders.Clear();
            if (SelectedCollection is { } collection)
            {
                foreach (var request in collection.Requests)
                {
                    CollectionItems.Add(new CollectionItemViewModel(request, collection.BaseUrl));
                }

                if (collection.Headers is { } inheritedHeaders)
                {
                    foreach (var inheritedHeader in inheritedHeaders)
                    {
                        CollectionInheritedHeaders.Add(new RequestHeaderViewModel
                        {
                            Name = inheritedHeader.Name,
                            Value = inheritedHeader.Value,
                            IsEnabled = inheritedHeader.IsEnabled
                        });
                    }
                }
            }
        }
        finally
        {
            _suppressCollectionInheritedHeadersAutoSave = previousSuppressCollectionInheritedHeadersAutoSave;
        }

        ApplyCollectionFilter();
    }

    private void OnUiFontSizeTextChangedCore()
    {
        OnPropertyChanged(nameof(UiFontSize));
        QueueOptionsAutoSave();
    }

    private void ApplyDefaultContentType()
    {
        _debugLogger.Information("Default content type changed to {ContentType}", DefaultContentType);
        _requestEditor.DefaultContentType = DefaultContentType;
        SyncDefaultContentTypeSelection(DefaultContentType);
        QueueOptionsAutoSave();
    }

    private void ApplySelectedDefaultContentTypeOption()
    {
        if (string.Equals(SelectedDefaultContentTypeOption, DefaultContentTypeCustomOption, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(CustomDefaultContentType))
            {
                DefaultContentType = CustomDefaultContentType;
            }

            return;
        }

        CustomDefaultContentType = string.Empty;
        DefaultContentType = SelectedDefaultContentTypeOption;
    }

    private void ApplyCustomDefaultContentType()
    {
        if (!IsCustomDefaultContentType)
        {
            return;
        }

        DefaultContentType = CustomDefaultContentType;
    }

    private void ApplyResponseSaveFileNamePattern()
    {
        if (ResponseSaveFileNamePatternFormatter.TryValidatePattern(ResponseSaveFileNamePattern, out var error))
        {
            ResponseSaveFileNamePatternValidationError = string.Empty;
            QueueOptionsAutoSave();
            return;
        }

        ResponseSaveFileNamePatternValidationError = error;
    }

    private void ApplyUiFontFamily()
    {
        if (Application.Current is { } currentApp)
        {
            var firstFamily = UiFontFamily.Split(',', 2, StringSplitOptions.TrimEntries)[0];
            var fontFamily = string.IsNullOrEmpty(firstFamily)
                ? FontFamily.Default
                : new FontFamily(firstFamily);
            currentApp.Resources["AppFontFamily"] = fontFamily;
        }

        QueueOptionsAutoSave();
    }

    private void ApplyDefaultRequestTimeoutSeconds()
    {
        if (DefaultRequestTimeoutSeconds < 1)
        {
            DefaultRequestTimeoutSeconds = 1;
            return;
        }

        _httpRequestService.SetDefaultRequestTimeout(TimeSpan.FromSeconds(DefaultRequestTimeoutSeconds));
        QueueOptionsAutoSave();
    }

    private void ApplyThemeOption()
    {
        _debugLogger.Information("Theme changed to {Theme}", SelectedThemeOption);

        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = SelectedThemeOption switch
        {
            DarkThemeOption => ThemeVariant.Dark,
            LightThemeOption => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };

        QueueOptionsAutoSave();
    }

    private void ApplyUnhandledExceptionCollectionSetting()
    {
        if (_unhandledExceptionCollector is { } collector)
        {
            collector.IsCollecting = CollectUnhandledExceptions;
        }

        QueueOptionsAutoSave();
    }

    private void ApplySelectedTlsVersionOption()
    {
        LogAndQueueOptionsAutoSave("Selected TLS version changed to {TlsVersion}", SelectedTlsVersionOption);
        if (InsecureTlsVersions.Contains(SelectedTlsVersionOption))
        {
            _debugLogger.Warning("TLS version {TlsVersion} is cryptographically broken and should only be used for testing against legacy servers", SelectedTlsVersionOption);
        }
    }

    private void ApplyShowRequestPreviewByDefault()
    {
        _requestEditor.ShowRequestPreview = ShowRequestPreviewByDefault;
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
        _debugLogger = appLogger.ForContext("LogTab", LogTab.Debug);
        _httpRequestsLogger = appLogger.ForContext("LogTab", LogTab.HttpRequests);

        _collectionInheritedHeadersAutoSaveDisposables.Add(_collectionInheritedHeadersAutoSaveRequestedSubject
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(snapshot => Dispatcher.UIThread.Post(() => TriggerCollectionInheritedHeadersAutoSave(snapshot))));

        _optionsAutoSaveDisposables.Add(_optionsAutoSaveRequestedSubject
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(_ => Dispatcher.UIThread.Post(SaveOptions)));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => propertyName is not null && AutoSaveOnlyOptionPropertyNames.Contains(propertyName))
            .Subscribe(_ => QueueOptionsAutoSave()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(FollowHttpRedirects), StringComparison.Ordinal))
            .Subscribe(_ => LogAndQueueOptionsAutoSave("Follow redirects changed to {FollowRedirects}", FollowHttpRedirects)));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(EnableHttpDiagnostics), StringComparison.Ordinal))
            .Subscribe(_ => LogAndQueueOptionsAutoSave("HTTP diagnostics enabled changed to {IsEnabled}", EnableHttpDiagnostics)));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(SelectedThemeOption), StringComparison.Ordinal))
            .Subscribe(_ => ApplyThemeOption()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(CollectUnhandledExceptions), StringComparison.Ordinal))
            .Subscribe(_ => ApplyUnhandledExceptionCollectionSetting()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(SelectedTlsVersionOption), StringComparison.Ordinal))
            .Subscribe(_ => ApplySelectedTlsVersionOption()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(ShowRequestPreviewByDefault), StringComparison.Ordinal))
            .Subscribe(_ => ApplyShowRequestPreviewByDefault()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(DefaultContentType), StringComparison.Ordinal))
            .Subscribe(_ => ApplyDefaultContentType()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(SelectedDefaultContentTypeOption), StringComparison.Ordinal))
            .Subscribe(_ => ApplySelectedDefaultContentTypeOption()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(CustomDefaultContentType), StringComparison.Ordinal))
            .Subscribe(_ => ApplyCustomDefaultContentType()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(ResponseSaveFileNamePattern), StringComparison.Ordinal))
            .Subscribe(_ => ApplyResponseSaveFileNamePattern()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(UiFontFamily), StringComparison.Ordinal))
            .Subscribe(_ => ApplyUiFontFamily()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(UiFontSizeText), StringComparison.Ordinal))
            .Subscribe(_ => OnUiFontSizeTextChangedCore()));

        _optionsAutoSaveDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(DefaultRequestTimeoutSeconds), StringComparison.Ordinal))
            .Subscribe(_ => ApplyDefaultRequestTimeoutSeconds()));

        _historyFilterDisposables.Add(_historyFilterRequestedSubject
            .Throttle(TimeSpan.FromMilliseconds(150))
            .DistinctUntilChanged(StringComparer.Ordinal)
            .Subscribe(query => ApplyHistoryFilter(query)));

        _collectionSearchFilterDisposables.Add(_collectionSearchFilterRequestedSubject
            .Throttle(TimeSpan.FromMilliseconds(150))
            .DistinctUntilChanged(StringComparer.Ordinal)
            .Subscribe(_ => ApplyCollectionFilter()));

        _collectionSearchFilterDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(SelectedCollection), StringComparison.Ordinal))
            .Subscribe(_ => ApplySelectedCollection()));

        _collectionSearchFilterDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(CollectionSearchQuery), StringComparison.Ordinal))
            .Subscribe(_ => _collectionSearchFilterRequestedSubject.OnNext(CollectionSearchQuery)));

        _collectionSearchFilterDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => propertyName is nameof(CollectionSortBy)
                or nameof(IsCollectionTreeView))
            .Subscribe(_ => ApplyCollectionFilter()));

        _historyFilterDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(HistorySearchQuery), StringComparison.Ordinal))
            .Subscribe(_ => _historyFilterRequestedSubject.OnNext(HistorySearchQuery)));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(ActiveRequestTab), StringComparison.Ordinal))
            .Subscribe(_ => ApplyActiveRequestTab()));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(SelectedLayoutName), StringComparison.Ordinal))
            .Subscribe(_ => ApplySelectedLayoutName()));

        _requestEditor = new RequestEditorViewModel(
            _variableResolver,
            GetActiveVariablesForEditor,
            _debugLogger,
            QueueOptionsAutoSave);
        RequestTabs = [];
        // NOTE: CreateRequestEditor() cannot be used here because it reads _applicationOptions
        // which is populated by ApplyOptions (called later in the constructor). The initial
        // tab is created with bare defaults; ApplyOptions then sets URL/content-type/etc.
        var firstTab = new RequestTabViewModel(_requestEditor);
        RequestTabs.Add(firstTab);
        _activeRequestTab = firstTab;
        _environmentsViewModel = new EnvironmentsViewModel(
            environmentRepository,
            () => _requestEditor,
            () => StorageProvider,
            _debugLogger);
        _optionsViewModel = new OptionsViewModel(this);
        _cookieJarViewModel = new CookieJarViewModel(cookieContainer);
        _collectionsWorkflow = new CollectionsWorkflow(_collectionRepository, _debugLogger);
        _collectionsManagementCoordinator = new CollectionsManagementCoordinator(
            _collectionRepository,
            LoadCollectionsAsync,
            () => Collections?.ToList() ?? [],
            () => SelectedCollection,
            _requestEditor.BuildResolvedHttpRequestDraft,
            _debugLogger);
        _graphQlViewModel = new GraphQlViewModel(_protocolHttpClient, appLogger);
        _graphQlRequestWorkflow = new GraphQlRequestWorkflow(_requestEditor, _graphQlViewModel, _httpRequestsLogger);
        _manualGraphQlRequestCoordinator = new ManualGraphQlRequestCoordinator(
            _graphQlRequestWorkflow,
            _collectionsWorkflow,
            LoadCollectionsAsync,
            LoadHistoryAsync,
            () => new GraphQlRequestCollectionState(
                _requestEditor.RequestName,
                _requestEditor.RequestNotes,
                _graphQlViewModel.BuildRequestBodyJson()),
            _httpRequestsLogger);
        _demoDataWorkflow = new DemoDataWorkflow(
            _collectionRepository,
            cancellationToken => _environmentsViewModel.GetAllEnvironmentsAsync(cancellationToken),
            (environmentName, variables, cancellationToken) => _environmentsViewModel.SeedEnvironmentAsync(environmentName, variables, cancellationToken),
            () => DemoServerPort,
            _debugLogger);
        _demoServerLifecycleCoordinator = new DemoServerLifecycleCoordinator(_demoServer, _debugLogger);
        _httpRequestWorkflow = new HttpRequestWorkflow(
            _httpRequestService,
            _requestEditor,
            GetActiveVariablesForEditor,
            _scriptRunner,
            _scriptViewModel,
            _httpRequestsLogger);
        _manualHttpRequestCoordinator = new ManualHttpRequestCoordinator(
            _httpRequestWorkflow,
            _requestEditor,
            _collectionsWorkflow,
            LoadCollectionsAsync,
            LoadHistoryAsync,
            _httpRequestsLogger);
        _webSocketViewModel = new WebSocketViewModel(appLogger);
        _sseViewModel = new SseViewModel(_protocolHttpClient, appLogger);
        _streamingConnectionWorkflow = new StreamingConnectionWorkflow(_requestEditor, _webSocketViewModel, _sseViewModel);

        History = [];
        Collections = [];
        CollectionItems = [];
        CollectionInheritedHeaders = [];
        FilteredCollectionItems = [];
        CollectionGroups = [];
        ScheduledJobs = [];
        SavedLayoutNames = [];

        SendRequestCommand = new AsyncRelayCommand(SendRequestAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => _environmentsViewModel.PropertyChanged += handler,
                handler => _environmentsViewModel.PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(EnvironmentsViewModel.ActiveEnvironment), StringComparison.Ordinal))
            .Subscribe(_ =>
            {
                OnPropertyChanged(nameof(ActiveEnvironment));
                OnPropertyChanged(nameof(ActiveEnvironmentAccentColor));
                OnPropertyChanged(nameof(ActiveEnvironmentHasColor));
                OnPropertyChanged(nameof(ActiveEnvironmentShowWarningBanner));
            }));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => _environmentsViewModel.PropertyChanged += handler,
                handler => _environmentsViewModel.PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(EnvironmentsViewModel.NewEnvironmentName), StringComparison.Ordinal))
            .Subscribe(_ => OnPropertyChanged(nameof(NewEnvironmentName))));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => _environmentsViewModel.PropertyChanged += handler,
                handler => _environmentsViewModel.PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(EnvironmentsViewModel.IsEnvironmentPanelVisible), StringComparison.Ordinal))
            .Subscribe(_ => OnPropertyChanged(nameof(IsEnvironmentPanelVisible))));

        _crossFeatureDisposables.Add(Observable.Merge(
                _webSocketViewModel.PropertyChangedObservable,
                _sseViewModel.PropertyChangedObservable)
            .Where(eventArgs => eventArgs.PropertyName is nameof(WebSocketViewModel.IsConnected)
                or nameof(SseViewModel.IsConnected))
            .Subscribe(_ => OnPropertyChanged(nameof(PrimaryActionLabel))));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => SendRequestCommand.PropertyChanged += handler,
                handler => SendRequestCommand.PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => propertyName is nameof(IAsyncRelayCommand.IsRunning)
                or nameof(IAsyncRelayCommand.CanBeCanceled))
            .Subscribe(_ =>
            {
                OnPropertyChanged(nameof(PrimaryActionLabel));
                OnPropertyChanged(nameof(IsRequestInProgress));
            }));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                handler => CollectionInheritedHeaders.CollectionChanged += handler,
                handler => CollectionInheritedHeaders.CollectionChanged -= handler)
            .Subscribe(_ =>
            {
                SyncActiveCollectionRequestInheritedHeaders();
                QueueCollectionInheritedHeadersAutoSave();
            }));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                handler => CollectionInheritedHeaders.CollectionChanged += handler,
                handler => CollectionInheritedHeaders.CollectionChanged -= handler)
            .Select(_ => ObserveCollectionInheritedHeaderPropertyChanges())
            .StartWith(ObserveCollectionInheritedHeaderPropertyChanges())
            .Switch()
            .Where(eventArgs => eventArgs.PropertyName is nameof(RequestHeaderViewModel.Name)
                or nameof(RequestHeaderViewModel.Value)
                or nameof(RequestHeaderViewModel.IsEnabled))
            .Subscribe(_ =>
            {
                SyncActiveCollectionRequestInheritedHeaders();
                QueueCollectionInheritedHeadersAutoSave();
            }));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Where(eventPattern => string.Equals(eventPattern.EventArgs.PropertyName, nameof(ActiveRequestTab), StringComparison.Ordinal))
            .Select(_ => ActiveRequestTab?.RequestEditor)
            .StartWith(ActiveRequestTab?.RequestEditor)
            .Select(editor => editor?.PropertyChangedObservable ?? Observable.Empty<PropertyChangedEventArgs>())
            .Switch()
            .Where(eventArgs => string.Equals(eventArgs.PropertyName, nameof(RequestEditorViewModel.SelectedRequestType), StringComparison.Ordinal))
            .Subscribe(_ => OnPropertyChanged(nameof(PrimaryActionLabel))));

        _dockFactory = new DockFactory(this, _environmentsViewModel, _optionsViewModel, _cookieJarViewModel);
        Layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(Layout);
        _defaultLayout = CaptureLayoutSnapshot();

        _suppressLayoutRestore = true;
        var options = initialOptions ?? new ApplicationOptions();
        ApplyOptions(options);
        ApplyLayoutOptions(options.Layouts);
        _startupLayoutSnapshot = options.Layouts?.CurrentLayout;
        _suppressLayoutRestore = false;
        _httpRequestService.SetDefaultRequestTimeout(TimeSpan.FromSeconds(DefaultRequestTimeoutSeconds));
        _requestEditor.RefreshRequestPreview();
        _responseActions = new ResponseActionsViewModel(this);
    }

    public ObservableCollection<RequestHistoryEntry> History { get; }
    public ObservableCollection<Collection> Collections { get; }
    public ObservableCollection<CollectionItemViewModel> CollectionItems { get; }
    public ObservableCollection<RequestHeaderViewModel> CollectionInheritedHeaders { get; }

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
    public ScriptViewModel ScriptEditor => _scriptViewModel;

    /// <summary>
    /// The collection of open request tabs. There is always at least one tab.
    /// </summary>
    public ObservableCollection<RequestTabViewModel> RequestTabs { get; }

    private void ApplyActiveRequestTab()
    {
        if (ActiveRequestTab is not { } newValue)
        {
            return;
        }

        _requestEditor = newValue.RequestEditor;
        // Capture the editor in a local so the dispatcher callback always targets
        // the same instance even if ActiveRequestTab changes again before it runs.
        var editorForThisTab = _requestEditor;
        // Suppress refreshes while Avalonia's TwoWay bindings re-subscribe to the new
        // editor and fire current ComboBox/TextBox values back as if they changed.
        // Avalonia processes PropertyChanged notifications synchronously within the
        // current dispatcher frame, so a Post at default priority is guaranteed to run
        // after all binding subscriptions have settled for the current frame.
        editorForThisTab.BeginBulkUpdate();
        OnPropertyChanged(nameof(RequestEditor));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        Dispatcher.UIThread.Post(() => editorForThisTab.EndBulkUpdate());
    }

    [RelayCommand]
    private void NewRequestTab()
    {
        var tab = new RequestTabViewModel(CreateRequestEditor());
        RequestTabs.Add(tab);
        ActiveRequestTab = tab;
    }

    [RelayCommand]
    private void CloseRequestTab(RequestTabViewModel? tab)
    {
        if (tab is null || RequestTabs.Count <= 1)
        {
            return;
        }

        if (ReferenceEquals(ActiveRequestTab, tab))
        {
            var index = RequestTabs.IndexOf(tab);
            RequestTabs.Remove(tab);
            var newIndex = Math.Max(0, Math.Min(index, RequestTabs.Count - 1));
            ActiveRequestTab = RequestTabs[newIndex];
        }
        else
        {
            RequestTabs.Remove(tab);
        }

        tab.Dispose();
    }

    /// <summary>
    /// Creates a new <see cref="RequestEditorViewModel"/> pre-configured with the current
    /// application defaults. Used both for the initial tab and for each new tab created via
    /// <see cref="NewRequestTabCommand"/>.
    /// </summary>
    private RequestEditorViewModel CreateRequestEditor() =>
        new(
            _variableResolver,
            GetActiveVariablesForEditor,
            _debugLogger,
            QueueOptionsAutoSave)
        {
            RequestUrl = _applicationOptions.Http.DefaultRequestUrl,
            DefaultContentType = _applicationOptions.Http.DefaultContentType,
            FollowRedirectsForRequest = _applicationOptions.Http.FollowRedirects,
            ShowRequestPreview = _applicationOptions.Http.ShowRequestPreviewByDefault,
            SelectedHttpVersionOption = _applicationOptions.Http.HttpVersion
        };

    /// <summary>
    /// Label for the primary action button in the request composer.
    /// Shows "Send" for HTTP/GraphQL, "Connect"/"Disconnect" for streaming protocols.
    /// </summary>
    public string PrimaryActionLabel
    {
        get
        {
            if (SendRequestCommand.IsRunning && SendRequestCommand.CanBeCanceled)
            {
                return "Cancel";
            }

            return _requestEditor.SelectedRequestType switch
            {
                RequestType.WebSocket => _webSocketViewModel.IsConnected ? "Disconnect" : "Connect",
                RequestType.Sse => _sseViewModel.IsConnected ? "Disconnect" : "Connect",
                _ => "Send"
            };
        }
    }

    public RequestEnvironment? ActiveEnvironment
    {
        get => _environmentsViewModel.ActiveEnvironment;
        set => _environmentsViewModel.ActiveEnvironment = value;
    }

    /// <summary>
    /// The accent color of the currently active environment, or <see langword="null"/> when none is set.
    /// Bound to the warning banner background and the activity-bar badge dot (UX idea 7.1 — Patterns B and D).
    /// </summary>
    public string? ActiveEnvironmentAccentColor => _environmentsViewModel.ActiveEnvironment?.AccentColor;

    /// <summary>
    /// <see langword="true"/> when the active environment has a non-empty accent color.
    /// Used to toggle the badge dot on the activity-bar Environments button (UX idea 7.1 — Pattern D).
    /// </summary>
    public bool ActiveEnvironmentHasColor => !string.IsNullOrEmpty(ActiveEnvironmentAccentColor);

    /// <summary>
    /// <see langword="true"/> when the active environment has <see cref="RequestEnvironment.ShowWarningBanner"/>
    /// enabled. Bound to the full-width warning banner below the toolbar (UX idea 7.1 — Pattern B).
    /// </summary>
    public bool ActiveEnvironmentShowWarningBanner =>
        _environmentsViewModel.ActiveEnvironment?.ShowWarningBanner ?? false;

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
    public bool IsRequestInProgress => SendRequestCommand.IsRunning;

    [RelayCommand]
    private void ExecutePrimaryAction()
    {
        if (SendRequestCommand.IsRunning && SendRequestCommand.CanBeCanceled)
        {
            SendRequestCommand.Cancel();
            return;
        }

        if (SendRequestCommand.CanExecute(null))
        {
            SendRequestCommand.Execute(null);
        }
    }

    public IRelayCommand AddEnvironmentVariableCommand => _environmentsViewModel.AddEnvironmentVariableCommand;
    public IRelayCommand<EnvironmentVariableViewModel?> RemoveEnvironmentVariableCommand => _environmentsViewModel.RemoveEnvironmentVariableCommand;
    public IAsyncRelayCommand SaveEnvironmentCommand => _environmentsViewModel.SaveEnvironmentCommand;
    public IAsyncRelayCommand<RequestEnvironment?> DeleteEnvironmentCommand => _environmentsViewModel.DeleteEnvironmentCommand;
    public IRelayCommand<RequestEnvironment?> EditEnvironmentCommand => _environmentsViewModel.EditEnvironmentCommand;
    public IRelayCommand NewEnvironmentCommand => _environmentsViewModel.NewEnvironmentCommand;
    public IAsyncRelayCommand ExportEnvironmentsCommand => _environmentsViewModel.ExportEnvironmentsCommand;

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
                ShowRequestPreviewByDefault = ShowRequestPreviewByDefault,
                DefaultRequestUrl = DefaultRequestUrl,
                ResponseSaveDefaultFolder = ResponseSaveDefaultFolder,
                ResponseSaveFileNamePattern = ResponseSaveFileNamePattern,
                DemoServerPort = DemoServerPort,
                DemoServerHttpsPort = DemoServerHttpsPort,
                DemoServerHttpEnabled = IsDemoServerHttpEnabled,
                DemoServerHttpsEnabled = IsDemoServerHttpsEnabled,
                DefaultRequestTimeoutSeconds = DefaultRequestTimeoutSeconds
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
            ShowRequestPreviewByDefault = options.Http.ShowRequestPreviewByDefault;
            DefaultRequestUrl = options.Http.DefaultRequestUrl;
            DefaultContentType = options.Http.DefaultContentType;
            DefaultRequestTimeoutSeconds = options.Http.DefaultRequestTimeoutSeconds;
            ResponseSaveDefaultFolder = options.Http.ResponseSaveDefaultFolder;
            ResponseSaveFileNamePattern = options.Http.ResponseSaveFileNamePattern;
            _requestEditor.DefaultContentType = options.Http.DefaultContentType;
            UiFontFamily = options.Appearance.FontFamily;
            UiFontSizeText = options.Appearance.FontSize.ToString("0.##", CultureInfo.InvariantCulture);
            AutoStartScheduledJobsOnLaunch = options.ScheduledJobs.AutoStartOnLaunch;
            DefaultScheduledJobIntervalSeconds = options.ScheduledJobs.DefaultIntervalSeconds;
            DemoServerPort = options.Http.DemoServerPort;
            DemoServerHttpsPort = options.Http.DemoServerHttpsPort;
            IsDemoServerHttpEnabled = options.Http.DemoServerHttpEnabled;
            IsDemoServerHttpsEnabled = options.Http.DemoServerHttpsEnabled;
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
        job.Dispose();
    }

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
        if (_dockFactory?.LeftPanelViewModel is { Owner: IDock ownerDock } leftPanelVm)
        {
            ownerDock.ActiveDockable = leftPanelVm;
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
        if (_dockFactory?.LogPanelViewModel is { Owner: IDock ownerDock } logPanelVm)
        {
            ownerDock.ActiveDockable = logPanelVm;
        }
    }

    [RelayCommand]
    private void OpenCookieJar()
    {
        if (_dockFactory?.CookieJarViewModel is { Owner: IDock ownerDock } cookieJarVm)
        {
            cookieJarVm.RefreshCookies();
            ownerDock.ActiveDockable = cookieJarVm;
        }
    }

    [RelayCommand]
    private void ExitApplication() => ExitApplicationAction?.Invoke();

    [RelayCommand]
    private void OpenOptions()
    {
        if (_dockFactory?.OptionsViewModel is { Owner: IDock ownerDock } optVm)
        {
            ownerDock.ActiveDockable = optVm;
        }
    }

    [RelayCommand]
    private void OpenEnvironments()
    {
        if (_dockFactory?.EnvironmentsViewModel is { Owner: IDock ownerDock } environmentsVm)
        {
            ownerDock.ActiveDockable = environmentsVm;
        }
    }

    [RelayCommand]
    private void OpenLayoutPanel()
    {
        if (_dockFactory?.LayoutManagementViewModel is { Owner: IDock ownerDock } layoutVm)
        {
            ownerDock.ActiveDockable = layoutVm;
        }
    }

    private void ApplySelectedLayoutName()
    {
        if (_suppressLayoutRestore || string.IsNullOrWhiteSpace(SelectedLayoutName))
        {
            return;
        }

        if (_savedLayouts.TryGetValue(SelectedLayoutName, out var snapshot))
        {
            ApplyLayoutSnapshot(snapshot);
            PersistLayoutOptions();
        }
    }

    [RelayCommand]
    private void SaveLayoutAsNew()
    {
        RefreshDockTreeCache();
        var savedLayoutName = _layoutWorkflow.SaveLayoutAsNew(CaptureLayoutSnapshot, GenerateNextLayoutName, _savedLayouts);
        if (string.IsNullOrWhiteSpace(savedLayoutName))
        {
            return;
        }

        _suppressLayoutRestore = true;
        RefreshSavedLayoutNames();
        SelectedLayoutName = savedLayoutName;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [RelayCommand]
    private void SaveLayoutToExisting(string? layoutName)
    {
        RefreshDockTreeCache();
        if (!_layoutWorkflow.SaveLayoutToExisting(layoutName, CaptureLayoutSnapshot, _savedLayouts))
        {
            return;
        }

        _suppressLayoutRestore = true;
        RefreshSavedLayoutNames();
        SelectedLayoutName = layoutName;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [RelayCommand]
    private void RestoreDefaultLayout()
    {
        if (!_layoutWorkflow.RestoreDefaultLayout(_defaultLayout, ApplyLayoutSnapshot))
        {
            return;
        }

        _suppressLayoutRestore = true;
        SelectedLayoutName = null;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [RelayCommand]
    private void RemoveLayout(string? layoutName)
    {
        var removeLayoutResult = _layoutWorkflow.RemoveLayout(layoutName, _savedLayouts, SelectedLayoutName);
        if (!removeLayoutResult.Removed)
        {
            return;
        }

        _suppressLayoutRestore = true;
        RefreshSavedLayoutNames();
        SelectedLayoutName = removeLayoutResult.SelectedLayoutName;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [RelayCommand]
    private void LoadCollectionRequest(CollectionItemViewModel? item) => LoadCollectionRequestCore(item);

    internal void LoadCollectionRequestCore(CollectionItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (TryActivateExistingCollectionRequestTab(item))
        {
            SyncActiveCollectionRequestInheritedHeaders();
            return;
        }

        NewRequestTab();
        ApplyCollectionRequestToEditor(item);
        SyncActiveCollectionRequestInheritedHeaders();
    }

    private bool TryActivateExistingCollectionRequestTab(CollectionItemViewModel item)
    {
        var collectionId = SelectedCollection?.Id ?? 0;
        var existingTab = RequestTabs.FirstOrDefault(tab =>
            tab.MatchesCollectionRequest(collectionId, item.Method, item.Path, item.Name));
        if (existingTab is null)
        {
            return false;
        }

        ActiveRequestTab = existingTab;
        return true;
    }

    private void ApplyCollectionRequestToEditor(CollectionItemViewModel item)
    {
        using (_requestEditor.BeginBulkUpdate())
        {
            var requestType = ResolveCollectionRequestType(item.Method);
            _requestEditor.SelectedRequestType = requestType;
            if (requestType == RequestType.Http)
            {
                _requestEditor.SelectedMethod = item.Method;
            }

            var resolvedUrl = BuildCollectionRequestUrl(item.Path, requestType);
            _requestEditor.RequestUrl = resolvedUrl;
            _requestEditor.RequestName = item.Name;
            _requestEditor.RequestNotes = item.Notes ?? string.Empty;

            ApplyCollectionRequestHeaders(item);
            ApplyCollectionRequestContent(item);
            IsDemoServerBannerVisible = ShouldShowDemoServerBanner(resolvedUrl);

            var collectionId = SelectedCollection?.Id ?? 0;
            ActiveRequestTab?.SetCollectionRequestSource(collectionId, item.Method, item.Path, item.Name);
        }
    }

    private static RequestType ResolveCollectionRequestType(string method) =>
        method switch
        {
            "WS" or "WSS" => RequestType.WebSocket,
            "SSE" => RequestType.Sse,
            _ => RequestType.Http
        };

    private string BuildCollectionRequestUrl(string path, RequestType requestType)
    {
        var collectionBaseUrl = SelectedCollection?.BaseUrl;
        var activeEnvironment = ActiveEnvironment;

        var baseUrl = activeEnvironment is { }
            ? _variableResolver.Resolve(collectionBaseUrl ?? string.Empty, _requestEditor.GetResolvedVariables())
            : (collectionBaseUrl ?? string.Empty);

        var resolvedUrl = CollectionUrlHelper.BuildFullUrl(baseUrl, path);
        if (requestType == RequestType.WebSocket)
        {
            resolvedUrl = resolvedUrl
                .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
        }

        return resolvedUrl;
    }

    private void ApplyCollectionRequestHeaders(CollectionItemViewModel item)
    {
        _requestEditor.RequestHeaders.Clear();

        var inheritedHeaders = SelectedCollection?.Headers;
        var mergedHeaders = MergeCollectionAndRequestHeaders(inheritedHeaders, item.Headers);
        if (mergedHeaders is null)
        {
            _requestEditor.EnsurePlaceholderRows();
            return;
        }

        foreach (var mergedHeader in mergedHeaders)
        {
            _requestEditor.RequestHeaders.Add(new RequestHeaderViewModel
            {
                Name = mergedHeader.Name,
                Value = mergedHeader.Value,
                IsEnabled = mergedHeader.IsEnabled,
                IsInherited = inheritedHeaders?.Any(inheritedHeader =>
                    string.Equals(inheritedHeader.Name, mergedHeader.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(inheritedHeader.Value, mergedHeader.Value, StringComparison.Ordinal)
                    && inheritedHeader.IsEnabled == mergedHeader.IsEnabled) == true
            });
        }

        _requestEditor.EnsurePlaceholderRows();
    }

    private void ApplyCollectionRequestContent(CollectionItemViewModel item)
    {
        if (string.IsNullOrEmpty(item.ContentType))
        {
            _requestEditor.SelectedContentTypeOption = RequestEditorViewModel.NoneContentTypeOption;
            _requestEditor.CustomContentType = string.Empty;
        }
        else if (_requestEditor.ContentTypeOptions.Contains(item.ContentType))
        {
            _requestEditor.SelectedContentTypeOption = item.ContentType;
            _requestEditor.CustomContentType = string.Empty;
        }
        else
        {
            _requestEditor.SelectedContentTypeOption = RequestEditorViewModel.CustomContentTypeOption;
            _requestEditor.CustomContentType = item.ContentType;
        }

        if (!string.IsNullOrEmpty(item.Body))
        {
            _requestEditor.RequestBody = item.Body;
        }
        else if (item.Method is "POST" or "PUT" or "PATCH")
        {
            _requestEditor.RequestBody = "{}";
        }
        else
        {
            _requestEditor.RequestBody = string.Empty;
        }
    }

    private bool ShouldShowDemoServerBanner(string resolvedUrl) =>
        _demoServer is { } server
        && !server.IsRunning
        && (IsDemoServerUrl(resolvedUrl, server.Port) || IsDemoServerUrl(resolvedUrl, server.HttpsPort));

    private static IReadOnlyList<RequestHeader>? MergeCollectionAndRequestHeaders(
        IReadOnlyList<RequestHeader>? collectionHeaders,
        IReadOnlyList<RequestHeader>? requestHeaders)
    {
        var merged = new List<RequestHeader>();
        var headerIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (collectionHeaders is { })
        {
            foreach (var header in collectionHeaders)
            {
                headerIndexes[header.Name] = merged.Count;
                merged.Add(header);
            }
        }

        if (requestHeaders is { })
        {
            foreach (var requestHeader in requestHeaders)
            {
                if (headerIndexes.TryGetValue(requestHeader.Name, out var index))
                {
                    merged[index] = requestHeader;
                }
                else
                {
                    headerIndexes[requestHeader.Name] = merged.Count;
                    merged.Add(requestHeader);
                }
            }
        }

        return merged.Count > 0 ? merged : null;
    }

    /// <summary>Starts the embedded demo server on <see cref="DemoServerPort"/> and/or <see cref="DemoServerHttpsPort"/>.</summary>
    [RelayCommand]
    private async Task StartDemoServerAsync()
    {
        var outcome = await _demoServerLifecycleCoordinator.StartAsync(
            DemoServerPort,
            DemoServerHttpsPort,
            IsDemoServerHttpEnabled,
            IsDemoServerHttpsEnabled);

        if (outcome.ErrorMessage is { } errorMessage)
        {
            ErrorMessage = errorMessage;
            return;
        }

        if (!outcome.Changed)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsDemoServerRunning = outcome.IsRunning;
        IsDemoServerBannerVisible = false;
    }

    /// <summary>Stops the embedded demo server.</summary>
    [RelayCommand]
    private async Task StopDemoServerAsync()
    {
        var outcome = await _demoServerLifecycleCoordinator.StopAsync();
        if (!outcome.Changed)
        {
            return;
        }

        IsDemoServerRunning = outcome.IsRunning;
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
    private async Task CreateCollectionAsync(CancellationToken cancellationToken)
    {
        var outcome = await _collectionsManagementCoordinator.CreateCollectionAsync(NewCollectionName, cancellationToken);
        if (outcome.ErrorMessage is { } errorMessage)
        {
            ErrorMessage = errorMessage;
            return;
        }

        if (!outcome.Changed)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsNewCollectionFormVisible = false;
        NewCollectionName = string.Empty;
        SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == outcome.SelectedCollectionId);
        LeftPanelTab = "Collections";
    }

    [RelayCommand]
    private void ShowRenameCollectionForm()
    {
        if (SelectedCollection is not { } collection)
        {
            return;
        }

        RenameCollectionName = collection.Name;
        IsRenameCollectionFormVisible = true;
    }

    [RelayCommand]
    private void CancelRenameCollection()
    {
        RenameCollectionName = string.Empty;
        IsRenameCollectionFormVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmRenameCollectionAsync(CancellationToken cancellationToken)
    {
        var outcome = await _collectionsManagementCoordinator.RenameSelectedCollectionAsync(RenameCollectionName, cancellationToken);
        if (outcome.ErrorMessage is { } errorMessage)
        {
            ErrorMessage = errorMessage;
            return;
        }

        if (!outcome.Changed)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsRenameCollectionFormVisible = false;
        RenameCollectionName = string.Empty;
        SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == outcome.SelectedCollectionId);
    }

    [RelayCommand]
    private async Task AddRequestToCollectionAsync()
    {
        var outcome = await _collectionsManagementCoordinator.AddCurrentRequestToSelectedCollectionAsync();
        if (!outcome.Changed)
        {
            return;
        }

        SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == outcome.SelectedCollectionId);
    }

    [RelayCommand]
    private void AddCollectionInheritedHeader()
    {
        CollectionInheritedHeaders.Add(new RequestHeaderViewModel());
    }

    [RelayCommand]
    private void RemoveCollectionInheritedHeader(RequestHeaderViewModel? header)
    {
        if (header is null)
        {
            return;
        }

        CollectionInheritedHeaders.Remove(header);
    }

    [RelayCommand]
    private async Task SaveCollectionInheritedHeadersAsync(CancellationToken cancellationToken)
    {
        if (BuildCollectionInheritedHeadersAutoSaveSnapshot() is { } snapshot)
        {
            await PersistCollectionInheritedHeadersSnapshotAsync(snapshot, cancellationToken, selectUpdatedCollection: true);
            ClearPendingCollectionInheritedHeadersAutoSaveState();
        }
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
            .GroupBy(i => i.GroupKey, StringComparer.OrdinalIgnoreCase))
        {
            var groupVm = new CollectionGroupViewModel(group.Key, group.ToList());
            if (previousExpanded.TryGetValue(group.Key, out var wasExpanded))
            {
                groupVm.IsExpanded = wasExpanded;
            }

            CollectionGroups.Add(groupVm);
        }
    }

    private static IReadOnlyList<RequestHeader>? BuildCollectionInheritedHeaders(
        IEnumerable<RequestHeaderViewModel> headerViewModels)
    {
        var headers = headerViewModels
            .Where(headerViewModel => !string.IsNullOrWhiteSpace(headerViewModel.Name))
            .Select(headerViewModel => new RequestHeader(
                headerViewModel.Name.Trim(),
                headerViewModel.Value,
                headerViewModel.IsEnabled))
            .ToList();

        return headers is { Count: > 0 } ? headers : null;
    }

    private CollectionInheritedHeadersAutoSaveSnapshot? BuildCollectionInheritedHeadersAutoSaveSnapshot()
    {
        if (SelectedCollection is not { } collection)
        {
            return null;
        }

        var inheritedHeaders = BuildCollectionInheritedHeaders(CollectionInheritedHeaders);
        return new CollectionInheritedHeadersAutoSaveSnapshot(
            collection.Id,
            collection.Name,
            collection.SourcePath,
            collection.BaseUrl,
            collection.Requests,
            inheritedHeaders);
    }

    private async Task PersistCollectionInheritedHeadersSnapshotAsync(
        CollectionInheritedHeadersAutoSaveSnapshot snapshot,
        CancellationToken cancellationToken,
        bool selectUpdatedCollection)
    {
        var currentCollection = Collections.FirstOrDefault(collection => collection.Id == snapshot.CollectionId);
        if (currentCollection is not { })
        {
            return;
        }

        if (CollectionHeadersEqual(currentCollection.Headers, snapshot.InheritedHeaders))
        {
            return;
        }

        await _collectionRepository.UpdateAsync(
            snapshot.CollectionId,
            snapshot.CollectionName,
            snapshot.CollectionSourcePath,
            snapshot.CollectionBaseUrl,
            snapshot.CollectionRequests,
            snapshot.InheritedHeaders,
            cancellationToken);

        await LoadCollectionsAsync(cancellationToken);
        if (selectUpdatedCollection && SelectedCollection?.Id == snapshot.CollectionId)
        {
            SelectedCollection = Collections.FirstOrDefault(candidateCollection => candidateCollection.Id == snapshot.CollectionId);
        }

        _debugLogger.Information("Updated inherited headers for collection {CollectionName}", snapshot.CollectionName);
    }

    private void QueueCollectionInheritedHeadersAutoSave()
    {
        if (_suppressCollectionInheritedHeadersAutoSave)
        {
            return;
        }

        if (BuildCollectionInheritedHeadersAutoSaveSnapshot() is not { } snapshot)
        {
            return;
        }

        _hasPendingCollectionInheritedHeadersAutoSave = true;
        _pendingCollectionInheritedHeadersAutoSaveSnapshot = snapshot;

        _collectionInheritedHeadersAutoSaveVersion++;
        _collectionInheritedHeadersAutoSaveRequestedSubject.OnNext(snapshot);
    }

    private void TriggerCollectionInheritedHeadersAutoSave(CollectionInheritedHeadersAutoSaveSnapshot snapshot)
    {
        var autoSaveVersion = _collectionInheritedHeadersAutoSaveVersion;
        _collectionInheritedHeadersAutoSaveTask = TriggerCollectionInheritedHeadersAutoSaveAsync(snapshot, autoSaveVersion);
    }

    private async Task TriggerCollectionInheritedHeadersAutoSaveAsync(
        CollectionInheritedHeadersAutoSaveSnapshot snapshot,
        int autoSaveVersion)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                async () => await PersistCollectionInheritedHeadersSnapshotAsync(
                    snapshot,
                    CancellationToken.None,
                    selectUpdatedCollection: false),
                DispatcherPriority.Background);

            if (autoSaveVersion == _collectionInheritedHeadersAutoSaveVersion)
            {
                ClearPendingCollectionInheritedHeadersAutoSaveState();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _debugLogger.Warning(ex, "Collection inherited headers auto-save failed");
        }
    }

    public async Task FlushPendingCollectionInheritedHeadersAutoSaveAsync()
    {
        if (!_hasPendingCollectionInheritedHeadersAutoSave || _pendingCollectionInheritedHeadersAutoSaveSnapshot is not { } snapshot)
        {
            return;
        }

        if (_collectionInheritedHeadersAutoSaveTask is { } autoSaveTask)
        {
            await autoSaveTask.ConfigureAwait(false);
        }

        await PersistCollectionInheritedHeadersSnapshotAsync(snapshot, CancellationToken.None, selectUpdatedCollection: true);
        ClearPendingCollectionInheritedHeadersAutoSaveState();
    }

    private void ClearPendingCollectionInheritedHeadersAutoSaveState()
    {
        _hasPendingCollectionInheritedHeadersAutoSave = false;
        _pendingCollectionInheritedHeadersAutoSaveSnapshot = null;
    }

    private IObservable<PropertyChangedEventArgs> ObserveCollectionInheritedHeaderPropertyChanges()
    {
        var itemStreams = CollectionInheritedHeaders
            .Select(header => header.PropertyChangedObservable)
            .ToArray();

        return itemStreams.Length == 0
            ? Observable.Empty<PropertyChangedEventArgs>()
            : Observable.Merge(itemStreams);
    }

    private void SyncActiveCollectionRequestInheritedHeaders()
    {
        if (ShouldSkipInheritedHeaderSync())
        {
            return;
        }

        if (!TryGetActiveCollectionRequestContext(out var selectedCollection, out var matchingRequest))
        {
            return;
        }

        var inheritedHeaders = BuildCollectionInheritedHeaders(CollectionInheritedHeaders);
        var manualRequestHeaders = _requestEditor.RequestHeaders
            .Where(header => !header.IsInherited)
            .ToList();

        var manualHeaders = BuildCollectionInheritedHeaders(manualRequestHeaders);
        var mergedHeaders = MergeCollectionAndRequestHeaders(inheritedHeaders, manualHeaders ?? matchingRequest.Headers);

        ApplyInheritedHeaderPreview(manualRequestHeaders, inheritedHeaders, mergedHeaders);
    }

    private bool ShouldSkipInheritedHeaderSync() =>
        _suppressCollectionInheritedHeadersLivePreviewSync || _suppressCollectionInheritedHeadersAutoSave;

    private bool TryGetActiveCollectionRequestContext(
        out Collection selectedCollection,
        out CollectionRequest matchingRequest)
    {
        selectedCollection = null!;
        matchingRequest = null!;

        if (SelectedCollection is not { } activeCollection || ActiveRequestTab is not { } activeTab)
        {
            return false;
        }

        if (!activeTab.TryGetCollectionRequestSource(out var collectionId, out var method, out var path, out var name)
            || activeCollection.Id != collectionId)
        {
            return false;
        }

        var request = activeCollection.Requests.FirstOrDefault(collectionRequest =>
            string.Equals(collectionRequest.Method, method, StringComparison.Ordinal)
            && string.Equals(collectionRequest.Path, path, StringComparison.Ordinal)
            && string.Equals(collectionRequest.Name, name, StringComparison.Ordinal));
        if (request is null)
        {
            return false;
        }

        selectedCollection = activeCollection;
        matchingRequest = request;
        return true;
    }

    private void ApplyInheritedHeaderPreview(
        IReadOnlyList<RequestHeaderViewModel> manualRequestHeaders,
        IReadOnlyList<RequestHeader>? inheritedHeaders,
        IReadOnlyList<RequestHeader>? mergedHeaders)
    {
        _suppressCollectionInheritedHeadersLivePreviewSync = true;
        try
        {
            _requestEditor.RequestHeaders.Clear();
            foreach (var nonInheritedHeader in manualRequestHeaders)
            {
                _requestEditor.RequestHeaders.Add(new RequestHeaderViewModel
                {
                    Name = nonInheritedHeader.Name,
                    Value = nonInheritedHeader.Value,
                    Description = nonInheritedHeader.Description,
                    IsEnabled = nonInheritedHeader.IsEnabled,
                    IsInherited = false
                });
            }

            if (mergedHeaders is { } mergedInheritedHeaders)
            {
                AppendInheritedHeadersWithoutManualOverrides(mergedInheritedHeaders, inheritedHeaders, manualRequestHeaders);
            }

            _requestEditor.EnsurePlaceholderRows();
        }
        finally
        {
            _suppressCollectionInheritedHeadersLivePreviewSync = false;
        }
    }

    private void AppendInheritedHeadersWithoutManualOverrides(
        IReadOnlyList<RequestHeader> mergedHeaders,
        IReadOnlyList<RequestHeader>? inheritedHeaders,
        IReadOnlyList<RequestHeaderViewModel> manualRequestHeaders)
    {
        foreach (var mergedHeader in mergedHeaders)
        {
            if (!IsInheritedHeader(mergedHeader, inheritedHeaders) || HasManualHeaderOverride(mergedHeader.Name, manualRequestHeaders))
            {
                continue;
            }

            _requestEditor.RequestHeaders.Add(new RequestHeaderViewModel
            {
                Name = mergedHeader.Name,
                Value = mergedHeader.Value,
                IsEnabled = mergedHeader.IsEnabled,
                IsInherited = true
            });
        }
    }

    private static bool IsInheritedHeader(RequestHeader header, IReadOnlyList<RequestHeader>? inheritedHeaders) =>
        inheritedHeaders?.Any(inheritedHeader =>
            string.Equals(inheritedHeader.Name, header.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(inheritedHeader.Value, header.Value, StringComparison.Ordinal)
            && inheritedHeader.IsEnabled == header.IsEnabled) == true;

    private static bool HasManualHeaderOverride(string headerName, IReadOnlyList<RequestHeaderViewModel> manualRequestHeaders) =>
        manualRequestHeaders.Any(header => string.Equals(header.Name, headerName, StringComparison.OrdinalIgnoreCase));

    private static bool CollectionHeadersEqual(IReadOnlyList<RequestHeader>? left, IReadOnlyList<RequestHeader>? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            var leftHeader = left[index];
            var rightHeader = right[index];
            if (!string.Equals(leftHeader.Name, rightHeader.Name, StringComparison.Ordinal)
                || !string.Equals(leftHeader.Value, rightHeader.Value, StringComparison.Ordinal)
                || leftHeader.IsEnabled != rightHeader.IsEnabled)
            {
                return false;
            }
        }

        return true;
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
                collection.Requests,
                collection.Headers);

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
        if (_fileWatcherCts is { })
        {
            await _fileWatcherCts.CancelAsync();
        }

        _fileWatcherCts?.Dispose();
        _requestBodyWatcher?.Dispose();
        _requestBodyWatcher = null;

        _fileWatcherCts = new CancellationTokenSource();

        var path = await WriteTempFileAsync("arbor-request", _requestEditor.RequestBody);

        var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnRequestBodyFileChanged;
        _requestBodyWatcher = watcher;

        ResponseActionsViewModel.OpenWithShell(path);
    }

    // CommunityToolkit.Mvvm strips the "Async" suffix when generating command properties,
    // so the XAML binding target is OpenResponseBodyInExternalEditorCommand (not OpenResponseBodyInExternalEditorAsyncCommand).
    [RelayCommand]
    private Task OpenResponseBodyInExternalEditorAsync(CancellationToken cancellationToken) =>
        _responseActions.OpenResponseBodyInExternalEditorAsync(cancellationToken);

    [RelayCommand]
    private Task SaveBinaryResponseAndOpenAsync(CancellationToken cancellationToken) =>
        _responseActions.SaveBinaryResponseAndOpenAsync(cancellationToken);

    /// <summary>
    /// Copies the given history item to the clipboard formatted as a single-line
    /// <c>curl</c> command. Matches the "Copy as cURL" action available in
    /// Hoppscotch, Insomnia, and the browser devtools network panel.
    /// No-op when the clipboard or request is unavailable.
    /// </summary>
    [RelayCommand]
    private Task CopyHistoryItemAsCurlAsync(RequestHistoryEntry? request) =>
        _responseActions.CopyHistoryItemAsCurlAsync(request);

    /// <summary>
    /// Copies the current (pretty-printed) response body text to the clipboard.
    /// No-op when the clipboard is unavailable or the response body is empty.
    /// </summary>
    [RelayCommand]
    private Task CopyResponseBodyAsync() => _responseActions.CopyResponseBodyAsync();

    /// <summary>
    /// Opens a save-file dialog and writes the currently selected response tab content to the chosen path.
    /// The suggested file extension is derived from the response <c>Content-Type</c> when applicable.
    /// No-op when the storage provider is unavailable or the selected tab has no saveable text content.
    /// </summary>
    [RelayCommand]
    private Task SaveResponseBodyAsFileAsync(CancellationToken cancellationToken) =>
        _responseActions.SaveResponseBodyAsFileAsync(cancellationToken);

    /// <summary>
    /// Copies the current request (as configured in the request editor) to the
    /// clipboard formatted as a single-line <c>curl</c> command.
    /// No-op when the clipboard is unavailable.
    /// </summary>
    [RelayCommand]
    private Task CopyCurrentRequestAsCurlAsync() => _responseActions.CopyCurrentRequestAsCurlAsync();

    public void Dispose()
    {
        _crossFeatureDisposables.Dispose();

        _environmentsViewModel.Dispose();
        _historyFilterRequestedSubject.OnCompleted();
        _historyFilterDisposables.Dispose();
        _collectionSearchFilterRequestedSubject.OnCompleted();
        _collectionSearchFilterDisposables.Dispose();
        _optionsAutoSaveRequestedSubject.OnCompleted();
        _optionsAutoSaveDisposables.Dispose();
        _collectionInheritedHeadersAutoSaveRequestedSubject.OnCompleted();
        _collectionInheritedHeadersAutoSaveDisposables.Dispose();
        _draftAutoSaveCts?.Cancel();
        _draftAutoSaveCts?.Dispose();
        _draftPersistenceService?.ClearDraft();
        _scheduledJobService.Dispose();
        foreach (var scheduledJobViewModel in ScheduledJobs)
        {
            scheduledJobViewModel.Dispose();
        }
        _fileWatcherCts?.Cancel();
        _fileWatcherCts?.Dispose();
        _requestBodyWatcher?.Dispose();
        _requestBodyWatcher = null;
        _streamingCts?.Cancel();
        _streamingCts?.Dispose();
        _webSocketViewModel.Dispose();
        _sseViewModel.Dispose();
        _protocolHttpClient.Dispose();

        foreach (var tab in RequestTabs)
        {
            tab.Dispose();
        }

        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); }
            catch (UnauthorizedAccessException) { /* best-effort cleanup */ }
            catch (SecurityException) { /* best-effort cleanup */ }
            catch (PathTooLongException) { /* best-effort cleanup */ }
            catch (NotSupportedException) { /* best-effort cleanup */ }
            catch (IOException) { /* best-effort cleanup */ }
        }
    }

    public void PersistCurrentLayout()
    {
        // Refresh the tree snapshot so that window-close persists the latest structure,
        // including any panels the user docked to new positions during the session.
        RefreshDockTreeCache();
        PersistLayoutOptions();
    }

    /// <summary>
    /// Records the main window's current size and position so they are included in the next
    /// <see cref="PersistCurrentLayout"/> call.  Call this from <c>MainWindow.OnClosing</c>
    /// before <see cref="PersistCurrentLayout"/>.
    /// </summary>
    public void SetWindowGeometry(double width, double height, int x, int y)
    {
        _windowWidthAtClose = width;
        _windowHeightAtClose = height;
        _windowXAtClose = x;
        _windowYAtClose = y;
        _windowPositionCaptured = true;
    }

    /// <summary>
    /// Re-applies the proportions from the startup layout snapshot to the dock model.
    /// Call this once from <c>window.Opened</c> so the <see cref="ProportionalStackPanel"/>
    /// re-measures with the saved proportions after all visual bindings are established.
    /// The snapshot is cleared after the first call (one-shot).
    /// When the snapshot contains a <see cref="DockLayoutSnapshot.DockTree"/>, all
    /// proportions in the serialized tree are reapplied by walking the tree recursively.
    /// </summary>
    public void ReapplyStartupLayout()
    {
        var snapshot = _startupLayoutSnapshot;
        _startupLayoutSnapshot = null;

        if (snapshot is null || Layout is null)
        {
            return;
        }

        if (snapshot.DockTree is { } treeNode)
        {
            // Re-apply all proportions recorded in the full tree snapshot so that
            // ProportionalStackPanel re-measures with the correct values after the
            // visual tree and all PSP bindings are established.
            ReapplyProportionsFromTree(Layout, treeNode);
            return;
        }

        // Legacy path: re-apply the four well-known dock proportions.
        var leftToolDock = FindDockById<ToolDock>(Layout, "left-tool-dock");
        var documentLayout = FindDockById<ProportionalDock>(Layout, "document-layout");
        var requestDock = FindDockById<DocumentDock>(Layout, "request-dock");
        var responseDock = FindDockById<DocumentDock>(Layout, "response-dock");
        if (leftToolDock is null || documentLayout is null || requestDock is null || responseDock is null)
        {
            return;
        }

        if (snapshot.LeftToolProportion > 0)
        {
            leftToolDock.Proportion = snapshot.LeftToolProportion;
        }

        if (snapshot.DocumentProportion > 0)
        {
            documentLayout.Proportion = snapshot.DocumentProportion;
        }

        if (snapshot.RequestDockProportion > 0)
        {
            requestDock.Proportion = snapshot.RequestDockProportion;
        }

        if (snapshot.ResponseDockProportion > 0)
        {
            responseDock.Proportion = snapshot.ResponseDockProportion;
        }
    }

    /// <summary>
    /// Walks the saved <see cref="DockTreeNode"/> tree and re-applies each node's
    /// proportion to the matching dockable in the current <see cref="Layout"/> tree
    /// (matched by ID). Nodes without an ID or with a zero proportion are skipped.
    /// </summary>
    private void ReapplyProportionsFromTree(IDockable currentRoot, DockTreeNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Id) && node.Proportion > 0)
        {
            var dockable = FindDockById<IDockable>(currentRoot, node.Id);
            if (dockable is { })
            {
                dockable.Proportion = node.Proportion;
            }
        }

        foreach (var child in node.Children)
        {
            ReapplyProportionsFromTree(currentRoot, child);
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

    /// <summary>
    /// Returns a snapshot of the current layout options (including floating windows).
    /// Exposed for testing the save/restore cycle without a real ApplicationOptionsStore.
    /// </summary>
    public LayoutOptions CaptureCurrentLayout()
    {
        RefreshDockTreeCache();
        return BuildLayoutOptions();
    }

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

        var demoSeedResult = await SeedDemoDataAsync(cancellationToken).ConfigureAwait(false);
        if (demoSeedResult.SeededCollectionId is { } newCollectionId)
        {
            await LoadCollectionsAsync(cancellationToken).ConfigureAwait(false);
            SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == newCollectionId);
            LeftPanelTab = "Collections";
        }

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

    private Task<DemoDataSeedResult> SeedDemoDataAsync(CancellationToken cancellationToken = default) =>
        _demoDataWorkflow.SeedDemoDataAsync(cancellationToken);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="url"/> targets the local demo
    /// server at the given <paramref name="port"/>.
    /// </summary>
    private static bool IsDemoServerUrl(string url, int port) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal))
        && uri.Port == port;

    private void ApplyHttpResponseProjection(HttpResponseDetails response)
    {
        var projection = _httpResponseProjectionWorkflow.BuildProjection(response);

        ResponseStatus = projection.ResponseStatus;
        ResponseStatusCode = projection.ResponseStatusCode;
        ResponseTimeDisplay = projection.ResponseTimeDisplay;
        ResponseSizeDisplay = projection.ResponseSizeDisplay;
        _lastResponseBodyBytes = projection.LastResponseBodyBytes;
        RawResponseBody = projection.RawResponseBody;

        ResponseHeaders.Clear();
        foreach (var responseHeader in projection.ResponseHeaders)
        {
            ResponseHeaders.Add(responseHeader);
        }

        HasResponseHeaders = projection.HasResponseHeaders;
        ResponseContentType = projection.ResponseContentType;
        ResponseBodyTabLabel = projection.ResponseBodyTabLabel;
        IsBinaryResponse = projection.IsBinaryResponse;
        ResponseBody = projection.ResponseBody;
        IsResponseWebViewAvailable = projection.IsResponseWebViewAvailable;
        ResponseWebViewUri = projection.ResponseWebViewUri;
        ResponseRawText = projection.ResponseRawText;
        HasTextResponse = projection.HasTextResponse;
    }

    private void OnRequestBodyFileChanged(object sender, FileSystemEventArgs e)
    {
        if (Interlocked.Exchange(ref _requestBodyReadPending, 1) == 1)
        {
            return;
        }

        var cancellationToken = CancellationToken.None;

        if (_fileWatcherCts is { } fileWatcherCts)
        {
            try
            {
                cancellationToken = fileWatcherCts.Token;
            }
            catch (ObjectDisposedException)
            {
                // The watcher is being torn down; fall back to an uncancelable token so the
                // pending flag can still be reset by the background task.
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                var content = await File.ReadAllTextAsync(e.FullPath, cancellationToken).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() => _requestEditor.RequestBody = content);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // Transient read errors while the editor is still writing, or task cancelled during shutdown
            }
            finally
            {
                Interlocked.Exchange(ref _requestBodyReadPending, 0);
            }
        });
    }

    private async Task<string> WriteTempFileAsync(string prefix, string content, CancellationToken cancellationToken = default)
    {
        var ext = !string.IsNullOrEmpty(_requestEditor.ContentType)
            ? ExtensionFromContentType(_requestEditor.ContentType)
            : DetectExtensionFromContent(content);
        var path = Path.Join(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}{ext}");
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        _tempFiles.Add(path);
        return path;
    }

    private async Task<IStorageFolder?> GetResponseSaveSuggestedStartLocationAsync()
    {
        if (StorageProvider is null || string.IsNullOrWhiteSpace(ResponseSaveDefaultFolder))
        {
            return null;
        }

        return await StorageProvider.TryGetFolderFromPathAsync(ResponseSaveDefaultFolder);
    }

    /// <summary>
    /// Delegates to <see cref="ResponseActionsViewModel.TryGetSaveableResponseContent"/> for
    /// backward-compatibility with existing tests and XAML bindings (Phase 2 delegation).
    /// </summary>
    internal bool TryGetSaveableResponseContent(out string content, out string extension) =>
        _responseActions.TryGetSaveableResponseContent(out content, out extension);

    private void SyncDefaultContentTypeSelection(string value)
    {
        if (DefaultContentTypeOptions.Contains(value))
        {
            SelectedDefaultContentTypeOption = value;
            CustomDefaultContentType = string.Empty;
            return;
        }

        SelectedDefaultContentTypeOption = DefaultContentTypeCustomOption;
        CustomDefaultContentType = value;
    }

    /// <summary>
    /// Delegates to <see cref="ResponseActionsViewModel.ExtensionFromContentType"/> for
    /// backward-compatibility with existing tests and internal callers (Phase 2 delegation).
    /// </summary>
    internal static string ExtensionFromContentType(string contentType) =>
        ResponseActionsViewModel.ExtensionFromContentType(contentType);

    /// <summary>
    /// Delegates to <see cref="ResponseActionsViewModel.DetectExtensionFromContent"/> for
    /// backward-compatibility with existing tests and internal callers (Phase 2 delegation).
    /// </summary>
    internal static string DetectExtensionFromContent(string content) =>
        ResponseActionsViewModel.DetectExtensionFromContent(content);

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

    private DockLayoutSnapshot? CaptureLayoutSnapshot() =>
        _layoutTreeWorkflow.CaptureLayoutSnapshot(
            Layout,
            _cachedDockTree,
            _windowWidthAtClose,
            _windowHeightAtClose,
            _windowXAtClose,
            _windowYAtClose,
            _windowPositionCaptured);

    /// <summary>
    /// Rebuilds <see cref="_cachedDockTree"/> from the current <see cref="Layout"/>.
    /// Must be called whenever the dock layout structure changes (after window open,
    /// after applying a saved layout, or just before persisting on window close)
    /// so that the cached tree is up-to-date.
    /// </summary>
    private void RefreshDockTreeCache() =>
        _cachedDockTree = _layoutTreeWorkflow.CaptureDockTree(Layout);

    private void ApplyLayoutSnapshot(DockLayoutSnapshot? snapshot)
    {
        var applyResult = _layoutTreeWorkflow.ApplyLayoutSnapshot(snapshot, Layout, _dockFactory);
        if (!ReferenceEquals(Layout, applyResult.Layout))
        {
            Layout = applyResult.Layout;
            OnPropertyChanged(nameof(Layout));
        }

        if (applyResult.Applied)
        {
            RefreshDockTreeCache();
        }
    }

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

    private async Task SendRequestAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        if (_requestEditor.SelectedRequestType is RequestType.Http or RequestType.GraphQL)
        {
            ClearResponseState();
        }

        switch (_requestEditor.SelectedRequestType)
        {
            case RequestType.GraphQL:
                await SendGraphQlRequestAsync(cancellationToken);
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
                await SendHttpRequestAsync(cancellationToken);
                break;
        }
    }

    private async Task SendHttpRequestAsync(CancellationToken cancellationToken)
    {
        var outcome = await _manualHttpRequestCoordinator.SendAsync(cancellationToken);
        if (!outcome.IsSuccessful)
        {
            ErrorMessage = outcome.ErrorMessage;
            if (outcome.ClearResponseMetadata)
            {
                ResponseStatusCode = 0;
                ResponseTimeDisplay = string.Empty;
                ResponseSizeDisplay = string.Empty;
            }

            return;
        }

        if (outcome.Response is not { } response)
        {
            ErrorMessage = string.Empty;
            return;
        }

        ApplyHttpResponseProjection(response);
        _cookieJarViewModel.RefreshCookies();
    }

    private async Task SendGraphQlRequestAsync(CancellationToken cancellationToken = default)
    {
        var outcome = await _manualGraphQlRequestCoordinator.SendAsync(cancellationToken);
        if (!outcome.IsSuccessful)
        {
            ErrorMessage = outcome.ErrorMessage;
            if (outcome.ClearResponseMetadata)
            {
                ResponseStatusCode = 0;
                ResponseTimeDisplay = string.Empty;
                ResponseSizeDisplay = string.Empty;
            }

            return;
        }

        if (outcome.Response is not { } response)
        {
            ErrorMessage = string.Empty;
            return;
        }

        ApplyHttpResponseProjection(response);
        _cookieJarViewModel.RefreshCookies();
    }

    private async Task ToggleWebSocketConnectionAsync() =>
        _streamingCts = await _streamingConnectionWorkflow.ToggleWebSocketConnectionAsync(_streamingCts);

    private async Task ToggleSseConnectionAsync() =>
        _streamingCts = await _streamingConnectionWorkflow.ToggleSseConnectionAsync(_streamingCts);

    public static string FormatElapsedMilliseconds(double milliseconds) =>
        HttpResponseProjectionWorkflow.FormatElapsedMilliseconds(milliseconds);

    public static string FormatByteSize(long byteCount) =>
        HttpResponseProjectionWorkflow.FormatByteSize(byteCount);

    private async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        var requests = (await _requestHistoryRepository.GetRecentAsync(100, cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();

        _allHistory.Clear();
        _allHistory.AddRange(requests);

        ApplyHistoryFilter(HistorySearchQuery);
    }

    [RelayCommand]
    private void LoadHistoryRequest(RequestHistoryEntry? request)
    {
        if (request is null)
        {
            return;
        }

        _requestEditor.SelectedRequestType = RequestType.Http;
        _requestEditor.SelectedMethod = request.Method;
        _requestEditor.RequestName = request.Name;
        _requestEditor.RequestUrl = request.Url;
        _requestEditor.RequestBody = request.Body ?? string.Empty;
        _requestEditor.RequestHeaders.Clear();
        _requestEditor.EnsurePlaceholderRows();
        LeftPanelTab = "History";
    }

    private void ClearResponseState()
    {
        ResponseStatus = string.Empty;
        ResponseStatusCode = 0;
        ResponseTimeDisplay = string.Empty;
        ResponseSizeDisplay = string.Empty;
        ResponseBody = string.Empty;
        RawResponseBody = string.Empty;
        ResponseRawText = string.Empty;
        ResponseContentType = string.Empty;
        ResponseBodyTabLabel = "Body";
        IsBinaryResponse = false;
        IsResponseWebViewAvailable = false;
        ResponseWebViewUri = "about:blank";
        HasResponseHeaders = false;
        HasTextResponse = false;
        _lastResponseBodyBytes = [];
        ResponseHeaders.Clear();
    }

    private async Task LoadCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var selectedCollectionId = SelectedCollection?.Id;
        var all = await _collectionRepository.GetAllAsync(cancellationToken);

        Collections.Clear();
        foreach (var c in all)
        {
            Collections.Add(c);
        }

        if (selectedCollectionId.HasValue)
        {
            SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == selectedCollectionId.Value);
        }
    }

    private void QueueOptionsAutoSave()
    {
        if (_suppressOptionsAutoSave || _applicationOptionsStore is null)
        {
            return;
        }

        _optionsAutoSaveRequestedSubject.OnNext(Unit.Default);
    }


    private async Task LoadScheduledJobsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _scheduledJobRepository.GetAllAsync(cancellationToken);

        foreach (var scheduledJobViewModel in ScheduledJobs)
        {
            scheduledJobViewModel.Dispose();
        }

        ScheduledJobs.Clear();
        foreach (var config in all)
        {
            var vm = ScheduledJobViewModel.FromConfig(config, _scheduledJobRepository, _scheduledJobService, FollowHttpRedirects);
            ScheduledJobs.Add(vm);

            if (AutoStartScheduledJobsOnLaunch && config.AutoStart && !_scheduledJobService.IsRunning(config.Id))
            {
                _scheduledJobService.Start(config, vm.IsWebViewEnabled ? vm.HandleResponseAsync : null);
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

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cancellationRegistration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        using var subscription = Observable.Interval(TimeSpan.FromSeconds(30))
            .Subscribe(_tick =>
            {
                _ = SaveDraftTickAsync(cancellationToken);
            });

        try
        {
            await completion.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown — auto-save subscription cancelled.
        }
    }

    private async Task SaveDraftTickAsync(CancellationToken cancellationToken)
    {
        if (_draftPersistenceService is null)
        {
            return;
        }

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
