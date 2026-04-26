using System.Net;
using System.Text;
using System.Text.Json;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Testing.Fakes;

namespace Arbor.HttpClient.Core.Tests;

public class GraphQlServiceTests
{
    // ── SendQueryAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendQueryAsync_ShouldPostQueryAsJson()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;

        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"__typename":"Query"}}""", Encoding.UTF8, "application/json")
            };
        });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "{ __typename }", null, null);

        var result = await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(200);
        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        capturedBody.Should().NotBeNull();

        using var doc = JsonDocument.Parse(capturedBody!);
        doc.RootElement.GetProperty("query").GetString().Should().Be("{ __typename }");
    }

    [Fact]
    public async Task SendQueryAsync_ShouldIncludeVariablesWhenProvided()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{}}""", Encoding.UTF8, "application/json")
            };
        });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "query Q($id: ID!) { node(id: $id) { id } }", """{"id":"1"}""", "Q");

        await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        doc.RootElement.GetProperty("operationName").GetString().Should().Be("Q");
        doc.RootElement.GetProperty("variables").GetProperty("id").GetString().Should().Be("1");
    }

    [Fact]
    public async Task SendQueryAsync_ShouldOmitNullOperationName()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{}}""", Encoding.UTF8, "application/json")
            };
        });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "{ __typename }", null, null);

        await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        // operationName should be absent (null) or set to null
        if (doc.RootElement.TryGetProperty("operationName", out var prop))
        {
            prop.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task SendQueryAsync_ShouldSendContentTypeApplicationJson()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{}}""", Encoding.UTF8, "application/json")
            };
        });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "{ __typename }", null, null);

        await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!.Content.Should().NotBeNull();
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendQueryAsync_ShouldSendCustomHeaders()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{}}""", Encoding.UTF8, "application/json")
            };
        });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        var headers = new[] { new RequestHeader("X-Api-Key", "my-key") };
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "{ __typename }", null, null, headers);

        await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!.Headers.Should().Contain(h => h.Key == "X-Api-Key" && h.Value.Contains("my-key"));
    }

    [Fact]
    public async Task SendQueryAsync_ShouldRejectNonHttpUrl()
    {
        var service = new GraphQlService(new global::System.Net.Http.HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var draft = new GraphQlDraft("file:///etc/passwd", "{ __typename }", null, null);
        var action = () => service.SendQueryAsync(draft);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendQueryAsync_ShouldThrowOnNullDraft()
    {
        var service = new GraphQlService(new global::System.Net.Http.HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var action = () => service.SendQueryAsync(null!);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendQueryAsync_ShouldHandleInvalidVariablesJson()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{}}""", Encoding.UTF8, "application/json")
            });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        // Invalid JSON for variables should not throw – service sends null variables instead
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "{ __typename }", "not-valid-json{{{", null);

        var result = await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(200);
    }

    // ── IntrospectSchemaAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task IntrospectSchemaAsync_ShouldReturnFormattedJson_WhenServerRespondsWithSchema()
    {
        var schemaResponse = """{"data":{"__schema":{"types":[{"name":"Query","kind":"OBJECT"}]}}}""";

        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(schemaResponse, Encoding.UTF8, "application/json")
            });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));

        var result = await service.IntrospectSchemaAsync("http://localhost:5000/graphql", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().NotBeNullOrWhiteSpace();
        // Result is pretty-printed JSON
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("__schema").Should().NotBeNull();
    }

    [Fact]
    public async Task IntrospectSchemaAsync_ShouldThrow_WhenServerReturnsNonSuccessStatus()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"errors":[{"message":"Unauthorized"}]}""", Encoding.UTF8, "application/json")
            });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));

        var action = () => service.IntrospectSchemaAsync("http://localhost:5000/graphql");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*401*");
    }

    [Fact]
    public async Task SendQueryAsync_ShouldFallBackToUtf8_WhenCharsetIsInvalid()
    {
        // Arrange: response carries an unrecognised charset; service should not throw
        var handler = new StubHttpMessageHandler(_ =>
        {
            var content = new System.Net.Http.ByteArrayContent(Encoding.UTF8.GetBytes("""{"data":{}}"""));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
            {
                CharSet = "not-a-real-charset"
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "{ __typename }", null, null);

        var result = await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(200);
        result.Body.Should().Contain("data");
    }

    [Fact]
    public async Task IntrospectSchemaAsync_ShouldReturnBodyAsIs_WhenResponseBodyIsNotJson()
    {
        // Arrange: server returns 200 with a plain-text body (not JSON)
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json-content", Encoding.UTF8, "text/plain")
            });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));

        var result = await service.IntrospectSchemaAsync("http://localhost:5000/graphql", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().Be("not-json-content");
    }

    [Fact]
    public async Task SendQueryAsync_ShouldSkipContentTypeHeaderFromCustomHeaders()
    {
        // Arrange: a custom Content-Type header should be ignored (service sets its own)
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{}}""", Encoding.UTF8, "application/json")
            };
        });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        var headers = new[] { new RequestHeader("Content-Type", "text/plain") };
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "{ __typename }", null, null, headers);

        await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        // The request's Content-Type should still be application/json (not text/plain)
        captured.Should().NotBeNull();
        captured!.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendQueryAsync_ShouldSkipDisabledHeaders()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{}}""", Encoding.UTF8, "application/json")
            };
        });

        var service = new GraphQlService(new global::System.Net.Http.HttpClient(handler));
        var headers = new[] { new RequestHeader("X-Disabled", "value", IsEnabled: false) };
        var draft = new GraphQlDraft("http://localhost:5000/graphql", "{ __typename }", null, null, headers);

        await service.SendQueryAsync(draft, TestContext.Current.CancellationToken);

        captured!.Headers.Should().NotContain(h => h.Key == "X-Disabled");
    }
}
