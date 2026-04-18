using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Avalonia;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

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
    private readonly List<string> _tempFiles = [];
    private readonly List<SavedRequest> _allHistory = [];
    private FileSystemWatcher? _requestBodyWatcher;
    private int _requestBodyReadPending;
    private DockFactory? _dockFactory;
    private ApplicationOptions _applicationOptions = new();
    private readonly Dictionary<string, DockLayoutSnapshot> _savedLayouts = new(StringComparer.OrdinalIgnoreCase);
    private int _layoutNameCounter = 1;
    private bool _suppressLayoutRestore;
    private DockLayoutSnapshot? _defaultLayout;

    // Needed for file picker – set by the view
    public IStorageProvider? StorageProvider { get; set; }

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
    private string _requestBody = string.Empty;

    [ObservableProperty]
    private string _responseStatus = string.Empty;

    [ObservableProperty]
    private string _responseBody = string.Empty;

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
    public const string NoneContentTypeOption = "(none)";
    public const string CustomContentTypeOption = "Custom...";

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
        UpdateRequestHeadersPreview();
    }

    partial void OnCustomContentTypeChanged(string value)
    {
        if (IsCustomContentType)
        {
            OnPropertyChanged(nameof(ContentType));
            UpdateRequestHeadersPreview();
        }
    }

    partial void OnRequestBodyChanged(string value) =>
        UpdateRequestHeadersPreview();

    partial void OnDefaultContentTypeChanged(string value) =>
        UpdateRequestHeadersPreview();

    partial void OnUiFontSizeTextChanged(string value) =>
        OnPropertyChanged(nameof(UiFontSize));

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
    }

    public MainWindowViewModel(
        HttpRequestService httpRequestService,
        IRequestHistoryRepository requestHistoryRepository,
        ICollectionRepository collectionRepository,
        IEnvironmentRepository environmentRepository,
        IScheduledJobRepository scheduledJobRepository,
        ScheduledJobService scheduledJobService,
        LogWindowViewModel logWindowViewModel,
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

        Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
        History = [];
        Collections = [];
        CollectionItems = [];
        Environments = [];
        ActiveEnvironmentVariables = [];
        RequestHeaders = [];
        ScheduledJobs = [];
        SavedLayoutNames = [];

        RequestHeaders.CollectionChanged += OnRequestHeadersCollectionChanged;

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
    }

    public IReadOnlyList<string> Methods { get; }

    public ObservableCollection<SavedRequest> History { get; }
    public ObservableCollection<Collection> Collections { get; }
    public ObservableCollection<CollectionItemViewModel> CollectionItems { get; }
    public ObservableCollection<RequestEnvironment> Environments { get; }
    public ObservableCollection<EnvironmentVariableViewModel> ActiveEnvironmentVariables { get; }
    public ObservableCollection<RequestHeaderViewModel> RequestHeaders { get; }
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

        UpdateRequestHeadersPreview();
    }

    private void OnRequestHeaderPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        UpdateRequestHeadersPreview();

    private void UpdateRequestHeadersPreview()
    {
        RequestHeadersPreview.Clear();

        var effectiveContentType = ResolveContentType(RequestBody);
        if (!string.IsNullOrEmpty(effectiveContentType))
        {
            RequestHeadersPreview.Add($"Content-Type: {effectiveContentType}");
        }

        foreach (var h in RequestHeaders.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name)))
        {
            RequestHeadersPreview.Add($"{h.Name}: {h.Value}");
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
                HttpVersion = SelectedHttpVersionOption,
                TlsVersion = SelectedTlsVersionOption,
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
        var previousDefaultUrl = _applicationOptions.Http.DefaultRequestUrl;
        _applicationOptions = options;

        SelectedThemeOption = options.Appearance.Theme;
        SelectedHttpVersionOption = options.Http.HttpVersion;
        SelectedTlsVersionOption = options.Http.TlsVersion;
        FollowHttpRedirects = options.Http.FollowRedirects;
        DefaultRequestUrl = options.Http.DefaultRequestUrl;
        DefaultContentType = options.Http.DefaultContentType;
        UiFontFamily = options.Appearance.FontFamily;
        UiFontSizeText = options.Appearance.FontSize.ToString("0.##", CultureInfo.InvariantCulture);
        AutoStartScheduledJobsOnLaunch = options.ScheduledJobs.AutoStartOnLaunch;
        DefaultScheduledJobIntervalSeconds = options.ScheduledJobs.DefaultIntervalSeconds;

        if (updateCurrentRequestUrl || string.IsNullOrWhiteSpace(RequestUrl) || string.Equals(RequestUrl, previousDefaultUrl, StringComparison.Ordinal))
        {
            RequestUrl = options.Http.DefaultRequestUrl;
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
            IntervalSeconds = Math.Max(MinScheduledJobIntervalSeconds, DefaultScheduledJobIntervalSeconds)
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
        ActiveEnvironmentVariables.Clear();
        if (value is not null)
        {
            foreach (var v in value.Variables)
            {
                ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel(v.Name, v.Value));
            }
        }
    }

    [RelayCommand]
    private void ShowHistoryTab() => LeftPanelTab = "History";

    [RelayCommand]
    private void ShowCollectionsTab() => LeftPanelTab = "Collections";

    [RelayCommand]
    private void ShowScheduledJobsTab() => LeftPanelTab = "ScheduledJobs";

    /// <summary>Set by the view layer to open the log window.</summary>
    public Action? OpenLogWindowAction { get; set; }

    /// <summary>Set by the view layer to close the main window.</summary>
    public Action? ExitApplicationAction { get; set; }

    [RelayCommand]
    private void OpenLogWindow() => OpenLogWindowAction?.Invoke();

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
            ? _variableResolver.Resolve(SelectedCollection?.BaseUrl ?? string.Empty, ActiveEnvironment.Variables)
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
    }

    [RelayCommand]
    private void RemoveEnvironmentVariable(EnvironmentVariableViewModel? variable)
    {
        if (variable is not null)
        {
            ActiveEnvironmentVariables.Remove(variable);
        }
    }

    [RelayCommand]
    private async Task SaveEnvironmentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEnvironmentName))
        {
            return;
        }

        var variables = ActiveEnvironmentVariables
            .Where(v => !string.IsNullOrWhiteSpace(v.Name))
            .Select(v => new EnvironmentVariable(v.Name, v.Value))
            .ToList();

        if (ActiveEnvironment is not null)
        {
            await _environmentRepository.UpdateAsync(ActiveEnvironment.Id, NewEnvironmentName, variables);
        }
        else
        {
            await _environmentRepository.SaveAsync(NewEnvironmentName, variables);
        }

        await LoadEnvironmentsAsync();
        IsEnvironmentPanelVisible = false;
    }

    [RelayCommand]
    private async Task DeleteEnvironmentAsync(RequestEnvironment? environment)
    {
        if (environment is null)
        {
            return;
        }

        await _environmentRepository.DeleteAsync(environment.Id);
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

        ActiveEnvironment = environment;
        NewEnvironmentName = environment.Name;
        ActiveEnvironmentVariables.Clear();
        foreach (var v in environment.Variables)
        {
            ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel(v.Name, v.Value));
        }

        IsEnvironmentPanelVisible = true;
    }

    [RelayCommand]
    private void NewEnvironment()
    {
        ActiveEnvironment = null;
        NewEnvironmentName = string.Empty;
        ActiveEnvironmentVariables.Clear();
        IsEnvironmentPanelVisible = true;
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

    public void Dispose()
    {
        PersistCurrentLayout();
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
            : DetectExtension(content);
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}{ext}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    internal static string ExtensionFromContentType(string contentType)
    {
        var mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return mediaType switch
        {
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            "text/html" => ".html",
            _ => ".txt"
        };
    }

    private static string DetectExtension(string content)
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

        // Close any existing floating windows before restoring new ones
        if (Layout.Windows is { Count: > 0 } windows)
        {
            foreach (var win in windows.ToList())
            {
                _dockFactory.CloseWindow(win);
            }
        }

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

            var variables = ActiveEnvironment?.Variables ?? [];
            var resolvedUrl = _variableResolver.Resolve(RequestUrl, variables);
            var resolvedBody = _variableResolver.Resolve(RequestBody, variables);

            var headers = RequestHeaders
                .Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name))
                .Select(h => new RequestHeader(h.Name, _variableResolver.Resolve(h.Value, variables)))
                .ToList();

            var effectiveContentType = ResolveContentType(resolvedBody);
            if (!string.IsNullOrEmpty(effectiveContentType))
            {
                headers.Insert(0, new RequestHeader("Content-Type", effectiveContentType));
            }

            var response = await _httpRequestService.SendAsync(
                new HttpRequestDraft(RequestName, SelectedMethod, resolvedUrl, resolvedBody, headers, ParseHttpVersion(SelectedHttpVersionOption)));

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}";
            ResponseBody = response.Body;

            ResponseHeaders.Clear();
            foreach (var (name, value) in response.Headers)
            {
                ResponseHeaders.Add($"{name}: {value}");
            }

            HasResponseHeaders = ResponseHeaders.Count > 0;

            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
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

    private async Task LoadScheduledJobsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _scheduledJobRepository.GetAllAsync(cancellationToken);

        ScheduledJobs.Clear();
        foreach (var config in all)
        {
            var vm = ScheduledJobViewModel.FromConfig(config, _scheduledJobRepository, _scheduledJobService);
            ScheduledJobs.Add(vm);

            if (AutoStartScheduledJobsOnLaunch && config.AutoStart && !_scheduledJobService.IsRunning(config.Id))
            {
                _scheduledJobService.Start(config);
                vm.IsRunning = true;
            }
        }
    }
}
