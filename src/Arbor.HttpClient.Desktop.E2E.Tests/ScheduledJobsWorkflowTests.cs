using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Microsoft.Reactive.Testing;
using Serilog.Core;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class ScheduledJobsWorkflowTests
{
    private sealed class Harness : IDisposable
    {
        private readonly System.Net.Http.HttpClient _httpClient;

        public InMemoryScheduledJobRepository Repository { get; } = new();

        public ScheduledJobService JobService { get; }

        public ScheduledJobsWorkflow Workflow { get; }

        public Harness()
        {
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("ok"),
                ReasonPhrase = "OK"
            });
            _httpClient = new System.Net.Http.HttpClient(handler);
            var httpRequestService = new HttpRequestService(_httpClient, new InMemoryRequestHistoryRepository());

            // TestScheduler is never advanced, so started jobs register without ever ticking.
            JobService = new ScheduledJobService(httpRequestService, Logger.None, scheduler: new TestScheduler());
            Workflow = new ScheduledJobsWorkflow(Repository, JobService);
        }

        public Task<int> SeedJobAsync(string name, bool autoStart = false, bool? followRedirects = null) =>
            Repository.SaveAsync(
                new ScheduledJobConfig(
                    Id: 0,
                    Name: name,
                    Method: "GET",
                    Url: "http://localhost:5000/test",
                    Body: null,
                    HeadersJson: null,
                    IntervalSeconds: 5,
                    AutoStart: autoStart,
                    FollowRedirects: followRedirects),
                TestContext.Current.CancellationToken);

        public void Dispose()
        {
            Workflow.Dispose();
            JobService.Dispose();
            _httpClient.Dispose();
        }
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void AddJob_IntervalBelowMinimum_ClampsToMinimum()
    {
        using var harness = new Harness();

        var job = harness.Workflow.AddJob(defaultIntervalSeconds: 0, followRedirects: true);

        job.IntervalSeconds.Should().Be(ScheduledJobsWorkflow.MinIntervalSeconds);
        harness.Workflow.Jobs.Should().ContainSingle().Which.Should().BeSameAs(job);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void AddJob_WithDefaults_AppliesIntervalAndFollowRedirects()
    {
        using var harness = new Harness();

        var job = harness.Workflow.AddJob(defaultIntervalSeconds: 90, followRedirects: false);

        job.IntervalSeconds.Should().Be(90);
        job.FollowRedirects.Should().BeFalse();
        job.Id.Should().Be(0);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RemoveJobAsync_NullJob_LeavesJobsUnchanged()
    {
        using var harness = new Harness();
        harness.Workflow.AddJob(defaultIntervalSeconds: 60, followRedirects: true);

        await harness.Workflow.RemoveJobAsync(null, TestContext.Current.CancellationToken);

        harness.Workflow.Jobs.Should().ContainSingle();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RemoveJobAsync_PersistedJob_StopsJobAndDeletesFromRepository()
    {
        using var harness = new Harness();
        var jobId = await harness.SeedJobAsync("Persisted job", autoStart: true);
        await harness.Workflow.LoadJobsAsync(
            autoStartOnLaunch: true, defaultFollowRedirects: true, TestContext.Current.CancellationToken);
        var job = harness.Workflow.Jobs.Single();

        await harness.Workflow.RemoveJobAsync(job, TestContext.Current.CancellationToken);

        harness.Workflow.Jobs.Should().BeEmpty();
        harness.JobService.IsRunning(jobId).Should().BeFalse();
        var remaining = await harness.Repository.GetAllAsync(TestContext.Current.CancellationToken);
        remaining.Should().BeEmpty();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RemoveJobAsync_UnsavedJob_RemovesWithoutTouchingRepository()
    {
        using var harness = new Harness();
        await harness.SeedJobAsync("Persisted job");
        await harness.Workflow.LoadJobsAsync(
            autoStartOnLaunch: false, defaultFollowRedirects: true, TestContext.Current.CancellationToken);
        var unsavedJob = harness.Workflow.AddJob(defaultIntervalSeconds: 60, followRedirects: true);

        await harness.Workflow.RemoveJobAsync(unsavedJob, TestContext.Current.CancellationToken);

        harness.Workflow.Jobs.Should().ContainSingle().Which.Id.Should().NotBe(0);
        var remaining = await harness.Repository.GetAllAsync(TestContext.Current.CancellationToken);
        remaining.Should().ContainSingle();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadJobsAsync_AutoStartEnabled_StartsAutoStartJobs()
    {
        using var harness = new Harness();
        var autoStartJobId = await harness.SeedJobAsync("Auto start", autoStart: true);
        await harness.SeedJobAsync("Manual start", autoStart: false);

        await harness.Workflow.LoadJobsAsync(
            autoStartOnLaunch: true, defaultFollowRedirects: true, TestContext.Current.CancellationToken);

        harness.Workflow.Jobs.Should().HaveCount(2);
        harness.Workflow.Jobs.Single(job => job.Id == autoStartJobId).IsRunning.Should().BeTrue();
        harness.Workflow.Jobs.Single(job => job.Id != autoStartJobId).IsRunning.Should().BeFalse();
        harness.JobService.IsRunning(autoStartJobId).Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadJobsAsync_AutoStartOnLaunchDisabled_LoadsJobsWithoutStarting()
    {
        using var harness = new Harness();
        var jobId = await harness.SeedJobAsync("Auto start", autoStart: true);

        await harness.Workflow.LoadJobsAsync(
            autoStartOnLaunch: false, defaultFollowRedirects: true, TestContext.Current.CancellationToken);

        var job = harness.Workflow.Jobs.Should().ContainSingle().Subject;
        job.AutoStart.Should().BeTrue();
        job.IsRunning.Should().BeFalse();
        harness.JobService.IsRunning(jobId).Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadJobsAsync_ConfigWithoutFollowRedirects_UsesDefault()
    {
        using var harness = new Harness();
        await harness.SeedJobAsync("No redirect preference", followRedirects: null);

        await harness.Workflow.LoadJobsAsync(
            autoStartOnLaunch: false, defaultFollowRedirects: false, TestContext.Current.CancellationToken);

        harness.Workflow.Jobs.Should().ContainSingle().Which.FollowRedirects.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadJobsAsync_CalledTwice_ReplacesExistingJobs()
    {
        using var harness = new Harness();
        await harness.SeedJobAsync("Persisted job");
        await harness.Workflow.LoadJobsAsync(
            autoStartOnLaunch: false, defaultFollowRedirects: true, TestContext.Current.CancellationToken);
        var firstLoadJob = harness.Workflow.Jobs.Single();

        await harness.Workflow.LoadJobsAsync(
            autoStartOnLaunch: false, defaultFollowRedirects: true, TestContext.Current.CancellationToken);

        harness.Workflow.Jobs.Should().ContainSingle().Which.Should().NotBeSameAs(firstLoadJob);
    }
}
