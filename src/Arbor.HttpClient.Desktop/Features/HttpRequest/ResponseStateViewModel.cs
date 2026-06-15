using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Shared;
using ReactiveUI.SourceGenerators;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Owns the integrated response panel's display state (status, timing, body/raw/headers tabs,
/// web-view preview) and the logic to project an <see cref="HttpResponseDetails"/> onto it, capture
/// it into a per-tab snapshot, restore it, or clear it. Extracted from <c>MainWindowViewModel</c> so
/// the response surface is owned and tested independently of the request pipeline.
/// </summary>
public sealed partial class ResponseStateViewModel : ReactiveViewModelBase
{
    private readonly HttpResponseProjectionWorkflow _projectionWorkflow = new();
    private byte[] _lastResponseBodyBytes = [];

    [Reactive]
    private string _responseStatus = string.Empty;

    [Reactive]
    private int _responseStatusCode;

    [Reactive]
    private string _responseTimeDisplay = string.Empty;

    [Reactive]
    private string _responseSizeDisplay = string.Empty;

    [Reactive]
    private string _responseBody = string.Empty;

    [Reactive]
    private string _rawResponseBody = string.Empty;

    [Reactive]
    private string _responseBodyTabLabel = "Body";

    [Reactive]
    private string _responseContentType = string.Empty;

    [Reactive]
    private string _responseRawText = string.Empty;

    [Reactive]
    private int _selectedResponseTabIndex;

    [Reactive]
    private bool _isResponseWebViewAvailable;

    [Reactive]
    private string _responseWebViewUri = "about:blank";

    [Reactive]
    private bool _isBinaryResponse;

    [Reactive]
    private bool _hasResponseHeaders;

    [Reactive]
    private bool _hasTextResponse;

    /// <summary>Response header lines displayed in the "Headers" tab.</summary>
    public ObservableCollection<string> ResponseHeaders { get; } = [];

    /// <summary>Raw bytes of the last response body, used for binary save/open actions.</summary>
    public byte[] GetLastResponseBodyBytes() => _lastResponseBodyBytes;

    /// <summary>Projects a completed HTTP response onto the response display state.</summary>
    public void Apply(HttpResponseDetails response)
    {
        var projection = _projectionWorkflow.BuildProjection(response);

        ResponseStatus = projection.ResponseStatus;
        ResponseStatusCode = projection.ResponseStatusCode;
        ResponseTimeDisplay = projection.ResponseTimeDisplay;
        ResponseSizeDisplay = projection.ResponseSizeDisplay;
        _lastResponseBodyBytes = projection.LastResponseBodyBytes;
        RawResponseBody = projection.RawResponseBody;

        ResponseHeaders.Clear();
        foreach (var responseHeader in projection.ResponseHeaders)
        {
            ResponseHeaders.Add(responseHeader);
        }

        HasResponseHeaders = projection.HasResponseHeaders;
        ResponseContentType = projection.ResponseContentType;
        ResponseBodyTabLabel = projection.ResponseBodyTabLabel;
        IsBinaryResponse = projection.IsBinaryResponse;
        ResponseBody = projection.ResponseBody;
        IsResponseWebViewAvailable = projection.IsResponseWebViewAvailable;
        ResponseWebViewUri = projection.ResponseWebViewUri;
        ResponseRawText = projection.ResponseRawText;
        HasTextResponse = projection.HasTextResponse;
    }

    /// <summary>Captures the current response state into a per-tab snapshot.</summary>
    public RequestTabViewModel.ResponseStateSnapshot CaptureSnapshot() =>
        new(
            ResponseStatus,
            ResponseStatusCode,
            ResponseTimeDisplay,
            ResponseSizeDisplay,
            ResponseBody,
            RawResponseBody,
            ResponseBodyTabLabel,
            ResponseContentType,
            ResponseRawText,
            SelectedResponseTabIndex,
            IsResponseWebViewAvailable,
            ResponseWebViewUri,
            IsBinaryResponse,
            HasResponseHeaders,
            HasTextResponse,
            ResponseHeaders.ToList(),
            _lastResponseBodyBytes);

    /// <summary>Restores response state from a per-tab snapshot, or clears it when none exists.</summary>
    public void Restore(RequestTabViewModel.ResponseStateSnapshot? snapshot)
    {
        if (snapshot is not { } state)
        {
            Clear();
            return;
        }

        ResponseStatus = state.ResponseStatus;
        ResponseStatusCode = state.ResponseStatusCode;
        ResponseTimeDisplay = state.ResponseTimeDisplay;
        ResponseSizeDisplay = state.ResponseSizeDisplay;
        ResponseBody = state.ResponseBody;
        RawResponseBody = state.RawResponseBody;
        ResponseRawText = state.ResponseRawText;
        ResponseContentType = state.ResponseContentType;
        ResponseBodyTabLabel = state.ResponseBodyTabLabel;
        SelectedResponseTabIndex = state.SelectedResponseTabIndex;
        IsBinaryResponse = state.IsBinaryResponse;
        IsResponseWebViewAvailable = state.IsResponseWebViewAvailable;
        ResponseWebViewUri = state.ResponseWebViewUri;
        HasResponseHeaders = state.HasResponseHeaders;
        HasTextResponse = state.HasTextResponse;
        _lastResponseBodyBytes = RequestTabsWorkflow.GetResponseStateBytes(state.LastResponseBodyBytes);

        ResponseHeaders.Clear();
        foreach (var header in state.ResponseHeaders)
        {
            ResponseHeaders.Add(header);
        }
    }

    /// <summary>Clears just the status/timing/size metadata (used when a request fails).</summary>
    public void ClearMetadata()
    {
        ResponseStatusCode = 0;
        ResponseTimeDisplay = string.Empty;
        ResponseSizeDisplay = string.Empty;
    }

    /// <summary>Clears the response display state back to its empty defaults.</summary>
    public void Clear()
    {
        ResponseStatus = string.Empty;
        ResponseStatusCode = 0;
        ResponseTimeDisplay = string.Empty;
        ResponseSizeDisplay = string.Empty;
        ResponseBody = string.Empty;
        RawResponseBody = string.Empty;
        ResponseRawText = string.Empty;
        ResponseContentType = string.Empty;
        ResponseBodyTabLabel = "Body";
        SelectedResponseTabIndex = 0;
        IsBinaryResponse = false;
        IsResponseWebViewAvailable = false;
        ResponseWebViewUri = "about:blank";
        HasResponseHeaders = false;
        HasTextResponse = false;
        _lastResponseBodyBytes = [];
        ResponseHeaders.Clear();
    }
}
