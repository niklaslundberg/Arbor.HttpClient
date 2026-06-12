using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.History;
using Arbor.HttpClient.Testing.Repositories;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="RequestHistoryWorkflow"/> — the history
/// load/filter pipeline extracted from <c>MainWindowViewModel</c>.
/// These tests do NOT require the Avalonia headless session.
/// </summary>
public sealed class RequestHistoryWorkflowTests
{
    private static RequestHistoryEntry Entry(string name, string method, string url, DateTimeOffset createdAtUtc, string? body = null) =>
        new(name, method, url, body, createdAtUtc);

    [Fact]
    public async Task LoadAsync_PopulatesHistoryOrderedByMostRecentFirst()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var older = Entry("Get users", "GET", "http://localhost/users", DateTimeOffset.UtcNow.AddMinutes(-5));
        var newer = Entry("Get pets", "GET", "http://localhost/pets", DateTimeOffset.UtcNow);
        await repository.SaveAsync(older, TestContext.Current.CancellationToken);
        await repository.SaveAsync(newer, TestContext.Current.CancellationToken);

        var workflow = new RequestHistoryWorkflow(repository);

        await workflow.LoadAsync(string.Empty, TestContext.Current.CancellationToken);

        workflow.History.Should().Equal(newer, older);
    }

    [Fact]
    public async Task LoadAsync_WithSearchQuery_AppliesFilterImmediately()
    {
        var repository = new InMemoryRequestHistoryRepository();
        await repository.SaveAsync(Entry("Get users", "GET", "http://localhost/users", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        await repository.SaveAsync(Entry("Get pets", "GET", "http://localhost/pets", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        var workflow = new RequestHistoryWorkflow(repository);

        await workflow.LoadAsync("pets", TestContext.Current.CancellationToken);

        workflow.History.Should().ContainSingle().Which.Name.Should().Be("Get pets");
    }

    [Theory]
    [InlineData("users", new[] { "Get users" })]
    [InlineData("PETS", new[] { "Get pets" })]
    [InlineData("post", new[] { "Create pet" })]
    [InlineData("nothing-matches", new string[0])]
    public async Task ApplyFilter_MatchesNameUrlOrMethodCaseInsensitive(string query, string[] expectedNames)
    {
        var repository = new InMemoryRequestHistoryRepository();
        var now = DateTimeOffset.UtcNow;
        await repository.SaveAsync(Entry("Get users", "GET", "http://localhost/users", now), TestContext.Current.CancellationToken);
        await repository.SaveAsync(Entry("Get pets", "GET", "http://localhost/pets", now.AddSeconds(-1)), TestContext.Current.CancellationToken);
        await repository.SaveAsync(Entry("Create pet", "POST", "http://localhost/animals", now.AddSeconds(-2)), TestContext.Current.CancellationToken);

        var workflow = new RequestHistoryWorkflow(repository);
        await workflow.LoadAsync(string.Empty, TestContext.Current.CancellationToken);

        workflow.ApplyFilter(query);

        workflow.History.Select(item => item.Name).Should().Equal(expectedNames);
    }

    [Fact]
    public async Task ApplyFilter_EmptyQuery_RestoresFullHistory()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var now = DateTimeOffset.UtcNow;
        await repository.SaveAsync(Entry("Get users", "GET", "http://localhost/users", now), TestContext.Current.CancellationToken);
        await repository.SaveAsync(Entry("Get pets", "GET", "http://localhost/pets", now.AddSeconds(-1)), TestContext.Current.CancellationToken);

        var workflow = new RequestHistoryWorkflow(repository);
        await workflow.LoadAsync(string.Empty, TestContext.Current.CancellationToken);
        workflow.ApplyFilter("pets");

        workflow.ApplyFilter(string.Empty);

        workflow.History.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApplyFilter_PreservesItemIdentityForUnchangedEntries()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var now = DateTimeOffset.UtcNow;
        await repository.SaveAsync(Entry("Get users", "GET", "http://localhost/users", now), TestContext.Current.CancellationToken);
        await repository.SaveAsync(Entry("Get pets", "GET", "http://localhost/pets", now.AddSeconds(-1)), TestContext.Current.CancellationToken);

        var workflow = new RequestHistoryWorkflow(repository);
        await workflow.LoadAsync(string.Empty, TestContext.Current.CancellationToken);
        var originalFirst = workflow.History[0];

        workflow.ApplyFilter("g");

        workflow.History[0].Should().BeSameAs(originalFirst);
    }

    [Fact]
    public void BuildEditorProjection_MapsFieldsAndDefaultsNullBodyToEmpty()
    {
        var entry = Entry("Get users", "GET", "http://localhost/users", DateTimeOffset.UtcNow, body: null);

        var projection = RequestHistoryWorkflow.BuildEditorProjection(entry);

        projection.Method.Should().Be("GET");
        projection.Name.Should().Be("Get users");
        projection.Url.Should().Be("http://localhost/users");
        projection.Body.Should().Be(string.Empty);
    }

    [Fact]
    public void BuildEditorProjection_PreservesBodyWhenPresent()
    {
        var entry = Entry("Create user", "POST", "http://localhost/users", DateTimeOffset.UtcNow, body: "{\"name\":\"Ada\"}");

        var projection = RequestHistoryWorkflow.BuildEditorProjection(entry);

        projection.Body.Should().Be("{\"name\":\"Ada\"}");
    }
}
