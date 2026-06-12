using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Collections;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Testing.Repositories;
using Microsoft.Reactive.Testing;
using Serilog.Core;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class CollectionInheritedHeadersWorkflowTests
{
    private sealed class Harness
    {
        public InMemoryCollectionRepository Repository { get; } = new();

        public List<Collection> Collections { get; private set; } = [];

        public List<int> ReselectedCollectionIds { get; } = [];

        public TestScheduler Scheduler { get; } = new();

        public CollectionInheritedHeadersWorkflow Workflow { get; }

        public Harness(Func<CancellationToken, Task>? reloadOverride = null)
        {
            Workflow = new CollectionInheritedHeadersWorkflow(
                Repository,
                collectionId => Collections.FirstOrDefault(collection => collection.Id == collectionId),
                reloadOverride ?? ReloadCollectionsAsync,
                ReselectedCollectionIds.Add,
                Logger.None,
                Scheduler);
        }

        public async Task<Collection> SeedCollectionAsync(IReadOnlyList<RequestHeader>? headers = null)
        {
            var collectionId = await Repository.SaveAsync(
                "Test Collection",
                sourcePath: null,
                baseUrl: "https://api.example.com",
                requests: [new CollectionRequest("List pets", "GET", "/pets", null)],
                headers);
            await ReloadCollectionsAsync(TestContext.Current.CancellationToken);
            return Collections.First(collection => collection.Id == collectionId);
        }

        public async Task ReloadCollectionsAsync(CancellationToken cancellationToken) =>
            Collections = [.. await Repository.GetAllAsync(cancellationToken)];
    }

    private static RequestHeaderViewModel HeaderViewModel(string name, string value = "1", bool isEnabled = true) =>
        new() { Name = name, Value = value, IsEnabled = isEnabled };

    [Fact]
    public async Task QueueAutoSave_NoSelectedCollection_DoesNotMarkPending()
    {
        var harness = new Harness();
        await harness.SeedCollectionAsync();

        harness.Workflow.QueueAutoSave(null, [HeaderViewModel("X-Trace")]);

        harness.Workflow.HasPendingAutoSave.Should().BeFalse();
    }

    [Fact]
    public async Task QueueAutoSave_WhileSuppressed_DoesNotMarkPending()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync();

        using (harness.Workflow.SuppressAutoSave())
        {
            harness.Workflow.QueueAutoSave(collection, [HeaderViewModel("X-Trace")]);
        }

        harness.Workflow.HasPendingAutoSave.Should().BeFalse();
    }

    [Fact]
    public void SuppressAutoSave_NestedScopes_ResumeOnlyAfterOutermostDisposed()
    {
        var harness = new Harness();

        using var outerScope = harness.Workflow.SuppressAutoSave();
        using var innerScope = harness.Workflow.SuppressAutoSave();

        harness.Workflow.IsAutoSaveSuppressed.Should().BeTrue();
        innerScope.Dispose();
        harness.Workflow.IsAutoSaveSuppressed.Should().BeTrue();
        outerScope.Dispose();
        harness.Workflow.IsAutoSaveSuppressed.Should().BeFalse();
    }

    [Fact]
    public async Task QueueAutoSave_WithSelectedCollection_EmitsRequestOnlyAfterThrottleInterval()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync();
        var emittedSnapshots = new List<CollectionInheritedHeadersAutoSaveSnapshot>();
        using var subscription = harness.Workflow.AutoSaveRequested.Subscribe(emittedSnapshots.Add);

        harness.Workflow.QueueAutoSave(collection, [HeaderViewModel("X-Trace")]);

        harness.Workflow.HasPendingAutoSave.Should().BeTrue();
        harness.Scheduler.AdvanceBy(CollectionInheritedHeadersWorkflow.AutoSaveThrottleInterval.Ticks - 1);
        emittedSnapshots.Should().BeEmpty();

        harness.Scheduler.AdvanceBy(1);
        emittedSnapshots.Should().ContainSingle()
            .Which.InheritedHeaders.Should().ContainSingle()
            .Which.Name.Should().Be("X-Trace");
    }

    [Fact]
    public async Task QueueAutoSave_RapidEdits_EmitsOnlyLatestSnapshot()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync();
        var emittedSnapshots = new List<CollectionInheritedHeadersAutoSaveSnapshot>();
        using var subscription = harness.Workflow.AutoSaveRequested.Subscribe(emittedSnapshots.Add);

        harness.Workflow.QueueAutoSave(collection, [HeaderViewModel("X-Trace", "1")]);
        harness.Scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);
        harness.Workflow.QueueAutoSave(collection, [HeaderViewModel("X-Trace", "2")]);
        harness.Scheduler.AdvanceBy(CollectionInheritedHeadersWorkflow.AutoSaveThrottleInterval.Ticks);

        emittedSnapshots.Should().ContainSingle()
            .Which.InheritedHeaders.Should().ContainSingle()
            .Which.Value.Should().Be("2");
    }

    [Fact]
    public async Task TriggerAutoSave_WithChangedHeaders_PersistsSnapshotAndClearsPending()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync();
        CollectionInheritedHeadersAutoSaveSnapshot? emittedSnapshot = null;
        using var subscription = harness.Workflow.AutoSaveRequested.Subscribe(snapshot => emittedSnapshot = snapshot);

        harness.Workflow.QueueAutoSave(collection, [HeaderViewModel("X-Trace")]);
        harness.Scheduler.AdvanceBy(CollectionInheritedHeadersWorkflow.AutoSaveThrottleInterval.Ticks);
        await harness.Workflow.TriggerAutoSave(emittedSnapshot!);

        var persistedCollection = (await harness.Repository.GetAllAsync(TestContext.Current.CancellationToken)).Single();
        persistedCollection.Headers.Should().ContainSingle().Which.Name.Should().Be("X-Trace");
        harness.Workflow.HasPendingAutoSave.Should().BeFalse();
        harness.ReselectedCollectionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task TriggerAutoSave_ReloadFails_SwallowsExceptionAndKeepsPendingState()
    {
        var harness = new Harness(_ => throw new InvalidOperationException("reload failed"));
        var collectionId = await harness.Repository.SaveAsync(
            "Test Collection",
            sourcePath: null,
            baseUrl: null,
            requests: [],
            cancellationToken: TestContext.Current.CancellationToken);
        harness.Collections.AddRange(await harness.Repository.GetAllAsync(TestContext.Current.CancellationToken));
        var collection = harness.Collections.First(candidate => candidate.Id == collectionId);

        harness.Workflow.QueueAutoSave(collection, [HeaderViewModel("X-Trace")]);
        var snapshot = harness.Workflow.BuildSnapshot(collection, [HeaderViewModel("X-Trace")]);

        var triggerAutoSave = async () => await harness.Workflow.TriggerAutoSave(snapshot!);

        await triggerAutoSave.Should().NotThrowAsync();
        harness.Workflow.HasPendingAutoSave.Should().BeTrue();
    }

    [Fact]
    public async Task FlushPendingAutoSaveAsync_WithPendingChanges_PersistsAndReselectsCollection()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync();

        harness.Workflow.QueueAutoSave(collection, [HeaderViewModel("X-Trace")]);
        await harness.Workflow.FlushPendingAutoSaveAsync();

        var persistedCollection = (await harness.Repository.GetAllAsync(TestContext.Current.CancellationToken)).Single();
        persistedCollection.Headers.Should().ContainSingle().Which.Name.Should().Be("X-Trace");
        harness.Workflow.HasPendingAutoSave.Should().BeFalse();
        harness.ReselectedCollectionIds.Should().Equal(collection.Id);
    }

    [Fact]
    public async Task FlushPendingAutoSaveAsync_NoPendingChanges_DoesNothing()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync();

        await harness.Workflow.FlushPendingAutoSaveAsync();

        (await harness.Repository.GetAllAsync(TestContext.Current.CancellationToken)).Single().Headers.Should().BeNull();
        harness.ReselectedCollectionIds.Should().BeEmpty();
        collection.Headers.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithChangedHeaders_PersistsImmediatelyAndReselectsCollection()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync();

        await harness.Workflow.SaveAsync(collection, [HeaderViewModel("X-Api-Version", "2")], TestContext.Current.CancellationToken);

        var persistedCollection = (await harness.Repository.GetAllAsync(TestContext.Current.CancellationToken)).Single();
        persistedCollection.Headers.Should().ContainSingle().Which.Value.Should().Be("2");
        harness.ReselectedCollectionIds.Should().Equal(collection.Id);
    }

    [Fact]
    public async Task SaveAsync_UnchangedHeaders_SkipsPersistence()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync(headers: [new RequestHeader("X-Trace", "1", true)]);

        await harness.Workflow.SaveAsync(collection, [HeaderViewModel("X-Trace", "1")], TestContext.Current.CancellationToken);

        harness.ReselectedCollectionIds.Should().BeEmpty();
    }

    [Fact]
    public void BuildSnapshot_NoSelectedCollection_ReturnsNull()
    {
        var harness = new Harness();

        harness.Workflow.BuildSnapshot(null, [HeaderViewModel("X-Trace")]).Should().BeNull();
    }

    [Fact]
    public async Task BuildSnapshot_SelectedCollection_CapturesCollectionIdentityAndHeaders()
    {
        var harness = new Harness();
        var collection = await harness.SeedCollectionAsync();

        var snapshot = harness.Workflow.BuildSnapshot(collection, [HeaderViewModel("X-Trace")]);

        snapshot.Should().NotBeNull();
        snapshot!.CollectionId.Should().Be(collection.Id);
        snapshot.CollectionName.Should().Be("Test Collection");
        snapshot.CollectionBaseUrl.Should().Be("https://api.example.com");
        snapshot.CollectionRequests.Should().ContainSingle().Which.Name.Should().Be("List pets");
        snapshot.InheritedHeaders.Should().ContainSingle().Which.Name.Should().Be("X-Trace");
    }

    [Fact]
    public void BuildHeaders_BlankNamesSkippedAndNamesTrimmed()
    {
        var headers = CollectionInheritedHeadersWorkflow.BuildHeaders(
        [
            HeaderViewModel("  X-Trace  ", "1"),
            HeaderViewModel("   "),
            HeaderViewModel(string.Empty)
        ]);

        headers.Should().ContainSingle().Which.Name.Should().Be("X-Trace");
    }

    [Fact]
    public void BuildHeaders_NoNamedRows_ReturnsNull()
    {
        CollectionInheritedHeadersWorkflow.BuildHeaders([HeaderViewModel(string.Empty)]).Should().BeNull();
    }

    [Fact]
    public void MergeCollectionAndRequestHeaders_RequestHeaderOverridesCollectionHeaderInPlace()
    {
        var collectionHeaders = new List<RequestHeader>
        {
            new("X-Api-Key", "collection", true),
            new("X-Trace", "1", true)
        };
        var requestHeaders = new List<RequestHeader> { new("x-api-key", "request", true) };

        var merged = CollectionInheritedHeadersWorkflow.MergeCollectionAndRequestHeaders(collectionHeaders, requestHeaders);

        merged.Should().HaveCount(2);
        merged![0].Value.Should().Be("request");
        merged[1].Name.Should().Be("X-Trace");
    }

    [Fact]
    public void MergeCollectionAndRequestHeaders_BothEmpty_ReturnsNull()
    {
        CollectionInheritedHeadersWorkflow.MergeCollectionAndRequestHeaders(null, null).Should().BeNull();
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("X-Trace", null, false)]
    [InlineData(null, "X-Trace", false)]
    [InlineData("X-Trace", "X-Trace", true)]
    [InlineData("X-Trace", "X-Other", false)]
    public void HeadersEqual_ComparesNullToleranceAndNames(string? leftName, string? rightName, bool expectedEqual)
    {
        var left = leftName is null ? null : new List<RequestHeader> { new(leftName, "1", true) };
        var right = rightName is null ? null : new List<RequestHeader> { new(rightName, "1", true) };

        CollectionInheritedHeadersWorkflow.HeadersEqual(left, right).Should().Be(expectedEqual);
    }

    [Fact]
    public void HeadersEqual_DifferentValueOrEnabledState_ReturnsFalse()
    {
        var baseline = new List<RequestHeader> { new("X-Trace", "1", true) };

        CollectionInheritedHeadersWorkflow.HeadersEqual(baseline, [new RequestHeader("X-Trace", "2", true)])
            .Should().BeFalse();
        CollectionInheritedHeadersWorkflow.HeadersEqual(baseline, [new RequestHeader("X-Trace", "1", false)])
            .Should().BeFalse();
        CollectionInheritedHeadersWorkflow.HeadersEqual(baseline, [new RequestHeader("X-Trace", "1", true), new RequestHeader("X-Other", "1", true)])
            .Should().BeFalse();
    }
}
