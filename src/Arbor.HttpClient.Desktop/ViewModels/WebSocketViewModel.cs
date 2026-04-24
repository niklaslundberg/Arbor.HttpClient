using System.Collections.ObjectModel;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Arbor.HttpClient.Desktop.ViewModels;

/// <summary>
/// Owns the WebSocket connection lifecycle and the message log displayed in the
/// Response panel when the request type is <see cref="RequestType.WebSocket"/>.
/// </summary>
public sealed partial class WebSocketViewModel : ViewModelBase, IDisposable
{
    private readonly WebSocketService _service = new();
    private readonly ILogger _logger;
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

    public string ConnectButtonLabel => IsConnected ? "Disconnect" : "Connect";

    public WebSocketViewModel(ILogger logger)
    {
        _logger = logger.ForContext<WebSocketViewModel>();
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
                msg => Avalonia.Threading.Dispatcher.UIThread.Post(() => Messages.Add(msg)),
                () => Avalonia.Threading.Dispatcher.UIThread.Post(() => IsConnected = false),
                headers,
                _connectionCts.Token).ConfigureAwait(false);

            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsConnected = true);
            _logger.Information("WebSocket connected to {Url}", url);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "WebSocket connection failed for {Url}", url);
            ConnectionError = $"Connection failed: {ex.Message}";
            IsConnected = false;
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
            _connectionCts?.Cancel();
            IsConnected = false;
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
            ConnectionError = $"Send failed: {ex.Message}";
        }
    }

    /// <summary>Clears the message log.</summary>
    [RelayCommand]
    private void ClearMessages() => Messages.Clear();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionCts?.Cancel();
        _connectionCts?.Dispose();
        _connectionCts = null;
        _service.Dispose();
    }
}
