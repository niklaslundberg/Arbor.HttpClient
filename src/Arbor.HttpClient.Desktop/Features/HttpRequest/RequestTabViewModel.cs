using System;
using System.Collections.Generic;
using System.ComponentModel;
using Arbor.HttpClient.Desktop.Shared;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Represents a single request tab in the main request area.
/// Each tab owns its own <see cref="RequestEditorViewModel"/> (request state).
/// The <see cref="DisplayTitle"/> returns the request name, falling back to "New"
/// when no name has been set, so the tab always has visible text.
/// </summary>
public sealed partial class RequestTabViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private CollectionRequestSource? _collectionRequestSource;

    /// <summary>
    /// Per-tab response state snapshot so each request tab can preserve its own response view.
    /// </summary>
    public sealed record ResponseStateSnapshot(
        string ResponseStatus,
        int ResponseStatusCode,
        string ResponseTimeDisplay,
        string ResponseSizeDisplay,
        string ResponseBody,
        string RawResponseBody,
        string ResponseBodyTabLabel,
        string ResponseContentType,
        string ResponseRawText,
        int SelectedResponseTabIndex,
        bool IsResponseWebViewAvailable,
        string ResponseWebViewUri,
        bool IsBinaryResponse,
        bool HasResponseHeaders,
        bool HasTextResponse,
        IReadOnlyList<string> ResponseHeaders,
        ReadOnlyMemory<byte> LastResponseBodyBytes);

    public ResponseStateSnapshot? ResponseState { get; set; }

    public RequestTabViewModel(RequestEditorViewModel requestEditor)
    {
        RequestEditor = requestEditor;
        RequestEditor.PropertyChanged += OnRequestEditorPropertyChanged;
    }

    /// <summary>The request editor owned exclusively by this tab.</summary>
    public RequestEditorViewModel RequestEditor { get; }

    /// <summary>
    /// Tab header text: the request name if non-empty, otherwise "New".
    /// Bound to the tab header in the view; truncation is applied in XAML.
    /// </summary>
    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(RequestEditor.RequestName) ? "New" : RequestEditor.RequestName;

    public void SetCollectionRequestSource(int collectionId, string method, string path, string name)
    {
        _collectionRequestSource = new CollectionRequestSource(collectionId, method, path, name);
    }

    public bool MatchesCollectionRequest(int collectionId, string method, string path, string name) =>
        _collectionRequestSource is { } source
        && source.CollectionId == collectionId
        && string.Equals(source.Method, method, StringComparison.Ordinal)
        && string.Equals(source.Path, path, StringComparison.Ordinal)
        && string.Equals(source.Name, name, StringComparison.Ordinal);

    public bool TryGetCollectionRequestSource(
        out int collectionId,
        out string method,
        out string path,
        out string name)
    {
        if (_collectionRequestSource is { } source)
        {
            collectionId = source.CollectionId;
            method = source.Method;
            path = source.Path;
            name = source.Name;
            return true;
        }

        collectionId = default;
        method = string.Empty;
        path = string.Empty;
        name = string.Empty;
        return false;
    }

    private void OnRequestEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(RequestEditorViewModel.RequestName), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(DisplayTitle));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RequestEditor.PropertyChanged -= OnRequestEditorPropertyChanged;
    }

    private sealed record CollectionRequestSource(int CollectionId, string Method, string Path, string Name);
}
