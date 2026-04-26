using System.Net;
using System.Text;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Core.Tests;

public class HttpRequestServiceTests
{
    [Fact]
    public async Task SendAsync_ShouldReturnResponseAndPersistRequest()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("hello", Encoding.UTF8, "text/plain")
            });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository, new FakeTimeProvider(new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero)));

        var response = await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000", null), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(200);
        response.Body.Should().Be("hello");
        response.BodyBytes.Should().Equal(Encoding.UTF8.GetBytes("hello"));
        response.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        repository.Items.Should().ContainSingle();
        repository.Items[0].Name.Should().Be("Test");
        repository.Items[0].CreatedAtUtc.Should().Be(new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task SendAsync_ShouldRejectNonHttpUrls()
    {
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryRequestHistoryRepository());

        var action = () => service.SendAsync(new HttpRequestDraft("Invalid", "GET", "file:///etc/passwd", null));

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendAsync_ShouldSendContentTypeHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent(string.Empty)
            };
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        var headers = new[] { new RequestHeader("Content-Type", "application/json") };
        await service.SendAsync(new HttpRequestDraft("Test", "POST", "http://localhost:5000", "{}", headers), TestContext.Current.CancellationToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().NotBeNull();
        capturedRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendAsync_ShouldSendCustomRequestHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent(string.Empty)
            };
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        var headers = new[] { new RequestHeader("X-Api-Key", "secret") };
        await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000", null, headers), TestContext.Current.CancellationToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == "X-Api-Key" && h.Value.Contains("secret"));
    }

    [Fact]
    public async Task SendAsync_ShouldSkipDisabledHeaders()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent(string.Empty)
            };
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        var headers = new[] { new RequestHeader("X-Disabled", "value", IsEnabled: false) };
        await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000", null, headers), TestContext.Current.CancellationToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().NotContain(h => h.Key == "X-Disabled");
    }

    [Fact]
    public async Task SendAsync_ShouldSetRequestHttpVersionWhenProvided()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent(string.Empty)
            };
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000", null, HttpVersion: global::System.Net.HttpVersion.Version20), TestContext.Current.CancellationToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Version.Should().Be(global::System.Net.HttpVersion.Version20);
    }

    [Fact]
    public async Task SendAsync_ShouldUseConfiguredHttpClientFactory()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var fallbackHandler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Fallback client should not be used"));
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(fallbackHandler), repository);

        var factoryHandler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("from-factory")
            });

        service.SetHttpClientFactory(() => new global::System.Net.Http.HttpClient(factoryHandler));

        var response = await service.SendAsync(new HttpRequestDraft("Factory", "GET", "http://localhost:5000", null), TestContext.Current.CancellationToken);

        response.Body.Should().Be("from-factory");
        response.BodyBytes.Should().Equal(Encoding.UTF8.GetBytes("from-factory"));
    }

    [Fact]
    public async Task SendAsync_ShouldUseConfiguredHttpClientFactoryWithFollowRedirectOverride()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var fallbackHandler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Fallback client should not be used"));
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(fallbackHandler), repository);

        var followHandler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("follow")
            });

        var noFollowHandler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("no-follow")
            });

        service.SetHttpClientFactory(followRedirects =>
            new global::System.Net.Http.HttpClient((followRedirects ?? true) ? followHandler : noFollowHandler));

        var followResponse = await service.SendAsync(new HttpRequestDraft("Factory", "GET", "http://localhost:5000", null, FollowRedirects: true), TestContext.Current.CancellationToken);
        var noFollowResponse = await service.SendAsync(new HttpRequestDraft("Factory", "GET", "http://localhost:5000", null, FollowRedirects: false), TestContext.Current.CancellationToken);

        followResponse.Body.Should().Be("follow");
        noFollowResponse.Body.Should().Be("no-follow");
    }

    [Fact]
    public async Task SendAsync_ShouldPublishHttpDiagnostics_WhenEnabled()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = global::System.Net.HttpVersion.Version20,
                ReasonPhrase = "OK",
                Content = new StringContent("diagnostics")
            });
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());
        HttpRequestDiagnostics? diagnostics = null;
        service.SetHttpDiagnosticsObserver(entry => diagnostics = entry);
        service.SetHttpDiagnosticsEnabled(true);

        await service.SendAsync(new HttpRequestDraft("Diagnostics", "GET", "http://localhost:5000/test", null), TestContext.Current.CancellationToken);

        diagnostics.Should().NotBeNull();
        diagnostics!.Method.Should().Be("GET");
        diagnostics.Url.Should().Be("http://localhost:5000/test");
        diagnostics.ResponseHttpVersion.Should().Be("2.0");
        diagnostics.DnsLookup.Should().NotBeNullOrWhiteSpace();
        diagnostics.ResponseHeadersMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        diagnostics.ResponseBodyMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        diagnostics.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task SendAsync_ShouldNotPublishDiagnostics_WhenDisabled()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("test")
            });
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());
        HttpRequestDiagnostics? diagnostics = null;
        service.SetHttpDiagnosticsObserver(entry => diagnostics = entry);
        service.SetHttpDiagnosticsEnabled(false);

        await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000/test", null), TestContext.Current.CancellationToken);

        diagnostics.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ShouldRejectEmptyMethod()
    {
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryRequestHistoryRepository());

        var action = () => service.SendAsync(new HttpRequestDraft("Test", "", "http://localhost:5000", null));

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*HTTP method is required*");
    }

    [Fact]
    public async Task SendAsync_ShouldRejectNullMethod()
    {
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryRequestHistoryRepository());

        var action = () => service.SendAsync(new HttpRequestDraft("Test", null!, "http://localhost:5000", null));

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*HTTP method is required*");
    }

    [Fact]
    public async Task SendAsync_ShouldHandleResponseWithCustomCharset()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("test", Encoding.UTF8)
            };
            response.Content.Headers.ContentType!.CharSet = "utf-8";
            return response;
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        var response = await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000", null), TestContext.Current.CancellationToken);

        response.Body.Should().Be("test");
    }

    [Fact]
    public async Task SendAsync_ShouldHandleInvalidCharset()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("test", Encoding.UTF8)
            };
            response.Content.Headers.ContentType!.CharSet = "invalid-charset-name";
            return response;
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        var response = await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000", null), TestContext.Current.CancellationToken);

        response.Body.Should().Be("test");
    }

    [Fact]
    public async Task SendAsync_ShouldSkipHeadersWithEmptyName()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent(string.Empty)
            };
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        var headers = new[] { new RequestHeader("", "value") };
        await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000", null, headers), TestContext.Current.CancellationToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Count().Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_ShouldIncludeResponseHeaders()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("test")
            };
            response.Headers.Add("X-Custom-Header", "custom-value");
            return response;
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        var response = await service.SendAsync(new HttpRequestDraft("Test", "GET", "http://localhost:5000", null), TestContext.Current.CancellationToken);

        response.Headers.Should().Contain(h => h.Name == "X-Custom-Header" && h.Value == "custom-value");
    }

    [Fact]
    public void SetHttpClientFactory_ShouldThrowOnNull()
    {
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryRequestHistoryRepository());

        var action = () => service.SetHttpClientFactory((Func<global::System.Net.Http.HttpClient>)null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetHttpClientFactoryWithRedirectOverride_ShouldThrowOnNull()
    {
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryRequestHistoryRepository());

        var action = () => service.SetHttpClientFactory((Func<bool?, global::System.Net.Http.HttpClient>)null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetHttpDiagnosticsObserver_ShouldThrowOnNull()
    {
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryRequestHistoryRepository());

        var action = () => service.SetHttpDiagnosticsObserver(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_ShouldUseRequestNameInHistory_WhenProvided()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("test")
            });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);

        await service.SendAsync(new HttpRequestDraft("My Custom Request", "GET", "http://localhost:5000/path", null), TestContext.Current.CancellationToken);

        repository.Items.Should().ContainSingle();
        repository.Items[0].Name.Should().Be("My Custom Request");
    }

    [Fact]
    public async Task SendAsync_ShouldUseUrlAsName_WhenNameIsEmpty()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("test")
            });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository);

        await service.SendAsync(new HttpRequestDraft("", "GET", "http://localhost:5000/path", null), TestContext.Current.CancellationToken);

        repository.Items.Should().ContainSingle();
        repository.Items[0].Name.Should().Be("http://localhost:5000/path");
    }
}
