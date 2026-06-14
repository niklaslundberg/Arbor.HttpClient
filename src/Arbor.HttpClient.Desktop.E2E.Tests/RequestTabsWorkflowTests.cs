using Arbor.HttpClient.Core.Variables;
using Arbor.HttpClient.Desktop.Features.HttpRequest;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="RequestTabsWorkflow"/> — the tab-list
/// lifecycle extracted from <c>MainWindowViewModel</c> (add/close while keeping
/// at least one tab open).
/// </summary>
public sealed class RequestTabsWorkflowTests
{
    private static RequestEditorViewModel CreateEditor() =>
        new(new VariableResolver(), () => []);

    [AvaloniaFact(Timeout = 10_000)]
    public void AddTab_AddsToTabsAndReturnsTheNewTab()
    {
        var workflow = new RequestTabsWorkflow();

        var tab = workflow.AddTab(CreateEditor());

        workflow.Tabs.Should().ContainSingle().Which.Should().BeSameAs(tab);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void CloseTab_NullTab_ReturnsNullAndLeavesTabsUnchanged()
    {
        var workflow = new RequestTabsWorkflow();
        var tab = workflow.AddTab(CreateEditor());

        var result = workflow.CloseTab(null, tab);

        result.Should().BeNull();
        workflow.Tabs.Should().ContainSingle().Which.Should().BeSameAs(tab);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void CloseTab_OnlyTab_DoesNotCloseAndReturnsNull()
    {
        var workflow = new RequestTabsWorkflow();
        var tab = workflow.AddTab(CreateEditor());

        var result = workflow.CloseTab(tab, tab);

        result.Should().BeNull();
        workflow.Tabs.Should().ContainSingle().Which.Should().BeSameAs(tab);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void CloseTab_InactiveTab_RemovesItAndReturnsNull()
    {
        var workflow = new RequestTabsWorkflow();
        var activeTab = workflow.AddTab(CreateEditor());
        var otherTab = workflow.AddTab(CreateEditor());

        var result = workflow.CloseTab(otherTab, activeTab);

        result.Should().BeNull();
        workflow.Tabs.Should().ContainSingle().Which.Should().BeSameAs(activeTab);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void CloseTab_ActiveTabWithLaterTabRemaining_ReturnsTheNextTabAtSameIndex()
    {
        var workflow = new RequestTabsWorkflow();
        var first = workflow.AddTab(CreateEditor());
        var second = workflow.AddTab(CreateEditor());
        var third = workflow.AddTab(CreateEditor());

        var result = workflow.CloseTab(second, second);

        result.Should().BeSameAs(third);
        workflow.Tabs.Should().Equal(first, third);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void CloseTab_ActiveLastTab_ReturnsThePreviousTab()
    {
        var workflow = new RequestTabsWorkflow();
        var first = workflow.AddTab(CreateEditor());
        var second = workflow.AddTab(CreateEditor());

        var result = workflow.CloseTab(second, second);

        result.Should().BeSameAs(first);
        workflow.Tabs.Should().ContainSingle().Which.Should().BeSameAs(first);
    }

    [Fact]
    public void GetResponseStateBytes_EmptyMemory_ReturnsEmptyArray()
    {
        RequestTabsWorkflow.GetResponseStateBytes(ReadOnlyMemory<byte>.Empty).Should().BeEmpty();
    }

    [Fact]
    public void GetResponseStateBytes_WholeArray_ReturnsSameArrayInstance()
    {
        byte[] array = [1, 2, 3];

        var result = RequestTabsWorkflow.GetResponseStateBytes(array);

        result.Should().BeSameAs(array);
    }

    [Fact]
    public void GetResponseStateBytes_ArraySlice_ReturnsCopyOfTheSlice()
    {
        byte[] array = [1, 2, 3, 4, 5];
        var slice = new ReadOnlyMemory<byte>(array, 1, 3);

        var result = RequestTabsWorkflow.GetResponseStateBytes(slice);

        result.Should().Equal(2, 3, 4);
        result.Should().NotBeSameAs(array);
    }
}
