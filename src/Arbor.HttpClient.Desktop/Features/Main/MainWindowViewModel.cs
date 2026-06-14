using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
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
using Arbor.HttpClient.Desktop.Features.History;
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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
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

[SuppressMessage("Major Code Smell", "S3881", Justification = "Lifetime is managed as a UI root ViewModel with deterministic cleanup via Dispose.")]
public partial class MainWindowViewModel : ReactiveViewModelBase, IResponseActionsContext
{
    private readonly HttpRequestService _httpRequestService;
    private HttpRequestWorkflow _httpRequestWorkflow = null!;
    private ManualHttpRequestCoordinator _manualHttpRequestCoordinator = null!;
    private readonly HttpResponseProjectionWorkflow _httpResponseProjectionWorkflow = new();
    private readonly IRequestHistoryRepository _requestHistoryRepository;
    private readonly ICollectionRepository _collectionRepository;
    private CollectionsWorkflow _collectionsWorkflow = null!;
    private CollectionsManagementCoordinator _collectionsManagementCoordinator = null!;
    private readonly ScheduledJobService _scheduledJobService;
    private readonly ScheduledJobsWorkflow _scheduledJobsWorkflow;
    private readonly LogWindowViewModel _logWindowViewModel;
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
    private readonly RequestHistoryWorkflow _requestHistoryWorkflow;
    private readonly RequestTabsWorkflow _requestTabsWorkflow = new();
    private readonly RequestBodyExternalEditWorkflow _requestBodyExternalEditWorkflow = new();
    private readonly Subject<string> _historyFilterRequestedSubject = new();
    private readonly CompositeDisposable _historyFilterDisposables = new();
    private RequestTabViewModel? _lastAppliedRequestTab;
    private readonly Subject<string> _collectionSearchFilterRequestedSubject = new();
    private readonly CompositeDisposable _collectionSearchFilterDisposables = new();
    private DockFactory? _dockFactory;
    private readonly LayoutWorkflow _layoutWorkflow = new();
    private readonly LayoutTreeWorkflow _layoutTreeWorkflow = new();
    private ApplicationOptions _applicationOptions = new();
    private bool _suppressLayoutRestore;
    private CancellationTokenSource? _sendRequestCts;
    private readonly ApplicationOptionsWorkflow _optionsWorkflow;
    private readonly CompositeDisposable _optionsAutoSaveDisposables = new();
    private bool _suppressCollectionInheritedHeadersLivePreviewSync;
    private readonly CollectionInheritedHeadersWorkflow _collectionInheritedHeadersWorkflow;
    private readonly CollectionFilterWorkflow _collectionFilterWorkflow = new();
    private readonly CompositeDisposable _collectionInheritedHeadersAutoSaveDisposables = new();
    private readonly CompositeDisposable _crossFeatureDisposables = new();
    private DockLayoutSnapshot? _defaultLayout;
    private byte[] _lastResponseBodyBytes = [];
    private readonly DraftWorkflow _draftWorkflow;
    private readonly DemoServer? _demoServer;
    private readonly UnhandledExceptionCollector? _unhandledExceptionCollector;
    private readonly IScriptRunner _scriptRunner = new RoslynScriptRunner();
    private readonly ScriptViewModel _scriptViewModel = new();
    private readonly ResponseActionsViewModel _responseActions = null!;
    private readonly CollectionRequestEditorProjectionWorkflow _collectionRequestEditorProjectionWorkflow;
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

    [Reactive]
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

    // Needed for file picker – set by the view
    public IStorageProvider? StorageProvider { get; set; }

    // Needed for clipboard (e.g. "Copy as cURL" on history items) – set by the view
    public IClipboard? Clipboard { get; set; }

    public bool HasPendingCollectionInheritedHeadersAutoSave => _collectionInheritedHeadersWorkflow.HasPendingAutoSave;

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

    void IResponseActionsContext.SetResponseSaveFileNamePatternValidationError(string validationMessage) =>
        ResponseSaveFileNamePatternValidationError = validationMessage;

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The collector used for unhandled exceptions; may be null when not configured.</summary>
    public UnhandledExceptionCollector? UnhandledExceptionCollector => _unhandledExceptionCollector;

    /// <summary>Dock layout root; bound to DockControl.Layout in MainWindow.</summary>
    public IRootDock? Layout { get; private set; }

    /// <summary>Dock factory; bound to DockControl.Factory in MainWindow.</summary>
    public IFactory? Factory => _dockFactory;

    [Reactive]
    private string _responseStatus = string.Empty;

    /// <summary>
    /// Numeric HTTP status code for the last completed response (0 when no response yet
    /// or the request failed before receiving one). Used by the UI to color-code the
    /// response status by family (1xx/2xx/3xx/4xx/5xx).
    /// </summary>
    [Reactive]
    private int _responseStatusCode;

    /// <summary>
    /// Human-readable elapsed time for the last response, e.g. "142 ms" or "1.23 s".
    /// Empty when no response has been received.
    /// </summary>
    [Reactive]
    private string _responseTimeDisplay = string.Empty;

    /// <summary>
    /// Human-readable size of the last response body, e.g. "512 B", "1.3 KB", "4.7 MB".
    /// Empty when no response has been received.
    /// </summary>
    [Reactive]
    private string _responseSizeDisplay = string.Empty;

    [Reactive]
    private string _responseBody = string.Empty;

    [Reactive]
    private string _rawResponseBody = string.Empty;

    [Reactive]
    private string _responseBodyTabLabel = "Body";

    [Reactive]
    private string _responseContentType = string.Empty;

    [Reactive]
    private string _responseRawText = string.Empty;

    [Reactive]
    private int _selectedResponseTabIndex;

    [Reactive]
    private bool _isResponseWebViewAvailable;

    [Reactive]
    private string _responseWebViewUri = "about:blank";

    [Reactive]
    private bool _isBinaryResponse;

    [Reactive]
    private string _errorMessage = string.Empty;

    [Reactive]
    private string _historySearchQuery = string.Empty;

    [Reactive]
    private string _leftPanelTab = "History"; // "History" | "Collections"

    [Reactive]
    private Collection? _selectedCollection;

    [Reactive]
    private string _collectionSearchQuery = string.Empty;

    /// <summary>Sort order for collection requests: "Default" | "Name" | "Method" | "Path".</summary>
    [Reactive]
    private string _collectionSortBy = "Default";

    /// <summary>
    /// Display mode for collection request entries:
    /// "NameAndPath" | "NameOnly" | "PathOnly" | "FullUrl".
    /// </summary>
    [Reactive]
    private string _collectionDisplayMode = "NameAndPath";

    /// <summary>When true, requests are shown grouped by their top-level path segment.</summary>
    [Reactive]
    private bool _isCollectionTreeView;

    [Reactive]
    private string _newCollectionName = string.Empty;

    [Reactive]
    private bool _isNewCollectionFormVisible;

    [Reactive]
    private string _renameCollectionName = string.Empty;

    [Reactive]
    private bool _isRenameCollectionFormVisible;

    [Reactive]
    private string? _selectedLayoutName;

    public const string SystemThemeOption = "System";
    public const string DarkThemeOption = "Dark";
    public const string LightThemeOption = "Light";

    public IReadOnlyList<string> ThemeOptions { get; } =
    [
        SystemThemeOption,
        DarkThemeOption,
        LightThemeOption
    ];

    [Reactive]
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

    [Reactive]
    private string _selectedTlsVersionOption = "SystemDefault";

    [Reactive]
    private bool _followHttpRedirects = true;

    [Reactive]
    private bool _showRequestPreviewByDefault = true;

    [Reactive]
    private bool _enableHttpDiagnostics;

    [Reactive]
    private string _defaultRequestUrl = "http://localhost:5000/echo";

    [Reactive]
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

    [Reactive]
    private string _selectedDefaultContentTypeOption = "application/json";

    [Reactive]
    private string _customDefaultContentType = string.Empty;

    [Reactive]
    private string _responseSaveDefaultFolder = string.Empty;

    [Reactive]
    private string _responseSaveFileNamePattern = ResponseSaveFileNamePatternFormatter.DefaultPattern;

    [Reactive]
    private string _responseSaveFileNamePatternValidationError = string.Empty;

    [Reactive]
    private int _defaultRequestTimeoutSeconds = 100;

    public IReadOnlyList<string> FontFamilyOptions { get; } =
    [
        "Cascadia Code,Consolas,Menlo,monospace",
        "Consolas,Menlo,monospace",
        "JetBrains Mono,Cascadia Code,Consolas,monospace"
    ];

    [Reactive]
    private string _uiFontFamily = "Cascadia Code,Consolas,Menlo,monospace";

    [Reactive]
    private string _uiFontSizeText = "13";

    [Reactive]
    private bool _autoStartScheduledJobsOnLaunch = true;

    [Reactive]
    private int _defaultScheduledJobIntervalSeconds = 60;

    [Reactive]
    private bool _collectUnhandledExceptions;

    public double UiFontSize => ApplicationOptionsWorkflow.ParseFontSize(UiFontSizeText, fallback: 13d);

    public string RequestTimeoutDefaultWatermark =>
        $"{Strings.RequestTimeoutDefaultWatermark} ({DefaultRequestTimeoutSeconds})";

    // Response headers panel (populated after each successful request)
    public ObservableCollection<string> ResponseHeaders { get; } = [];

    [Reactive]
    private bool _hasResponseHeaders;

    /// <summary>
    /// True when a non-binary text response has been received and the response shortcuts
    /// toolbar (Copy body / Save as file / Copy as cURL) should be visible.
    /// </summary>
    [Reactive]
    private bool _hasTextResponse;

    [Reactive]
    private bool _hasDraftToRestore;

    [Reactive]
    private string _draftRestoreMessage = string.Empty;

    /// <summary>Gets or sets the HTTP port the demo server listens on.</summary>
    [Reactive]
    private int _demoServerPort = DemoServer.DefaultPort;

    /// <summary>Gets or sets the HTTPS port the demo server listens on.</summary>
    [Reactive]
    private int _demoServerHttpsPort = DemoServer.DefaultHttpsPort;

    /// <summary>Gets or sets whether the demo server HTTP endpoint is enabled.</summary>
    [Reactive]
    private bool _isDemoServerHttpEnabled = true;

    /// <summary>Gets or sets whether the demo server HTTPS endpoint is enabled.</summary>
    [Reactive]
    private bool _isDemoServerHttpsEnabled;

    /// <summary>True while the embedded demo server is running.</summary>
    [Reactive]
    private bool _isDemoServerRunning;

    /// <summary>
    /// True when a collection request targeting the demo server was loaded but the
    /// server is not yet running.  Shows an inline banner offering to start it.
    /// </summary>
    [Reactive]
    private bool _isDemoServerBannerVisible;

    [ReactiveCommand]
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

        using (_collectionInheritedHeadersWorkflow.SuppressAutoSave())
        {
            CollectionItems.Clear();
            CollectionInheritedHeaders.Clear();
            if (SelectedCollection is { } collection)
            {
                foreach (var item in CollectionFilterWorkflow.BuildCollectionItems(collection))
                {
                    CollectionItems.Add(item);
                }

                foreach (var header in CollectionInheritedHeadersWorkflow.BuildHeaderViewModels(collection.Headers))
                {
                    CollectionInheritedHeaders.Add(header);
                }
            }
        }

        ApplyCollectionFilter();
    }

    private void OnUiFontSizeTextChangedCore()
    {
        this.RaisePropertyChanged(nameof(UiFontSize));
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
            currentApp.Resources["AppFontFamily"] = ApplicationOptionsWorkflow.ResolveFontFamily(UiFontFamily);
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

        Application.Current.RequestedThemeVariant = ApplicationOptionsWorkflow.ResolveThemeVariant(SelectedThemeOption);

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

    [SuppressMessage("Major Code Smell", "S107", Justification = "Root composition constructor wires all feature services and optional host integrations.")]
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
        _requestHistoryWorkflow = new RequestHistoryWorkflow(_requestHistoryRepository);
        _collectionRepository = collectionRepository;
        _scheduledJobService = scheduledJobService;
        _scheduledJobsWorkflow = new ScheduledJobsWorkflow(scheduledJobRepository, scheduledJobService);
        _logWindowViewModel = logWindowViewModel;
        _variableResolver = new VariableResolver();
        _collectionRequestEditorProjectionWorkflow = new CollectionRequestEditorProjectionWorkflow(_variableResolver);
        _applicationOptionsStore = applicationOptionsStore;
        _onApplicationOptionsChanged = onApplicationOptionsChanged;
        _demoServer = demoServer;
        _unhandledExceptionCollector = unhandledExceptionCollector;
        var appLogger = (logger ?? Log.Logger).ForContext<MainWindowViewModel>();
        _debugLogger = appLogger.ForContext("LogTab", LogTab.Debug);
        _httpRequestsLogger = appLogger.ForContext("LogTab", LogTab.HttpRequests);
        _draftWorkflow = new DraftWorkflow(draftPersistenceService, _debugLogger);

        _collectionInheritedHeadersWorkflow = new CollectionInheritedHeadersWorkflow(
            _collectionRepository,
            collectionId => Collections?.FirstOrDefault(collection => collection.Id == collectionId),
            LoadCollectionsAsync,
            collectionId =>
            {
                if (SelectedCollection?.Id == collectionId)
                {
                    SelectedCollection = Collections?.FirstOrDefault(collection => collection.Id == collectionId);
                }
            },
            _debugLogger,
            invokeOnUiThreadAsync: persistAsync => Dispatcher.UIThread.InvokeAsync(persistAsync, DispatcherPriority.Background));

        _collectionInheritedHeadersAutoSaveDisposables.Add(_collectionInheritedHeadersWorkflow.AutoSaveRequested
            .Subscribe(snapshot => Dispatcher.UIThread.Post(() => _ = _collectionInheritedHeadersWorkflow.TriggerAutoSave(snapshot))));

        _optionsWorkflow = new ApplicationOptionsWorkflow(_applicationOptionsStore, _debugLogger);
        _optionsAutoSaveDisposables.Add(_optionsWorkflow.AutoSaveRequested
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
            .Subscribe(query => _requestHistoryWorkflow.ApplyFilter(query)));

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
        // NOTE: CreateRequestEditor() cannot be used here because it reads _applicationOptions
        // which is populated by ApplyOptions (called later in the constructor). The initial
        // tab is created with bare defaults; ApplyOptions then sets URL/content-type/etc.
        var firstTab = _requestTabsWorkflow.AddTab(_requestEditor);
        _activeRequestTab = firstTab;
        _lastAppliedRequestTab = firstTab;
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
            new OpenApiImportService(),
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

        Collections = [];
        CollectionItems = [];
        CollectionInheritedHeaders = [];
        FilteredCollectionItems = [];
        CollectionGroups = [];

        // Cancellation: ExecutePrimaryAction cancels _sendRequestCts, which is linked to the
        // execution's CancellationToken — equivalent to the old AsyncRelayCommand.Cancel().
        // IsExecuting stays true until SendRequestAsync has fully completed (including its
        // cancellation handling), matching the previous ExecutionTask semantics.
        SendRequestCommand = ReactiveCommand.CreateFromTask(async cancellationToken =>
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _sendRequestCts = linkedCts;
            try
            {
                await SendRequestAsync(linkedCts.Token);
            }
            finally
            {
                _sendRequestCts = null;
            }
        });
        LoadHistoryCommand = ReactiveCommand.CreateFromTask(LoadHistoryAsync);

        SendRequestCommand.ThrownExceptions
            .Subscribe(exception => _debugLogger.Error(exception, "Send request failed unexpectedly"))
            .DisposeWith(_crossFeatureDisposables);
        LoadHistoryCommand.ThrownExceptions
            .Subscribe(exception => _debugLogger.Error(exception, "Loading history failed unexpectedly"))
            .DisposeWith(_crossFeatureDisposables);

        _isRequestInProgress = SendRequestCommand.IsExecuting
            .ToProperty(this, viewModel => viewModel.IsRequestInProgress);

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => _environmentsViewModel.PropertyChanged += handler,
                handler => _environmentsViewModel.PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(EnvironmentsViewModel.ActiveEnvironment), StringComparison.Ordinal))
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(ActiveEnvironment));
                this.RaisePropertyChanged(nameof(ActiveEnvironmentAccentColor));
                this.RaisePropertyChanged(nameof(ActiveEnvironmentHasColor));
                this.RaisePropertyChanged(nameof(ActiveEnvironmentShowWarningBanner));
            }));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => _environmentsViewModel.PropertyChanged += handler,
                handler => _environmentsViewModel.PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(EnvironmentsViewModel.NewEnvironmentName), StringComparison.Ordinal))
            .Subscribe(_ => this.RaisePropertyChanged(nameof(NewEnvironmentName))));

        _crossFeatureDisposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => _environmentsViewModel.PropertyChanged += handler,
                handler => _environmentsViewModel.PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs.PropertyName)
            .Where(propertyName => string.Equals(propertyName, nameof(EnvironmentsViewModel.IsEnvironmentPanelVisible), StringComparison.Ordinal))
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsEnvironmentPanelVisible))));

        _crossFeatureDisposables.Add(Observable.Merge(
                _webSocketViewModel.PropertyChangedObservable,
                _sseViewModel.PropertyChangedObservable)
            .Where(eventArgs => eventArgs.PropertyName is nameof(WebSocketViewModel.IsConnected)
                or nameof(SseViewModel.IsConnected))
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PrimaryActionLabel))));

        _crossFeatureDisposables.Add(SendRequestCommand.IsExecuting
            .Skip(1)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PrimaryActionLabel))));

        _crossFeatureDisposables.Add(this
            .WhenAnyValue(viewModel => viewModel.SelectedTlsVersionOption)
            .Skip(1)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsInsecureTlsVersionSelected))));

        _crossFeatureDisposables.Add(this
            .WhenAnyValue(viewModel => viewModel.SelectedDefaultContentTypeOption)
            .Skip(1)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsCustomDefaultContentType))));

        _crossFeatureDisposables.Add(this
            .WhenAnyValue(viewModel => viewModel.DefaultRequestTimeoutSeconds)
            .Skip(1)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(RequestTimeoutDefaultWatermark))));

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
            .Subscribe(_ => this.RaisePropertyChanged(nameof(PrimaryActionLabel))));

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

    public ObservableCollection<RequestHistoryEntry> History => _requestHistoryWorkflow.History;
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
    public ObservableCollection<ScheduledJobViewModel> ScheduledJobs => _scheduledJobsWorkflow.Jobs;
    public ObservableCollection<string> SavedLayoutNames => _layoutWorkflow.SavedLayoutNames;
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
    public ObservableCollection<RequestTabViewModel> RequestTabs => _requestTabsWorkflow.Tabs;

    private void ApplyActiveRequestTab()
    {
        if (ActiveRequestTab is not { } newValue)
        {
            return;
        }

        SaveResponseStateForTab(_lastAppliedRequestTab);

        _requestEditor = newValue.RequestEditor;
        RestoreResponseStateForTab(newValue);
        _lastAppliedRequestTab = newValue;
        // Capture the editor in a local so the dispatcher callback always targets
        // the same instance even if ActiveRequestTab changes again before it runs.
        var editorForThisTab = _requestEditor;
        // Suppress refreshes while Avalonia's TwoWay bindings re-subscribe to the new
        // editor and fire current ComboBox/TextBox values back as if they changed.
        // Avalonia processes PropertyChanged notifications synchronously within the
        // current dispatcher frame, so a Post at default priority is guaranteed to run
        // after all binding subscriptions have settled for the current frame.
        editorForThisTab.BeginBulkUpdate();
        this.RaisePropertyChanged(nameof(RequestEditor));
        this.RaisePropertyChanged(nameof(PrimaryActionLabel));
        Dispatcher.UIThread.Post(() => editorForThisTab.EndBulkUpdate());
    }

    [ReactiveCommand]
    private void NewRequestTab()
    {
        ActiveRequestTab = _requestTabsWorkflow.AddTab(CreateRequestEditor());
    }

    [ReactiveCommand]
    private void CloseRequestTab(RequestTabViewModel? tab)
    {
        if (_requestTabsWorkflow.CloseTab(tab, ActiveRequestTab) is { } nextActiveTab)
        {
            ActiveRequestTab = nextActiveTab;
        }
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
            if (IsRequestInProgress)
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

    public ReactiveCommand<Unit, Unit> SendRequestCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadHistoryCommand { get; }

    private readonly ObservableAsPropertyHelper<bool> _isRequestInProgress;
    public bool IsRequestInProgress => _isRequestInProgress.Value;

    [ReactiveCommand]
    private void ExecutePrimaryAction()
    {
        if (IsRequestInProgress)
        {
            _sendRequestCts?.Cancel();
            return;
        }

        if (((System.Windows.Input.ICommand)SendRequestCommand).CanExecute(null))
        {
            SendRequestCommand.Execute().Subscribe();
        }
    }

    public System.Windows.Input.ICommand AddEnvironmentVariableCommand => _environmentsViewModel.AddEnvironmentVariableCommand;
    public System.Windows.Input.ICommand RemoveEnvironmentVariableCommand => _environmentsViewModel.RemoveEnvironmentVariableCommand;
    public System.Windows.Input.ICommand SaveEnvironmentCommand => _environmentsViewModel.SaveEnvironmentCommand;
    public System.Windows.Input.ICommand DeleteEnvironmentCommand => _environmentsViewModel.DeleteEnvironmentCommand;
    public System.Windows.Input.ICommand EditEnvironmentCommand => _environmentsViewModel.EditEnvironmentCommand;
    public System.Windows.Input.ICommand NewEnvironmentCommand => _environmentsViewModel.NewEnvironmentCommand;
    public System.Windows.Input.ICommand ExportEnvironmentsCommand => _environmentsViewModel.ExportEnvironmentsCommand;

    private ApplicationOptions BuildOptionsFromCurrentState() =>
        ApplicationOptionsWorkflow.BuildOptions(BuildOptionsSnapshot(), BuildLayoutOptions());

    private ApplicationOptionsSnapshot BuildOptionsSnapshot() => new()
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
        DefaultRequestTimeoutSeconds = DefaultRequestTimeoutSeconds,
        Theme = SelectedThemeOption,
        FontFamily = UiFontFamily,
        FontSizeText = UiFontSizeText,
        AutoStartScheduledJobsOnLaunch = AutoStartScheduledJobsOnLaunch,
        DefaultScheduledJobIntervalSeconds = DefaultScheduledJobIntervalSeconds,
        CollectUnhandledExceptions = CollectUnhandledExceptions
    };

    private void ApplyOptions(ApplicationOptions options, bool updateCurrentRequestUrl = true)
    {
        var previousDefaultFollowRedirects = _applicationOptions.Http.FollowRedirects;
        var previousDefaultUrl = _applicationOptions.Http.DefaultRequestUrl;
        _applicationOptions = options;

        using var autoSaveSuppression = _optionsWorkflow.SuppressAutoSave();

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

        if (ApplicationOptionsWorkflow.ShouldUpdateRequestUrl(updateCurrentRequestUrl, _requestEditor.RequestUrl, previousDefaultUrl))
        {
            _requestEditor.RequestUrl = options.Http.DefaultRequestUrl;
        }
    }

    private void ApplyLayoutOptions(LayoutOptions? layouts)
    {
        SelectedLayoutName = _layoutWorkflow.LoadFromOptions(layouts);
        ApplyLayoutSnapshot(layouts?.CurrentLayout);
    }

    [ReactiveCommand]
    private void AddScheduledJob()
    {
        _scheduledJobsWorkflow.AddJob(DefaultScheduledJobIntervalSeconds, FollowHttpRedirects);
        LeftPanelTab = "ScheduledJobs";
    }

    [ReactiveCommand]
    private Task RemoveScheduledJobAsync(ScheduledJobViewModel? job) =>
        _scheduledJobsWorkflow.RemoveJobAsync(job);

    [ReactiveCommand]
    private void ShowHistoryTab()
    {
        LeftPanelTab = "History";
        ActivateLeftPanel();
    }

    [ReactiveCommand]
    private void ShowCollectionsTab()
    {
        LeftPanelTab = "Collections";
        ActivateLeftPanel();
    }

    [ReactiveCommand]
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

    [ReactiveCommand]
    private void OpenAbout() => OpenAboutWindowAction?.Invoke();

    /// <summary>Set by the view layer to open the Diagnostics window.</summary>
    public Action? OpenDiagnosticsWindowAction { get; set; }

    [ReactiveCommand]
    private void OpenDiagnostics() => OpenDiagnosticsWindowAction?.Invoke();

    [ReactiveCommand]
    private void OpenLogWindow()
    {
        if (_dockFactory?.LogPanelViewModel is { Owner: IDock ownerDock } logPanelVm)
        {
            ownerDock.ActiveDockable = logPanelVm;
        }
    }

    [ReactiveCommand]
    private void OpenCookieJar()
    {
        if (_dockFactory?.CookieJarViewModel is { Owner: IDock ownerDock } cookieJarVm)
        {
            cookieJarVm.RefreshCookies();
            ownerDock.ActiveDockable = cookieJarVm;
        }
    }

    [ReactiveCommand]
    private void ExitApplication() => ExitApplicationAction?.Invoke();

    [ReactiveCommand]
    private void OpenOptions()
    {
        if (_dockFactory?.OptionsViewModel is { Owner: IDock ownerDock } optVm)
        {
            ownerDock.ActiveDockable = optVm;
        }
    }

    [ReactiveCommand]
    private void OpenEnvironments()
    {
        if (_dockFactory?.EnvironmentsViewModel is { Owner: IDock ownerDock } environmentsVm)
        {
            ownerDock.ActiveDockable = environmentsVm;
        }
    }

    [ReactiveCommand]
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

        if (_layoutWorkflow.TryGetLayout(SelectedLayoutName, out var snapshot))
        {
            ApplyLayoutSnapshot(snapshot);
            PersistLayoutOptions();
        }
    }

    [ReactiveCommand]
    private void SaveLayoutAsNew()
    {
        RefreshDockTreeCache();
        var savedLayoutName = _layoutWorkflow.SaveLayoutAsNew(CaptureLayoutSnapshot);
        if (string.IsNullOrWhiteSpace(savedLayoutName))
        {
            return;
        }

        _suppressLayoutRestore = true;
        SelectedLayoutName = savedLayoutName;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [ReactiveCommand]
    private void SaveLayoutToExisting(string? layoutName)
    {
        RefreshDockTreeCache();
        if (!_layoutWorkflow.SaveLayoutToExisting(layoutName, CaptureLayoutSnapshot))
        {
            return;
        }

        _suppressLayoutRestore = true;
        SelectedLayoutName = layoutName;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [ReactiveCommand]
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

    [ReactiveCommand]
    private void RemoveLayout(string? layoutName)
    {
        var removeLayoutResult = _layoutWorkflow.RemoveLayout(layoutName, SelectedLayoutName);
        if (!removeLayoutResult.Removed)
        {
            return;
        }

        _suppressLayoutRestore = true;
        SelectedLayoutName = removeLayoutResult.SelectedLayoutName;
        _suppressLayoutRestore = false;
        PersistLayoutOptions();
    }

    [ReactiveCommand]
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
        if (RequestTabsWorkflow.FindMatchingTab(RequestTabs, collectionId, item.Method, item.Path, item.Name) is not { } existingTab)
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
            var projection = _collectionRequestEditorProjectionWorkflow.BuildProjection(
                item,
                SelectedCollection,
                ActiveEnvironment,
                _requestEditor.GetResolvedVariables(),
                _requestEditor.ContentTypeOptions,
                hasDemoServer: _demoServer is { },
                isDemoServerRunning: _demoServer?.IsRunning ?? false,
                demoServerPort: _demoServer?.Port ?? 0,
                demoServerHttpsPort: _demoServer?.HttpsPort ?? 0);

            _requestEditor.SelectedRequestType = projection.RequestType;
            if (projection.Method is { } method)
            {
                _requestEditor.SelectedMethod = method;
            }

            _requestEditor.RequestUrl = projection.ResolvedUrl;
            _requestEditor.RequestName = projection.Name;
            _requestEditor.RequestNotes = projection.Notes;

            _requestEditor.RequestHeaders.Clear();
            foreach (var header in projection.Headers)
            {
                _requestEditor.RequestHeaders.Add(new RequestHeaderViewModel
                {
                    Name = header.Name,
                    Value = header.Value,
                    IsEnabled = header.IsEnabled,
                    IsInherited = header.IsInherited
                });
            }

            _requestEditor.EnsurePlaceholderRows();

            _requestEditor.SelectedContentTypeOption = projection.SelectedContentTypeOption;
            _requestEditor.CustomContentType = projection.CustomContentType;
            _requestEditor.RequestBody = projection.Body;

            IsDemoServerBannerVisible = projection.ShowDemoServerBanner;

            var collectionId = SelectedCollection?.Id ?? 0;
            ActiveRequestTab?.SetCollectionRequestSource(collectionId, item.Method, item.Path, item.Name);
        }
    }

    /// <summary>Starts the embedded demo server on <see cref="DemoServerPort"/> and/or <see cref="DemoServerHttpsPort"/>.</summary>
    [ReactiveCommand]
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
    [ReactiveCommand]
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
    [ReactiveCommand]
    private void DismissDemoServerBanner() => IsDemoServerBannerVisible = false;

    [ReactiveCommand]
    private void ShowNewCollectionForm()
    {
        NewCollectionName = string.Empty;
        IsNewCollectionFormVisible = true;
    }

    [ReactiveCommand]
    private void CancelNewCollection()
    {
        NewCollectionName = string.Empty;
        IsNewCollectionFormVisible = false;
    }

    [ReactiveCommand]
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

    [ReactiveCommand]
    private void ShowRenameCollectionForm()
    {
        if (SelectedCollection is not { } collection)
        {
            return;
        }

        RenameCollectionName = collection.Name;
        IsRenameCollectionFormVisible = true;
    }

    [ReactiveCommand]
    private void CancelRenameCollection()
    {
        RenameCollectionName = string.Empty;
        IsRenameCollectionFormVisible = false;
    }

    [ReactiveCommand]
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

    [ReactiveCommand]
    private async Task AddRequestToCollectionAsync()
    {
        var outcome = await _collectionsManagementCoordinator.AddCurrentRequestToSelectedCollectionAsync();
        if (!outcome.Changed)
        {
            return;
        }

        SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == outcome.SelectedCollectionId);
    }

    [ReactiveCommand]
    private void AddCollectionInheritedHeader()
    {
        CollectionInheritedHeaders.Add(new RequestHeaderViewModel());
    }

    [ReactiveCommand]
    private void RemoveCollectionInheritedHeader(RequestHeaderViewModel? header)
    {
        if (header is null)
        {
            return;
        }

        CollectionInheritedHeaders.Remove(header);
    }

    [ReactiveCommand]
    private Task SaveCollectionInheritedHeadersAsync(CancellationToken cancellationToken) =>
        _collectionInheritedHeadersWorkflow.SaveAsync(SelectedCollection, CollectionInheritedHeaders, cancellationToken);

    [ReactiveCommand]
    private void SetCollectionSortBy(string? sortBy) =>
        CollectionSortBy = sortBy ?? "Default";

    [ReactiveCommand]
    private void SetCollectionDisplayMode(string? mode) =>
        CollectionDisplayMode = mode ?? "NameAndPath";

    [ReactiveCommand]
    private void ToggleCollectionTreeView() =>
        IsCollectionTreeView = !IsCollectionTreeView;

    private void ApplyCollectionFilter()
    {
        // Preserve expansion state keyed by GroupKey so user-collapsed groups survive filter/sort changes.
        var previousExpanded = CollectionGroups.ToDictionary(
            group => group.GroupKey,
            group => group.IsExpanded,
            StringComparer.OrdinalIgnoreCase);

        var filterResult = _collectionFilterWorkflow.Apply(
            CollectionItems,
            CollectionSearchQuery,
            CollectionSortBy,
            previousExpanded);

        FilteredCollectionItems.Clear();
        foreach (var item in filterResult.Items)
        {
            FilteredCollectionItems.Add(item);
        }

        CollectionGroups.Clear();
        foreach (var group in filterResult.Groups)
        {
            CollectionGroups.Add(group);
        }
    }

    private void QueueCollectionInheritedHeadersAutoSave() =>
        _collectionInheritedHeadersWorkflow.QueueAutoSave(SelectedCollection, CollectionInheritedHeaders);

    public Task FlushPendingCollectionInheritedHeadersAutoSaveAsync() =>
        _collectionInheritedHeadersWorkflow.FlushPendingAutoSaveAsync();

    private IObservable<PropertyChangedEventArgs> ObserveCollectionInheritedHeaderPropertyChanges() =>
        CollectionInheritedHeadersWorkflow.ObservePropertyChanges(CollectionInheritedHeaders);

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

        var inheritedHeaders = CollectionInheritedHeadersWorkflow.BuildHeaders(CollectionInheritedHeaders);
        var manualRequestHeaders = _requestEditor.RequestHeaders
            .Where(header => !header.IsInherited)
            .ToList();

        var manualHeaders = CollectionInheritedHeadersWorkflow.BuildHeaders(manualRequestHeaders);
        var mergedHeaders = CollectionInheritedHeadersWorkflow.MergeCollectionAndRequestHeaders(
            inheritedHeaders,
            manualHeaders ?? matchingRequest.Headers);

        ApplyInheritedHeaderPreview(manualRequestHeaders, inheritedHeaders, mergedHeaders);
    }

    private bool ShouldSkipInheritedHeaderSync() =>
        _suppressCollectionInheritedHeadersLivePreviewSync || _collectionInheritedHeadersWorkflow.IsAutoSaveSuppressed;

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

        if (CollectionInheritedHeadersWorkflow.FindMatchingRequest(activeCollection, method, path, name) is not { } request)
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
            if (!CollectionInheritedHeadersWorkflow.IsInheritedHeader(mergedHeader, inheritedHeaders)
                || CollectionInheritedHeadersWorkflow.HasManualHeaderOverride(mergedHeader.Name, manualRequestHeaders))
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

    [ReactiveCommand]
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

        ErrorMessage = string.Empty;
        var outcome = await _collectionsManagementCoordinator.ImportCollectionAsync(
            () => files[0].OpenReadAsync(),
            files[0].Path.LocalPath);
        if (outcome.ErrorMessage is { } errorMessage)
        {
            ErrorMessage = errorMessage;
            return;
        }

        SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == outcome.SelectedCollectionId);
        LeftPanelTab = "Collections";
    }

    [ReactiveCommand]
    private async Task DeleteCollectionAsync(Collection? collection)
    {
        var outcome = await _collectionsManagementCoordinator.DeleteCollectionAsync(collection);
        if (outcome.Changed && outcome.WasSelected)
        {
            SelectedCollection = null;
        }
    }

    [ReactiveCommand]
    private void SaveOptions()
    {
        ErrorMessage = string.Empty;
        var outcome = _optionsWorkflow.Save(BuildOptionsFromCurrentState, ApplySavedOptions);
        if (!outcome.IsSuccessful)
        {
            ErrorMessage = outcome.ErrorMessage;
        }
    }

    private void ApplySavedOptions(ApplicationOptions options)
    {
        ApplyOptions(options, updateCurrentRequestUrl: false);
        _onApplicationOptionsChanged?.Invoke(options);
    }

    [ReactiveCommand]
    private async Task ExportOptionsAsync()
    {
        if (StorageProvider is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        var outcome = await _optionsWorkflow.ExportAsync(BuildOptionsFromCurrentState, PickOptionsExportPathAsync);
        if (!outcome.IsSuccessful)
        {
            ErrorMessage = outcome.ErrorMessage;
        }
    }

    private async Task<string?> PickOptionsExportPathAsync()
    {
        var file = await StorageProvider!.SaveFilePickerAsync(new FilePickerSaveOptions
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

        return file?.Path.LocalPath;
    }

    [ReactiveCommand]
    private async Task ImportOptionsAsync()
    {
        if (StorageProvider is null || !_optionsWorkflow.HasStore)
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

        ErrorMessage = string.Empty;
        var outcome = _optionsWorkflow.Import(files[0].Path.LocalPath, ApplySavedOptions);
        if (!outcome.IsSuccessful)
        {
            ErrorMessage = outcome.ErrorMessage;
        }
    }

    // ReactiveUI.SourceGenerators strips the "Async" suffix when generating command properties,
    // so the XAML binding target is OpenRequestBodyInExternalEditorCommand (not OpenRequestBodyInExternalEditorAsyncCommand).
    [ReactiveCommand]
    private async Task OpenRequestBodyInExternalEditorAsync()
    {
        var path = await _requestBodyExternalEditWorkflow.OpenInExternalEditorAsync(
            _requestEditor.RequestBody,
            _requestEditor.ContentType,
            async content => await Dispatcher.UIThread.InvokeAsync(() => _requestEditor.RequestBody = content),
            _tempFiles.Add);

        ResponseActionsViewModel.OpenWithShell(path);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        _crossFeatureDisposables.Dispose();

        _sendRequestCts?.Cancel();
        _responseActions.Dispose();
        _environmentsViewModel.Dispose();
        _historyFilterRequestedSubject.OnCompleted();
        _historyFilterDisposables.Dispose();
        _collectionSearchFilterRequestedSubject.OnCompleted();
        _collectionSearchFilterDisposables.Dispose();
        _optionsWorkflow.Dispose();
        _optionsAutoSaveDisposables.Dispose();
        _collectionInheritedHeadersAutoSaveDisposables.Dispose();
        _collectionInheritedHeadersWorkflow.Dispose();
        _draftWorkflow.Dispose();
        _draftWorkflow.ClearPersistedDraft();
        _scheduledJobService.Dispose();
        _scheduledJobsWorkflow.Dispose();
        _requestBodyExternalEditWorkflow.Dispose();
        _streamingCts?.Cancel();
        _streamingCts?.Dispose();
        _webSocketViewModel.Dispose();
        _sseViewModel.Dispose();
        _protocolHttpClient.Dispose();

        foreach (var tab in RequestTabs)
        {
            tab.Dispose();
        }

        ResponseActionsViewModel.DeleteTempFiles(_tempFiles);

        base.Dispose(disposing);
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
            LayoutTreeWorkflow.ReapplyProportionsFromTree(Layout, treeNode);
            return;
        }

        // Legacy path: re-apply the four well-known dock proportions.
        var leftToolDock = LayoutTreeWorkflow.FindDockById<ToolDock>(Layout, "left-tool-dock");
        var documentLayout = LayoutTreeWorkflow.FindDockById<ProportionalDock>(Layout, "document-layout");
        var requestDock = LayoutTreeWorkflow.FindDockById<DocumentDock>(Layout, "request-dock");
        if (leftToolDock is null || documentLayout is null || requestDock is null)
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
            _scheduledJobsWorkflow.LoadJobsAsync(AutoStartScheduledJobsOnLaunch, FollowHttpRedirects, cancellationToken)).ConfigureAwait(false);

        var demoSeedResult = await SeedDemoDataAsync(cancellationToken).ConfigureAwait(false);
        if (demoSeedResult.SeededCollectionId is { } newCollectionId)
        {
            await LoadCollectionsAsync(cancellationToken).ConfigureAwait(false);
            SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == newCollectionId);
            LeftPanelTab = "Collections";
        }

        var savedDraft = await _draftWorkflow.LoadPendingDraftAsync(cancellationToken).ConfigureAwait(false);
        if (savedDraft is { } draft)
        {
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

    private async Task<IStorageFolder?> GetResponseSaveSuggestedStartLocationAsync()
    {
        if (StorageProvider is null || string.IsNullOrWhiteSpace(ResponseSaveDefaultFolder))
        {
            return null;
        }

        return await StorageProvider.TryGetFolderFromPathAsync(ResponseSaveDefaultFolder);
    }

    private void SyncDefaultContentTypeSelection(string value)
    {
        (SelectedDefaultContentTypeOption, CustomDefaultContentType) =
            ApplicationOptionsWorkflow.ResolveDefaultContentTypeSelection(value, DefaultContentTypeOptions, DefaultContentTypeCustomOption);
    }

    private LayoutOptions BuildLayoutOptions() =>
        new()
        {
            CurrentLayout = CaptureLayoutSnapshot(),
            SavedLayouts = _layoutWorkflow.BuildNamedLayouts().ToList()
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
            this.RaisePropertyChanged(nameof(Layout));
        }

        if (applyResult.Applied)
        {
            RefreshDockTreeCache();
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
        ApplyManualRequestOutcome(outcome.IsSuccessful, outcome.Response, outcome.ErrorMessage, outcome.ClearResponseMetadata);
    }

    private async Task SendGraphQlRequestAsync(CancellationToken cancellationToken = default)
    {
        var outcome = await _manualGraphQlRequestCoordinator.SendAsync(cancellationToken);
        ApplyManualRequestOutcome(outcome.IsSuccessful, outcome.Response, outcome.ErrorMessage, outcome.ClearResponseMetadata);
    }

    /// <summary>
    /// Applies the shared result-handling for a manual HTTP/GraphQL send: on failure sets
    /// <see cref="ErrorMessage"/> (and optionally clears response metadata), otherwise projects
    /// the response and refreshes the cookie jar.
    /// </summary>
    private void ApplyManualRequestOutcome(bool isSuccessful, HttpResponseDetails? response, string errorMessage, bool clearResponseMetadata)
    {
        if (!isSuccessful)
        {
            ErrorMessage = errorMessage;
            if (clearResponseMetadata)
            {
                ResponseStatusCode = 0;
                ResponseTimeDisplay = string.Empty;
                ResponseSizeDisplay = string.Empty;
            }

            return;
        }

        if (response is null)
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

    private Task LoadHistoryAsync(CancellationToken cancellationToken = default) =>
        _requestHistoryWorkflow.LoadAsync(HistorySearchQuery, cancellationToken);

    [ReactiveCommand]
    private void LoadHistoryRequest(RequestHistoryEntry? request)
    {
        if (request is null)
        {
            return;
        }

        var projection = RequestHistoryWorkflow.BuildEditorProjection(request);
        _requestEditor.SelectedRequestType = RequestType.Http;
        _requestEditor.SelectedMethod = projection.Method;
        _requestEditor.RequestName = projection.Name;
        _requestEditor.RequestUrl = projection.Url;
        _requestEditor.RequestBody = projection.Body;
        _requestEditor.RequestHeaders.Clear();
        _requestEditor.EnsurePlaceholderRows();
        LeftPanelTab = "History";
    }

    private void SaveResponseStateForTab(RequestTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        tab.ResponseState = new RequestTabViewModel.ResponseStateSnapshot(
            ResponseStatus,
            ResponseStatusCode,
            ResponseTimeDisplay,
            ResponseSizeDisplay,
            ResponseBody,
            RawResponseBody,
            ResponseBodyTabLabel,
            ResponseContentType,
            ResponseRawText,
            SelectedResponseTabIndex,
            IsResponseWebViewAvailable,
            ResponseWebViewUri,
            IsBinaryResponse,
            HasResponseHeaders,
            HasTextResponse,
            ResponseHeaders.ToList(),
            _lastResponseBodyBytes);
    }

    private void RestoreResponseStateForTab(RequestTabViewModel tab)
    {
        if (tab.ResponseState is { } state)
        {
            ResponseStatus = state.ResponseStatus;
            ResponseStatusCode = state.ResponseStatusCode;
            ResponseTimeDisplay = state.ResponseTimeDisplay;
            ResponseSizeDisplay = state.ResponseSizeDisplay;
            ResponseBody = state.ResponseBody;
            RawResponseBody = state.RawResponseBody;
            ResponseRawText = state.ResponseRawText;
            ResponseContentType = state.ResponseContentType;
            ResponseBodyTabLabel = state.ResponseBodyTabLabel;
            SelectedResponseTabIndex = state.SelectedResponseTabIndex;
            IsBinaryResponse = state.IsBinaryResponse;
            IsResponseWebViewAvailable = state.IsResponseWebViewAvailable;
            ResponseWebViewUri = state.ResponseWebViewUri;
            HasResponseHeaders = state.HasResponseHeaders;
            HasTextResponse = state.HasTextResponse;
            _lastResponseBodyBytes = RequestTabsWorkflow.GetResponseStateBytes(state.LastResponseBodyBytes);
            ResponseHeaders.Clear();
            foreach (var header in state.ResponseHeaders)
            {
                ResponseHeaders.Add(header);
            }
        }
        else
        {
            ClearResponseState();
        }
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
        SelectedResponseTabIndex = 0;
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

    private void QueueOptionsAutoSave() => _optionsWorkflow.QueueAutoSave();

    private IReadOnlyList<EnvironmentVariable> GetActiveVariablesForEditor() =>
        _environmentsViewModel.GetActiveVariablesForEditor();

    [ReactiveCommand]
    private async Task RestoreDraftAsync()
    {
        if (await _draftWorkflow.TakePendingDraftAsync().ConfigureAwait(false) is { } pendingDraft)
        {
            await Dispatcher.UIThread.InvokeAsync(() => DraftPersistenceService.RestoreToEditor(pendingDraft, _requestEditor));
        }

        HasDraftToRestore = false;
        StartDraftAutoSave();
    }

    [ReactiveCommand]
    private void DiscardDraft()
    {
        _draftWorkflow.DiscardDraft();
        HasDraftToRestore = false;
        StartDraftAutoSave();
    }

    private void StartDraftAutoSave() => _draftWorkflow.StartAutoSave(SaveDraftTickAsync);

    private async Task SaveDraftTickAsync()
    {
        var state = await Dispatcher.UIThread.InvokeAsync(
            () => DraftPersistenceService.CaptureFromEditor(_requestEditor));
        await _draftWorkflow.SaveDraftAsync(state).ConfigureAwait(false);
    }
}
