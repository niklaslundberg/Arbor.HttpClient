using System;
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
}
