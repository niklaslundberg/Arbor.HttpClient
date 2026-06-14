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
public class MainWindowOptionsUiTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadEnvironmentsAsync_WhenActiveEnvironmentWasRemoved_SelectsFirstAvailableEnvironment()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var environmentRepository = new InMemoryEnvironmentRepository();
        var developmentId = await environmentRepository.SaveAsync("Development", [new EnvironmentVariable("key", "dev")]);
        var productionId = await environmentRepository.SaveAsync("Production", [new EnvironmentVariable("key", "prod")]);
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
            environmentRepository,
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();
        viewModel.ActiveEnvironment = viewModel.Environments.First(environment => environment.Id == productionId);

        await environmentRepository.DeleteAsync(productionId);
        await viewModel.EnvironmentsPanel.LoadEnvironmentsAsync();

        viewModel.ActiveEnvironment.Should().NotBeNull();
        viewModel.ActiveEnvironment!.Id.Should().Be(developmentId);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OptionsView_ShouldDisplayScheduledJobsPage_WithAutoStartAndIntervalOptions()
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

        var optionsVm = new OptionsViewModel(viewModel);
        var window = new Window { Width = 820, Height = 560, DataContext = optionsVm, Content = new OptionsView() };
        window.Show();

        // Navigate to the Scheduled Jobs page via the ViewModel
        optionsVm.SelectedOptionsPage = "ScheduledJobs";

        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var scheduledJobsPage = window.GetVisualDescendants().OfType<ScheduledJobsOptionsPageView>().Single();
        scheduledJobsPage.IsVisible.Should().BeTrue("the extracted Scheduled Jobs page should be visible when the Scheduled Jobs options page is selected");

        var textBlocks = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(scheduledJobsPage).OfType<TextBlock>().Select(tb => tb.Text).ToList();

        textBlocks
            .Any(t => string.Equals(t, "Default interval for new jobs", StringComparison.Ordinal))
            .Should()
            .BeTrue("default interval label should be on the Scheduled Jobs page");

        var checkBoxes = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(scheduledJobsPage).OfType<CheckBox>();
        checkBoxes
            .Any(cb => string.Equals(cb.Content?.ToString(), "Auto-start scheduled jobs on launch", StringComparison.Ordinal))
            .Should()
            .BeTrue("auto-start toggle should be on the Scheduled Jobs page");

        var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
        var screenshotPath = Path.Join(Path.GetTempPath(), "arbor-httpclient-options-view.png");
        screenshot?.Save(screenshotPath);

        window.Close();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OptionsView_ShouldDisplayManageOptionsPage_WithImportAndExportButtons()
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

        var optionsVm = new OptionsViewModel(viewModel);
        var window = new Window { Width = 820, Height = 560, DataContext = optionsVm, Content = new OptionsView() };
        window.Show();

        optionsVm.SelectedOptionsPage = "ManageOptions";
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);

        var manageOptionsPage = window.GetVisualDescendants().OfType<ManageOptionsPageView>().Single();
        manageOptionsPage.IsVisible.Should().BeTrue("the Manage Options page should be visible when selected");

        var buttons = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(manageOptionsPage).OfType<Button>().ToList();
        buttons.Any(button => string.Equals(button.Content?.ToString(), Strings.OptionsImportJson, StringComparison.Ordinal))
            .Should()
            .BeTrue("import button should be present on the Manage Options page");
        buttons.Any(button => string.Equals(button.Content?.ToString(), Strings.OptionsExportJson, StringComparison.Ordinal))
            .Should()
            .BeTrue("export button should be present on the Manage Options page");

        window.Close();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task OptionsChanges_ShouldAutoSaveAndLogToDebug()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);
        var optionsPath = Path.Join(Path.GetTempPath(), $"arbor-options-autosave-{Guid.NewGuid():N}.json");
        var optionsStore = new ApplicationOptionsStore(optionsPath);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel,
            logger,
            optionsStore);

        viewModel.SelectedTlsVersionOption = "Tls13";

        await Task.Delay(1200, TestContext.Current.CancellationToken);

        var saved = optionsStore.Load();
        saved.Http.TlsVersion.Should().Be("Tls13");
        inMemorySink.GetSnapshot().Should().Contain(entry => entry.Message.Contains("Saved application options", StringComparison.Ordinal));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task EnvironmentEdits_ShouldAutoSaveWithoutExplicitSaveCommand()
    {

        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var environmentRepository = new InMemoryEnvironmentRepository();
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            environmentRepository,
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        viewModel.NewEnvironmentCommand.Execute(null);
        viewModel.NewEnvironmentName = "myenv";
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost"));

        await Task.Delay(1200, TestContext.Current.CancellationToken);

        var all = await environmentRepository.GetAllAsync();
        all.Should().ContainSingle(e => e.Name == "myenv");
        viewModel.ActiveEnvironment.Should().NotBeNull();
        viewModel.ActiveEnvironment!.Name.Should().Be("myenv");
        viewModel.IsEnvironmentPanelVisible.Should().BeTrue();
    }
}
