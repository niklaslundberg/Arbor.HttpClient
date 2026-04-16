using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arbor.HttpClient.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly HttpRequestService _httpRequestService;
    private readonly IRequestHistoryRepository _requestHistoryRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly OpenApiImportService _openApiImportService;
    private readonly VariableResolver _variableResolver;
    private readonly List<string> _tempFiles = [];
    private readonly List<SavedRequest> _allHistory = [];
    private FileSystemWatcher? _requestBodyWatcher;
    private int _requestBodyReadPending;

    // Needed for file picker – set by the view
    public IStorageProvider? StorageProvider { get; set; }

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
        IEnvironmentRepository environmentRepository)
    {
        _httpRequestService = httpRequestService;
        _requestHistoryRepository = requestHistoryRepository;
        _collectionRepository = collectionRepository;
        _environmentRepository = environmentRepository;
        _openApiImportService = new OpenApiImportService();
        _variableResolver = new VariableResolver();

        Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
        History = [];
        Collections = [];
        CollectionItems = [];
        Environments = [];
        ActiveEnvironmentVariables = [];

        SendRequestCommand = new AsyncRelayCommand(SendRequestAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
    }

    public IReadOnlyList<string> Methods { get; }

    public ObservableCollection<SavedRequest> History { get; }
    public ObservableCollection<Collection> Collections { get; }
    public ObservableCollection<CollectionItemViewModel> CollectionItems { get; }
    public ObservableCollection<RequestEnvironment> Environments { get; }
    public ObservableCollection<EnvironmentVariableViewModel> ActiveEnvironmentVariables { get; }

    public IAsyncRelayCommand SendRequestCommand { get; }
    public IAsyncRelayCommand LoadHistoryCommand { get; }

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
            LoadEnvironmentsAsync(cancellationToken)).ConfigureAwait(false);
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
        var ext = DetectExtension(content);
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}{ext}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
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

            var response = await _httpRequestService.SendAsync(
                new HttpRequestDraft(RequestName, SelectedMethod, resolvedUrl, resolvedBody));

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}";
            ResponseBody = response.Body;

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
}
