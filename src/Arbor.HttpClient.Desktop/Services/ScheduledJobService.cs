using System.Collections.Concurrent;
using System.Text.Json;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.Logging;
using Serilog;

namespace Arbor.HttpClient.Desktop.Services;

/// <summary>
/// Runs scheduled HTTP request jobs using <see cref="PeriodicTimer"/>.
/// Each job fires an <see cref="HttpRequestService.SendAsync"/> call on its configured interval,
/// logging each invocation (and any failures) through Serilog.
/// When an optional <paramref name="onResponse"/> callback is supplied to <see cref="Start"/>,
/// it is invoked on the background thread after each successful response so that callers can
/// update their state (e.g. to show a web preview).
/// </summary>
public sealed class ScheduledJobService : IDisposable
{
    private readonly HttpRequestService _httpRequestService;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, JobHandle> _handles = new();

    public ScheduledJobService(HttpRequestService httpRequestService, ILogger logger)
    {
        _httpRequestService = httpRequestService;
        _logger = logger.ForContext<ScheduledJobService>().ForContext("LogTab", LogTab.ScheduledLive);
    }

    public bool IsRunning(int jobId) => _handles.ContainsKey(jobId);

    /// <summary>
    /// Starts the scheduled job described by <paramref name="config"/>.
    /// If the job is already running, this call is a no-op.
    /// </summary>
    /// <param name="config">The job configuration.</param>
    /// <param name="onResponse">
    /// An optional callback invoked on the background timer thread after each successful response.
    /// Use <c>Dispatcher.UIThread.InvokeAsync</c> inside the callback to update UI-bound properties.
    /// </param>
    public void Start(ScheduledJobConfig config, Action<HttpResponseDetails>? onResponse = null)
    {
        if (_handles.ContainsKey(config.Id))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        var task = RunAsync(config, onResponse, cts.Token);
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

    private async Task RunAsync(ScheduledJobConfig config, Action<HttpResponseDetails>? onResponse, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, config.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await ExecuteJobAsync(config, onResponse, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // normal stop
        }
    }

    private async Task ExecuteJobAsync(ScheduledJobConfig config, Action<HttpResponseDetails>? onResponse, CancellationToken cancellationToken)
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
            onResponse?.Invoke(response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "Scheduled job {JobName} failed", config.Name);
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
