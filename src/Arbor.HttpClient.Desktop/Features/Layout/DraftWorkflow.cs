using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Owns the request-editor draft persistence pipeline: loading a pending crash-recovery
/// draft on startup, the periodic auto-save subscription, and clearing the persisted draft
/// file. Capturing/restoring editor state must happen on the UI thread and is the caller's
/// responsibility via the injected callbacks.
/// </summary>
public sealed class DraftWorkflow : IDisposable
{
    public static readonly TimeSpan AutoSaveInterval = TimeSpan.FromSeconds(30);

    private readonly DraftPersistenceService? _draftPersistenceService;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private IDisposable? _autoSaveSubscription;
    private RequestEditorSnapshot? _pendingDraft;

    public DraftWorkflow(DraftPersistenceService? draftPersistenceService, ILogger logger, IScheduler? scheduler = null)
    {
        _draftPersistenceService = draftPersistenceService;
        _logger = logger;
        _scheduler = scheduler ?? DefaultScheduler.Instance;
    }

    /// <summary>
    /// Loads a previously persisted draft, if any, and retains it pending restoration via
    /// <see cref="TakePendingDraftAsync"/>.
    /// </summary>
    public async Task<RequestEditorSnapshot?> LoadPendingDraftAsync(CancellationToken cancellationToken = default)
    {
        _pendingDraft = _draftPersistenceService is { } service
            ? await service.LoadDraftAsync(cancellationToken).ConfigureAwait(false)
            : null;
        return _pendingDraft;
    }

    /// <summary>
    /// Returns the pending draft for restoration (loading it from disk if it has not been
    /// loaded yet) and clears it so it is only consumed once.
    /// </summary>
    public async Task<RequestEditorSnapshot?> TakePendingDraftAsync(CancellationToken cancellationToken = default)
    {
        var draft = _pendingDraft
            ?? (_draftPersistenceService is { } service
                ? await service.LoadDraftAsync(cancellationToken).ConfigureAwait(false)
                : null);
        _pendingDraft = null;
        return draft;
    }

    /// <summary>Discards the pending draft and deletes the persisted draft file (best-effort).</summary>
    public void DiscardDraft()
    {
        _pendingDraft = null;
        ClearPersistedDraft();
    }

    /// <summary>Deletes the persisted draft file (best-effort), keeping any pending in-memory draft.</summary>
    public void ClearPersistedDraft() => _draftPersistenceService?.ClearDraft();

    /// <summary>
    /// Starts (or restarts) the periodic auto-save loop. <paramref name="saveTickAsync"/> runs on
    /// the configured scheduler and is responsible for capturing the editor state on the UI thread
    /// and persisting it via <see cref="SaveDraftAsync"/>. No-op when no draft store is configured.
    /// </summary>
    public void StartAutoSave(Func<Task> saveTickAsync)
    {
        StopAutoSave();
        if (_draftPersistenceService is null)
        {
            return;
        }

        _autoSaveSubscription = Observable.Interval(AutoSaveInterval, _scheduler)
            .Subscribe(_tick =>
            {
                _ = saveTickAsync();
            });
    }

    /// <summary>Stops the periodic auto-save loop, if running.</summary>
    public void StopAutoSave()
    {
        _autoSaveSubscription?.Dispose();
        _autoSaveSubscription = null;
    }

    /// <summary>Persists <paramref name="state"/>, logging and swallowing failures.</summary>
    public async Task SaveDraftAsync(RequestEditorSnapshot state, CancellationToken cancellationToken = default)
    {
        if (_draftPersistenceService is null)
        {
            return;
        }

        try
        {
            await _draftPersistenceService.SaveDraftAsync(state, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warning(ex, "Auto-save draft failed");
        }
    }

    public void Dispose() => StopAutoSave();
}
