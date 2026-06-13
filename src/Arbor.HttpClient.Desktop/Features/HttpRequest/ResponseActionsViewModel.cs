using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Shared;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Encapsulates the response-action logic: copy body to clipboard, save body as file,
/// open body in an external editor, and copy the current or history request as a <c>curl</c> command.
/// Depends on <see cref="IResponseActionsContext"/> for all response state; owns no observable
/// state of its own so it can be tested without the full <c>MainWindowViewModel</c>.
/// <para>
/// <c>MainWindowViewModel</c> composes this class and exposes it via the <c>ResponseActions</c>
/// property; XAML bindings target <c>App.ResponseActions.*Command</c> directly.
/// </para>
/// </summary>
public sealed partial class ResponseActionsViewModel : ReactiveViewModelBase
{
    // Tab index constants mirror the tab order in ResponseView.axaml.
    private const int ResponseBodyTabIndex = 0;
    private const int ResponseBodyRawTabIndex = 1;
    private const int ResponseHeadersTabIndex = 2;
    private const int ResponseRawTabIndex = 3;
    private const int ResponseWebViewTabIndex = 4;

    private readonly IResponseActionsContext _context;

    public ResponseActionsViewModel(IResponseActionsContext context)
    {
        _context = context;
    }

    // ── Action methods (called by MainWindowViewModel relay commands) ──────────

    /// <summary>
    /// Copies the current (pretty-printed) response body text to the clipboard.
    /// No-op when the clipboard is unavailable or the response body is empty.
    /// </summary>
    [ReactiveCommand]
    private async Task CopyResponseBodyAsync()
    {
        if (_context.Clipboard is null || string.IsNullOrEmpty(_context.ResponseBody))
        {
            return;
        }

        await _context.Clipboard.SetTextAsync(_context.ResponseBody);
    }

    /// <summary>
    /// Opens a save-file dialog and writes the currently selected response tab content to the chosen path.
    /// The suggested file extension is derived from the response <c>Content-Type</c> when applicable.
    /// No-op when the storage provider is unavailable or the selected tab has no saveable text content.
    /// </summary>
    [ReactiveCommand]
    private async Task SaveResponseBodyAsFileAsync(CancellationToken cancellationToken)
    {
        if (_context.StorageProvider is null || !TryGetSaveableResponseContent(out var contentToSave, out var extension))
        {
            return;
        }

        var suggestedStartLocation = await GetResponseSaveSuggestedStartLocationAsync();
        var file = await _context.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Response",
            SuggestedFileName = BuildResponseSaveFileName(extension),
            SuggestedStartLocation = suggestedStartLocation,
            FileTypeChoices =
            [
                new FilePickerFileType("Response file")
                {
                    Patterns = [$"*{extension}"]
                }
            ]
        });

        if (file is null)
        {
            return;
        }

        await File.WriteAllTextAsync(file.Path.LocalPath, contentToSave, Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    /// Writes the current response body to a temporary file and opens it in the default
    /// external editor. The file is registered for cleanup on application exit.
    /// </summary>
    [ReactiveCommand]
    private async Task OpenResponseBodyInExternalEditorAsync(CancellationToken cancellationToken)
    {
        var ext = !string.IsNullOrEmpty(_context.ResponseContentType)
            ? ExtensionFromContentType(_context.ResponseContentType)
            : DetectExtensionFromContent(_context.ResponseBody);
        var path = Path.Join(Path.GetTempPath(), $"arbor-response-{Guid.NewGuid():N}{ext}");
        await File.WriteAllTextAsync(path, _context.ResponseBody, cancellationToken).ConfigureAwait(false);
        _context.RecordTempFile(path);
        OpenWithShell(path);
    }

    /// <summary>
    /// Opens a save-file dialog, writes the binary response bytes to the chosen path, and
    /// then opens the saved file in the default associated application.
    /// No-op when the last response was not binary, no bytes are available, or the storage
    /// provider is unavailable.
    /// </summary>
    [ReactiveCommand]
    private async Task SaveBinaryResponseAndOpenAsync(CancellationToken cancellationToken)
    {
        var bytes = _context.GetLastResponseBodyBytes();
        if (!_context.IsBinaryResponse || bytes.Length == 0 || _context.StorageProvider is null)
        {
            return;
        }

        var extension = ExtensionFromContentType(_context.ResponseContentType);
        var suggestedStartLocation = await GetResponseSaveSuggestedStartLocationAsync();
        var file = await _context.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Response",
            SuggestedFileName = BuildResponseSaveFileName(extension),
            SuggestedStartLocation = suggestedStartLocation,
            FileTypeChoices =
            [
                new FilePickerFileType("Response file")
                {
                    Patterns = [$"*{extension}"]
                }
            ]
        });

        if (file is null)
        {
            return;
        }

        await File.WriteAllBytesAsync(file.Path.LocalPath, bytes, cancellationToken);
        OpenWithShell(file.Path.LocalPath);
    }

    /// <summary>
    /// Copies the given history item to the clipboard formatted as a single-line
    /// <c>curl</c> command.
    /// No-op when the clipboard or request is unavailable.
    /// </summary>
    [ReactiveCommand]
    private async Task CopyHistoryItemAsCurlAsync(RequestHistoryEntry? request)
    {
        if (request is null || _context.Clipboard is null)
        {
            return;
        }

        var command = CurlFormatter.Format(request);
        await _context.Clipboard.SetTextAsync(command);
    }

    /// <summary>
    /// Copies the current request (as configured in the request editor) to the
    /// clipboard formatted as a single-line <c>curl</c> command.
    /// No-op when the clipboard is unavailable.
    /// </summary>
    [ReactiveCommand]
    private async Task CopyCurrentRequestAsCurlAsync()
    {
        if (_context.Clipboard is null)
        {
            return;
        }

        var resolvedRequest = _context.BuildResolvedHttpRequestDraft();
        var command = CurlFormatter.Format(resolvedRequest.Method, resolvedRequest.Url, resolvedRequest.Body, resolvedRequest.Headers);
        await _context.Clipboard.SetTextAsync(command);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Determines the content and file extension to save based on the currently selected response tab.
    /// Returns <see langword="false"/> when the selected tab has no saveable content.
    /// </summary>
    internal bool TryGetSaveableResponseContent(out string content, out string extension)
    {
        content = string.Empty;
        extension = ".txt";

        if (_context.SelectedResponseTabIndex == ResponseWebViewTabIndex)
        {
            return false;
        }

        if (_context.SelectedResponseTabIndex == ResponseHeadersTabIndex)
        {
            if (_context.ResponseHeaders.Count == 0)
            {
                return false;
            }

            content = string.Join(Environment.NewLine, _context.ResponseHeaders);
            extension = ".txt";
            return true;
        }

        if (_context.SelectedResponseTabIndex == ResponseRawTabIndex)
        {
            if (string.IsNullOrEmpty(_context.ResponseRawText))
            {
                return false;
            }

            content = _context.ResponseRawText;
            extension = !string.IsNullOrWhiteSpace(_context.ResponseContentType)
                ? ExtensionFromContentType(_context.ResponseContentType)
                : DetectExtensionFromContent(_context.RawResponseBody);
            return true;
        }

        if (_context.SelectedResponseTabIndex == ResponseBodyTabIndex && !string.IsNullOrEmpty(_context.ResponseBody))
        {
            content = _context.ResponseBody;
            extension = !string.IsNullOrWhiteSpace(_context.ResponseContentType)
                ? ExtensionFromContentType(_context.ResponseContentType)
                : DetectExtensionFromContent(_context.ResponseBody);
            return true;
        }

        if (_context.SelectedResponseTabIndex == ResponseBodyRawTabIndex && string.IsNullOrEmpty(_context.RawResponseBody))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(_context.RawResponseBody))
        {
            content = _context.RawResponseBody;
            extension = !string.IsNullOrWhiteSpace(_context.ResponseContentType)
                ? ExtensionFromContentType(_context.ResponseContentType)
                : DetectExtensionFromContent(_context.RawResponseBody);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Maps a <c>Content-Type</c> media-type string to a file extension.
    /// Falls back to <c>.txt</c> for unrecognised types.
    /// </summary>
    internal static string ExtensionFromContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return ".txt";
        }

        var mediaType = HttpContentTypeHelper.NormalizeMediaType(contentType);
        return mediaType switch
        {
            "application/json" => ".json",
            "application/xml" or "text/xml" => ".xml",
            "text/html" => ".html",
            "text/markdown" => ".md",
            "application/pdf" => ".pdf",
            "application/zip" => ".zip",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            _ when HttpContentTypeHelper.IsJsonMediaType(mediaType) => ".json",
            _ when HttpContentTypeHelper.IsXmlMediaType(mediaType) => ".xml",
            _ => ".txt"
        };
    }

    /// <summary>
    /// Heuristically detects a file extension from the content string by inspecting its first character.
    /// Falls back to <c>.txt</c>.
    /// </summary>
    internal static string DetectExtensionFromContent(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return ".json";
        }

        if (trimmed.StartsWith('<'))
        {
            return ".xml";
        }

        return ".txt";
    }

    private async Task<IStorageFolder?> GetResponseSaveSuggestedStartLocationAsync()
    {
        if (_context.StorageProvider is null || string.IsNullOrWhiteSpace(_context.ResponseSaveDefaultFolder))
        {
            return null;
        }

        return await _context.StorageProvider.TryGetFolderFromPathAsync(_context.ResponseSaveDefaultFolder);
    }

    private string BuildResponseSaveFileName(string extension)
    {
        var requestPath = "root";

        var resolvedUrl = _context.RequestEditorResolvedUrl;
        if (Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri))
        {
            var absolutePath = uri.AbsolutePath.Trim('/');
            requestPath = string.IsNullOrWhiteSpace(absolutePath) ? "root" : absolutePath.Replace('/', '-');
        }

        var collectionName = string.IsNullOrEmpty(_context.SelectedCollectionName)
            ? "NoCollection"
            : _context.SelectedCollectionName;
        var requestName = string.IsNullOrWhiteSpace(_context.RequestEditorRequestName)
            ? "request"
            : _context.RequestEditorRequestName;
        var pattern = string.IsNullOrWhiteSpace(_context.ResponseSaveFileNamePattern)
            ? ResponseSaveFileNamePatternFormatter.DefaultPattern
            : _context.ResponseSaveFileNamePattern;

        if (ResponseSaveFileNamePatternFormatter.TryFormat(
                pattern,
                collectionName,
                requestPath,
                requestName,
                extension,
                DateTimeOffset.UtcNow,
                out var fileName,
                out var validationError))
        {
            _context.SetResponseSaveFileNamePatternValidationError(string.Empty);
            return fileName;
        }

        _context.SetResponseSaveFileNamePatternValidationError(validationError);

        // DefaultPattern is a compile-time constant guaranteed to pass validation, so this
        // fallback path only fails when TryFormat itself has a logic defect.
        if (!ResponseSaveFileNamePatternFormatter.TryFormat(
                ResponseSaveFileNamePatternFormatter.DefaultPattern,
                collectionName,
                requestPath,
                requestName,
                extension,
                DateTimeOffset.UtcNow,
                out var defaultFileName,
                out _))
        {
            return string.Empty;
        }

        return defaultFileName;
    }

    internal static void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException or PlatformNotSupportedException)
        {
            // No associated application or platform does not support shell execution — silently ignore.
        }
    }
}
