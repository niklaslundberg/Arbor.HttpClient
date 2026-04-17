using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.Services;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;

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
    private readonly List<string> _tempFiles = [];
    private readonly List<SavedRequest> _allHistory = [];
    private FileSystemWatcher? _requestBodyWatcher;
    private int _requestBodyReadPending;
    private DockFactory? _dockFactory;

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

    public MainWindowViewModel(
        HttpRequestService httpRequestService,
        IRequestHistoryRepository requestHistoryRepository,
        ICollectionRepository collectionRepository,
        IEnvironmentRepository environmentRepository,
        IScheduledJobRepository scheduledJobRepository,
        ScheduledJobService scheduledJobService,
        LogWindowViewModel logWindowViewModel)
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

        Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
        History = [];
        Collections = [];
        CollectionItems = [];
        Environments = [];
        ActiveEnvironmentVariables = [];
        RequestHeaders = [];
        ScheduledJobs = [];

        RequestHeaders.CollectionChanged += OnRequestHeadersCollectionChanged;

        SendRequestCommand = new AsyncRelayCommand(SendRequestAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);

        _dockFactory = new DockFactory(this);
        Layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(Layout);
    }

    public IReadOnlyList<string> Methods { get; }

    public ObservableCollection<SavedRequest> History { get; }
    public ObservableCollection<Collection> Collections { get; }
    public ObservableCollection<CollectionItemViewModel> CollectionItems { get; }
    public ObservableCollection<RequestEnvironment> Environments { get; }
    public ObservableCollection<EnvironmentVariableViewModel> ActiveEnvironmentVariables { get; }
    public ObservableCollection<RequestHeaderViewModel> RequestHeaders { get; }
    public ObservableCollection<ScheduledJobViewModel> ScheduledJobs { get; }
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

        if (!string.IsNullOrEmpty(ContentType))
        {
            RequestHeadersPreview.Add($"Content-Type: {ContentType}");
        }

        foreach (var h in RequestHeaders.Where(h => h.IsEnabled && !string.IsNullOrWhiteSpace(h.Name)))
        {
            RequestHeadersPreview.Add($"{h.Name}: {h.Value}");
        }
    }

    [RelayCommand]
    private void AddScheduledJob()
    {
        ScheduledJobs.Add(new ScheduledJobViewModel(_scheduledJobRepository, _scheduledJobService));
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

    [RelayCommand]
    private void OpenLogWindow() => OpenLogWindowAction?.Invoke();

    [RelayCommand]
    private void ToggleEnvironmentPanel() => IsEnvironmentPanelVisible = !IsEnvironmentPanelVisible;

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
        _scheduledJobService.Dispose();
        _requestBodyWatcher?.Dispose();
        _requestBodyWatcher = null;

        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); }
            catch { /* best-effort cleanup */ }
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

            if (!string.IsNullOrEmpty(ContentType))
            {
                headers.Insert(0, new RequestHeader("Content-Type", ContentType));
            }

            var response = await _httpRequestService.SendAsync(
                new HttpRequestDraft(RequestName, SelectedMethod, resolvedUrl, resolvedBody, headers));

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
        var requests = await _requestHistoryRepository.GetRecentAsync(100, cancellationToken);

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

            if (config.AutoStart && !_scheduledJobService.IsRunning(config.Id))
            {
                _scheduledJobService.Start(config);
                vm.IsRunning = true;
            }
        }
    }
}
