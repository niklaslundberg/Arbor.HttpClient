using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Serilog;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Trait("Category", "Integration")]
public class ScheduledJobViewModelTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public async Task QueueAutoSave_WhenNameAndUrlSet_ShouldPersistAfterDebounce()
    {
        var repository = new InMemoryScheduledJobRepository();
        var viewModel = CreateViewModel(repository);

        viewModel.Name = "Job 1";
        viewModel.Url = "http://localhost:5000/test";

        await Task.Delay(1500, TestContext.Current.CancellationToken);

        var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);
        jobs.Should().ContainSingle();
        jobs[0].Name.Should().Be("Job 1");
        jobs[0].Url.Should().Be("http://localhost:5000/test");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task QueueAutoSave_WhenRapidEdits_ShouldPersistLatestStateOnce()
    {
        var repository = new InMemoryScheduledJobRepository();
        var viewModel = CreateViewModel(repository);

        viewModel.Name = "Job";
        viewModel.Url = "http://localhost:5000/1";

        await Task.Delay(200, TestContext.Current.CancellationToken);
        viewModel.Url = "http://localhost:5000/2";

        await Task.Delay(200, TestContext.Current.CancellationToken);
        viewModel.Url = "http://localhost:5000/final";

        await Task.Delay(1500, TestContext.Current.CancellationToken);

        var jobs = await repository.GetAllAsync(TestContext.Current.CancellationToken);
        jobs.Should().ContainSingle();
        jobs[0].Url.Should().Be("http://localhost:5000/final");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task HandleResponseAsync_ShouldPopulateLastResponseFields()
    {
        var repository = new InMemoryScheduledJobRepository();
        var viewModel = CreateViewModel(repository);
        var response = new HttpResponseDetails(
            StatusCode: 200,
            ReasonPhrase: "OK",
            Body: "payload",
            Headers: [],
            ElapsedMilliseconds: 25);

        await viewModel.HandleResponseAsync(response, TestContext.Current.CancellationToken);

        viewModel.LastResponseBody.Should().Be("payload");
        viewModel.LastResponseStatus.Should().Be("200 OK");
        viewModel.HasLastResponse.Should().BeTrue();
        viewModel.LastResponseAtDisplay.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void IsWebViewEnabled_WhenMethodIsGetAndUseWebViewTrue_ShouldBeTrue()
    {
        var repository = new InMemoryScheduledJobRepository();
        var viewModel = CreateViewModel(repository);

        viewModel.Method = "GET";
        viewModel.UseWebView = true;

        viewModel.IsWebViewApplicable.Should().BeTrue();
        viewModel.IsWebViewEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsWebViewEnabled_WhenMethodIsPost_ShouldBeFalse()
    {
        var repository = new InMemoryScheduledJobRepository();
        var viewModel = CreateViewModel(repository);

        viewModel.Method = "POST";
        viewModel.UseWebView = true;

        viewModel.IsWebViewApplicable.Should().BeFalse();
        viewModel.IsWebViewEnabled.Should().BeFalse();
    }

    private static ScheduledJobViewModel CreateViewModel(InMemoryScheduledJobRepository repository)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("ok"),
            ReasonPhrase = "OK"
        });

        var httpClient = new System.Net.Http.HttpClient(handler);
        var requestHistoryRepository = new InMemoryRequestHistoryRepository();
        var requestService = new HttpRequestService(httpClient, requestHistoryRepository);
        var logger = new LoggerConfiguration().CreateLogger();
        var jobService = new ScheduledJobService(requestService, logger);

        return new ScheduledJobViewModel(repository, jobService);
    }
}
