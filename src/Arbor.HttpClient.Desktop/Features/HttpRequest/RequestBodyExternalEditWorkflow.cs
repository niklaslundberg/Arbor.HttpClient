using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Owns the "open request body in external editor" pipeline: writes the current request
/// body to a temp file, watches it for changes made by the external editor, and re-applies
/// the updated content via <paramref name="applyBodyAsync"/> (passed to
/// <see cref="OpenInExternalEditorAsync"/>). Applying the content is the caller's
/// responsibility and must marshal to the UI thread if required.
/// </summary>
public sealed class RequestBodyExternalEditWorkflow : IDisposable
{
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _watcherCts;
    private Func<string, Task>? _applyBodyAsync;
    private int _readPending;

    /// <summary>
    /// Writes <paramref name="requestBody"/> to a new temp file, replaces any previous
    /// watcher, and starts watching the file for external edits. Returns the temp file path.
    /// </summary>
    public async Task<string> OpenInExternalEditorAsync(
        string requestBody,
        string? contentType,
        Func<string, Task> applyBodyAsync,
        Action<string> recordTempFile,
        CancellationToken cancellationToken = default)
    {
        if (_watcherCts is { } previousCts)
        {
            await previousCts.CancelAsync();
        }

        _watcherCts?.Dispose();
        _watcher?.Dispose();
        _watcher = null;

        _watcherCts = new CancellationTokenSource();
        _applyBodyAsync = applyBodyAsync;

        var ext = !string.IsNullOrEmpty(contentType)
            ? ResponseActionsViewModel.ExtensionFromContentType(contentType)
            : ResponseActionsViewModel.DetectExtensionFromContent(requestBody);
        var path = Path.Join(Path.GetTempPath(), $"arbor-request-{Guid.NewGuid():N}{ext}");
        await File.WriteAllTextAsync(path, requestBody, cancellationToken).ConfigureAwait(false);
        recordTempFile(path);

        var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnFileChanged;
        _watcher = watcher;

        return path;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) => HandleFileChanged(e);

    /// <summary>Exposed internally so the debounce/cancellation behavior can be unit-tested directly.</summary>
    internal void HandleFileChanged(FileSystemEventArgs e)
    {
        if (Interlocked.Exchange(ref _readPending, 1) == 1)
        {
            return;
        }

        var cancellationToken = CancellationToken.None;
        if (_watcherCts is { } watcherCts)
        {
            try
            {
                cancellationToken = watcherCts.Token;
            }
            catch (ObjectDisposedException)
            {
                // The watcher is being torn down; fall back to an uncancelable token so the
                // pending flag can still be reset by the background task.
            }
        }

        var applyBodyAsync = _applyBodyAsync;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                var content = await File.ReadAllTextAsync(e.FullPath, cancellationToken).ConfigureAwait(false);
                if (applyBodyAsync is { })
                {
                    await applyBodyAsync(content).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // Transient read errors while the editor is still writing, or task cancelled during shutdown
            }
            finally
            {
                Interlocked.Exchange(ref _readPending, 0);
            }
        });
    }

    /// <summary>Exposed internally for tests asserting the debounce flag resets.</summary>
    internal bool IsReadPending => _readPending == 1;

    public void Dispose()
    {
        _watcherCts?.Cancel();
        _watcherCts?.Dispose();
        _watcherCts = null;
        _watcher?.Dispose();
        _watcher = null;
    }
}
