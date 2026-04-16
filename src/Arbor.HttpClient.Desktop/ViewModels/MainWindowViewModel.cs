using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arbor.HttpClient.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly HttpRequestService _httpRequestService;
    private readonly IRequestHistoryRepository _requestHistoryRepository;
    private readonly List<string> _tempFiles = [];
    private readonly List<SavedRequest> _allHistory = [];
    private FileSystemWatcher? _requestBodyWatcher;
    private int _requestBodyReadPending;

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

    public MainWindowViewModel(HttpRequestService httpRequestService, IRequestHistoryRepository requestHistoryRepository)
    {
        _httpRequestService = httpRequestService;
        _requestHistoryRepository = requestHistoryRepository;
        Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
        History = [];
        SendRequestCommand = new AsyncRelayCommand(SendRequestAsync);
        LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
    }

    public IReadOnlyList<string> Methods { get; }

    public ObservableCollection<SavedRequest> History { get; }

    public IAsyncRelayCommand SendRequestCommand { get; }

    public IAsyncRelayCommand LoadHistoryCommand { get; }

    partial void OnHistorySearchQueryChanged(string value) => ApplyHistoryFilter(value);

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

    private void OnRequestBodyFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: skip if a read is already queued (editors often write in multiple bursts)
        if (Interlocked.Exchange(ref _requestBodyReadPending, 1) == 1)
            return;

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
            return ".json";
        if (trimmed.StartsWith('<'))
            return ".xml";
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
        History.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var item in _allHistory)
            {
                History.Add(item);
            }
            return;
        }

        var lower = query.ToLowerInvariant();
        foreach (var item in _allHistory)
        {
            if (item.Name.Contains(lower, StringComparison.OrdinalIgnoreCase)
                || item.Url.Contains(lower, StringComparison.OrdinalIgnoreCase)
                || item.Method.Contains(lower, StringComparison.OrdinalIgnoreCase))
            {
                History.Add(item);
            }
        }
    }

    private async Task SendRequestAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            var response = await _httpRequestService.SendAsync(
                new HttpRequestDraft(RequestName, SelectedMethod, RequestUrl, RequestBody));

            ResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}";
            ResponseBody = response.Body;

            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    private async Task LoadHistoryAsync()
    {
        var requests = await _requestHistoryRepository.GetRecentAsync(100);

        _allHistory.Clear();
        _allHistory.AddRange(requests);

        ApplyHistoryFilter(HistorySearchQuery);
    }
}
