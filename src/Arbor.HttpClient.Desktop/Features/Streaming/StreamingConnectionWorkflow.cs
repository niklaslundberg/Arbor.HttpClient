using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Sse;
using Arbor.HttpClient.Desktop.Features.WebSocket;

namespace Arbor.HttpClient.Desktop.Features.Streaming;

/// <summary>
/// Coordinates connect/disconnect orchestration for streaming request types (WebSocket and SSE),
/// including cancellation token source lifecycle management.
/// </summary>
public sealed class StreamingConnectionWorkflow
{
    private readonly RequestEditorViewModel _requestEditor;
    private readonly WebSocketViewModel _webSocketViewModel;
    private readonly SseViewModel _sseViewModel;

    public StreamingConnectionWorkflow(
        RequestEditorViewModel requestEditor,
        WebSocketViewModel webSocketViewModel,
        SseViewModel sseViewModel)
    {
        _requestEditor = requestEditor;
        _webSocketViewModel = webSocketViewModel;
        _sseViewModel = sseViewModel;
    }

    public async Task<CancellationTokenSource?> ToggleWebSocketConnectionAsync(CancellationTokenSource? streamingCancellationTokenSource)
    {
        if (_webSocketViewModel.IsConnected)
        {
            await _webSocketViewModel.DisconnectCommand.ExecuteAsync(null);
            if (streamingCancellationTokenSource is { } existingCancellationTokenSource)
            {
                await existingCancellationTokenSource.CancelAsync();
                existingCancellationTokenSource.Dispose();
            }

            return null;
        }

        var url = _requestEditor.GetResolvedUrl();
        var headers = _requestEditor.GetResolvedHeaders();

        streamingCancellationTokenSource?.Dispose();
        var replacementCancellationTokenSource = new CancellationTokenSource();

        _ = _webSocketViewModel.ConnectAsync(url, headers, replacementCancellationTokenSource.Token);
        return replacementCancellationTokenSource;
    }

    public Task<CancellationTokenSource?> ToggleSseConnectionAsync(CancellationTokenSource? streamingCancellationTokenSource)
    {
        if (_sseViewModel.IsConnected)
        {
            _sseViewModel.DisconnectCommand.Execute(null);
            streamingCancellationTokenSource?.Cancel();
            streamingCancellationTokenSource?.Dispose();
            return Task.FromResult<CancellationTokenSource?>(null);
        }

        var url = _requestEditor.GetResolvedUrl();
        var headers = _requestEditor.GetResolvedHeaders();

        streamingCancellationTokenSource?.Dispose();
        var replacementCancellationTokenSource = new CancellationTokenSource();

        _ = _sseViewModel.ConnectAsync(url, headers, replacementCancellationTokenSource.Token);
        return Task.FromResult<CancellationTokenSource?>(replacementCancellationTokenSource);
    }
}
