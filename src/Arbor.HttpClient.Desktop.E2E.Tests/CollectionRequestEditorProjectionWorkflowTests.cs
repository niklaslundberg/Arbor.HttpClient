using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.Variables;
using Arbor.HttpClient.Desktop.Features.Collections;
using Arbor.HttpClient.Desktop.Features.HttpRequest;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class CollectionRequestEditorProjectionWorkflowTests
{
    private static readonly IReadOnlyList<EnvironmentVariable> NoVariables = [];

    private static readonly IReadOnlyList<string> ContentTypeOptions =
    [
        RequestEditorViewModel.NoneContentTypeOption,
        "application/json",
        "application/xml",
        "text/plain",
        "text/html",
        "application/x-www-form-urlencoded",
        "multipart/form-data",
        RequestEditorViewModel.CustomContentTypeOption
    ];

    private static CollectionItemViewModel Item(
        string name,
        string method,
        string path,
        string? body = null,
        string? contentType = null,
        string? notes = null,
        IReadOnlyList<RequestHeader>? headers = null) =>
        new(new CollectionRequest(name, method, path, null, Notes: notes, Body: body, ContentType: contentType, Headers: headers));

    private readonly CollectionRequestEditorProjectionWorkflow _workflow = new(new VariableResolver());

    [Theory]
    [InlineData("GET", RequestType.Http)]
    [InlineData("POST", RequestType.Http)]
    [InlineData("WS", RequestType.WebSocket)]
    [InlineData("WSS", RequestType.WebSocket)]
    [InlineData("SSE", RequestType.Sse)]
    public void ResolveRequestType_MapsMethodToRequestType(string method, RequestType expected)
    {
        CollectionRequestEditorProjectionWorkflow.ResolveRequestType(method).Should().Be(expected);
    }

    [Fact]
    public void BuildProjection_HttpRequestWithBaseUrl_ResolvesFullUrl()
    {
        var collection = new Collection(1, "My Collection", null, "http://localhost:5000", []);
        var item = Item("Get pet", "GET", "/pets/1");

        var projection = _workflow.BuildProjection(
            item,
            collection,
            activeEnvironment: null,
            NoVariables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.RequestType.Should().Be(RequestType.Http);
        projection.Method.Should().Be("GET");
        projection.ResolvedUrl.Should().Be("http://localhost:5000/pets/1");
        projection.Name.Should().Be("Get pet");
    }

    [Fact]
    public void BuildProjection_WebSocketRequest_RewritesSchemeToWs()
    {
        var collection = new Collection(1, "My Collection", null, "http://localhost:5000", []);
        var item = Item("Chat", "WS", "/chat");

        var projection = _workflow.BuildProjection(
            item,
            collection,
            activeEnvironment: null,
            NoVariables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.RequestType.Should().Be(RequestType.WebSocket);
        projection.Method.Should().BeNull();
        projection.ResolvedUrl.Should().Be("ws://localhost:5000/chat");
    }

    [Fact]
    public void BuildProjection_BaseUrlWithActiveEnvironment_ResolvesVariablesBeforeJoining()
    {
        var collection = new Collection(1, "My Collection", null, "{{baseUrl}}", []);
        var item = Item("Get pet", "GET", "/pets/1");
        var environment = new RequestEnvironment(1, "Local", []);
        var variables = new EnvironmentVariable[] { new("baseUrl", "http://localhost:6000") };

        var projection = _workflow.BuildProjection(
            item,
            collection,
            environment,
            variables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.ResolvedUrl.Should().Be("http://localhost:6000/pets/1");
    }

    [Fact]
    public void BuildProjection_NoCollectionHeaders_ReturnsRequestHeadersAsNonInherited()
    {
        var collection = new Collection(1, "My Collection", null, "http://localhost:5000", []);
        var item = Item("Get pet", "GET", "/pets/1", headers: [new RequestHeader("X-Request", "value")]);

        var projection = _workflow.BuildProjection(
            item,
            collection,
            activeEnvironment: null,
            NoVariables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.Headers.Should().ContainSingle()
            .Which.Should().Match<CollectionRequestHeaderProjection>(header =>
                header.Name == "X-Request" && header.Value == "value" && !header.IsInherited);
    }

    [Fact]
    public void BuildProjection_CollectionHeadersWithoutRequestOverride_MarksHeaderAsInherited()
    {
        var collection = new Collection(1, "My Collection", null, "http://localhost:5000", [],
            Headers: [new RequestHeader("X-Tenant", "acme")]);
        var item = Item("Get pet", "GET", "/pets/1");

        var projection = _workflow.BuildProjection(
            item,
            collection,
            activeEnvironment: null,
            NoVariables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.Headers.Should().ContainSingle()
            .Which.Should().Match<CollectionRequestHeaderProjection>(header =>
                header.Name == "X-Tenant" && header.Value == "acme" && header.IsInherited);
    }

    [Fact]
    public void BuildProjection_RequestHeaderOverridesInheritedHeader_NotMarkedInherited()
    {
        var collection = new Collection(1, "My Collection", null, "http://localhost:5000", [],
            Headers: [new RequestHeader("X-Tenant", "acme")]);
        var item = Item("Get pet", "GET", "/pets/1", headers: [new RequestHeader("X-Tenant", "override")]);

        var projection = _workflow.BuildProjection(
            item,
            collection,
            activeEnvironment: null,
            NoVariables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.Headers.Should().ContainSingle()
            .Which.Should().Match<CollectionRequestHeaderProjection>(header =>
                header.Name == "X-Tenant" && header.Value == "override" && !header.IsInherited);
    }

    [Theory]
    [InlineData(null, null, "{}", RequestEditorViewModel.NoneContentTypeOption, "")]
    [InlineData("application/json", "{\"a\":1}", "{\"a\":1}", "application/json", "")]
    [InlineData("application/custom+xml", "<a/>", "<a/>", RequestEditorViewModel.CustomContentTypeOption, "application/custom+xml")]
    public void BuildProjection_PostRequestContent_ProjectsContentTypeAndBody(
        string? contentType, string? body, string expectedBody, string expectedContentTypeOption, string expectedCustomContentType)
    {
        var collection = new Collection(1, "My Collection", null, "http://localhost:5000", []);
        var item = Item("Create pet", "POST", "/pets", body: body, contentType: contentType);

        var projection = _workflow.BuildProjection(
            item,
            collection,
            activeEnvironment: null,
            NoVariables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.Body.Should().Be(expectedBody);
        projection.SelectedContentTypeOption.Should().Be(expectedContentTypeOption);
        projection.CustomContentType.Should().Be(expectedCustomContentType);
    }

    [Fact]
    public void BuildProjection_GetRequestWithoutBody_LeavesBodyEmpty()
    {
        var collection = new Collection(1, "My Collection", null, "http://localhost:5000", []);
        var item = Item("Get pet", "GET", "/pets/1");

        var projection = _workflow.BuildProjection(
            item,
            collection,
            activeEnvironment: null,
            NoVariables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.Body.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://localhost:5999/echo", true, false, 5999, 5998, true)]
    [InlineData("https://localhost:5998/echo", true, false, 5999, 5998, true)]
    [InlineData("http://localhost:5999/echo", true, true, 5999, 5998, false)]
    [InlineData("http://localhost:5999/echo", false, false, 5999, 5998, false)]
    [InlineData("http://example.com/echo", true, false, 5999, 5998, false)]
    public void ShouldShowDemoServerBanner_EvaluatesUrlAndServerState(
        string resolvedUrl, bool hasDemoServer, bool isDemoServerRunning, int port, int httpsPort, bool expected)
    {
        CollectionRequestEditorProjectionWorkflow
            .ShouldShowDemoServerBanner(resolvedUrl, hasDemoServer, isDemoServerRunning, port, httpsPort)
            .Should().Be(expected);
    }

    [Fact]
    public void BuildProjection_RequestNotes_AreCopiedToProjection()
    {
        var collection = new Collection(1, "My Collection", null, "http://localhost:5000", []);
        var item = Item("Get pet", "GET", "/pets/1", notes: "Returns a pet by id");

        var projection = _workflow.BuildProjection(
            item,
            collection,
            activeEnvironment: null,
            NoVariables,
            ContentTypeOptions,
            hasDemoServer: false,
            isDemoServerRunning: false,
            demoServerPort: 0,
            demoServerHttpsPort: 0);

        projection.Notes.Should().Be("Returns a pet by id");
    }
}
