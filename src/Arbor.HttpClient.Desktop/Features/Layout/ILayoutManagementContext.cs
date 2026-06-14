using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// The minimal surface the layout-management dockable needs from its host: the saved-layout
/// names, the currently selected layout name (two-way bound to the combo box), and the four
/// layout commands. Implemented by <c>MainWindowViewModel</c> so <see cref="LayoutManagementViewModel"/>
/// depends on this contract instead of the whole main view model.
/// </summary>
public interface ILayoutManagementContext
{
    ObservableCollection<string> SavedLayoutNames { get; }

    string? SelectedLayoutName { get; set; }

    ICommand SaveLayoutAsNewCommand { get; }

    ICommand SaveLayoutToExistingCommand { get; }

    ICommand RemoveLayoutCommand { get; }

    ICommand RestoreDefaultLayoutCommand { get; }
}
