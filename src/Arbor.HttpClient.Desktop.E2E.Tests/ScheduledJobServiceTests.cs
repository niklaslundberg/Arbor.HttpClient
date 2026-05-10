using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Serilog;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Trait("Category", "Integration")]
public class ScheduledJobServiceTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public async Task Start_JobFailure_AddsExceptionToCollector()
    {
        var collectorPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(collectorPath) { IsCollecting = true };

        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("simulated job failure"));
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, new InMemoryRequestHistoryRepository());
        var logger = new LoggerConfiguration().CreateLogger();

        using var jobService = new ScheduledJobService(httpRequestService, logger, collector);

        var config = new ScheduledJobConfig(
            Id: 1,
            Name: "Test Job",
            Method: "GET",
            Url: "http://localhost:5000/test",
            Body: null,
            HeadersJson: null,
            IntervalSeconds: 1,
            AutoStart: false);

        jobService.Start(config);
        await Task.Delay(1500, TestContext.Current.CancellationToken);
        jobService.Stop(1);

        collector.GetAll().Should().NotBeEmpty();
        collector.GetAll()[0].ExceptionType.Should().Be("System.Net.Http.HttpRequestException");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Start_JobFailure_NoCollector_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("simulated job failure"));
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, new InMemoryRequestHistoryRepository());
        var logger = new LoggerConfiguration().CreateLogger();

        using var jobService = new ScheduledJobService(httpRequestService, logger);

        var config = new ScheduledJobConfig(
            Id: 1,
            Name: "Test Job",
            Method: "GET",
            Url: "http://localhost:5000/test",
            Body: null,
            HeadersJson: null,
            IntervalSeconds: 1,
            AutoStart: false);

        jobService.Start(config);
        await Task.Delay(1500, TestContext.Current.CancellationToken);
        jobService.Stop(1);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Start_JobFailure_CollectorNotCollecting_DoesNotStoreEntry()
    {
        var collectorPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(collectorPath) { IsCollecting = false };

        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("simulated job failure"));
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, new InMemoryRequestHistoryRepository());
        var logger = new LoggerConfiguration().CreateLogger();

        using var jobService = new ScheduledJobService(httpRequestService, logger, collector);

        var config = new ScheduledJobConfig(
            Id: 1,
            Name: "Test Job",
            Method: "GET",
            Url: "http://localhost:5000/test",
            Body: null,
            HeadersJson: null,
            IntervalSeconds: 1,
            AutoStart: false);

        jobService.Start(config);
        await Task.Delay(1500, TestContext.Current.CancellationToken);
        jobService.Stop(1);

        collector.GetAll().Should().BeEmpty();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Start_WithResponseCallback_PassesCancelableToken()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("ok"),
            ReasonPhrase = "OK"
        });
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, new InMemoryRequestHistoryRepository());
        var logger = new LoggerConfiguration().CreateLogger();

        using var jobService = new ScheduledJobService(httpRequestService, logger);
        var callbackTokenCanBeCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var config = new ScheduledJobConfig(
            Id: 2,
            Name: "Token Callback Job",
            Method: "GET",
            Url: "http://localhost:5000/test",
            Body: null,
            HeadersJson: null,
            IntervalSeconds: 1,
            AutoStart: false);

        jobService.Start(config, (_, cancellationToken) =>
        {
            callbackTokenCanBeCanceled.TrySetResult(cancellationToken.CanBeCanceled);
            return Task.CompletedTask;
        });

        var result = await callbackTokenCanBeCanceled.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        result.Should().BeTrue();
        jobService.Stop(2);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Start_WhenStopped_CancelsCallbackToken()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("ok"),
            ReasonPhrase = "OK"
        });
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, new InMemoryRequestHistoryRepository());
        var logger = new LoggerConfiguration().CreateLogger();

        using var jobService = new ScheduledJobService(httpRequestService, logger);
        var callbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var config = new ScheduledJobConfig(
            Id: 3,
            Name: "Cancelable Callback Job",
            Method: "GET",
            Url: "http://localhost:5000/test",
            Body: null,
            HeadersJson: null,
            IntervalSeconds: 1,
            AutoStart: false);

        jobService.Start(config, async (_, cancellationToken) =>
        {
            callbackStarted.TrySetResult(true);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                callbackCancelled.TrySetResult(true);
                throw;
            }
        });

        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        jobService.Stop(3);

        var wasCancelled = await callbackCancelled.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        wasCancelled.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Start_WhenAlreadyRunning_DoesNotStartDuplicateJob()
    {
        var sendCount = 0;
        var firstInvocation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new StubHttpMessageHandler(_ =>
        {
            if (Interlocked.Increment(ref sendCount) == 1)
            {
                firstInvocation.TrySetResult(true);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("ok"),
                ReasonPhrase = "OK"
            };
        });
        using var httpClient = new System.Net.Http.HttpClient(handler);
        var httpRequestService = new HttpRequestService(httpClient, new InMemoryRequestHistoryRepository());
        var logger = new LoggerConfiguration().CreateLogger();

        using var jobService = new ScheduledJobService(httpRequestService, logger);

        var config = new ScheduledJobConfig(
            Id: 4,
            Name: "Single Runner Job",
            Method: "GET",
            Url: "http://localhost:5000/test",
            Body: null,
            HeadersJson: null,
            IntervalSeconds: 1,
            AutoStart: false);

        jobService.Start(config);
        jobService.Start(config);

        await firstInvocation.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        jobService.Stop(4);

        sendCount.Should().Be(1);
    }
}
