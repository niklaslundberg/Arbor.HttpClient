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
public class MainWindowCollectionsUiTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public async Task ShowCollectionsTabCommand_SwitchesToExplorerPanel_WhenAnotherPanelIsActive()
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

        // Now click Collections — should switch back to the Explorer (left-panel) dock
        viewModel.ShowCollectionsTabCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("left-panel");
        viewModel.LeftPanelTab.Should().Be("Collections");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ShowHistoryTabCommand_SwitchesToExplorerPanel_WhenAnotherPanelIsActive()
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

        // Now click History — should switch back to the Explorer (left-panel) dock
        viewModel.ShowHistoryTabCommand.Execute().Subscribe();

        leftToolDock.ActiveDockable?.Id.Should().Be("left-panel");
        viewModel.LeftPanelTab.Should().Be("History");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadHistoryRequestCommand_WithHistoryEntry_LoadsRequestIntoEditor()
    {
        var repository = new InMemoryRequestHistoryRepository();
        await repository.SaveAsync(new RequestHistoryEntry(
            Name: "History entry",
            Method: "POST",
            Url: "http://localhost:5000/api/items",
            Body: "{\"name\":\"item\"}",
            CreatedAtUtc: DateTimeOffset.UtcNow));

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

        await viewModel.InitializeAsync();

        var historyItem = viewModel.HistoryPanel.History.Should().ContainSingle().Subject;
        viewModel.HistoryPanel.LoadHistoryRequestCommand.Execute(historyItem).Subscribe();

        viewModel.RequestEditor.SelectedMethod.Should().Be("POST");
        viewModel.RequestEditor.RequestUrl.Should().Be("http://localhost:5000/api/items");
        viewModel.RequestEditor.RequestBody.Should().Be("{\"name\":\"item\"}");
        viewModel.RequestEditor.RequestName.Should().Be("History entry");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAsync_WhenNoCollectionSelected_SavesRequestToImplicitCollection()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "Implicit Save";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/implicit";
        viewModel.RequestEditor.SelectedMethod = "GET";
        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.FirstOrDefault(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));

        implicitCollection.Should().NotBeNull();
        implicitCollection!.Requests.Should().ContainSingle(request =>
            string.Equals(request.Name, "Implicit Save", StringComparison.Ordinal)
            && string.Equals(request.Method, "GET", StringComparison.Ordinal)
            && string.Equals(request.Path, "http://localhost:5000/implicit", StringComparison.Ordinal));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAsync_WhenRequestHasSensitiveHeaders_DoesNotPersistSensitiveHeadersInImplicitCollection()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "Sensitive Header Save";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/secure";
        viewModel.RequestEditor.SelectedMethod = "GET";
        viewModel.RequestEditor.RequestHeaders.Clear();
        viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
        {
            Name = "Authorization",
            Value = "******",
            IsEnabled = true
        });
        viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
        {
            Name = "X-Correlation-Id",
            Value = "abc-123",
            IsEnabled = true
        });
        viewModel.RequestEditor.RequestHeaders.Add(new RequestHeaderViewModel
        {
            Name = "X-Session-Token",
            Value = "token-value",
            IsEnabled = true
        });
        viewModel.RequestEditor.EnsurePlaceholderRows();

        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.First(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));
        var collectionRequest = implicitCollection.Requests.First(request =>
            string.Equals(request.Name, "Sensitive Header Save", StringComparison.Ordinal));

        collectionRequest.Headers.Should().ContainSingle(header =>
            string.Equals(header.Name, "X-Correlation-Id", StringComparison.Ordinal)
            && string.Equals(header.Value, "abc-123", StringComparison.Ordinal));
        collectionRequest.Headers.Should().NotContain(header =>
            string.Equals(header.Name, "Authorization", StringComparison.OrdinalIgnoreCase));
        collectionRequest.Headers.Should().NotContain(header =>
            string.Equals(header.Name, "X-Session-Token", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendGraphQlRequestAsync_SavesActualGraphQlPayloadInImplicitCollection()
    {
        await using var demoServer = new DemoServer();
        await demoServer.StartAsync();

        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "GraphQL implicit save";
        viewModel.RequestEditor.RequestUrl = $"http://localhost:{demoServer.Port}/echo";
        viewModel.RequestEditor.SelectedRequestType = RequestType.GraphQL;
        viewModel.GraphQlEditor.Query = "query Ping { ping }";
        viewModel.GraphQlEditor.VariablesJson = "{\"userId\":42}";
        viewModel.GraphQlEditor.OperationName = "Ping";

        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.First(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));
        var collectionRequest = implicitCollection.Requests.First(request =>
            string.Equals(request.Name, "GraphQL implicit save", StringComparison.Ordinal));

        collectionRequest.Method.Should().Be("POST");
        collectionRequest.ContentType.Should().Be("application/json");

        using var bodyDocument = JsonDocument.Parse(collectionRequest.Body!);
        bodyDocument.RootElement.GetProperty("query").GetString().Should().Be("query Ping { ping }");
        bodyDocument.RootElement.GetProperty("operationName").GetString().Should().Be("Ping");
        bodyDocument.RootElement.GetProperty("variables").GetProperty("userId").GetInt32().Should().Be(42);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendGraphQlRequestAsync_WithInvalidVariablesJson_SavesBodyWithNullVariables()
    {
        await using var demoServer = new DemoServer();
        await demoServer.StartAsync();

        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "GraphQL invalid vars";
        viewModel.RequestEditor.RequestUrl = $"http://localhost:{demoServer.Port}/echo";
        viewModel.RequestEditor.SelectedRequestType = RequestType.GraphQL;
        viewModel.GraphQlEditor.Query = "query Ping { ping }";
        viewModel.GraphQlEditor.VariablesJson = "{bad-json";
        viewModel.GraphQlEditor.OperationName = "Ping";

        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.First(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));
        var collectionRequest = implicitCollection.Requests.First(request =>
            string.Equals(request.Name, "GraphQL invalid vars", StringComparison.Ordinal));

        using var bodyDocument = JsonDocument.Parse(collectionRequest.Body!);
        bodyDocument.RootElement.GetProperty("variables").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SendRequestAsync_WhenImplicitCollectionIsSelected_RefreshesSelectedCollectionItems()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();

        viewModel.RequestEditor.RequestName = "Initial implicit request";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/implicit/one";
        viewModel.RequestEditor.SelectedMethod = "GET";
        await viewModel.SendRequestCommand.Execute();

        var implicitCollection = viewModel.Collections.First(collection =>
            string.Equals(collection.Name, "Implicit Requests", StringComparison.Ordinal));
        viewModel.SelectedCollection = implicitCollection;

        viewModel.RequestEditor.RequestName = "Second implicit request";
        viewModel.RequestEditor.RequestUrl = "http://localhost:5000/implicit/two";
        await viewModel.SendRequestCommand.Execute();

        viewModel.CollectionItems.Should().Contain(item =>
            string.Equals(item.Name, "Second implicit request", StringComparison.Ordinal));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SetCollectionSortByCommand_InTreeView_ChangesGroupOrder()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var collectionRepository = new InMemoryCollectionRepository();
        await collectionRepository.SaveAsync(
            "Sort Test",
            sourcePath: null,
            baseUrl: null,
            requests:
            [
                new CollectionRequest("Zulu", "GET", "/a/one", null),
                new CollectionRequest("Alpha", "GET", "/z/two", null)
            ]);

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        using var viewModel = new MainWindowViewModel(
            httpRequestService,
            repository,
            collectionRepository,
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);

        await viewModel.InitializeAsync();
        viewModel.SelectedCollection = viewModel.Collections.First(collection => collection.Name == "Sort Test");
        viewModel.IsCollectionTreeView = true;

        viewModel.CollectionGroups.Select(group => group.GroupKey).Should().Equal("a", "z");

        viewModel.SetCollectionSortByCommand.Execute("Name").Subscribe();

        viewModel.CollectionGroups.Select(group => group.GroupKey).Should().Equal("z", "a");
    }
}
