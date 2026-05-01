using System.Net;
using Arbor.HttpClient.Desktop.Demo;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;
using Serilog;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class DemoServerTests
{
    // ── DemoServer start/stop ────────────────────────────────────────────────

    [Fact]
    public async Task DemoServer_IsNotRunning_ByDefault()
    {
        await using var server = new DemoServer();
        server.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task DemoServer_IsRunning_AfterStartAsync()
    {
        await using var server = new DemoServer();

        await server.StartAsync();

        server.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task DemoServer_IsNotRunning_AfterStopAsync()
    {
        await using var server = new DemoServer();
        await server.StartAsync();

        await server.StopAsync();

        server.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task DemoServer_EchoEndpoint_ReturnsJsonWhenNoBody()
    {
        await using var server = new DemoServer();
        await server.StartAsync();

        using var client = new System.Net.Http.HttpClient();
        var response = await client.GetAsync($"http://localhost:{server.Port}/echo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("method");
    }

    [Fact]
    public async Task DemoServer_EchoEndpoint_ReflectsBodyOnPost()
    {
        await using var server = new DemoServer();
        await server.StartAsync();

        using var client = new System.Net.Http.HttpClient();
        var json = "{\"hello\":\"world\"}";
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"http://localhost:{server.Port}/echo", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be(json);
    }

    [Fact]
    public async Task DemoServer_StatusEndpoint_ReturnsServerInfo()
    {
        await using var server = new DemoServer();
        await server.StartAsync();

        using var client = new System.Net.Http.HttpClient();
        var response = await client.GetAsync($"http://localhost:{server.Port}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Arbor.HttpClient Demo Server");
    }

    [Fact]
    public async Task DemoServer_StartAsync_IsNoOp_WhenAlreadyRunning()
    {
        await using var server = new DemoServer();
        await server.StartAsync();
        var portAfterFirst = server.Port;

        await server.StartAsync(); // second call should be a no-op

        server.IsRunning.Should().BeTrue();
        server.Port.Should().Be(portAfterFirst);
    }

    // ── Collection request loading — WS / SSE type detection ────────────────

    [Fact]
    public async Task LoadCollectionRequest_WithWsMethod_SetsWebSocketRequestType()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            await collectionRepo.SaveAsync(
                "WS Test",
                null,
                $"http://localhost:{DemoServer.DefaultPort}",
                [new CollectionRequest("WS echo", "WS", "/ws", null)]);

            var viewModel = CreateViewModel(collectionRepository: collectionRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            var wsCollection = viewModel.Collections.First(c => c.Name == "WS Test");
            viewModel.SelectedCollection = null;
            viewModel.SelectedCollection = wsCollection;

            var wsItem = viewModel.FilteredCollectionItems.First(i => i.Method == "WS");
            viewModel.LoadCollectionRequestCore(wsItem);

            viewModel.RequestEditor.SelectedRequestType.Should().Be(RequestType.WebSocket);
            viewModel.RequestEditor.RequestUrl.Should().StartWith("ws://");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task LoadCollectionRequest_WithSseMethod_SetsSseRequestType()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            await collectionRepo.SaveAsync(
                "SSE Test",
                null,
                $"http://localhost:{DemoServer.DefaultPort}",
                [new CollectionRequest("SSE stream", "SSE", "/sse", null)]);

            var viewModel = CreateViewModel(collectionRepository: collectionRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            var sseCollection = viewModel.Collections.First(c => c.Name == "SSE Test");
            viewModel.SelectedCollection = null;
            viewModel.SelectedCollection = sseCollection;

            var sseItem = viewModel.FilteredCollectionItems.First(i => i.Method == "SSE");
            viewModel.LoadCollectionRequestCore(sseItem);

            viewModel.RequestEditor.SelectedRequestType.Should().Be(RequestType.Sse);

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task LoadCollectionRequest_WithGetMethod_LeavesHttpRequestType()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            await collectionRepo.SaveAsync(
                "HTTP Test",
                null,
                $"http://localhost:{DemoServer.DefaultPort}",
                [new CollectionRequest("Echo GET", "GET", "/echo", null)]);

            var viewModel = CreateViewModel(collectionRepository: collectionRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            var httpCollection = viewModel.Collections.First(c => c.Name == "HTTP Test");
            viewModel.SelectedCollection = null;
            viewModel.SelectedCollection = httpCollection;

            var getItem = viewModel.FilteredCollectionItems.First(i => i.Method == "GET");
            viewModel.LoadCollectionRequestCore(getItem);

            viewModel.RequestEditor.SelectedRequestType.Should().Be(RequestType.Http);
            viewModel.RequestEditor.SelectedMethod.Should().Be("GET");

            return true;
        }, CancellationToken.None);
    }

    // ── Demo server banner visibility ────────────────────────────────────────

    [Fact]
    public async Task LoadCollectionRequest_ShowsBanner_WhenDemoServerNotRunningAndUrlMatchesPort()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            await using var demoServer = new DemoServer();
            var collectionRepo = new InMemoryCollectionRepository();
            // Use a concrete URL so variable resolution is not required.
            await collectionRepo.SaveAsync(
                "Banner Test",
                null,
                $"http://localhost:{DemoServer.DefaultPort}",
                [new CollectionRequest("Echo GET", "GET", "/echo", null)]);

            var viewModel = CreateViewModel(
                collectionRepository: collectionRepo,
                demoServer: demoServer);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            var bannerCollection = viewModel.Collections.First(c => c.Name == "Banner Test");
            viewModel.SelectedCollection = null;
            viewModel.SelectedCollection = bannerCollection;

            var echoItem = viewModel.FilteredCollectionItems.First(i => i.Method == "GET");
            viewModel.LoadCollectionRequestCore(echoItem);

            viewModel.IsDemoServerBannerVisible.Should().BeTrue();

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task LoadCollectionRequest_HidesBanner_WhenDemoServerIsRunning()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            await using var demoServer = new DemoServer();
            await demoServer.StartAsync(DemoServer.DefaultPort);

            var collectionRepo = new InMemoryCollectionRepository();
            await collectionRepo.SaveAsync(
                "Banner Test",
                null,
                $"http://localhost:{DemoServer.DefaultPort}",
                [new CollectionRequest("Echo GET", "GET", "/echo", null)]);

            var viewModel = CreateViewModel(
                collectionRepository: collectionRepo,
                demoServer: demoServer);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            var bannerCollection = viewModel.Collections.First(c => c.Name == "Banner Test");
            viewModel.SelectedCollection = null;
            viewModel.SelectedCollection = bannerCollection;

            var echoItem = viewModel.FilteredCollectionItems.First(i => i.Method == "GET");
            viewModel.LoadCollectionRequestCore(echoItem);

            viewModel.IsDemoServerBannerVisible.Should().BeFalse();

            return true;
        }, CancellationToken.None);
    }

    // ── Demo data seeding ────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_SeedsLocalhostDemoCollection_OnFirstRun()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            var environmentRepo = new InMemoryEnvironmentRepository();
            var viewModel = CreateViewModel(
                collectionRepository: collectionRepo,
                environmentRepository: environmentRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            viewModel.Collections.Should().Contain(c => c.Name == "Localhost Demo");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task InitializeAsync_SeedsDemoEnvironment_OnFirstRun()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            var environmentRepo = new InMemoryEnvironmentRepository();
            var viewModel = CreateViewModel(
                collectionRepository: collectionRepo,
                environmentRepository: environmentRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            viewModel.Environments.Should().Contain(e => e.Name == "Demo (localhost)");
            var demoEnv = viewModel.Environments.First(e => e.Name == "Demo (localhost)");
            demoEnv.Variables.Should().Contain(v => v.Name == "baseUrl");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task InitializeAsync_DoesNotDuplicateDemo_WhenCalledTwice()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            var environmentRepo = new InMemoryEnvironmentRepository();
            var viewModel = CreateViewModel(
                collectionRepository: collectionRepo,
                environmentRepository: environmentRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();
            await viewModel.InitializeAsync();

            viewModel.Collections.Count(c => c.Name == "Localhost Demo").Should().Be(1);
            viewModel.Environments.Count(e => e.Name == "Demo (localhost)").Should().Be(1);

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task DemoCollection_ContainsEchoGetRequest()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            var viewModel = CreateViewModel(collectionRepository: collectionRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            var demoCollection = viewModel.Collections.First(c => c.Name == "Localhost Demo");
            demoCollection.Requests.Should().Contain(r => r.Method == "GET" && r.Path == "/echo");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task DemoCollection_ContainsWebSocketRequest()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            var viewModel = CreateViewModel(collectionRepository: collectionRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            var demoCollection = viewModel.Collections.First(c => c.Name == "Localhost Demo");
            demoCollection.Requests.Should().Contain(r => r.Method == "WS" && r.Path == "/ws");

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task DemoCollection_ContainsSseRequest()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var collectionRepo = new InMemoryCollectionRepository();
            var viewModel = CreateViewModel(collectionRepository: collectionRepo);
            using var _ = viewModel;

            await viewModel.InitializeAsync();

            var demoCollection = viewModel.Collections.First(c => c.Name == "Localhost Demo");
            demoCollection.Requests.Should().Contain(r => r.Method == "SSE" && r.Path == "/sse");

            return true;
        }, CancellationToken.None);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MainWindowViewModel CreateViewModel(
        ICollectionRepository? collectionRepository = null,
        IEnvironmentRepository? environmentRepository = null,
        DemoServer? demoServer = null)
    {
        var historyRepository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), historyRepository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        return new MainWindowViewModel(
            httpRequestService,
            historyRepository,
            collectionRepository ?? new InMemoryCollectionRepository(),
            environmentRepository ?? new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel,
            demoServer: demoServer);
    }

    private sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Desktop.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .LogToTrace();
    }
}
