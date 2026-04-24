using System.Collections.ObjectModel;
using System.Net.Http;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Arbor.HttpClient.Desktop.ViewModels;

/// <summary>
/// Owns the Server-Sent Events connection lifecycle and the event log displayed in the
/// Response panel when the request type is <see cref="RequestType.Sse"/>.
/// </summary>
public sealed partial class SseViewModel : ViewModelBase, IDisposable
{
    private readonly SseService _service;
    private readonly ILogger _logger;
    private CancellationTokenSource? _connectionCts;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectButtonLabel))]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionError = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>All SSE events received in the current session, newest last.</summary>
    public ObservableCollection<SseEvent> Events { get; } = [];

    public string ConnectButtonLabel => IsConnected ? "Disconnect" : "Connect";

    public SseViewModel(global::System.Net.Http.HttpClient httpClient, ILogger logger)
    {
        _service = new SseService(httpClient);
        _logger = logger.ForContext<SseViewModel>();
    }

    /// <summary>Opens an SSE stream to the supplied URL with optional extra headers.</summary>
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
        StatusMessage = "Connecting…";
        Events.Clear();

        _connectionCts?.Dispose();
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        IsConnected = true;

        try
        {
            _logger.Information("SSE connecting to {Url}", url);
            StatusMessage = $"Connected — streaming events from {url}";

            await _service.ConnectAsync(
                url,
                evt => Avalonia.Threading.Dispatcher.UIThread.Post(() => Events.Add(evt)),
                headers,
                _connectionCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal disconnection
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SSE connection failed for {Url}", url);
            ConnectionError = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsConnected = false;
            StatusMessage = "Disconnected.";
            _logger.Information("SSE disconnected from {Url}", url);
        }
    }

    /// <summary>Cancels the active SSE stream.</summary>
    [RelayCommand]
    private void Disconnect()
    {
        _connectionCts?.Cancel();
        IsConnected = false;
    }

    /// <summary>Clears the event log.</summary>
    [RelayCommand]
    private void ClearEvents() => Events.Clear();

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
    }
}
