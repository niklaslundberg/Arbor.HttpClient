using Arbor.HttpClient.Desktop.Features.Layout;
using Microsoft.Reactive.Testing;
using Serilog;
using Serilog.Core;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Unit tests for <see cref="DraftWorkflow"/>. These do not require the Avalonia headless
/// session — UI-thread marshalling is the caller's responsibility and is exercised via plain
/// callbacks here.
/// </summary>
public sealed class DraftWorkflowTests
{
    private static ILogger CreateSilentLogger() => Logger.None;

    private static string CreateTempDraftsFolder() =>
        Path.Join(Path.GetTempPath(), $"arbor-drafts-{Guid.NewGuid():N}");

    private static RequestEditorSnapshot CreateSnapshot(string requestName = "My Request") => new()
    {
        RequestName = requestName,
        SavedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task LoadPendingDraftAsync_WithoutStore_ReturnsNull()
    {
        var workflow = new DraftWorkflow(draftPersistenceService: null, CreateSilentLogger());

        var result = await workflow.LoadPendingDraftAsync(TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadPendingDraftAsync_WithSavedDraft_ReturnsDraft()
    {
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        var saved = CreateSnapshot();
        await service.SaveDraftAsync(saved, TestContext.Current.CancellationToken);
        var workflow = new DraftWorkflow(service, CreateSilentLogger());

        var result = await workflow.LoadPendingDraftAsync(TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.RequestName.Should().Be(saved.RequestName);
    }

    [Fact]
    public async Task TakePendingDraftAsync_AfterLoad_ClearsInMemoryPendingDraft()
    {
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        await service.SaveDraftAsync(CreateSnapshot(), TestContext.Current.CancellationToken);
        var workflow = new DraftWorkflow(service, CreateSilentLogger());
        await workflow.LoadPendingDraftAsync(TestContext.Current.CancellationToken);

        var first = await workflow.TakePendingDraftAsync(TestContext.Current.CancellationToken);
        service.ClearDraft();
        var second = await workflow.TakePendingDraftAsync(TestContext.Current.CancellationToken);

        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public async Task TakePendingDraftAsync_WithoutPriorLoad_LoadsFromDisk()
    {
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        var saved = CreateSnapshot();
        await service.SaveDraftAsync(saved, TestContext.Current.CancellationToken);
        var workflow = new DraftWorkflow(service, CreateSilentLogger());

        var result = await workflow.TakePendingDraftAsync(TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.RequestName.Should().Be(saved.RequestName);
    }

    [Fact]
    public async Task DiscardDraft_DeletesPersistedDraftFile()
    {
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        await service.SaveDraftAsync(CreateSnapshot(), TestContext.Current.CancellationToken);
        var workflow = new DraftWorkflow(service, CreateSilentLogger());
        await workflow.LoadPendingDraftAsync(TestContext.Current.CancellationToken);

        workflow.DiscardDraft();

        File.Exists(service.DraftFilePath).Should().BeFalse();
        (await workflow.TakePendingDraftAsync(TestContext.Current.CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task SaveDraftAsync_WithStore_PersistsState()
    {
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        var workflow = new DraftWorkflow(service, CreateSilentLogger());
        var state = CreateSnapshot("Saved via workflow");

        await workflow.SaveDraftAsync(state, TestContext.Current.CancellationToken);

        var loaded = await service.LoadDraftAsync(TestContext.Current.CancellationToken);
        loaded.Should().NotBeNull();
        loaded!.RequestName.Should().Be("Saved via workflow");
    }

    [Fact]
    public async Task SaveDraftAsync_WithoutStore_DoesNotThrow()
    {
        var workflow = new DraftWorkflow(draftPersistenceService: null, CreateSilentLogger());

        var act = async () => await workflow.SaveDraftAsync(CreateSnapshot(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void StartAutoSave_WithoutStore_NeverInvokesCallback()
    {
        var scheduler = new TestScheduler();
        var workflow = new DraftWorkflow(draftPersistenceService: null, CreateSilentLogger(), scheduler);
        var tickCount = 0;

        workflow.StartAutoSave(() =>
        {
            tickCount++;
            return Task.CompletedTask;
        });
        scheduler.AdvanceBy(DraftWorkflow.AutoSaveInterval.Ticks * 3);

        tickCount.Should().Be(0);
    }

    [Fact]
    public void StartAutoSave_WithStore_InvokesCallbackOnEachInterval()
    {
        var scheduler = new TestScheduler();
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        var workflow = new DraftWorkflow(service, CreateSilentLogger(), scheduler);
        var tickCount = 0;

        workflow.StartAutoSave(() =>
        {
            tickCount++;
            return Task.CompletedTask;
        });

        scheduler.AdvanceBy(DraftWorkflow.AutoSaveInterval.Ticks - 1);
        tickCount.Should().Be(0);

        scheduler.AdvanceBy(1);
        tickCount.Should().Be(1);

        scheduler.AdvanceBy(DraftWorkflow.AutoSaveInterval.Ticks);
        tickCount.Should().Be(2);
    }

    [Fact]
    public void StartAutoSave_CalledAgain_ReplacesPreviousSubscription()
    {
        var scheduler = new TestScheduler();
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        var workflow = new DraftWorkflow(service, CreateSilentLogger(), scheduler);
        var firstCount = 0;
        var secondCount = 0;

        workflow.StartAutoSave(() =>
        {
            firstCount++;
            return Task.CompletedTask;
        });
        workflow.StartAutoSave(() =>
        {
            secondCount++;
            return Task.CompletedTask;
        });

        scheduler.AdvanceBy(DraftWorkflow.AutoSaveInterval.Ticks);

        firstCount.Should().Be(0);
        secondCount.Should().Be(1);
    }

    [Fact]
    public void StopAutoSave_StopsFurtherCallbacks()
    {
        var scheduler = new TestScheduler();
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        var workflow = new DraftWorkflow(service, CreateSilentLogger(), scheduler);
        var tickCount = 0;

        workflow.StartAutoSave(() =>
        {
            tickCount++;
            return Task.CompletedTask;
        });
        scheduler.AdvanceBy(DraftWorkflow.AutoSaveInterval.Ticks);
        workflow.StopAutoSave();
        scheduler.AdvanceBy(DraftWorkflow.AutoSaveInterval.Ticks * 3);

        tickCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_StopsAutoSave()
    {
        var scheduler = new TestScheduler();
        var service = new DraftPersistenceService(CreateTempDraftsFolder());
        using var workflow = new DraftWorkflow(service, CreateSilentLogger(), scheduler);
        var tickCount = 0;

        workflow.StartAutoSave(() =>
        {
            tickCount++;
            return Task.CompletedTask;
        });
        workflow.Dispose();
        scheduler.AdvanceBy(DraftWorkflow.AutoSaveInterval.Ticks * 3);

        tickCount.Should().Be(0);
    }
}
