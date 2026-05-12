using System.Collections.Generic;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Provides the response state and I/O handles required by <see cref="ResponseActionsViewModel"/>
/// to execute copy, save, and open-in-editor actions on the current response.
/// Implemented by <c>MainWindowViewModel</c>; extracted as an interface so that
/// <see cref="ResponseActionsViewModel"/> can be tested without the full main VM.
/// </summary>
public interface IResponseActionsContext
{
    /// <summary>Gets the clipboard service provided by the active top-level window, or <see langword="null"/> when unavailable.</summary>
    IClipboard? Clipboard { get; }

    /// <summary>Gets the storage provider provided by the active top-level window, or <see langword="null"/> when unavailable.</summary>
    IStorageProvider? StorageProvider { get; }

    /// <summary>Gets the pretty-printed response body text (empty when no response has been received).</summary>
    string ResponseBody { get; }

    /// <summary>Gets the raw (unformatted) response body text.</summary>
    string RawResponseBody { get; }

    /// <summary>Gets the <c>Content-Type</c> header value of the last response (empty when no response received).</summary>
    string ResponseContentType { get; }

    /// <summary>Gets the full raw HTTP response text (status line + headers + body) for the "Raw" tab.</summary>
    string ResponseRawText { get; }

    /// <summary>Gets the response header lines displayed in the "Headers" tab.</summary>
    IReadOnlyList<string> ResponseHeaders { get; }

    /// <summary>Gets the index of the currently selected response tab.</summary>
    int SelectedResponseTabIndex { get; }

    /// <summary>Gets a value indicating whether the last response contained binary (non-text) content.</summary>
    bool IsBinaryResponse { get; }

    /// <summary>Returns the raw bytes of the last binary response.</summary>
    byte[] GetLastResponseBodyBytes();

    /// <summary>Gets the default folder path suggested in the save-file dialog for response files.</summary>
    string ResponseSaveDefaultFolder { get; }

    /// <summary>Gets the pattern string used to build the suggested save file name.</summary>
    string ResponseSaveFileNamePattern { get; }

    /// <summary>Gets the name of the currently selected collection, or an empty string when none is selected.</summary>
    string SelectedCollectionName { get; }

    /// <summary>Returns the request URL with environment variables resolved.</summary>
    string RequestEditorResolvedUrl { get; }

    /// <summary>Gets the name of the current request as configured in the request editor.</summary>
    string RequestEditorRequestName { get; }

    /// <summary>Gets the <c>Content-Type</c> configured in the request editor (for temp-file extension detection).</summary>
    string RequestEditorContentType { get; }

    /// <summary>Builds the current request draft from the request editor state.</summary>
    HttpRequestDraft BuildRequestDraft();

    /// <summary>Registers a temp file path so it is cleaned up when the application closes.</summary>
    void RecordTempFile(string path);

    /// <summary>Updates the validation error message for the response save file-name pattern.</summary>
    void SetResponseSaveFileNamePatternValidationError(string validationMessage);
}
