using Arbor.HttpClient.Desktop.Features.Layout;
using Dock.Model.Controls;
using Dock.Model.ReactiveUI.Controls;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="LayoutTreeWorkflow"/>'s dock-tree lookup and
/// proportion-reapply helpers extracted from <c>MainWindowViewModel.ReapplyStartupLayout</c>.
/// These tests do NOT require the Avalonia headless session.
/// </summary>
public sealed class LayoutTreeWorkflowTests
{
    private static RootDock BuildLayout()
    {
        var requestDock = new DocumentDock { Id = "request-dock", Proportion = 0.3 };
        var leftToolDock = new ToolDock { Id = "left-tool-dock", Proportion = 0.25 };
        var documentLayout = new ProportionalDock
        {
            Id = "document-layout",
            Proportion = 0.75,
            VisibleDockables = [requestDock]
        };

        return new RootDock
        {
            Id = "root",
            VisibleDockables = [leftToolDock, documentLayout]
        };
    }

    [Fact]
    public void FindDockById_FindsNestedDockable_CaseInsensitive()
    {
        var root = BuildLayout();

        var found = LayoutTreeWorkflow.FindDockById<DocumentDock>(root, "REQUEST-DOCK");

        found.Should().NotBeNull();
        found!.Id.Should().Be("request-dock");
    }

    [Fact]
    public void FindDockById_ReturnsNull_WhenNoDockableMatchesIdOrType()
    {
        var root = BuildLayout();

        LayoutTreeWorkflow.FindDockById<ToolDock>(root, "request-dock").Should().BeNull();
        LayoutTreeWorkflow.FindDockById<DocumentDock>(root, "missing").Should().BeNull();
    }

    [Fact]
    public void ReapplyProportionsFromTree_AppliesProportionsToMatchingDockablesRecursively()
    {
        var root = BuildLayout();
        var leftToolDock = LayoutTreeWorkflow.FindDockById<ToolDock>(root, "left-tool-dock")!;
        var documentLayout = LayoutTreeWorkflow.FindDockById<ProportionalDock>(root, "document-layout")!;
        var requestDock = LayoutTreeWorkflow.FindDockById<DocumentDock>(root, "request-dock")!;

        var tree = new DockTreeNode
        {
            Type = "Root",
            Id = "root",
            Children =
            [
                new DockTreeNode { Type = "Tool", Id = "left-tool-dock", Proportion = 0.4 },
                new DockTreeNode
                {
                    Type = "Proportional",
                    Id = "document-layout",
                    Proportion = 0.6,
                    Children = [new DockTreeNode { Type = "Document", Id = "request-dock", Proportion = 0.5 }]
                }
            ]
        };

        LayoutTreeWorkflow.ReapplyProportionsFromTree(root, tree);

        leftToolDock.Proportion.Should().BeApproximately(0.4, 0.001);
        documentLayout.Proportion.Should().BeApproximately(0.6, 0.001);
        requestDock.Proportion.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void ReapplyProportionsFromTree_SkipsNodesWithoutIdOrZeroProportion()
    {
        var root = BuildLayout();
        var leftToolDock = LayoutTreeWorkflow.FindDockById<ToolDock>(root, "left-tool-dock")!;

        var tree = new DockTreeNode
        {
            Type = "Root",
            Children =
            [
                new DockTreeNode { Type = "Tool", Id = "left-tool-dock", Proportion = 0 },
                new DockTreeNode { Type = "Tool", Proportion = 0.9 }
            ]
        };

        LayoutTreeWorkflow.ReapplyProportionsFromTree(root, tree);

        leftToolDock.Proportion.Should().BeApproximately(0.25, 0.001, "zero proportions in the tree must not overwrite the existing value");
    }
}
