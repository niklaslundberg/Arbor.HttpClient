using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Arbor.HttpClient.Desktop.Shared;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.WebSocket;

namespace Arbor.HttpClient.Desktop.Features.WebSocket;

/// <summary>
/// Owns the WebSocket connection lifecycle and the message log displayed in the
/// Response panel when the request type is <see cref="RequestType.WebSocket"/>.
/// </summary>
public sealed partial class WebSocketViewModel : ViewModelBase, IDisposable
{
    private readonly WebSocketService _service = new();
    private readonly ILogger _logger;
    private readonly Subject<WebSocketMessage> _messageReceivedSubject = new();
    private readonly Subject<Unit> _disconnectedSubject = new();
    private readonly CompositeDisposable _subscriptions = new();
    private CancellationTokenSource? _connectionCts;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectButtonLabel))]
    private bool _isConnected;

    [ObservableProperty]
    private string _messageToSend = string.Empty;

    [ObservableProperty]
    private string _connectionError = string.Empty;

    /// <summary>All frames exchanged in the current session, newest last.</summary>
    public ObservableCollection<WebSocketMessage> Messages { get; } = [];
    public IObservable<WebSocketMessage> MessageReceivedObservable => _messageReceivedSubject.AsObservable();
    public IObservable<Unit> DisconnectedObservable => _disconnectedSubject.AsObservable();

    public string ConnectButtonLabel => IsConnected ? "Disconnect" : "Connect";

    public WebSocketViewModel(ILogger logger)
    {
        _logger = logger.ForContext<WebSocketViewModel>();

        _subscriptions.Add(MessageReceivedObservable.Subscribe(
            message => Dispatcher.UIThread.Post(() => Messages.Add(message))));

        _subscriptions.Add(DisconnectedObservable.Subscribe(
            _ => Dispatcher.UIThread.Post(() => IsConnected = false)));
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
    [RelayCommand]
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
    [RelayCommand]
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
    [RelayCommand]
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
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messageReceivedSubject.OnCompleted();
        _disconnectedSubject.OnCompleted();
        _subscriptions.Dispose();
        _connectionCts?.Cancel();
        _connectionCts?.Dispose();
        _connectionCts = null;
        _service.Dispose();
    }
}
