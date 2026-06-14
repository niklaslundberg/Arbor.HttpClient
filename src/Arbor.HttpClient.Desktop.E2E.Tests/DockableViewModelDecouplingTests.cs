using System.Collections.ObjectModel;
using System.Windows.Input;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Logging;
using ReactiveUI;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Verifies that the dockable view models no longer depend on the whole
/// <c>MainWindowViewModel</c>: <see cref="LogPanelViewModel"/> takes the log feature VM
/// directly, and <see cref="LayoutManagementViewModel"/> takes the narrow
/// <see cref="ILayoutManagementContext"/> contract.
/// </summary>
public class DockableViewModelDecouplingTests
{
    [Fact]
    public void LogPanelViewModel_ExposesProvidedLogWindowViewModel()
    {
        using var sink = new InMemorySink();
        using var logs = new LogWindowViewModel(sink);

        var dockable = new LogPanelViewModel(logs);

        dockable.Logs.Should().BeSameAs(logs);
        dockable.Id.Should().Be("log-panel");
        dockable.Title.Should().Be("Logs");
    }

    [Fact]
    public void LayoutManagementViewModel_ProxiesCommandsFromContext()
    {
        var context = new FakeLayoutManagementContext();

        var dockable = new LayoutManagementViewModel(context);

        dockable.App.Should().BeSameAs(context);
        dockable.Id.Should().Be("layout-management");
        dockable.Title.Should().Be("Layout");
        dockable.SaveLayoutAsNewCommand.Should().BeSameAs(context.SaveLayoutAsNewCommand);
        dockable.SaveLayoutToExistingCommand.Should().BeSameAs(context.SaveLayoutToExistingCommand);
        dockable.RemoveLayoutCommand.Should().BeSameAs(context.RemoveLayoutCommand);
        dockable.RestoreDefaultLayoutCommand.Should().BeSameAs(context.RestoreDefaultLayoutCommand);
    }

    private sealed class FakeLayoutManagementContext : ILayoutManagementContext
    {
        public ObservableCollection<string> SavedLayoutNames { get; } = [];

        public string? SelectedLayoutName { get; set; }

        public ICommand SaveLayoutAsNewCommand { get; } = ReactiveCommand.Create(() => { });

        public ICommand SaveLayoutToExistingCommand { get; } = ReactiveCommand.Create<string?>(_ => { });

        public ICommand RemoveLayoutCommand { get; } = ReactiveCommand.Create<string?>(_ => { });

        public ICommand RestoreDefaultLayoutCommand { get; } = ReactiveCommand.Create(() => { });
    }
}
