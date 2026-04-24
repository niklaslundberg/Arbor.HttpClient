using Arbor.HttpClient.Core.Models;
using AwesomeAssertions;

namespace Arbor.HttpClient.Core.Tests;

/// <summary>
/// Tests for the new protocol models introduced in UX idea 1.3:
/// <see cref="WebSocketMessage"/>, <see cref="SseEvent"/>, and <see cref="GraphQlDraft"/>.
/// </summary>
public class ProtocolModelTests
{
    // ── WebSocketMessage ──────────────────────────────────────────────────────

    [Fact]
    public void WebSocketMessage_ShouldExposeProperties()
    {
        var ts = DateTimeOffset.UtcNow;
        var msg = new WebSocketMessage("hello", WebSocketMessageDirection.Received, ts);

        msg.Content.Should().Be("hello");
        msg.Direction.Should().Be(WebSocketMessageDirection.Received);
        msg.Timestamp.Should().Be(ts);
    }

    [Fact]
    public void WebSocketMessage_SentDirection_ShouldBeRepresentable()
    {
        var msg = new WebSocketMessage("ping", WebSocketMessageDirection.Sent, DateTimeOffset.UtcNow);
        msg.Direction.Should().Be(WebSocketMessageDirection.Sent);
    }

    [Fact]
    public void WebSocketMessage_EqualityByValue_ShouldWork()
    {
        var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new WebSocketMessage("x", WebSocketMessageDirection.Received, ts);
        var b = new WebSocketMessage("x", WebSocketMessageDirection.Received, ts);

        a.Should().Be(b);
    }

    // ── SseEvent ──────────────────────────────────────────────────────────────

    [Fact]
    public void SseEvent_ShouldExposeProperties()
    {
        var ts = DateTimeOffset.UtcNow;
        var evt = new SseEvent("id1", "message", "payload data", ts);

        evt.Id.Should().Be("id1");
        evt.EventType.Should().Be("message");
        evt.Data.Should().Be("payload data");
        evt.Timestamp.Should().Be(ts);
    }

    [Fact]
    public void SseEvent_NullableFields_ShouldAllowNull()
    {
        var evt = new SseEvent(null, null, "data-only", DateTimeOffset.UtcNow);

        evt.Id.Should().BeNull();
        evt.EventType.Should().BeNull();
    }

    // ── GraphQlDraft ──────────────────────────────────────────────────────────

    [Fact]
    public void GraphQlDraft_ShouldExposeProperties()
    {
        var headers = new[] { new RequestHeader("X-Key", "val") };
        var draft = new GraphQlDraft("https://api.example.com", "{ __typename }", """{"id":"1"}""", "MyOp", headers);

        draft.Url.Should().Be("https://api.example.com");
        draft.Query.Should().Be("{ __typename }");
        draft.VariablesJson.Should().Be("""{"id":"1"}""");
        draft.OperationName.Should().Be("MyOp");
        draft.Headers.Should().BeEquivalentTo(headers);
    }

    [Fact]
    public void GraphQlDraft_DefaultHeaders_ShouldBeNull()
    {
        var draft = new GraphQlDraft("https://api.example.com", "{ __typename }", null, null);
        draft.Headers.Should().BeNull();
    }
}
