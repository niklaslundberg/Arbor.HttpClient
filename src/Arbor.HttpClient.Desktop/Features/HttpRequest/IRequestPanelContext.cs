using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Arbor.HttpClient.Desktop.Features.GraphQl;
using Arbor.HttpClient.Desktop.Features.Scripting;
using Arbor.HttpClient.Desktop.Features.Sse;
using Arbor.HttpClient.Desktop.Features.Variables;
using Arbor.HttpClient.Desktop.Features.WebSocket;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// The surface the Request document (<see cref="RequestViewModel"/> / <c>RequestView</c>) needs
/// from its host: the request-tab strip, the per-tab <see cref="RequestEditorViewModel"/> plus the
/// GraphQL / WebSocket / SSE / scripting sub-view-models, the primary-action and demo-server state,
/// and the UI font settings the embedded code editors read. Implemented by <c>MainWindowViewModel</c>
/// so the document depends on this contract instead of the whole VM. Extends
/// <see cref="IVariableAutoCompleteHost"/> for the request URL/body variable auto-completion, and
/// <see cref="INotifyPropertyChanged"/> because the view's code-behind subscribes to host changes
/// (active tab swap, font changes).
/// </summary>
public interface IRequestPanelContext : IVariableAutoCompleteHost, INotifyPropertyChanged
{
    // Request tabs
    ObservableCollection<RequestTabViewModel> RequestTabs { get; }
    RequestTabViewModel? ActiveRequestTab { get; set; }
    ICommand NewRequestTabCommand { get; }
    ICommand CloseRequestTabCommand { get; }

    // Per-tab editor and protocol sub-view-models
    RequestEditorViewModel RequestEditor { get; }
    GraphQlViewModel GraphQlEditor { get; }
    WebSocketViewModel WebSocketSession { get; }
    SseViewModel SseSession { get; }
    ScriptViewModel ScriptEditor { get; }

    // Primary action + status
    string PrimaryActionLabel { get; }
    string ErrorMessage { get; }
    string RequestTimeoutDefaultWatermark { get; }
    ICommand ExecutePrimaryActionCommand { get; }
    ICommand OpenRequestBodyInExternalEditorCommand { get; }

    // Demo-server banner
    bool IsDemoServerBannerVisible { get; }
    ICommand StartDemoServerCommand { get; }
    ICommand DismissDemoServerBannerCommand { get; }

    bool IsRequestInProgress { get; }

    // Integrated response panel (EmbeddedResponseView) state
    string ResponseStatus { get; }
    int ResponseStatusCode { get; }
    string ResponseTimeDisplay { get; }
    string ResponseSizeDisplay { get; }
    string ResponseBody { get; }
    string RawResponseBody { get; }
    string ResponseRawText { get; }
    string ResponseContentType { get; }
    string ResponseBodyTabLabel { get; }
    int SelectedResponseTabIndex { get; set; }
    bool IsResponseWebViewAvailable { get; }
    string ResponseWebViewUri { get; }
    bool IsBinaryResponse { get; }
    bool HasResponseHeaders { get; }
    bool HasTextResponse { get; }
    ObservableCollection<string> ResponseHeaders { get; }
    ResponseActionsViewModel ResponseActions { get; }

    // UI font settings read by the embedded code editors
    string UiFontFamily { get; }
    double UiFontSize { get; }
}
