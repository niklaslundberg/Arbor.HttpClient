using Arbor.HttpClient.Desktop.Features.Layout;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using static Arbor.HttpClient.Desktop.E2E.Tests.UiTestHelpers;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Verifies that <see cref="DockFactory"/> composes its layout purely from
/// <see cref="IDockPanelRegistration"/> entries, so adding a new dock panel requires only a new
/// registration class and no changes to <see cref="DockFactory"/> itself.
/// </summary>
public class DockFactoryRegistrationTests
{
    [Fact]
    public void CreateLayout_PlacesRegistrationsInLeftToolAndDocumentDocksByLocation()
    {
        var leftPanel = new FakeTool("left-panel", "Explorer");
        var options = new FakeTool("options", "Options");
        var request = new FakeDocument("request", "Request");

        var dockFactory = new DockFactory(new IDockPanelRegistration[]
        {
            new FakeRegistration(leftPanel, DockPanelLocation.LeftTool),
            new FakeRegistration(options, DockPanelLocation.LeftTool),
            new FakeRegistration(request, DockPanelLocation.Document)
        });

        var root = dockFactory.CreateLayout();

        var leftToolDock = FindDockById<ToolDock>(root, "left-tool-dock");
        var requestDock = FindDockById<DocumentDock>(root, "request-dock");

        leftToolDock.Should().NotBeNull();
        leftToolDock!.VisibleDockables.Should().Equal(leftPanel, options);
        leftToolDock.ActiveDockable.Should().BeSameAs(leftPanel);

        requestDock.Should().NotBeNull();
        requestDock!.VisibleDockables.Should().Equal(request);
        requestDock.ActiveDockable.Should().BeSameAs(request);
    }

    [Fact]
    public void GetDockable_ReturnsRegisteredDockableByType()
    {
        var leftPanel = new FakeTool("left-panel", "Explorer");
        var request = new FakeDocument("request", "Request");

        var dockFactory = new DockFactory(new IDockPanelRegistration[]
        {
            new FakeRegistration(leftPanel, DockPanelLocation.LeftTool),
            new FakeRegistration(request, DockPanelLocation.Document)
        });

        dockFactory.GetDockable<FakeTool>().Should().BeSameAs(leftPanel);
        dockFactory.GetDockable<FakeDocument>().Should().BeSameAs(request);
    }

    [Fact]
    public void GetDockable_NoMatchingRegistration_ReturnsNull()
    {
        var dockFactory = new DockFactory(new IDockPanelRegistration[]
        {
            new FakeRegistration(new FakeTool("left-panel", "Explorer"), DockPanelLocation.LeftTool)
        });

        dockFactory.GetDockable<FakeDocument>().Should().BeNull();
    }

    [Fact]
    public void UpdateLeftToolDock_AfterInitLayout_PointsAtLeftToolOwner()
    {
        var leftPanel = new FakeTool("left-panel", "Explorer");

        var dockFactory = new DockFactory(new IDockPanelRegistration[]
        {
            new FakeRegistration(leftPanel, DockPanelLocation.LeftTool),
            new FakeRegistration(new FakeDocument("request", "Request"), DockPanelLocation.Document)
        });

        var root = dockFactory.CreateLayout();
        dockFactory.InitLayout(root);

        dockFactory.UpdateLeftToolDock();

        dockFactory.LeftToolDock.Should().NotBeNull();
        leftPanel.Owner.Should().BeSameAs(dockFactory.LeftToolDock);
    }

    [Fact]
    public void CreateLayout_AddingNewRegistration_AppearsInLayoutWithoutFactoryChanges()
    {
        var leftPanel = new FakeTool("left-panel", "Explorer");
        var newFeaturePanel = new FakeTool("new-feature", "New Feature");

        var dockFactory = new DockFactory(new IDockPanelRegistration[]
        {
            new FakeRegistration(leftPanel, DockPanelLocation.LeftTool),
            new FakeRegistration(newFeaturePanel, DockPanelLocation.LeftTool),
            new FakeRegistration(new FakeDocument("request", "Request"), DockPanelLocation.Document)
        });

        var root = dockFactory.CreateLayout();
        var leftToolDock = FindDockById<ToolDock>(root, "left-tool-dock");

        leftToolDock!.VisibleDockables.Should().Contain(newFeaturePanel);
    }

    private sealed class FakeTool : Tool
    {
        public FakeTool(string id, string title)
        {
            Id = id;
            Title = title;
        }
    }

    private sealed class FakeDocument : Document
    {
        public FakeDocument(string id, string title)
        {
            Id = id;
            Title = title;
        }
    }

    private sealed class FakeRegistration : IDockPanelRegistration
    {
        public FakeRegistration(IDockable dockable, DockPanelLocation location)
        {
            Dockable = dockable;
            Location = location;
        }

        public DockPanelLocation Location { get; }

        public IDockable Dockable { get; }
    }
}
