using System.Collections.Concurrent;
using System.Text.Json;
using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Serilog;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.Features.ScheduledJobs;

/// <summary>
/// Runs scheduled HTTP request jobs using <see cref="PeriodicTimer"/>.
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
    private readonly ConcurrentDictionary<int, JobHandle> _handles = new();

    public ScheduledJobService(HttpRequestService httpRequestService, ILogger logger, UnhandledExceptionCollector? exceptionCollector = null)
    {
        _httpRequestService = httpRequestService;
        _logger = logger.ForContext<ScheduledJobService>().ForContext("LogTab", LogTab.ScheduledLive);
        _exceptionCollector = exceptionCollector;
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
        if (_handles.ContainsKey(config.Id))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        var task = RunAsync(config, onResponseAsync, cts.Token);
        _handles[config.Id] = new JobHandle(cts, task);
        _logger.Information("Scheduled job {JobName} (id={JobId}) started with interval {IntervalSeconds}s",
            config.Name, config.Id, config.IntervalSeconds);
    }

    public void Stop(int jobId)
    {
        if (_handles.TryRemove(jobId, out var handle))
        {
            handle.Cts.Cancel();
            _logger.Information("Scheduled job id={JobId} stopped", jobId);
        }
    }

    private async Task RunAsync(
        ScheduledJobConfig config,
        Func<HttpResponseDetails, CancellationToken, Task>? onResponseAsync,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, config.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExecuteJobAsync(config, onResponseAsync, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // normal stop
        }
    }

    private async Task ExecuteJobAsync(
        ScheduledJobConfig config,
        Func<HttpResponseDetails, CancellationToken, Task>? onResponseAsync,
        CancellationToken cancellationToken)
    {
        var headers = ParseHeaders(config.HeadersJson);
        var draft = new HttpRequestDraft(config.Name, config.Method, config.Url, config.Body, headers, FollowRedirects: config.FollowRedirects);

        _logger.Information(
            "Scheduled job {JobName} executing {Method} {Url}",
            config.Name, config.Method, config.Url);

        try
        {
            var response = await _httpRequestService.SendAsync(draft, cancellationToken).ConfigureAwait(false);
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
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var handle in _handles.Values)
        {
            handle.Cts.Cancel();
        }

        _handles.Clear();
    }

    private sealed record JobHandle(CancellationTokenSource Cts, Task Task);
}
