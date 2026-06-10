using System.Collections.ObjectModel;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Serilog;
using Arbor.HttpClient.Desktop.Shared;
using Avalonia.Threading;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.Sse;

namespace Arbor.HttpClient.Desktop.Features.Sse;

/// <summary>
/// Owns the Server-Sent Events connection lifecycle and the event log displayed in the
/// Response panel when the request type is <see cref="RequestType.Sse"/>.
/// </summary>
public sealed partial class SseViewModel : ReactiveViewModelBase
{
    private readonly SseService _service;
    private readonly ILogger _logger;
    private readonly Subject<SseEvent> _eventReceivedSubject = new();
    private CancellationTokenSource? _connectionCts;
    private bool _disposed;

    [Reactive]
    private bool _isConnected;

    [Reactive]
    private string _connectionError = string.Empty;

    [Reactive]
    private string _statusMessage = string.Empty;

    private readonly ObservableAsPropertyHelper<string> _connectButtonLabel;

    /// <summary>All SSE events received in the current session, newest last.</summary>
    public ObservableCollection<SseEvent> Events { get; } = [];
    public IObservable<SseEvent> EventReceivedObservable => _eventReceivedSubject.AsObservable();

    public string ConnectButtonLabel => _connectButtonLabel.Value;

    public SseViewModel(System.Net.Http.HttpClient httpClient, ILogger logger)
    {
        _service = new SseService(httpClient);
        _logger = logger.ForContext<SseViewModel>();

        _connectButtonLabel = this
            .WhenAnyValue(viewModel => viewModel.IsConnected)
            .Select(connected => connected ? "Disconnect" : "Connect")
            .ToProperty(this, viewModel => viewModel.ConnectButtonLabel);

        EventReceivedObservable
            .Subscribe(sseEvent => Dispatcher.UIThread.Post(() => Events.Add(sseEvent)))
            .DisposeWith(Disposables);
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
                evt => _eventReceivedSubject.OnNext(evt),
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
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                () => ConnectionError = $"Connection failed: {ex.Message}");
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsConnected = false;
                StatusMessage = "Disconnected.";
            });
            _logger.Information("SSE disconnected from {Url}", url);
        }
    }

    /// <summary>Cancels the active SSE stream.</summary>
    [ReactiveCommand]
    private void Disconnect()
    {
        _connectionCts?.Cancel();

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            IsConnected = false;
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsConnected = false);
        }
    }

    /// <summary>Clears the event log.</summary>
    [ReactiveCommand]
    private void ClearEvents()
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Events.Clear();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Events.Clear());
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _connectionCts?.Cancel();
            _connectionCts?.Dispose();
            _connectionCts = null;
            _eventReceivedSubject.OnCompleted();
        }

        base.Dispose(disposing);
    }
}
