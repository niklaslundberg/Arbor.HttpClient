using System.Net;
using System.Net.Http;
using System.Text;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using AwesomeAssertions;

namespace Arbor.HttpClient.Core.Tests;

public class HttpRequestServiceTests
{
    [Fact]
    public async Task SendAsync_ShouldReturnResponseAndPersistRequest()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("hello", Encoding.UTF8, "text/plain")
            });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository, new FakeTimeProvider(new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero)));

        var response = await service.SendAsync(new HttpRequestDraft("Test", "GET", "https://example.com", null));

        response.StatusCode.Should().Be(200);
        response.Body.Should().Be("hello");
        response.BodyBytes.Should().Equal(Encoding.UTF8.GetBytes("hello"));
        repository.Items.Should().ContainSingle();
        repository.Items[0].Name.Should().Be("Test");
        repository.Items[0].CreatedAtUtc.Should().Be(new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task SendAsync_ShouldRejectNonHttpUrls()
    {
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), new InMemoryRequestHistoryRepository());

        var action = () => service.SendAsync(new HttpRequestDraft("Invalid", "GET", "file:///etc/passwd", null));

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendAsync_ShouldSendContentTypeHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubMessageHandler(req =>
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
        await service.SendAsync(new HttpRequestDraft("Test", "POST", "https://example.com", "{}", headers));

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().NotBeNull();
        capturedRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendAsync_ShouldSendCustomRequestHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubMessageHandler(req =>
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
        await service.SendAsync(new HttpRequestDraft("Test", "GET", "https://example.com", null, headers));

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == "X-Api-Key" && h.Value.Contains("secret"));
    }

    [Fact]
    public async Task SendAsync_ShouldSkipDisabledHeaders()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubMessageHandler(req =>
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
        await service.SendAsync(new HttpRequestDraft("Test", "GET", "https://example.com", null, headers));

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().NotContain(h => h.Key == "X-Disabled");
    }

    [Fact]
    public async Task SendAsync_ShouldSetRequestHttpVersionWhenProvided()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent(string.Empty)
            };
        });

        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(handler), new InMemoryRequestHistoryRepository());

        await service.SendAsync(new HttpRequestDraft("Test", "GET", "https://example.com", null, HttpVersion: global::System.Net.HttpVersion.Version20));

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Version.Should().Be(global::System.Net.HttpVersion.Version20);
    }

    [Fact]
    public async Task SendAsync_ShouldUseConfiguredHttpClientFactory()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var fallbackHandler = new StubMessageHandler(_ => throw new InvalidOperationException("Fallback client should not be used"));
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(fallbackHandler), repository);

        var factoryHandler = new StubMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("from-factory")
            });

        service.SetHttpClientFactory(() => new global::System.Net.Http.HttpClient(factoryHandler));

        var response = await service.SendAsync(new HttpRequestDraft("Factory", "GET", "https://example.com", null));

        response.Body.Should().Be("from-factory");
        response.BodyBytes.Should().Equal(Encoding.UTF8.GetBytes("from-factory"));
    }

    [Fact]
    public async Task SendAsync_ShouldUseConfiguredHttpClientFactoryWithFollowRedirectOverride()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var fallbackHandler = new StubMessageHandler(_ => throw new InvalidOperationException("Fallback client should not be used"));
        var service = new HttpRequestService(new global::System.Net.Http.HttpClient(fallbackHandler), repository);

        var followHandler = new StubMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("follow")
            });

        var noFollowHandler = new StubMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                ReasonPhrase = "OK",
                Content = new StringContent("no-follow")
            });

        service.SetHttpClientFactory(followRedirects =>
            new global::System.Net.Http.HttpClient((followRedirects ?? true) ? followHandler : noFollowHandler));

        var followResponse = await service.SendAsync(new HttpRequestDraft("Factory", "GET", "https://example.com", null, FollowRedirects: true));
        var noFollowResponse = await service.SendAsync(new HttpRequestDraft("Factory", "GET", "https://example.com", null, FollowRedirects: false));

        followResponse.Body.Should().Be("follow");
        noFollowResponse.Body.Should().Be("no-follow");
    }

    [Fact]
    public async Task SendAsync_ShouldPublishHttpDiagnostics_WhenEnabled()
    {
        var handler = new StubMessageHandler(_ =>
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

        await service.SendAsync(new HttpRequestDraft("Diagnostics", "GET", "http://localhost:5000/test", null));

        diagnostics.Should().NotBeNull();
        diagnostics!.Method.Should().Be("GET");
        diagnostics.Url.Should().Be("http://localhost:5000/test");
        diagnostics.ResponseHttpVersion.Should().Be("2.0");
        diagnostics.DnsLookup.Should().NotBeNullOrWhiteSpace();
        diagnostics.ResponseHeadersMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        diagnostics.ResponseBodyMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        diagnostics.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    private sealed class InMemoryRequestHistoryRepository : IRequestHistoryRepository
    {
        public List<SavedRequest> Items { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default)
        {
            Items.Add(request);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SavedRequest>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SavedRequest>>(Items.Take(limit).ToList());
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
