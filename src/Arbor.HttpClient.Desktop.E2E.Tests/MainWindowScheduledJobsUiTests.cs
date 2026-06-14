using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Demo;
using Arbor.HttpClient.Desktop.Features.Environments;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.Options;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.Variables;
using Arbor.HttpClient.Desktop.Localization;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using Serilog;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;
using static Arbor.HttpClient.Desktop.E2E.Tests.UiTestHelpers;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class MainWindowScheduledJobsUiTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public async Task ShowScheduledJobsTabCommand_SwitchesToExplorerPanel_WhenAnotherPanelIsActive()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        var leftToolDock = FindDockById<ToolDock>(viewModel.Layout!, "left-tool-dock");
        leftToolDock.Should().NotBeNull();

        // Switch to Environments first so the left panel is no longer active
        viewModel.OpenEnvironmentsCommand.Execute().Subscribe();
        leftToolDock.ActiveDockable?.Id.Should().Be("environments");

        // Now click Scheduled Jobs — should switch back to the Explorer (left-panel) dock
        viewModel.ShowScheduledJobsTabCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("left-panel");
        viewModel.LeftPanelTab.Should().Be("ScheduledJobs");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task InitializeAsync_ShouldNotAutoStartScheduledJobs_WhenApplicationOptionIsDisabled()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var scheduledJobRepository = new InMemoryScheduledJobRepository();
        var jobId = await scheduledJobRepository.SaveAsync(new ScheduledJobConfig(
            0,
            "Job 1",
            "GET",
            "http://localhost:5000/job",
            null,
            null,
            60,
            true));

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            scheduledJobRepository,
            scheduledJobService,
            logWindowViewModel,
            initialOptions: new ApplicationOptions
            {
                ScheduledJobs = new ScheduledJobsOptions
                {
                    AutoStartOnLaunch = false
                }
            });

        await viewModel.InitializeAsync();

        viewModel.ScheduledJobs.Should().HaveCount(1);
        viewModel.ScheduledJobs.Single().AutoStart.Should().BeTrue();
        viewModel.ScheduledJobs.Single().IsRunning.Should().BeFalse();
        scheduledJobService.IsRunning(jobId).Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ScheduledJobEdits_ShouldAutoSaveWithoutExplicitSaveCommand()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var scheduledJobRepository = new InMemoryScheduledJobRepository();
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            scheduledJobRepository,
            scheduledJobService,
            logWindowViewModel);

        viewModel.AddScheduledJobCommand.Execute().Subscribe();
        var job = viewModel.ScheduledJobs.Should().ContainSingle().Subject;
        job.Name = "sync job";
        job.Url = "http://localhost:5000/sync";

        await Task.Delay(1200, TestContext.Current.CancellationToken);

        var all = await scheduledJobRepository.GetAllAsync();
        all.Should().ContainSingle(config =>
            string.Equals(config.Name, "sync job", StringComparison.Ordinal) &&
            string.Equals(config.Url, "http://localhost:5000/sync", StringComparison.Ordinal));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ScheduledJob_ShouldRespectFollowRedirectsOverride()
    {
        using var server = new RedirectTestServer();

        var repository = new InMemoryRequestHistoryRepository();
        using var defaultClient = new System.Net.Http.HttpClient();
        using var followClient = new System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true });
        using var noFollowClient = new System.Net.Http.HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
        var httpRequestService = new HttpRequestService(defaultClient, repository);
        httpRequestService.SetHttpClientFactory(followRedirects =>
            (followRedirects ?? true) ? followClient : noFollowClient);

        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        using var scheduledJobService = new ScheduledJobService(httpRequestService, logger);

        const int noFollowId = 1;
        scheduledJobService.Start(new ScheduledJobConfig(
            noFollowId,
            "redirect-off",
            "GET",
            server.RedirectUrl,
            null,
            null,
            1,
            AutoStart: false,
            FollowRedirects: false));

        await Task.Delay(1300, TestContext.Current.CancellationToken);
        scheduledJobService.Stop(noFollowId);
        server.FinalRequestCount.Should().Be(0);

        const int followId = 2;
        scheduledJobService.Start(new ScheduledJobConfig(
            followId,
            "redirect-on",
            "GET",
            server.RedirectUrl,
            null,
            null,
            1,
            AutoStart: false,
            FollowRedirects: true));

        await Task.Delay(1300, TestContext.Current.CancellationToken);
        scheduledJobService.Stop(followId);
        server.FinalRequestCount.Should().BeGreaterThan(0);
    }
}
