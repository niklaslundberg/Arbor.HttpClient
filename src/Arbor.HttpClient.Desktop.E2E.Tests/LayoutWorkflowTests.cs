using Arbor.HttpClient.Desktop.Features.Layout;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="LayoutWorkflow"/> — the named-layout collection and
/// save/restore/remove orchestration extracted from <c>MainWindowViewModel</c>.
/// These tests do NOT require the Avalonia headless session.
/// </summary>
public sealed class LayoutWorkflowTests
{
    private static DockLayoutSnapshot Snapshot(double leftToolProportion = 0.25) =>
        new() { LeftToolProportion = leftToolProportion };

    [Fact]
    public void LoadFromOptions_NullLayouts_ReturnsNullAndLeavesNamesEmpty()
    {
        var workflow = new LayoutWorkflow();

        var selected = workflow.LoadFromOptions(null);

        selected.Should().BeNull();
        workflow.SavedLayoutNames.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromOptions_WithSavedLayouts_PopulatesNamesAlphabeticallyAndReturnsFirst()
    {
        var workflow = new LayoutWorkflow();
        var options = new LayoutOptions
        {
            SavedLayouts =
            [
                new NamedDockLayout { Name = "Zeta", Layout = Snapshot() },
                new NamedDockLayout { Name = "Alpha", Layout = Snapshot() }
            ]
        };

        var selected = workflow.LoadFromOptions(options);

        workflow.SavedLayoutNames.Should().Equal("Alpha", "Zeta");
        selected.Should().Be("Alpha");
    }

    [Fact]
    public void LoadFromOptions_IgnoresEntriesWithMissingNameOrLayout()
    {
        var workflow = new LayoutWorkflow();
        var options = new LayoutOptions
        {
            SavedLayouts =
            [
                new NamedDockLayout { Name = "  ", Layout = Snapshot() },
                new NamedDockLayout { Name = "Valid", Layout = Snapshot() }
            ]
        };

        workflow.LoadFromOptions(options);

        workflow.SavedLayoutNames.Should().Equal("Valid");
    }

    [Fact]
    public void TryGetLayout_KnownName_ReturnsTrueAndSnapshot()
    {
        var workflow = new LayoutWorkflow();
        var snapshot = Snapshot(0.4);
        workflow.LoadFromOptions(new LayoutOptions
        {
            SavedLayouts = [new NamedDockLayout { Name = "Saved", Layout = snapshot }]
        });

        var found = workflow.TryGetLayout("Saved", out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(snapshot);
    }

    [Fact]
    public void TryGetLayout_UnknownName_ReturnsFalse()
    {
        var workflow = new LayoutWorkflow();

        var found = workflow.TryGetLayout("Missing", out var result);

        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void SaveLayoutAsNew_CaptureReturnsNull_ReturnsNullAndDoesNotAddName()
    {
        var workflow = new LayoutWorkflow();

        var result = workflow.SaveLayoutAsNew(() => null);

        result.Should().BeNull();
        workflow.SavedLayoutNames.Should().BeEmpty();
    }

    [Fact]
    public void SaveLayoutAsNew_FirstCall_UsesLayoutOneAsName()
    {
        var workflow = new LayoutWorkflow();

        var result = workflow.SaveLayoutAsNew(() => Snapshot());

        result.Should().Be("Layout 1");
        workflow.SavedLayoutNames.Should().Equal("Layout 1");
    }

    [Fact]
    public void SaveLayoutAsNew_WhenDefaultNameAlreadyUsed_SkipsToNextAvailableName()
    {
        var workflow = new LayoutWorkflow();
        workflow.LoadFromOptions(new LayoutOptions
        {
            SavedLayouts = [new NamedDockLayout { Name = "Layout 1", Layout = Snapshot() }]
        });

        var result = workflow.SaveLayoutAsNew(() => Snapshot());

        result.Should().Be("Layout 2");
        workflow.SavedLayoutNames.Should().Equal("Layout 1", "Layout 2");
    }

    [Fact]
    public void SaveLayoutToExisting_NullOrWhitespaceName_ReturnsFalse()
    {
        var workflow = new LayoutWorkflow();

        var result = workflow.SaveLayoutToExisting("  ", () => Snapshot());

        result.Should().BeFalse();
    }

    [Fact]
    public void SaveLayoutToExisting_CaptureReturnsNull_ReturnsFalse()
    {
        var workflow = new LayoutWorkflow();

        var result = workflow.SaveLayoutToExisting("Layout 1", () => null);

        result.Should().BeFalse();
    }

    [Fact]
    public void SaveLayoutToExisting_ValidNameAndSnapshot_OverwritesAndReturnsTrue()
    {
        var workflow = new LayoutWorkflow();
        workflow.LoadFromOptions(new LayoutOptions
        {
            SavedLayouts = [new NamedDockLayout { Name = "Saved", Layout = Snapshot(0.25) }]
        });
        var updated = Snapshot(0.5);

        var result = workflow.SaveLayoutToExisting("Saved", () => updated);

        result.Should().BeTrue();
        workflow.TryGetLayout("Saved", out var stored).Should().BeTrue();
        stored.Should().BeSameAs(updated);
    }

    [Fact]
    public void RestoreDefaultLayout_NullDefault_ReturnsFalseAndDoesNotApply()
    {
        var workflow = new LayoutWorkflow();
        var applied = false;

        var result = workflow.RestoreDefaultLayout(null, _ => applied = true);

        result.Should().BeFalse();
        applied.Should().BeFalse();
    }

    [Fact]
    public void RestoreDefaultLayout_NonNullDefault_AppliesSnapshotAndReturnsTrue()
    {
        var workflow = new LayoutWorkflow();
        var defaultLayout = Snapshot();
        DockLayoutSnapshot? appliedSnapshot = null;

        var result = workflow.RestoreDefaultLayout(defaultLayout, snapshot => appliedSnapshot = snapshot);

        result.Should().BeTrue();
        appliedSnapshot.Should().BeSameAs(defaultLayout);
    }

    [Fact]
    public void RemoveLayout_NullOrWhitespaceName_ReturnsNotRemoved()
    {
        var workflow = new LayoutWorkflow();

        var result = workflow.RemoveLayout("  ", "Selected");

        result.Removed.Should().BeFalse();
        result.SelectedLayoutName.Should().Be("Selected");
    }

    [Fact]
    public void RemoveLayout_UnknownName_ReturnsNotRemoved()
    {
        var workflow = new LayoutWorkflow();
        workflow.LoadFromOptions(new LayoutOptions
        {
            SavedLayouts = [new NamedDockLayout { Name = "Saved", Layout = Snapshot() }]
        });

        var result = workflow.RemoveLayout("Missing", "Saved");

        result.Removed.Should().BeFalse();
        workflow.SavedLayoutNames.Should().Equal("Saved");
    }

    [Fact]
    public void RemoveLayout_NotSelected_RemovesAndKeepsSelection()
    {
        var workflow = new LayoutWorkflow();
        workflow.LoadFromOptions(new LayoutOptions
        {
            SavedLayouts =
            [
                new NamedDockLayout { Name = "Alpha", Layout = Snapshot() },
                new NamedDockLayout { Name = "Beta", Layout = Snapshot() }
            ]
        });

        var result = workflow.RemoveLayout("Beta", "Alpha");

        result.Removed.Should().BeTrue();
        result.SelectedLayoutName.Should().Be("Alpha");
        workflow.SavedLayoutNames.Should().Equal("Alpha");
    }

    [Fact]
    public void RemoveLayout_SelectedLayout_SelectsNextRemainingNameAlphabetically()
    {
        var workflow = new LayoutWorkflow();
        workflow.LoadFromOptions(new LayoutOptions
        {
            SavedLayouts =
            [
                new NamedDockLayout { Name = "Alpha", Layout = Snapshot() },
                new NamedDockLayout { Name = "Beta", Layout = Snapshot() }
            ]
        });

        var result = workflow.RemoveLayout("Alpha", "Alpha");

        result.Removed.Should().BeTrue();
        result.SelectedLayoutName.Should().Be("Beta");
    }

    [Fact]
    public void RemoveLayout_LastRemainingSelectedLayout_SelectionBecomesNull()
    {
        var workflow = new LayoutWorkflow();
        workflow.LoadFromOptions(new LayoutOptions
        {
            SavedLayouts = [new NamedDockLayout { Name = "Alpha", Layout = Snapshot() }]
        });

        var result = workflow.RemoveLayout("Alpha", "Alpha");

        result.Removed.Should().BeTrue();
        result.SelectedLayoutName.Should().BeNull();
        workflow.SavedLayoutNames.Should().BeEmpty();
    }

    [Fact]
    public void BuildNamedLayouts_ReturnsLayoutsOrderedAlphabeticallyByName()
    {
        var workflow = new LayoutWorkflow();
        var zetaSnapshot = Snapshot(0.1);
        var alphaSnapshot = Snapshot(0.2);
        workflow.LoadFromOptions(new LayoutOptions
        {
            SavedLayouts =
            [
                new NamedDockLayout { Name = "Zeta", Layout = zetaSnapshot },
                new NamedDockLayout { Name = "Alpha", Layout = alphaSnapshot }
            ]
        });

        var named = workflow.BuildNamedLayouts();

        named.Select(item => item.Name).Should().Equal("Alpha", "Zeta");
        named.Single(item => item.Name == "Alpha").Layout.Should().BeSameAs(alphaSnapshot);
        named.Single(item => item.Name == "Zeta").Layout.Should().BeSameAs(zetaSnapshot);
    }
}
