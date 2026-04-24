using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Testing.Fakes;
using AwesomeAssertions;

namespace Arbor.HttpClient.Core.Tests;

public class SseServiceTests
{
    // ── ParseSseStreamAsync (internal helper, tested directly for determinism) ─

    [Fact]
    public async Task ParseSseStreamAsync_ShouldDispatchSingleDataEvent()
    {
        var sseText = "data: hello world\n\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sseText)));

        var events = new List<SseEvent>();
        await SseService.ParseSseStreamAsync(reader, events.Add, CancellationToken.None);

        events.Should().ContainSingle();
        events[0].Data.Should().Be("hello world");
        events[0].Id.Should().BeNull();
        events[0].EventType.Should().BeNull();
    }

    [Fact]
    public async Task ParseSseStreamAsync_ShouldDispatchEventWithIdAndEventType()
    {
        var sseText = "id: 42\nevent: message\ndata: payload\n\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sseText)));

        var events = new List<SseEvent>();
        await SseService.ParseSseStreamAsync(reader, events.Add, CancellationToken.None);

        events.Should().ContainSingle();
        events[0].Id.Should().Be("42");
        events[0].EventType.Should().Be("message");
        events[0].Data.Should().Be("payload");
    }

    [Fact]
    public async Task ParseSseStreamAsync_ShouldConcatenateMultilineDataWithNewline()
    {
        var sseText = "data: line one\ndata: line two\n\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sseText)));

        var events = new List<SseEvent>();
        await SseService.ParseSseStreamAsync(reader, events.Add, CancellationToken.None);

        events.Should().ContainSingle();
        events[0].Data.Should().Be("line one\nline two");
    }

    [Fact]
    public async Task ParseSseStreamAsync_ShouldDispatchMultipleEvents()
    {
        var sseText = "data: first\n\ndata: second\n\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sseText)));

        var events = new List<SseEvent>();
        await SseService.ParseSseStreamAsync(reader, events.Add, CancellationToken.None);

        events.Should().HaveCount(2);
        events[0].Data.Should().Be("first");
        events[1].Data.Should().Be("second");
    }

    [Fact]
    public async Task ParseSseStreamAsync_ShouldIgnoreCommentLines()
    {
        var sseText = ": this is a comment\ndata: value\n\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sseText)));

        var events = new List<SseEvent>();
        await SseService.ParseSseStreamAsync(reader, events.Add, CancellationToken.None);

        events.Should().ContainSingle();
        events[0].Data.Should().Be("value");
    }

    [Fact]
    public async Task ParseSseStreamAsync_ShouldDispatchTrailingEventWithoutBlankLine()
    {
        // Some servers close the stream without a trailing blank line
        var sseText = "data: trailing";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sseText)));

        var events = new List<SseEvent>();
        await SseService.ParseSseStreamAsync(reader, events.Add, CancellationToken.None);

        events.Should().ContainSingle();
        events[0].Data.Should().Be("trailing");
    }

    [Fact]
    public async Task ParseSseStreamAsync_ShouldRespectCancellationToken()
    {
        // Build a large repeating SSE stream
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 100; i++)
        {
            sb.AppendLine($"data: event{i}");
            sb.AppendLine();
        }

        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())));
        using var cts = new CancellationTokenSource();
        var events = new List<SseEvent>();

        // Cancel after first event
        await SseService.ParseSseStreamAsync(
            reader,
            evt =>
            {
                events.Add(evt);
                cts.Cancel();
            },
            cts.Token);

        events.Should().ContainSingle();
    }

    [Fact]
    public async Task ParseSseStreamAsync_ShouldHandleEmptyStream()
    {
        using var reader = new StreamReader(new MemoryStream(Array.Empty<byte>()));
        var events = new List<SseEvent>();

        await SseService.ParseSseStreamAsync(reader, events.Add, CancellationToken.None);

        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseSseStreamAsync_ShouldTrimLeadingSpaceFromFieldValues()
    {
        var sseText = "data: trimmed\nid: myid\nevent: myevent\n\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(sseText)));

        var events = new List<SseEvent>();
        await SseService.ParseSseStreamAsync(reader, events.Add, CancellationToken.None);

        events.Should().ContainSingle();
        events[0].Data.Should().Be("trimmed");
        events[0].Id.Should().Be("myid");
        events[0].EventType.Should().Be("myevent");
    }

    // ── ConnectAsync (integration-style, using StubHttpMessageHandler) ────────

    [Fact]
    public async Task ConnectAsync_ShouldThrowOnNonHttpUrl()
    {
        var service = new SseService(new global::System.Net.Http.HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var action = () => service.ConnectAsync("ftp://example.com/stream", _ => { });

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConnectAsync_ShouldThrowWhenOnEventIsNull()
    {
        var service = new SseService(new global::System.Net.Http.HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var action = () => service.ConnectAsync("https://example.com/stream", null!);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ConnectAsync_ShouldStreamEventsFromHttpResponse()
    {
        var sseContent = "data: from-http\n\n";
        var responseBytes = Encoding.UTF8.GetBytes(sseContent);

        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseBytes)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return response;
        });

        var service = new SseService(new global::System.Net.Http.HttpClient(handler));
        var events = new List<SseEvent>();

        await service.ConnectAsync("https://example.com/stream", events.Add);

        events.Should().ContainSingle();
        events[0].Data.Should().Be("from-http");
    }

    [Fact]
    public async Task ConnectAsync_ShouldSetAcceptTextEventStreamHeader()
    {
        HttpRequestMessage? captured = null;

        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            };
        });

        var service = new SseService(new global::System.Net.Http.HttpClient(handler));
        await service.ConnectAsync("https://example.com/stream", _ => { });

        captured.Should().NotBeNull();
        captured!.Headers.Accept.Should().Contain(h => h.MediaType == "text/event-stream");
    }
}
