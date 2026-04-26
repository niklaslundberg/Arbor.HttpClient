using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;

namespace Arbor.HttpClient.Core.Integration.Tests;

/// <summary>
/// System integration tests for <see cref="SseService"/> that run against a real
/// in-process Kestrel SSE endpoint, validating the full HTTP streaming path.
/// </summary>
[Collection("KestrelServer")]
public sealed class SseServiceIntegrationTests(KestrelServerFixture fixture)
{
    [Fact]
    public async Task ConnectAsync_WithRealKestrelServer_ReceivesBothEvents()
    {
        using var httpClient = new System.Net.Http.HttpClient();
        var service = new SseService(httpClient);
        var events = new List<SseEvent>();

        await service.ConnectAsync(fixture.SseUrl, events.Add);

        events.Should().HaveCount(2);
        events[0].Data.Should().Be("event1");
        events[1].Data.Should().Be("event2");
    }
}
