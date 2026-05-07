using System.ComponentModel;
using System.Diagnostics;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.WebView;
using Arbor.HttpClient.Desktop.Shared;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.Features.ScheduledJobs;

public sealed partial class ScheduledJobViewModel : ViewModelBase
{
    private readonly IScheduledJobRepository _repository;
    private readonly ScheduledJobService _jobService;
    private bool _suppressAutoSave;
    private bool _isSaving;
    private CancellationTokenSource? _autoSaveCancellationTokenSource;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _method = "GET";

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    [ObservableProperty]
    private int _intervalSeconds = 60;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _followRedirects = true;

    [ObservableProperty]
    private bool _isRunning;

    /// <summary>
    /// When <c>true</c> and <see cref="IsWebViewApplicable"/> is also <c>true</c>,
    /// each scheduled tick stores the response body and a <see cref="WebViewWindow"/>
    /// can be opened to view the rendered URL in the platform-native browser engine.
    /// </summary>
    [ObservableProperty]
    private bool _useWebView;

    /// <summary>Body text of the most recent successful response, or an empty string.</summary>
    [ObservableProperty]
    private string _lastResponseBody = string.Empty;

    /// <summary>Status code + reason phrase from the most recent successful response (e.g. "200 OK").</summary>
    [ObservableProperty]
    private string _lastResponseStatus = string.Empty;

    /// <summary>Local-time display string for when the most recent response was received (e.g. "14:32:07").</summary>
    [ObservableProperty]
    private string _lastResponseAtDisplay = string.Empty;

    /// <summary>
    /// <c>true</c> when the web view feature is applicable for this job's HTTP method.
    /// Currently limited to <c>GET</c> requests because only those reliably return
    /// renderable content (HTML, JSON) rather than side-effecting mutations.
    /// </summary>
    public bool IsWebViewApplicable => string.Equals(Method, "GET", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>true</c> when web view is both applicable (GET method) and enabled by the user.
    /// Used to gate the "Open in browser" button and response callback registration.
    /// </summary>
    public bool IsWebViewEnabled => UseWebView && IsWebViewApplicable;

    /// <summary><c>true</c> when at least one response has been captured by <see cref="HandleResponseAsync"/>.</summary>
    public bool HasLastResponse => !string.IsNullOrEmpty(LastResponseStatus);

    public IReadOnlyList<string> Methods { get; } = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public ScheduledJobViewModel(IScheduledJobRepository repository, ScheduledJobService jobService)
    {
        _repository = repository;
        _jobService = jobService;
    }

    public static ScheduledJobViewModel FromConfig(
        ScheduledJobConfig config,
        IScheduledJobRepository repository,
        ScheduledJobService jobService,
        bool defaultFollowRedirects)
    {
        var vm = new ScheduledJobViewModel(repository, jobService);
        vm._suppressAutoSave = true;
        vm.Id = config.Id;
        vm.Name = config.Name;
        vm.Method = config.Method;
        vm.Url = config.Url;
        vm.Body = config.Body ?? string.Empty;
        vm.IntervalSeconds = config.IntervalSeconds;
        vm.AutoStart = config.AutoStart;
        vm.FollowRedirects = config.FollowRedirects ?? defaultFollowRedirects;
        vm.UseWebView = config.UseWebView;
        vm._suppressAutoSave = false;
        vm.IsRunning = jobService.IsRunning(config.Id);
        return vm;
    }

    public ScheduledJobConfig ToConfig() =>
        new(Id, Name, Method, Url,
            string.IsNullOrWhiteSpace(Body) ? null : Body,
            null, // headers not yet supported in the scheduled-job editor
            Math.Max(MainWindowViewModel.MinScheduledJobIntervalSeconds, IntervalSeconds),
            AutoStart,
            FollowRedirects,
            UseWebView: UseWebView);

    [RelayCommand]
    private void Start()
    {
        _jobService.Start(ToConfig(), IsWebViewEnabled ? HandleResponseAsync : null);
        IsRunning = true;
    }

    [RelayCommand]
    private void Stop()
    {
        _jobService.Stop(Id);
        IsRunning = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        var config = ToConfig();
        try
        {
            if (Id == 0)
            {
                Id = await _repository.SaveAsync(config);
            }
            else
            {
                await _repository.UpdateAsync(config with { Id = Id });
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    /// <summary>
    /// Opens the configured URL in the system's default browser as a fallback
    /// when the in-app web view cannot be used.
    /// </summary>
    [RelayCommand]
    private void OpenInBrowser()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException or PlatformNotSupportedException)
        {
            // No default browser or invalid URL — silently ignore.
        }
    }

    /// <summary>
    /// Called by <see cref="ScheduledJobService"/> after each successful HTTP response
    /// when <see cref="UseWebView"/> is enabled.
    /// Applies updates immediately on the UI thread, or dispatches asynchronously when invoked from a worker thread.
    /// </summary>
    internal async Task HandleResponseAsync(HttpResponseDetails response, CancellationToken cancellationToken)
    {
        void ApplyResponse()
        {
            LastResponseBody = response.Body;
            LastResponseStatus = $"{response.StatusCode} {response.ReasonPhrase}".Trim();
            LastResponseAtDisplay = DateTimeOffset.Now.ToString("HH:mm:ss");
            OnPropertyChanged(nameof(HasLastResponse));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyResponse();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(ApplyResponse, DispatcherPriority.Normal, cancellationToken);
    }

    partial void OnNameChanged(string value) => QueueAutoSave();

    partial void OnMethodChanged(string value)
    {
        OnPropertyChanged(nameof(IsWebViewApplicable));
        OnPropertyChanged(nameof(IsWebViewEnabled));
        QueueAutoSave();
    }

    partial void OnUrlChanged(string value) => QueueAutoSave();

    partial void OnBodyChanged(string value) => QueueAutoSave();

    partial void OnIntervalSecondsChanged(int value) => QueueAutoSave();

    partial void OnAutoStartChanged(bool value) => QueueAutoSave();

    partial void OnFollowRedirectsChanged(bool value) => QueueAutoSave();

    partial void OnUseWebViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsWebViewEnabled));
        QueueAutoSave();
    }

    private void QueueAutoSave()
    {
        if (_suppressAutoSave || _isSaving || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Url))
        {
            return;
        }

        _autoSaveCancellationTokenSource?.Cancel();
        _autoSaveCancellationTokenSource?.Dispose();
        _autoSaveCancellationTokenSource = new CancellationTokenSource();
        _ = TriggerAutoSaveAsync(_autoSaveCancellationTokenSource.Token);
    }

    private async Task TriggerAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (Dispatcher.UIThread.CheckAccess())
            {
                await SaveAsync();
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(SaveAsync, DispatcherPriority.Normal, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Debounced auto-save was superseded by a newer edit.
        }
    }
}
