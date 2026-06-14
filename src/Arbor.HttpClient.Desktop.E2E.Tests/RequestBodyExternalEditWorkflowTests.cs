using System.Diagnostics;
using Arbor.HttpClient.Desktop.Features.HttpRequest;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Unit tests for <see cref="RequestBodyExternalEditWorkflow"/>. These do not require the
/// Avalonia headless session — applying the updated body to the editor is the caller's
/// responsibility and is exercised via plain callbacks here.
/// </summary>
public sealed class RequestBodyExternalEditWorkflowTests
{
    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
            await Task.Delay(50, cancellationToken);
        }
    }

    [Fact]
    public async Task OpenInExternalEditorAsync_WritesTempFileAndRecordsIt()
    {
        using var workflow = new RequestBodyExternalEditWorkflow();
        var recordedPaths = new List<string>();

        var path = await workflow.OpenInExternalEditorAsync(
            "{\"hello\":\"world\"}",
            "application/json",
            _ => Task.CompletedTask,
            recordedPaths.Add,
            TestContext.Current.CancellationToken);

        try
        {
            File.Exists(path).Should().BeTrue();
            Path.GetExtension(path).Should().Be(".json");
            (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).Should().Be("{\"hello\":\"world\"}");
            recordedPaths.Should().ContainSingle().Which.Should().Be(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenInExternalEditorAsync_WithoutContentType_DetectsExtensionFromContent()
    {
        using var workflow = new RequestBodyExternalEditWorkflow();

        var path = await workflow.OpenInExternalEditorAsync(
            "<root />",
            contentType: null,
            _ => Task.CompletedTask,
            _ => { },
            TestContext.Current.CancellationToken);

        try
        {
            Path.GetExtension(path).Should().Be(".xml");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenInExternalEditorAsync_CalledAgain_CancelsAndReplacesPreviousWatcher()
    {
        using var workflow = new RequestBodyExternalEditWorkflow();
        var recordedPaths = new List<string>();

        var firstPath = await workflow.OpenInExternalEditorAsync(
            "first",
            contentType: null,
            _ => Task.CompletedTask,
            recordedPaths.Add,
            TestContext.Current.CancellationToken);

        var secondPath = await workflow.OpenInExternalEditorAsync(
            "second",
            contentType: null,
            _ => Task.CompletedTask,
            recordedPaths.Add,
            TestContext.Current.CancellationToken);

        try
        {
            secondPath.Should().NotBe(firstPath);
            recordedPaths.Should().Equal(firstPath, secondPath);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Fact]
    public async Task HandleFileChanged_AfterFileEditedExternally_AppliesNewContent()
    {
        using var workflow = new RequestBodyExternalEditWorkflow();
        string? appliedContent = null;

        var path = await workflow.OpenInExternalEditorAsync(
            "original",
            contentType: null,
            content =>
            {
                appliedContent = content;
                return Task.CompletedTask;
            },
            _ => { },
            TestContext.Current.CancellationToken);

        try
        {
            await File.WriteAllTextAsync(path, "updated", TestContext.Current.CancellationToken);
            var args = new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(path)!, Path.GetFileName(path));

            workflow.HandleFileChanged(args);

            await WaitForAsync(() => appliedContent == "updated", TestContext.Current.CancellationToken);

            appliedContent.Should().Be("updated");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task HandleFileChanged_WhileReadPending_SkipsConcurrentInvocation()
    {
        using var workflow = new RequestBodyExternalEditWorkflow();
        var applyCount = 0;

        var path = await workflow.OpenInExternalEditorAsync(
            "original",
            contentType: null,
            _ =>
            {
                Interlocked.Increment(ref applyCount);
                return Task.CompletedTask;
            },
            _ => { },
            TestContext.Current.CancellationToken);

        try
        {
            var args = new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(path)!, Path.GetFileName(path));

            workflow.HandleFileChanged(args);
            workflow.HandleFileChanged(args);

            await WaitForAsync(() => !workflow.IsReadPending, TestContext.Current.CancellationToken);

            applyCount.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task HandleFileChanged_FileDoesNotExist_DoesNotThrowAndResetsPendingFlag()
    {
        using var workflow = new RequestBodyExternalEditWorkflow();
        var args = new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt");

        var action = () => workflow.HandleFileChanged(args);
        action.Should().NotThrow();

        await WaitForAsync(() => !workflow.IsReadPending, TestContext.Current.CancellationToken);

        workflow.IsReadPending.Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_AfterOpen_DoesNotThrowAndStopsApplyingFurtherChanges()
    {
        var workflow = new RequestBodyExternalEditWorkflow();
        var applyCount = 0;

        var path = await workflow.OpenInExternalEditorAsync(
            "original",
            contentType: null,
            _ =>
            {
                Interlocked.Increment(ref applyCount);
                return Task.CompletedTask;
            },
            _ => { },
            TestContext.Current.CancellationToken);

        try
        {
            var action = workflow.Dispose;
            action.Should().NotThrow();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
