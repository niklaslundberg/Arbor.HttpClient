using System.Net;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
public class ScreenshotCaptureTests
{
    /// <summary>
    /// Saves a screenshot of the Options window (HTTP tab) showing the new
    /// "Auto-start scheduled jobs on launch" toggle to docs/screenshots/.
    /// </summary>
    [Fact]
    public async Task Capture_OptionsWindow_AutostartToggle()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
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

            var window = new OptionsWindow { DataContext = viewModel };
            window.Show();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

            var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            var dest = Path.Combine(FindRepoRoot(), "docs", "screenshots", "options-window-autostart.png");
            screenshot?.Save(dest);

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    /// <summary>
    /// Saves a screenshot of the Scheduled Jobs tab showing the per-job
    /// "Auto-start" checkbox on a populated job entry to docs/screenshots/.
    /// </summary>
    [Fact]
    public async Task Capture_ScheduledJobsPanel_AutostartCheckbox()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var repository = new InMemoryRequestHistoryRepository();
            var scheduledJobRepository = new InMemoryScheduledJobRepository();
            await scheduledJobRepository.SaveAsync(new ScheduledJobConfig(
                0, "Daily health-check", "GET", "https://example.com/health",
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
            var dest = Path.Combine(FindRepoRoot(), "docs", "screenshots", "scheduled-jobs-autostart.png");
            screenshot?.Save(dest);

            window.Close();
            viewModel.Dispose();
            return true;
        }, CancellationToken.None);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("Arbor.HttpClient.slnx").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (Arbor.HttpClient.slnx not found).");
    }

    private sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .LogToTrace();
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }

    private sealed class InMemoryRequestHistoryRepository : IRequestHistoryRepository
    {
        private readonly List<SavedRequest> _items = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default)
        {
            _items.Add(request);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SavedRequest>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SavedRequest>>(_items.Take(limit).ToList());
    }

    private sealed class InMemoryCollectionRepository : ICollectionRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SaveAsync(string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Collection>>([]);

        public Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryEnvironmentRepository : IEnvironmentRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SaveAsync(string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task UpdateAsync(int environmentId, string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<RequestEnvironment>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RequestEnvironment>>([]);

        public Task DeleteAsync(int environmentId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryScheduledJobRepository : IScheduledJobRepository
    {
        private readonly List<ScheduledJobConfig> _items = [];
        private int _nextId = 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SaveAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default)
        {
            var id = _nextId++;
            _items.Add(config with { Id = id });
            return Task.FromResult(id);
        }

        public Task UpdateAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default)
        {
            var idx = _items.FindIndex(x => x.Id == config.Id);
            if (idx >= 0) _items[idx] = config;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScheduledJobConfig>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScheduledJobConfig>>(_items.ToList());
    }
}
