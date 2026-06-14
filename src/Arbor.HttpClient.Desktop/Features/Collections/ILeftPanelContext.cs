using System.Collections.ObjectModel;
using System.Windows.Input;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.Variables;

namespace Arbor.HttpClient.Desktop.Features.Collections;

/// <summary>
/// The surface the Explorer dockable (<see cref="LeftPanelViewModel"/> / <c>LeftPanelView</c>)
/// needs from its host: the History / Collections / Scheduled-Jobs tab state, the collection
/// management commands and form state, and the two sub-view-models the panel reaches into
/// (<see cref="HttpRequest.RequestEditorViewModel"/> for well-known header names and
/// <see cref="ResponseActionsViewModel"/> for "Copy as cURL"). Implemented by
/// <c>MainWindowViewModel</c> so the dockable depends on this contract instead of the whole VM.
/// Extends <see cref="IVariableAutoCompleteHost"/> so the panel can be passed to the
/// inherited-header <c>VariableTextBox</c> inputs.
/// </summary>
public interface ILeftPanelContext : IVariableAutoCompleteHost
{
    // Tab selector
    string LeftPanelTab { get; }
    ICommand ShowHistoryTabCommand { get; }
    ICommand ShowCollectionsTabCommand { get; }
    ICommand ShowScheduledJobsTabCommand { get; }

    // History tab
    string HistorySearchQuery { get; set; }
    ObservableCollection<RequestHistoryEntry> History { get; }
    ICommand LoadHistoryRequestCommand { get; }

    // Collections tab — selection and management
    ObservableCollection<Collection> Collections { get; }
    Collection? SelectedCollection { get; set; }
    ICommand LoadCollectionRequestCommand { get; }
    ICommand AddRequestToCollectionCommand { get; }
    ICommand ImportCollectionCommand { get; }
    ICommand DeleteCollectionCommand { get; }

    // Collections tab — new/rename forms
    bool IsNewCollectionFormVisible { get; }
    string NewCollectionName { get; set; }
    ICommand ShowNewCollectionFormCommand { get; }
    ICommand CreateCollectionCommand { get; }
    ICommand CancelNewCollectionCommand { get; }
    bool IsRenameCollectionFormVisible { get; }
    string RenameCollectionName { get; set; }
    ICommand ShowRenameCollectionFormCommand { get; }
    ICommand ConfirmRenameCollectionCommand { get; }
    ICommand CancelRenameCollectionCommand { get; }

    // Collections tab — inherited headers
    ObservableCollection<RequestHeaderViewModel> CollectionInheritedHeaders { get; }
    ICommand AddCollectionInheritedHeaderCommand { get; }
    ICommand RemoveCollectionInheritedHeaderCommand { get; }

    // Collections tab — search / sort / display / grouping
    string CollectionSearchQuery { get; set; }
    string CollectionSortBy { get; }
    string CollectionDisplayMode { get; }
    bool IsCollectionTreeView { get; }
    ObservableCollection<CollectionItemViewModel> FilteredCollectionItems { get; }
    ObservableCollection<CollectionGroupViewModel> CollectionGroups { get; }
    ICommand SetCollectionSortByCommand { get; }
    ICommand SetCollectionDisplayModeCommand { get; }
    ICommand ToggleCollectionTreeViewCommand { get; }

    // Scheduled jobs tab
    ObservableCollection<ScheduledJobViewModel> ScheduledJobs { get; }
    ICommand AddScheduledJobCommand { get; }
    ICommand RemoveScheduledJobCommand { get; }

    // Sub-view-models the item templates bind into
    RequestEditorViewModel RequestEditor { get; }
    ResponseActionsViewModel ResponseActions { get; }
}
