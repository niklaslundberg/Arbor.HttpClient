using System.ComponentModel;
using System.Diagnostics;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.WebView;
using Arbor.HttpClient.Desktop.Shared;
using Avalonia.Threading;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.ScheduledJobs;

public sealed partial class ScheduledJobViewModel : ReactiveViewModelBase
{
    private readonly IScheduledJobRepository _repository;
    private readonly ScheduledJobService _jobService;
    private readonly Subject<Unit> _autoSaveRequestedSubject = new();
    private bool _suppressAutoSave;
    private bool _isSaving;
    private CancellationTokenSource? _autoSaveCancellationTokenSource;

    [Reactive]
    private int _id;

    [Reactive]
    private string _name = string.Empty;

    [Reactive]
    private string _method = "GET";

    [Reactive]
    private string _url = string.Empty;

    [Reactive]
    private string _body = string.Empty;

    [Reactive]
    private int _intervalSeconds = 60;

    [Reactive]
    private bool _autoStart;

    [Reactive]
    private bool _followRedirects = true;

    [Reactive]
    private bool _isRunning;

    /// <summary>
    /// When <c>true</c> and <see cref="IsWebViewApplicable"/> is also <c>true</c>,
    /// each scheduled tick stores the response body and a <see cref="WebViewWindow"/>
    /// can be opened to view the rendered URL in the platform-native browser engine.
    /// </summary>
    [Reactive]
    private bool _useWebView;

    /// <summary>Body text of the most recent successful response, or an empty string.</summary>
    [Reactive]
    private string _lastResponseBody = string.Empty;

    /// <summary>Status code + reason phrase from the most recent successful response (e.g. "200 OK").</summary>
    [Reactive]
    private string _lastResponseStatus = string.Empty;

    /// <summary>Local-time display string for when the most recent response was received (e.g. "14:32:07").</summary>
    [Reactive]
    private string _lastResponseAtDisplay = string.Empty;

    private readonly ObservableAsPropertyHelper<bool> _isWebViewApplicable;
    private readonly ObservableAsPropertyHelper<bool> _isWebViewEnabled;
    private readonly ObservableAsPropertyHelper<bool> _hasLastResponse;

    /// <summary>
    /// <c>true</c> when the web view feature is applicable for this job's HTTP method.
    /// Currently limited to <c>GET</c> requests because only those reliably return
    /// renderable content (HTML, JSON) rather than side-effecting mutations.
    /// </summary>
    public bool IsWebViewApplicable => _isWebViewApplicable.Value;

    /// <summary>
    /// <c>true</c> when web view is both applicable (GET method) and enabled by the user.
    /// Used to gate the "Open in browser" button and response callback registration.
    /// </summary>
    public bool IsWebViewEnabled => _isWebViewEnabled.Value;

    /// <summary><c>true</c> when at least one response has been captured by <see cref="HandleResponseAsync"/>.</summary>
    public bool HasLastResponse => _hasLastResponse.Value;

    public IReadOnlyList<string> Methods { get; } = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public ScheduledJobViewModel(IScheduledJobRepository repository, ScheduledJobService jobService)
    {
        _repository = repository;
        _jobService = jobService;

        _isWebViewApplicable = this
            .WhenAnyValue(viewModel => viewModel.Method)
            .Select(method => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            .ToProperty(this, viewModel => viewModel.IsWebViewApplicable);

        _isWebViewEnabled = this
            .WhenAnyValue(
                viewModel => viewModel.UseWebView,
                viewModel => viewModel.IsWebViewApplicable,
                (useWebView, applicable) => useWebView && applicable)
            .ToProperty(this, viewModel => viewModel.IsWebViewEnabled);

        _hasLastResponse = this
            .WhenAnyValue(viewModel => viewModel.LastResponseStatus)
            .Select(status => !string.IsNullOrEmpty(status))
            .ToProperty(this, viewModel => viewModel.HasLastResponse);

        Observable.Merge(
                this.WhenAnyValue(
                        viewModel => viewModel.Name,
                        viewModel => viewModel.Method,
                        viewModel => viewModel.Url,
                        viewModel => viewModel.Body)
                    .Skip(1)
                    .Select(_ => Unit.Default),
                this.WhenAnyValue(
                        viewModel => viewModel.IntervalSeconds,
                        viewModel => viewModel.AutoStart,
                        viewModel => viewModel.FollowRedirects,
                        viewModel => viewModel.UseWebView)
                    .Skip(1)
                    .Select(_ => Unit.Default))
            .Subscribe(_ => QueueAutoSave())
            .DisposeWith(Disposables);

        _autoSaveRequestedSubject
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(_ => TriggerAutoSave())
            .DisposeWith(Disposables);
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
            Math.Max(ScheduledJobsWorkflow.MinIntervalSeconds, IntervalSeconds),
            AutoStart,
            FollowRedirects,
            UseWebView: UseWebView);

    [ReactiveCommand]
    private void Start()
    {
        _jobService.Start(ToConfig(), IsWebViewEnabled ? HandleResponseAsync : null);
        IsRunning = true;
    }

    [ReactiveCommand]
    private void Stop()
    {
        _jobService.Stop(Id);
        IsRunning = false;
    }

    [ReactiveCommand]
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
    [ReactiveCommand]
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
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyResponse();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(ApplyResponse, DispatcherPriority.Normal, cancellationToken);
    }

    private void QueueAutoSave()
    {
        if (_suppressAutoSave || _isSaving || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Url))
        {
            return;
        }

        _autoSaveRequestedSubject.OnNext(Unit.Default);
    }

    private void TriggerAutoSave()
    {
        _autoSaveCancellationTokenSource?.Cancel();
        _autoSaveCancellationTokenSource?.Dispose();
        _autoSaveCancellationTokenSource = new CancellationTokenSource();

        _ = TriggerAutoSaveAsync(_autoSaveCancellationTokenSource.Token);
    }

    private async Task TriggerAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoSaveCancellationTokenSource?.Cancel();
            _autoSaveCancellationTokenSource?.Dispose();
            _autoSaveCancellationTokenSource = null;

            _autoSaveRequestedSubject.OnCompleted();
        }

        base.Dispose(disposing);
    }
}
