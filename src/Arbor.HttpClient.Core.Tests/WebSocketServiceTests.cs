using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using AwesomeAssertions;

namespace Arbor.HttpClient.Core.Tests;

/// <summary>
/// Tests for <see cref="WebSocketService"/> covering the validation, state, and disposal
/// behaviours that can be exercised without a live WebSocket server.
/// </summary>
public class WebSocketServiceTests
{
    // ── IsConnected ───────────────────────────────────────────────────────────

    [Fact]
    public void IsConnected_WhenNeverConnected_ReturnsFalse()
    {
        using var service = new WebSocketService();
        service.IsConnected.Should().BeFalse();
    }

    // ── ConnectAsync – input validation ──────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_ShouldThrowArgumentNullException_WhenOnMessageIsNull()
    {
        using var service = new WebSocketService();
        var action = () => service.ConnectAsync("ws://example.com", onMessage: null!);
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ConnectAsync_ShouldThrowArgumentException_WhenUrlIsHttp()
    {
        using var service = new WebSocketService();
        var action = () => service.ConnectAsync("https://example.com", _ => { });
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ws://*");
    }

    [Fact]
    public async Task ConnectAsync_ShouldThrowArgumentException_WhenUrlIsRelative()
    {
        using var service = new WebSocketService();
        var action = () => service.ConnectAsync("/relative/path", _ => { });
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConnectAsync_ShouldThrowArgumentException_WhenUrlIsEmpty()
    {
        using var service = new WebSocketService();
        var action = () => service.ConnectAsync(string.Empty, _ => { });
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConnectAsync_ShouldThrowArgumentException_WhenUrlHasFtpScheme()
    {
        using var service = new WebSocketService();
        var action = () => service.ConnectAsync("ftp://example.com", _ => { });
        await action.Should().ThrowAsync<ArgumentException>();
    }

    // ── SendMessageAsync – validation ─────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_ShouldThrowInvalidOperationException_WhenNotConnected()
    {
        using var service = new WebSocketService();
        var action = () => service.SendMessageAsync("hello");
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not connected*");
    }

    // ── DisconnectAsync – no-op when not connected ────────────────────────────

    [Fact]
    public async Task DisconnectAsync_ShouldNotThrow_WhenNeverConnected()
    {
        using var service = new WebSocketService();
        var action = () => service.DisconnectAsync();
        await action.Should().NotThrowAsync();
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var service = new WebSocketService();
        var action = () => service.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        var service = new WebSocketService();
        service.Dispose();
        var action = () => service.Dispose();
        action.Should().NotThrow();
    }
}
