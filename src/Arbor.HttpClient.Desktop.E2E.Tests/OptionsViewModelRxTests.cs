using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.Options;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class OptionsViewModelRxTests
{
    [Fact]
    public void SelectedOptionsPage_WhenChanged_UpdatesDerivedReactiveProperties()
    {
        using var app = CreateAppViewModel();
        using var viewModel = new OptionsViewModel(app);

        viewModel.SelectedOptionsPage = "ScheduledJobs";

        viewModel.App.Should().BeSameAs(app);
        viewModel.SelectedOptionsPageTitle.Should().Be("Scheduled Jobs");
        viewModel.SelectedOptionsPageBreadcrumb.Should().Be("Options ›  Scheduled Jobs");
    }

    [Fact]
    public void Dispose_WhenCalled_StopsDerivedReactivePropertyUpdates()
    {
        using var app = CreateAppViewModel();
        using var viewModel = new OptionsViewModel(app);
        viewModel.SelectedOptionsPage = "ScheduledJobs";
        viewModel.SelectedOptionsPageTitle.Should().Be("Scheduled Jobs");

        viewModel.Dispose();
        var act = () =>
        {
            viewModel.SelectedOptionsPage = "Diagnostics";
            _ = viewModel.SelectedOptionsPageTitle;
            _ = viewModel.SelectedOptionsPageBreadcrumb;
        };

        act.Should().NotThrow();
        viewModel.SelectedOptionsPageTitle.Should().Be("Scheduled Jobs");
        viewModel.SelectedOptionsPageBreadcrumb.Should().Be("Options ›  Scheduled Jobs");
    }

    private static MainWindowViewModel CreateAppViewModel()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", System.Text.Encoding.UTF8, "application/json")
        });
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        return new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);
    }
}
