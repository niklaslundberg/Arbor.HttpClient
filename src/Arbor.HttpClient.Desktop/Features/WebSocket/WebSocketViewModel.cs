using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Arbor.HttpClient.Desktop.Shared;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Serilog;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.WebSocket;

namespace Arbor.HttpClient.Desktop.Features.WebSocket;

/// <summary>
/// Owns the WebSocket connection lifecycle and the message log displayed in the
/// Response panel when the request type is <see cref="RequestType.WebSocket"/>.
/// </summary>
public sealed partial class WebSocketViewModel : ReactiveViewModelBase
{
    private readonly WebSocketService _service = new();
    private readonly ILogger _logger;
    private readonly Subject<WebSocketMessage> _messageReceivedSubject = new();
    private readonly Subject<Unit> _disconnectedSubject = new();
    private CancellationTokenSource? _connectionCts;
    private bool _disposed;

    [Reactive]
    private bool _isConnected;

    [Reactive]
    private string _messageToSend = string.Empty;

    [Reactive]
    private string _connectionError = string.Empty;

    private readonly ObservableAsPropertyHelper<string> _connectButtonLabel;

    /// <summary>All frames exchanged in the current session, newest last.</summary>
    public ObservableCollection<WebSocketMessage> Messages { get; } = [];
    public IObservable<WebSocketMessage> MessageReceivedObservable => _messageReceivedSubject.AsObservable();
    public IObservable<Unit> DisconnectedObservable => _disconnectedSubject.AsObservable();

    public string ConnectButtonLabel => _connectButtonLabel.Value;

    public WebSocketViewModel(ILogger logger)
    {
        _logger = logger.ForContext<WebSocketViewModel>();

        _connectButtonLabel = this
            .WhenAnyValue(viewModel => viewModel.IsConnected)
            .Select(connected => connected ? "Disconnect" : "Connect")
            .ToProperty(this, viewModel => viewModel.ConnectButtonLabel);

        MessageReceivedObservable
            .Subscribe(message => Dispatcher.UIThread.Post(() => Messages.Add(message)))
            .DisposeWith(Disposables);

        DisconnectedObservable
            .Subscribe(_ => Dispatcher.UIThread.Post(() => IsConnected = false))
            .DisposeWith(Disposables);
    }

    /// <summary>Connects to the supplied WebSocket URL with optional extra headers.</summary>
    public async Task ConnectAsync(
        string url,
        IReadOnlyList<RequestHeader>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        ConnectionError = string.Empty;
        Messages.Clear();

        _connectionCts?.Dispose();
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _logger.Information("WebSocket connecting to {Url}", url);

            await _service.ConnectAsync(
                url,
                msg => _messageReceivedSubject.OnNext(msg),
                onDisconnected: () => _disconnectedSubject.OnNext(Unit.Default),
                additionalHeaders: headers,
                cancellationToken: _connectionCts.Token).ConfigureAwait(false);

            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsConnected = true);
            _logger.Information("WebSocket connected to {Url}", url);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "WebSocket connection failed for {Url}", url);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ConnectionError = $"Connection failed: {ex.Message}";
                IsConnected = false;
            });
        }
    }

    /// <summary>Closes the WebSocket connection.</summary>
    [ReactiveCommand]
    private async Task DisconnectAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        try
        {
            await _service.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "WebSocket disconnect error");
        }
        finally
        {
            if (_connectionCts is { })
            {
                await _connectionCts.CancelAsync();
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                () => IsConnected = false);
        }
    }

    /// <summary>Sends the text in <see cref="MessageToSend"/> as a WebSocket text frame.</summary>
    [ReactiveCommand]
    private async Task SendMessageAsync()
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(MessageToSend))
        {
            return;
        }

        var text = MessageToSend;
        MessageToSend = string.Empty;

        try
        {
            await _service.SendMessageAsync(text).ConfigureAwait(false);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                Messages.Add(new WebSocketMessage(text, WebSocketMessageDirection.Sent, DateTimeOffset.UtcNow)));
            _logger.Information("WebSocket message sent ({Length} bytes)", text.Length);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "WebSocket send failed");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                () => ConnectionError = $"Send failed: {ex.Message}");
        }
    }

    /// <summary>Clears the message log.</summary>
    [ReactiveCommand]
    private void ClearMessages()
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Messages.Clear();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Messages.Clear());
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _messageReceivedSubject.OnCompleted();
            _disconnectedSubject.OnCompleted();
            _connectionCts?.Cancel();
            _connectionCts?.Dispose();
            _connectionCts = null;
            _service.Dispose();
        }

        base.Dispose(disposing);
    }
}
