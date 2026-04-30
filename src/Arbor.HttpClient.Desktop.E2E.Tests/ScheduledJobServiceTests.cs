using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Serilog;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class ScheduledJobServiceTests
{
    [Fact]
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
        await Task.Delay(1500, CancellationToken.None);
        jobService.Stop(1);

        collector.GetAll().Should().NotBeEmpty();
        collector.GetAll()[0].ExceptionType.Should().Be("System.Net.Http.HttpRequestException");
    }

    [Fact]
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
        await Task.Delay(1500, CancellationToken.None);
        jobService.Stop(1);
    }

    [Fact]
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
        await Task.Delay(1500, CancellationToken.None);
        jobService.Stop(1);

        collector.GetAll().Should().BeEmpty();
    }
}
