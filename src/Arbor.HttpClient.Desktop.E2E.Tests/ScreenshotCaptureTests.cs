using System.Net;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.VisualTree;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Screenshots")]
public class ScreenshotCaptureTests
{
    /// <summary>
    /// Saves a screenshot of the Options view (Scheduled Jobs page) showing the
    /// auto-start toggle and default interval option.
    /// Output directory is controlled by the SCREENSHOT_OUTPUT_DIR environment variable
    /// (defaults to the system temp folder).
    /// </summary>
    [Fact]
    public async Task Capture_OptionsView_ScheduledJobsPage()
    {
        var outputDir = ResolveOutputDir();

        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var repository = new InMemoryRequestHistoryRepository();
            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
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
            var window = new Window { Width = 820, Height = 560, DataContext = optionsVm };
            window.Content = new OptionsView();
            window.Show();

            // Navigate to the Scheduled Jobs page via the ViewModel
            optionsVm.SelectedOptionsPage = "ScheduledJobs";

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

            var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            screenshot?.Save(Path.Join(outputDir, "options-view-scheduled-jobs.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    /// <summary>
    /// Saves a screenshot of the Scheduled Jobs tab showing the per-job
    /// "Auto-start" checkbox on a populated job entry.
    /// Output directory is controlled by the SCREENSHOT_OUTPUT_DIR environment variable
    /// (defaults to the system temp folder).
    /// </summary>
    [Fact]
    public async Task Capture_ScheduledJobsPanel_AutostartCheckbox()
    {
        var outputDir = ResolveOutputDir();

        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var repository = new InMemoryRequestHistoryRepository();
            var scheduledJobRepository = new InMemoryScheduledJobRepository();
            await scheduledJobRepository.SaveAsync(new ScheduledJobConfig(
                0, "Daily health-check", "GET", "http://localhost:5000/health",
                null, null, 60, AutoStart: true));

            var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);
            var inMemorySink = new InMemorySink();
            var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
            var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
            var logWindowViewModel = new LogWindowViewModel(inMemorySink);

            var viewModel = new MainWindowViewModel(
                httpRequestService,
                repository,
                new InMemoryCollectionRepository(),
                new InMemoryEnvironmentRepository(),
                scheduledJobRepository,
                scheduledJobService,
                logWindowViewModel);

            await viewModel.InitializeAsync();
            viewModel.LeftPanelTab = "ScheduledJobs";

            var window = new MainWindow { DataContext = viewModel };
            window.Show();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(5);

            var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            screenshot?.Save(Path.Join(outputDir, "scheduled-jobs-autostart.png"));

            window.Close();
            viewModel.Dispose();
            return true;
        }, CancellationToken.None);
    }

    /// <summary>
    /// Saves a screenshot of the About window showing the version, git hash,
    /// copyright, license attribution, and GitHub link.
    /// Output directory is controlled by the SCREENSHOT_OUTPUT_DIR environment variable
    /// (defaults to the system temp folder).
    /// </summary>
    [Fact]
    public async Task Capture_AboutWindow()
    {
        var outputDir = ResolveOutputDir();

        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(() =>
        {
            var viewModel = new AboutWindowViewModel();
            var window = new AboutWindow { DataContext = viewModel };
            window.Show();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

            var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            screenshot?.Save(Path.Join(outputDir, "about-window.png"));

            window.Close();
            return Task.FromResult(true);
        }, CancellationToken.None);
    }

    private static string ResolveOutputDir()
    {
        var dir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .LogToTrace();
    }

}
