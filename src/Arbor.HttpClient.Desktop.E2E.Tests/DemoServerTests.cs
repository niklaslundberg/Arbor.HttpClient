using System.Net;
using Arbor.HttpClient.Desktop.Demo;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Avalonia.Threading;
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

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_IsNotRunning_ByDefault()
    {
        await using var server = new DemoServer();
        server.IsRunning.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_IsRunning_AfterStartAsync()
    {
        await using var server = new DemoServer();

        await server.StartAsync();

        server.IsRunning.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_IsNotRunning_AfterStopAsync()
    {
        await using var server = new DemoServer();
        await server.StartAsync();

        await server.StopAsync();

        server.IsRunning.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
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

    [AvaloniaFact(Timeout = 10_000)]
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

    [AvaloniaFact(Timeout = 10_000)]
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

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_DocsEndpoint_ReturnsMarkdownDocumentation()
    {
        await using var server = new DemoServer();
        await server.StartAsync();

        using var client = new System.Net.Http.HttpClient();
        using var response = await client.GetAsync($"http://localhost:{server.Port}/docs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/markdown");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("# Arbor.HttpClient Demo Server");
        body.Should().Contain("GET /status");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_DocsHtmlEndpoint_ReturnsHtmlDocumentation()
    {
        await using var server = new DemoServer();
        await server.StartAsync();

        using var client = new System.Net.Http.HttpClient();
        using var response = await client.GetAsync($"http://localhost:{server.Port}/docs.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<!doctype html>");
        body.Should().Contain("<h1>Arbor.HttpClient Demo Server</h1>");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_RootEndpoint_RedirectsToHtmlDocs()
    {
        await using var server = new DemoServer();
        await server.StartAsync();

        using var client = new System.Net.Http.HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync($"http://localhost:{server.Port}/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Be("/docs.html");
    }

    [AvaloniaFact(Timeout = 10_000)]
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

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_WithWsMethod_SetsWebSocketRequestType()
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
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.RequestEditor.SelectedRequestType.Should().Be(RequestType.WebSocket);
        viewModel.RequestEditor.RequestUrl.Should().StartWith("ws://");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_WithSseMethod_SetsSseRequestType()
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
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.RequestEditor.SelectedRequestType.Should().Be(RequestType.Sse);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_WithGetMethod_LeavesHttpRequestType()
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
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.RequestEditor.SelectedRequestType.Should().Be(RequestType.Http);
        viewModel.RequestEditor.SelectedMethod.Should().Be("GET");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_WhenRequestNotOpen_OpensNewTab()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        await collectionRepo.SaveAsync(
            "Tabs Test",
            null,
            $"http://localhost:{DemoServer.DefaultPort}",
            [new CollectionRequest("Echo GET", "GET", "/echo", null)]);

        var viewModel = CreateViewModel(collectionRepository: collectionRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();
        var initialTabCount = viewModel.RequestTabs.Count;

        var collection = viewModel.Collections.First(c => c.Name == "Tabs Test");
        viewModel.SelectedCollection = collection;
        var item = viewModel.FilteredCollectionItems.First();
        viewModel.LoadCollectionRequestCore(item);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.RequestTabs.Count.Should().Be(initialTabCount + 1);
        viewModel.ActiveRequestTab?.RequestEditor.RequestName.Should().Be("Echo GET");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_WhenRequestAlreadyOpen_ActivatesExistingTab()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        await collectionRepo.SaveAsync(
            "Tabs Test",
            null,
            $"http://localhost:{DemoServer.DefaultPort}",
            [new CollectionRequest("Echo GET", "GET", "/echo", null)]);

        var viewModel = CreateViewModel(collectionRepository: collectionRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();
        var collection = viewModel.Collections.First(c => c.Name == "Tabs Test");
        viewModel.SelectedCollection = collection;
        var item = viewModel.FilteredCollectionItems.First();

        viewModel.LoadCollectionRequestCore(item);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var firstOpenedTab = viewModel.ActiveRequestTab;
        var tabCountAfterFirstOpen = viewModel.RequestTabs.Count;

        viewModel.NewRequestTabCommand.Execute(null);
        viewModel.LoadCollectionRequestCore(item);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.RequestTabs.Count.Should().Be(tabCountAfterFirstOpen + 1);
        viewModel.ActiveRequestTab.Should().BeSameAs(firstOpenedTab);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_WithCollectionDefaultHeaders_InheritsAndResolvesVariables()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        var environmentRepo = new InMemoryEnvironmentRepository();

        await environmentRepo.SaveAsync(
            "Header Env",
            [new EnvironmentVariable("collectionToken", "token-from-env", IsEnabled: true)]);

        await collectionRepo.SaveAsync(
            "Header Defaults",
            null,
            $"http://localhost:{DemoServer.DefaultPort}",
            [new CollectionRequest("Echo GET", "GET", "/echo", null)],
            headers:
            [
                new RequestHeader("Authorization", "Bearer {{collectionToken}}")
            ]);

        var viewModel = CreateViewModel(
            collectionRepository: collectionRepo,
            environmentRepository: environmentRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();
        viewModel.ActiveEnvironment = viewModel.Environments.First(e => e.Name == "Header Env");

        var collection = viewModel.Collections.First(c => c.Name == "Header Defaults");
        viewModel.SelectedCollection = collection;

        var item = viewModel.FilteredCollectionItems.First();
        viewModel.LoadCollectionRequestCore(item);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.RequestEditor.RequestHeaders.Should().ContainSingle(h =>
            h.Name == "Authorization" && h.Value == "Bearer {{collectionToken}}");
        viewModel.RequestEditor.GetResolvedHeaders().Should().ContainSingle(h =>
            h.Name == "Authorization" && h.Value == "Bearer token-from-env");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_WhenRequestHeadersOverrideCollectionHeaders_RequestValuesWinAndDisabledHeaderOptsOut()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        var environmentRepo = new InMemoryEnvironmentRepository();

        await environmentRepo.SaveAsync(
            "Header Env",
            [
                new EnvironmentVariable("collectionToken", "token-from-collection", IsEnabled: true),
                    new EnvironmentVariable("requestToken", "token-from-request", IsEnabled: true),
                    new EnvironmentVariable("tenant", "northwind", IsEnabled: true)
            ]);

        await collectionRepo.SaveAsync(
            "Header Overrides",
            null,
            $"http://localhost:{DemoServer.DefaultPort}",
            [
                new CollectionRequest(
                        "Echo GET",
                        "GET",
                        "/echo",
                        null,
                        Headers:
                        [
                            new RequestHeader("Authorization", "Bearer {{requestToken}}"),
                            new RequestHeader("X-Tenant", "{{tenant}}", IsEnabled: false)
                        ])
            ],
            headers:
            [
                new RequestHeader("Authorization", "Bearer {{collectionToken}}"),
                    new RequestHeader("X-Tenant", "{{tenant}}")
            ]);

        var viewModel = CreateViewModel(
            collectionRepository: collectionRepo,
            environmentRepository: environmentRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();
        viewModel.ActiveEnvironment = viewModel.Environments.First(e => e.Name == "Header Env");

        var collection = viewModel.Collections.First(c => c.Name == "Header Overrides");
        viewModel.SelectedCollection = collection;

        var item = viewModel.FilteredCollectionItems.First();
        viewModel.LoadCollectionRequestCore(item);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.RequestEditor.RequestHeaders.Should().ContainSingle(h =>
            h.Name == "X-Tenant" && !h.IsEnabled);

        var resolvedHeaders = viewModel.RequestEditor.GetResolvedHeaders();
        resolvedHeaders.Should().ContainSingle(h =>
            h.Name == "Authorization" && h.Value == "Bearer token-from-request");
        resolvedHeaders.Should().NotContain(h => h.Name == "X-Tenant");
    }

    // ── Demo server banner visibility ────────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_ShowsBanner_WhenDemoServerNotRunningAndUrlMatchesPort()
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
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.IsDemoServerBannerVisible.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadCollectionRequest_HidesBanner_WhenDemoServerIsRunning()
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
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.IsDemoServerBannerVisible.Should().BeFalse();
    }

    // ── Demo data seeding ────────────────────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public async Task InitializeAsync_SeedsLocalhostDemoCollection_OnFirstRun()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        var environmentRepo = new InMemoryEnvironmentRepository();
        var viewModel = CreateViewModel(
            collectionRepository: collectionRepo,
            environmentRepository: environmentRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();

        viewModel.Collections.Should().Contain(c => c.Name == "Localhost Demo");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task InitializeAsync_SeedsDemoEnvironment_OnFirstRun()
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
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task InitializeAsync_DoesNotDuplicateDemo_WhenCalledTwice()
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
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoCollection_ContainsEchoGetRequest()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        var viewModel = CreateViewModel(collectionRepository: collectionRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();

        var demoCollection = viewModel.Collections.First(c => c.Name == "Localhost Demo");
        demoCollection.Requests.Should().Contain(r => r.Method == "GET" && r.Path == "/echo");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoCollection_DefaultStartSampleRequest_IsDocs()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        var viewModel = CreateViewModel(collectionRepository: collectionRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();

        var demoCollection = viewModel.Collections.First(c => c.Name == "Localhost Demo");
        demoCollection.Requests[0].Method.Should().Be("GET");
        demoCollection.Requests[0].Path.Should().Be("/docs.html");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoCollection_ContainsWebSocketRequest()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        var viewModel = CreateViewModel(collectionRepository: collectionRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();

        var demoCollection = viewModel.Collections.First(c => c.Name == "Localhost Demo");
        demoCollection.Requests.Should().Contain(r => r.Method == "WS" && r.Path == "/ws");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoCollection_ContainsSseRequest()
    {


        var collectionRepo = new InMemoryCollectionRepository();
        var viewModel = CreateViewModel(collectionRepository: collectionRepo);
        using var _ = viewModel;

        await viewModel.InitializeAsync();

        var demoCollection = viewModel.Collections.First(c => c.Name == "Localhost Demo");
        demoCollection.Requests.Should().Contain(r => r.Method == "SSE" && r.Path == "/sse");
    }

    // ── HTTPS support ────────────────────────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_IsRunning_AfterStartAsync_HttpsOnly()
    {
        await using var server = new DemoServer();

        await server.StartAsync(enableHttp: false, enableHttps: true);

        server.IsRunning.Should().BeTrue();
        server.IsHttpEnabled.Should().BeFalse();
        server.IsHttpsEnabled.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_IsRunning_AfterStartAsync_BothProtocols()
    {
        await using var server = new DemoServer();

        await server.StartAsync(enableHttp: true, enableHttps: true);

        server.IsRunning.Should().BeTrue();
        server.IsHttpEnabled.Should().BeTrue();
        server.IsHttpsEnabled.Should().BeTrue();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_StartAsync_IsNoOp_WhenBothDisabled()
    {
        await using var server = new DemoServer();

        await server.StartAsync(enableHttp: false, enableHttps: false);

        server.IsRunning.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_IsNotRunning_AfterStopAsync_WhenStartedHttps()
    {
        await using var server = new DemoServer();
        await server.StartAsync(enableHttp: false, enableHttps: true);

        await server.StopAsync();

        server.IsRunning.Should().BeFalse();
        server.IsHttpEnabled.Should().BeFalse();
        server.IsHttpsEnabled.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task DemoServer_HttpsEchoEndpoint_RespondsWith200_WhenCertValidationIgnored()
    {
        await using var server = new DemoServer();
        await server.StartAsync(enableHttp: false, enableHttps: true);

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        using var client = new System.Net.Http.HttpClient(handler);
        using var response = await client.GetAsync($"https://localhost:{server.HttpsPort}/echo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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


}

