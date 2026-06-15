using System;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests.ScheduledJobs;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class ScheduledJobsPanelViewModelTests
{
    private static ScheduledJobService CreateJobService()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());
        return new ScheduledJobService(httpRequestService, new LoggerConfiguration().CreateLogger());
    }

    private static ScheduledJobsPanelViewModel Create(
        IScheduledJobRepository repository,
        ScheduledJobService jobService,
        int interval = 60,
        bool followRedirects = false,
        bool autoStart = false,
        Action? onJobAdded = null) =>
        new(repository, jobService, () => interval, () => followRedirects, () => autoStart, onJobAdded);

    [AvaloniaFact(Timeout = 10_000)]
    public void AddJobCommand_AddsJobWithConfiguredInterval()
    {
        using var jobService = CreateJobService();
        using var viewModel = Create(new InMemoryScheduledJobRepository(), jobService, interval: 45);

        viewModel.AddJobCommand.Execute().Subscribe();

        viewModel.Jobs.Should().ContainSingle().Which.IntervalSeconds.Should().Be(45);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void AddJobCommand_InvokesOnJobAddedCallback()
    {
        using var jobService = CreateJobService();
        var invoked = false;
        using var viewModel = Create(new InMemoryScheduledJobRepository(), jobService, onJobAdded: () => invoked = true);

        viewModel.AddJobCommand.Execute().Subscribe();

        invoked.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadAsync_PopulatesJobsFromRepository()
    {
        var repository = new InMemoryScheduledJobRepository();
        await repository.SaveAsync(new ScheduledJobConfig(0, "Job 1", "GET", "http://localhost/job", null, null, 60, false));
        using var jobService = CreateJobService();
        using var viewModel = Create(repository, jobService);

        await viewModel.LoadAsync();

        viewModel.Jobs.Should().ContainSingle().Which.Name.Should().Be("Job 1");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task RemoveJobCommand_RemovesJob()
    {
        using var jobService = CreateJobService();
        using var viewModel = Create(new InMemoryScheduledJobRepository(), jobService);
        viewModel.AddJobCommand.Execute().Subscribe();
        var job = viewModel.Jobs.Should().ContainSingle().Subject;

        await viewModel.RemoveJobCommand.Execute(job);

        viewModel.Jobs.Should().BeEmpty();
    }
}
