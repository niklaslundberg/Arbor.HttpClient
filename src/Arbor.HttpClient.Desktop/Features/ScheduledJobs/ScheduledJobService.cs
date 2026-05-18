using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.Json;
using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Serilog;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.Features.ScheduledJobs;

/// <summary>
/// Runs scheduled HTTP request jobs using RX.NET <see cref="Observable.Interval(TimeSpan, IScheduler)"/>.
/// Each job fires an <see cref="HttpRequestService.SendAsync"/> call on its configured interval,
/// logging each invocation (and any failures) through Serilog.
/// When an optional <paramref name="onResponseAsync"/> callback is supplied to <see cref="Start"/>,
/// it is awaited after each successful response so that callers can safely marshal UI updates.
/// </summary>
public sealed class ScheduledJobService : IDisposable
{
    private readonly HttpRequestService _httpRequestService;
    private readonly ILogger _logger;
    private readonly UnhandledExceptionCollector? _exceptionCollector;
    private readonly IScheduler _scheduler;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<int, JobHandle> _handles = new();

    public ScheduledJobService(
        HttpRequestService httpRequestService,
        ILogger logger,
        UnhandledExceptionCollector? exceptionCollector = null,
        IScheduler? scheduler = null)
    {
        _httpRequestService = httpRequestService;
        _logger = logger.ForContext<ScheduledJobService>().ForContext("LogTab", LogTab.ScheduledLive);
        _exceptionCollector = exceptionCollector;
        _scheduler = scheduler ?? DefaultScheduler.Instance;
    }

    public bool IsRunning(int jobId) => _handles.ContainsKey(jobId);

    /// <summary>
    /// Starts the scheduled job described by <paramref name="config"/>.
    /// If the job is already running, this call is a no-op.
    /// </summary>
    /// <param name="config">The job configuration.</param>
    /// <param name="onResponseAsync">
    /// An optional callback invoked after each successful response.
    /// The callback receives the response and the job <see cref="CancellationToken"/>.
    /// </param>
    public void Start(
        ScheduledJobConfig config,
        Func<HttpResponseDetails, CancellationToken, Task>? onResponseAsync = null)
    {
        lock (_gate)
        {
            if (_handles.ContainsKey(config.Id))
            {
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var jobHandle = new JobHandle(cancellationTokenSource);

            var interval = TimeSpan.FromSeconds(Math.Max(1, config.IntervalSeconds));
            var subscription = Observable
                .Interval(interval, _scheduler)
                .Subscribe(
                    _tick =>
                    {
                        _ = ExecuteScheduledTickAsync(config, onResponseAsync, jobHandle);
                    },
                    ex => _logger.Error(ex, "Scheduled job {JobName} stream terminated unexpectedly", config.Name));

            jobHandle.Subscription = subscription;
            _handles[config.Id] = jobHandle;
        }

        _logger.Information(
            "Scheduled job {JobName} (id={JobId}) started with interval {IntervalSeconds}s",
            config.Name,
            config.Id,
            config.IntervalSeconds);
    }

    public void Stop(int jobId)
    {
        JobHandle? handle;
        lock (_gate)
        {
            _handles.TryRemove(jobId, out handle);
        }

        if (handle is { })
        {
            handle.Cts.Cancel();
            handle.Subscription?.Dispose();
            _logger.Information("Scheduled job id={JobId} stopped", jobId);
        }
    }

    private async Task ExecuteScheduledTickAsync(
        ScheduledJobConfig config,
        Func<HttpResponseDetails, CancellationToken, Task>? onResponseAsync,
        JobHandle jobHandle)
    {
        if (Interlocked.CompareExchange(ref jobHandle.IsExecuting, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await ExecuteJobAsync(config, onResponseAsync, jobHandle.Cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // normal stop
        }
        finally
        {
            Interlocked.Exchange(ref jobHandle.IsExecuting, 0);
        }
    }

    private async Task ExecuteJobAsync(
        ScheduledJobConfig config,
        Func<HttpResponseDetails, CancellationToken, Task>? onResponseAsync,
        CancellationToken cancellationToken)
    {
        var headers = ParseHeaders(config.HeadersJson);
        var resolvedRequest = new ResolvedHttpRequestDraft(config.Name, config.Method, config.Url, config.Body, headers, FollowRedirects: config.FollowRedirects);

        _logger.Information(
            "Scheduled job {JobName} executing {Method} {Url}",
            config.Name, config.Method, config.Url);

        try
        {
            var response = await _httpRequestService.SendAsync(resolvedRequest, cancellationToken).ConfigureAwait(false);
            _logger.Information(
                "Scheduled job {JobName} completed: {StatusCode} {ReasonPhrase}",
                config.Name, response.StatusCode, response.ReasonPhrase);
            if (onResponseAsync is { } callback)
            {
                await callback(response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "Scheduled job {JobName} failed", config.Name);
            _exceptionCollector?.Add(ex);
        }
    }

    private static IReadOnlyList<RequestHeader>? ParseHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<RequestHeader>>(headersJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        JobHandle[] handles;
        lock (_gate)
        {
            handles = [.. _handles.Values];
            _handles.Clear();
        }

        foreach (var handle in handles)
        {
            handle.Cts.Cancel();
            handle.Subscription?.Dispose();
        }

        foreach (var handle in handles)
        {
            var isIdle = SpinWait.SpinUntil(() => Volatile.Read(ref handle.IsExecuting) == 0, TimeSpan.FromSeconds(1));
            if (isIdle)
            {
                handle.Cts.Dispose();
            }
        }
    }

    private sealed class JobHandle(CancellationTokenSource cts)
    {
        public CancellationTokenSource Cts { get; } = cts;

        public IDisposable? Subscription { get; set; }

        public int IsExecuting;
    }
}
