using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Desktop.Features.Collections;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class CollectionFilterWorkflowTests
{
    private static readonly IReadOnlyDictionary<string, bool> NoPreviousExpansion =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    private static CollectionItemViewModel Item(string name, string method, string path, string? tag = null) =>
        new(new CollectionRequest(name, method, path, null, Tag: tag));

    private readonly CollectionFilterWorkflow _workflow = new();

    [Fact]
    public void Apply_EmptyQueryDefaultSort_ReturnsAllItemsInOriginalOrder()
    {
        var items = new[]
        {
            Item("Zulu", "GET", "/pets"),
            Item("Alpha", "POST", "/users")
        };

        var result = _workflow.Apply(items, string.Empty, "Default", NoPreviousExpansion);

        result.Items.Select(item => item.Name).Should().Equal("Zulu", "Alpha");
    }

    [Theory]
    [InlineData("zulu", new[] { "Zulu" })]
    [InlineData("USERS", new[] { "Alpha" })]
    [InlineData("post", new[] { "Alpha" })]
    [InlineData("nothing-matches", new string[0])]
    public void Apply_SearchQuery_MatchesNamePathOrMethodCaseInsensitive(string query, string[] expectedNames)
    {
        var items = new[]
        {
            Item("Zulu", "GET", "/pets"),
            Item("Alpha", "POST", "/users")
        };

        var result = _workflow.Apply(items, query, "Default", NoPreviousExpansion);

        result.Items.Select(item => item.Name).Should().Equal(expectedNames);
    }

    [Theory]
    [InlineData("Name", new[] { "Alpha", "Zulu" })]
    [InlineData("Method", new[] { "Zulu", "Alpha" })]
    [InlineData("Path", new[] { "Alpha", "Zulu" })]
    [InlineData("Default", new[] { "Zulu", "Alpha" })]
    public void Apply_SortBy_OrdersItemsBySelectedField(string sortBy, string[] expectedNames)
    {
        var items = new[]
        {
            Item("Zulu", "DELETE", "/pets"),
            Item("Alpha", "POST", "/animals")
        };

        var result = _workflow.Apply(items, string.Empty, sortBy, NoPreviousExpansion);

        result.Items.Select(item => item.Name).Should().Equal(expectedNames);
    }

    [Fact]
    public void Apply_GroupsByGroupKeyCaseInsensitive()
    {
        var items = new[]
        {
            Item("One", "GET", "/users/1"),
            Item("Two", "GET", "/Users/2"),
            Item("Three", "GET", "/pets/3")
        };

        var result = _workflow.Apply(items, string.Empty, "Default", NoPreviousExpansion);

        result.Groups.Select(group => group.GroupKey).Should().Equal("users", "pets");
        result.Groups[0].Items.Should().HaveCount(2);
    }

    [Fact]
    public void Apply_PreviousExpansionState_IsPreservedPerGroup()
    {
        var items = new[]
        {
            Item("One", "GET", "/users/1"),
            Item("Two", "GET", "/pets/2")
        };
        var previousExpansion = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["users"] = false
        };

        var result = _workflow.Apply(items, string.Empty, "Default", previousExpansion);

        result.Groups.Single(group => group.GroupKey == "users").IsExpanded.Should().BeFalse();
        result.Groups.Single(group => group.GroupKey == "pets").IsExpanded.Should().BeTrue();
    }
}
