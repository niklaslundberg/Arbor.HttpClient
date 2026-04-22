using System.Net;
using System.Net.Http;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.VisualTree;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Generates documentation screenshots using Avalonia's headless rendering.
/// Run with: dotnet test --filter "Category=Screenshots"
/// The output directory is controlled by the SCREENSHOT_OUTPUT_DIR environment variable
/// (defaults to the system temp folder).
/// </summary>
[Collection("HeadlessAvalonia")]
[Trait("Category", "Screenshots")]
public class ScreenshotGenerator
{
    [Fact]
    public async Task GenerateInitialStateScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(() =>
        {
            // App as it looks when first opened — URL field empty, no response yet
            var (_, window) = CreateWindow(
                BuildHandler("{}"),
                requestName: "Demo Request",
                method: "GET",
                url: string.Empty);   // start with empty URL to enable typing animation

            window.Show();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "state-initial.png"));

            window.Close();
            return Task.FromResult(true);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateAfterResponseScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("""
                    {
                      "status": "ok",
                      "path": "/get",
                      "params": { "hello": "world" },
                      "message": "Hello from Arbor.HttpClient demo!"
                    }
                    """),
                requestName: "Demo Request",
                method: "GET",
                url: "https://postman-echo.com/get?hello=world");

            window.Show();
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "state-response.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateMainWindowScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("""
                    {
                      "args": { "hello": "world" },
                      "headers": {
                        "host": "postman-echo.com",
                        "user-agent": "Arbor.HttpClient/1.0"
                      },
                      "url": "https://postman-echo.com/get?hello=world"
                    }
                    """),
                requestName: "Echo GET",
                method: "GET",
                url: "https://postman-echo.com/get?hello=world");

            window.Show();
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "main-window.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateVariablesScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("""
                    {
                      "args": { "env": "production" },
                      "headers": { "host": "postman-echo.com" },
                      "url": "https://postman-echo.com/get?env=production"
                    }
                    """),
                requestName: "Echo with variable",
                method: "POST",
                url: "{{baseUrl}}/get?env={{environment}}&{{queryKey}}={{queryValue}}");

            viewModel.RequestEditor.RequestBody = """
                {
                  "apiKey": "{{token}}",
                  "environment": "{{environment}}"
                }
                """;
            viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
            {
                Name = "X-{{headerName}}",
                Value = "{{headerValue}}",
                IsEnabled = true
            });

            // Pre-populate an environment so the Environments dock panel shows real variables
            viewModel.Environments.Add(new RequestEnvironment(1, "Demo Environment",
            [
                new EnvironmentVariable("environment", "production"),
                new EnvironmentVariable("baseUrl", "https://postman-echo.com"),
                new EnvironmentVariable("queryKey", "city"),
                new EnvironmentVariable("queryValue", "stockholm"),
                new EnvironmentVariable("token", "demo-token"),
                new EnvironmentVariable("headerName", "Tenant"),
                new EnvironmentVariable("headerValue", "blue", false)
            ]));
            viewModel.EditEnvironmentCommand.Execute(viewModel.Environments[0]);
            viewModel.OpenEnvironmentsCommand.Execute(null);

            window.Show();
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabControl is not null)
            {
                tabControl.SelectedIndex = 1;
            }

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "variables.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateVariablesQueryTabScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("{\"ok\":true}"),
                requestName: "Query with variables",
                method: "GET",
                url: "{{baseUrl}}/search");

            viewModel.RequestEditor.RequestQueryParameters.Add(new RequestQueryParameterViewModel
            {
                Key = "{{queryKey}}",
                Value = "{{queryValue}}",
                IsEnabled = true
            });
            viewModel.RequestEditor.RequestQueryParameters.Add(new RequestQueryParameterViewModel
            {
                Key = "env",
                Value = "{{environment}}",
                IsEnabled = true
            });
            viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
            {
                Name = "X-{{headerName}}",
                Value = "{{headerValue}}",
                IsEnabled = true
            });

            viewModel.NewEnvironmentName = "Demo Environment";
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("baseUrl", "https://postman-echo.com"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("queryKey", "city"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("queryValue", "stockholm"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("environment", "production"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("headerName", "Tenant"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("headerValue", "blue"));
            viewModel.IsEnvironmentPanelVisible = false;

            window.Show();
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabControl is not null)
            {
                tabControl.SelectedIndex = 0; // Query tab
            }

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "variables-query-tab.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateVariablesHeadersTabScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("{\"ok\":true}"),
                requestName: "Headers with variables",
                method: "POST",
                url: "{{baseUrl}}/api");

            viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
            {
                Name = "X-{{headerName}}",
                Value = "{{headerValue}}",
                IsEnabled = true
            });
            viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
            {
                Name = "Authorization",
                Value = "Bearer {{token}}",
                IsEnabled = true
            });

            viewModel.NewEnvironmentName = "Demo Environment";
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("baseUrl", "https://postman-echo.com"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("headerName", "Tenant"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("headerValue", "blue"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("token", "demo-token"));
            viewModel.IsEnvironmentPanelVisible = false;

            window.Show();
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabControl is not null)
            {
                tabControl.SelectedIndex = 2; // Headers tab
            }

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "variables-headers-tab.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateVariablesPreviewScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("""
                    {
                      "ok": true
                    }
                    """),
                requestName: "Preview with variables",
                method: "POST",
                url: "{{baseUrl}}/get?env={{environment}}");

            viewModel.RequestEditor.RequestBody = """
                {
                  "apiKey": "{{token}}"
                }
                """;
            viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
            {
                Name = "X-{{headerName}}",
                Value = "{{headerValue}}",
                IsEnabled = true
            });

            viewModel.NewEnvironmentName = "Demo Environment";
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("baseUrl", "https://postman-echo.com"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("environment", "production"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("token", "demo-token"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("headerName", "Tenant"));
            viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("headerValue", "blue"));
            viewModel.IsEnvironmentPanelVisible = true;

            window.Show();
            var tabControl = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabControl is not null)
            {
                tabControl.SelectedIndex = 3;
            }

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "variables-preview.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateScheduledJobsScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("{}"),
                requestName: "Health check",
                method: "GET",
                url: "https://postman-echo.com/get");

            // Add a scheduled job so the tab looks populated
            var jobVm = new ScheduledJobViewModel(new InMemoryScheduledJobRepo(), new ScheduledJobService(
                new HttpRequestService(new global::System.Net.Http.HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryHistoryRepo()),
                new LoggerConfiguration().WriteTo.Sink(new InMemorySink()).CreateLogger()))
            {
                Name = "Health check every 60 s",
                Method = "GET",
                Url = "https://postman-echo.com/get",
                IntervalSeconds = 60,
                AutoStart = true
            };
            viewModel.ScheduledJobs.Add(jobVm);
            viewModel.LeftPanelTab = "ScheduledJobs";

            window.Show();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "scheduled-jobs.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateFollowRedirectOverridesScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("{}"),
                requestName: "Redirect override demo",
                method: "GET",
                url: "https://postman-echo.com/redirect-to?url=https://postman-echo.com/get");

            viewModel.RequestEditor.FollowRedirectsForRequest = false;
            viewModel.LeftPanelTab = "ScheduledJobs";
            viewModel.ScheduledJobs.Add(new ScheduledJobViewModel(new InMemoryScheduledJobRepo(), new ScheduledJobService(
                new HttpRequestService(new global::System.Net.Http.HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryHistoryRepo()),
                new LoggerConfiguration().WriteTo.Sink(new InMemorySink()).CreateLogger()))
            {
                Name = "Redirect check",
                Method = "GET",
                Url = "https://postman-echo.com/redirect-to?url=https://postman-echo.com/get",
                IntervalSeconds = 60,
                AutoStart = false,
                FollowRedirects = false
            });

            window.Show();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "follow-redirect-overrides.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateOptionsWindowScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(() =>
        {
            var (viewModel, _) = CreateWindow(
                BuildHandler("{}"),
                requestName: "Options demo",
                method: "POST",
                url: "https://postman-echo.com/post");

            var optionsWindow = new OptionsWindow { DataContext = viewModel };
            optionsWindow.Show();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = optionsWindow.GetLastRenderedFrame() ?? optionsWindow.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "options-window.png"));

            optionsWindow.Close();
            return Task.FromResult(true);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateLayoutPanelScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(async () =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("{}"),
                requestName: "Layout demo",
                method: "GET",
                url: "https://postman-echo.com/get");

            // Open the layout panel so it appears in the screenshot
            viewModel.IsLayoutPanelVisible = true;

            window.Show();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "layout-panel.png"));

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateLogPanelScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(() =>
        {
            var (viewModel, window) = CreateWindow(
                BuildHandler("{}"),
                requestName: "Log panel demo",
                method: "GET",
                url: "https://postman-echo.com/get");

            // Navigate to the dockable Logs panel
            viewModel.OpenLogWindowCommand.Execute(null);

            window.Show();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(5);
            var frame = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "log-panel.png"));

            window.Close();
            return Task.FromResult(true);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task GenerateOptionsWindowHttpDiagnosticsScreenshot()
    {
        var outputDir = Environment.GetEnvironmentVariable("SCREENSHOT_OUTPUT_DIR")
            ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDir);

        using var session = HeadlessUnitTestSession.StartNew(typeof(ScreenshotEntryPoint));

        await session.Dispatch(() =>
        {
            var (viewModel, _) = CreateWindow(
                BuildHandler("{}"),
                requestName: "Diagnostics demo",
                method: "GET",
                url: "https://postman-echo.com/get");

            // Show HTTP options page (includes the new "Enable HTTP diagnostics" checkbox)
            viewModel.SelectedOptionsPage = "HTTP";

            var optionsWindow = new OptionsWindow { DataContext = viewModel };
            optionsWindow.Show();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            var frame = optionsWindow.GetLastRenderedFrame() ?? optionsWindow.CaptureRenderedFrame();
            frame?.Save(Path.Combine(outputDir, "options-window-http-diagnostics.png"));

            optionsWindow.Close();
            return Task.FromResult(true);
        }, CancellationToken.None);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static HttpMessageHandler BuildHandler(string body) =>
        new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            ReasonPhrase = "OK",
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

    private static (MainWindowViewModel ViewModel, MainWindow Window) CreateWindow(
        HttpMessageHandler handler,
        string requestName,
        string method,
        string url)
    {
        var historyRepo = new InMemoryHistoryRepo();
        var httpRequestService = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), historyRepo);
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowVm = new LogWindowViewModel(sink);

        var viewModel = new MainWindowViewModel(
            httpRequestService,
            historyRepo,
            new InMemoryCollectionRepo(),
            new InMemoryEnvironmentRepo(),
            new InMemoryScheduledJobRepo(),
            scheduledJobService,
            logWindowVm);

        viewModel.RequestEditor.RequestName = requestName;
        viewModel.RequestEditor.SelectedMethod = method;
        viewModel.RequestEditor.RequestUrl = url;

        return (viewModel, new MainWindow { DataContext = viewModel });
    }

    // -------------------------------------------------------------------------
    // Stubs
    // -------------------------------------------------------------------------

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }

    private sealed class InMemoryHistoryRepo : IRequestHistoryRepository
    {
        private readonly List<SavedRequest> _items = [];
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default) { _items.Add(request); return Task.CompletedTask; }
        public Task<IReadOnlyList<SavedRequest>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SavedRequest>>(_items.Take(limit).ToList());
    }

    private sealed class InMemoryCollectionRepo : ICollectionRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> SaveAsync(string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Collection>>([]);
        public Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryEnvironmentRepo : IEnvironmentRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> SaveAsync(string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task UpdateAsync(int environmentId, string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RequestEnvironment>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RequestEnvironment>>([]);
        public Task DeleteAsync(int environmentId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryScheduledJobRepo : IScheduledJobRepository
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
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) { _items.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
        public Task<IReadOnlyList<ScheduledJobConfig>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScheduledJobConfig>>(_items.ToList());
    }

    private sealed class ScreenshotEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .LogToTrace();
    }
}
