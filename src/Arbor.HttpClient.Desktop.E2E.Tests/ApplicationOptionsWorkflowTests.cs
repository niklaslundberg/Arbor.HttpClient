using System.Reactive;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Options;
using Microsoft.Reactive.Testing;
using Serilog;
using Serilog.Core;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class ApplicationOptionsWorkflowTests
{
    private static ApplicationOptionsSnapshot CreateValidSnapshot() => new()
    {
        HttpVersion = "1.1",
        TlsVersion = "SystemDefault",
        EnableHttpDiagnostics = true,
        DefaultContentType = "application/json",
        FollowRedirects = false,
        ShowRequestPreviewByDefault = true,
        DefaultRequestUrl = "https://example.com/api",
        ResponseSaveDefaultFolder = string.Empty,
        ResponseSaveFileNamePattern = "{requestName}-{timestamp}",
        DemoServerPort = 5000,
        DemoServerHttpsPort = 5001,
        DemoServerHttpEnabled = true,
        DemoServerHttpsEnabled = false,
        DefaultRequestTimeoutSeconds = 42,
        Theme = "Dark",
        FontFamily = "Consolas,Menlo,monospace",
        FontSizeText = "14.5",
        AutoStartScheduledJobsOnLaunch = false,
        DefaultScheduledJobIntervalSeconds = 30,
        CollectUnhandledExceptions = true
    };

    private static ILogger CreateSilentLogger() => Logger.None;

    private static ApplicationOptionsStore CreateTempStore() =>
        new(Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "options.json"));

    [Fact]
    public void BuildOptions_ValidSnapshot_MapsAllFields()
    {
        var snapshot = CreateValidSnapshot();
        var layouts = new LayoutOptions();

        var options = ApplicationOptionsWorkflow.BuildOptions(snapshot, layouts);

        options.Http.HttpVersion.Should().Be("1.1");
        options.Http.TlsVersion.Should().Be("SystemDefault");
        options.Http.EnableHttpDiagnostics.Should().BeTrue();
        options.Http.DefaultContentType.Should().Be("application/json");
        options.Http.FollowRedirects.Should().BeFalse();
        options.Http.ShowRequestPreviewByDefault.Should().BeTrue();
        options.Http.DefaultRequestUrl.Should().Be("https://example.com/api");
        options.Http.ResponseSaveFileNamePattern.Should().Be("{requestName}-{timestamp}");
        options.Http.DemoServerPort.Should().Be(5000);
        options.Http.DemoServerHttpsPort.Should().Be(5001);
        options.Http.DemoServerHttpEnabled.Should().BeTrue();
        options.Http.DemoServerHttpsEnabled.Should().BeFalse();
        options.Http.DefaultRequestTimeoutSeconds.Should().Be(42);
        options.Appearance.Theme.Should().Be("Dark");
        options.Appearance.FontFamily.Should().Be("Consolas,Menlo,monospace");
        options.Appearance.FontSize.Should().Be(14.5);
        options.ScheduledJobs.AutoStartOnLaunch.Should().BeFalse();
        options.ScheduledJobs.DefaultIntervalSeconds.Should().Be(30);
        options.Diagnostics.CollectUnhandledExceptions.Should().BeTrue();
        options.Layouts.Should().BeSameAs(layouts);
    }

    [Fact]
    public void BuildOptions_NonNumericFontSize_ThrowsInvalidDataException()
    {
        var snapshot = CreateValidSnapshot() with { FontSizeText = "large" };

        var act = () => ApplicationOptionsWorkflow.BuildOptions(snapshot, new LayoutOptions());

        act.Should().Throw<InvalidDataException>().WithMessage("Font size must be a number.");
    }

    [Fact]
    public void BuildOptions_ScheduledJobIntervalBelowOne_ClampsToOne()
    {
        var snapshot = CreateValidSnapshot() with { DefaultScheduledJobIntervalSeconds = 0 };

        var options = ApplicationOptionsWorkflow.BuildOptions(snapshot, new LayoutOptions());

        options.ScheduledJobs.DefaultIntervalSeconds.Should().Be(1);
    }

    [Fact]
    public void BuildOptions_UnsupportedTlsVersion_ThrowsInvalidDataException()
    {
        var snapshot = CreateValidSnapshot() with { TlsVersion = "Ssl3" };

        var act = () => ApplicationOptionsWorkflow.BuildOptions(snapshot, new LayoutOptions());

        act.Should().Throw<InvalidDataException>().WithMessage("*TLS version*");
    }

    [Fact]
    public void QueueAutoSave_WithStore_EmitsAfterThrottleInterval()
    {
        var scheduler = new TestScheduler();
        using var workflow = new ApplicationOptionsWorkflow(CreateTempStore(), CreateSilentLogger(), scheduler);
        var emitted = new List<Unit>();
        using var subscription = workflow.AutoSaveRequested.Subscribe(emitted.Add);

        workflow.QueueAutoSave();
        scheduler.AdvanceBy(ApplicationOptionsWorkflow.AutoSaveThrottleInterval.Ticks - 1);
        emitted.Should().BeEmpty();

        scheduler.AdvanceBy(1);
        emitted.Should().HaveCount(1);
    }

    [Fact]
    public void QueueAutoSave_BurstWithinThrottleWindow_EmitsOnce()
    {
        var scheduler = new TestScheduler();
        using var workflow = new ApplicationOptionsWorkflow(CreateTempStore(), CreateSilentLogger(), scheduler);
        var emitted = new List<Unit>();
        using var subscription = workflow.AutoSaveRequested.Subscribe(emitted.Add);

        workflow.QueueAutoSave();
        workflow.QueueAutoSave();
        workflow.QueueAutoSave();
        scheduler.AdvanceBy(ApplicationOptionsWorkflow.AutoSaveThrottleInterval.Ticks * 2);

        emitted.Should().HaveCount(1);
    }

    [Fact]
    public void QueueAutoSave_WithoutStore_DoesNotEmit()
    {
        var scheduler = new TestScheduler();
        using var workflow = new ApplicationOptionsWorkflow(store: null, CreateSilentLogger(), scheduler);
        var emitted = new List<Unit>();
        using var subscription = workflow.AutoSaveRequested.Subscribe(emitted.Add);

        workflow.QueueAutoSave();
        scheduler.AdvanceBy(ApplicationOptionsWorkflow.AutoSaveThrottleInterval.Ticks * 2);

        emitted.Should().BeEmpty();
        workflow.HasStore.Should().BeFalse();
    }

    [Fact]
    public void QueueAutoSave_WhileSuppressed_DoesNotEmit()
    {
        var scheduler = new TestScheduler();
        using var workflow = new ApplicationOptionsWorkflow(CreateTempStore(), CreateSilentLogger(), scheduler);
        var emitted = new List<Unit>();
        using var subscription = workflow.AutoSaveRequested.Subscribe(emitted.Add);

        using (workflow.SuppressAutoSave())
        {
            workflow.IsAutoSaveSuppressed.Should().BeTrue();
            workflow.QueueAutoSave();
        }

        scheduler.AdvanceBy(ApplicationOptionsWorkflow.AutoSaveThrottleInterval.Ticks * 2);

        emitted.Should().BeEmpty();
        workflow.IsAutoSaveSuppressed.Should().BeFalse();
    }

    [Fact]
    public void SuppressAutoSave_NestedScopes_ResumeOnlyAfterOutermostDisposed()
    {
        using var workflow = new ApplicationOptionsWorkflow(CreateTempStore(), CreateSilentLogger(), new TestScheduler());

        var outerScope = workflow.SuppressAutoSave();
        var innerScope = workflow.SuppressAutoSave();

        innerScope.Dispose();
        workflow.IsAutoSaveSuppressed.Should().BeTrue();

        outerScope.Dispose();
        workflow.IsAutoSaveSuppressed.Should().BeFalse();
    }

    [Fact]
    public void Save_BuildThrows_ReturnsFailedOutcome()
    {
        using var workflow = new ApplicationOptionsWorkflow(store: null, CreateSilentLogger());

        var outcome = workflow.Save(
            () => throw new InvalidDataException("Font size must be a number."),
            _ => { });

        outcome.IsSuccessful.Should().BeFalse();
        outcome.ErrorMessage.Should().Be("Options could not be saved: Font size must be a number.");
    }

    [Fact]
    public void Save_WithoutStore_StillInvokesOnSavedAndSucceeds()
    {
        using var workflow = new ApplicationOptionsWorkflow(store: null, CreateSilentLogger());
        var built = ApplicationOptionsWorkflow.BuildOptions(CreateValidSnapshot(), new LayoutOptions());
        ApplicationOptions? applied = null;

        var outcome = workflow.Save(() => built, options => applied = options);

        outcome.IsSuccessful.Should().BeTrue();
        outcome.ErrorMessage.Should().BeEmpty();
        applied.Should().BeSameAs(built);
    }

    [Fact]
    public void Save_OnSavedThrows_ReturnsFailedOutcome()
    {
        using var workflow = new ApplicationOptionsWorkflow(store: null, CreateSilentLogger());
        var built = ApplicationOptionsWorkflow.BuildOptions(CreateValidSnapshot(), new LayoutOptions());

        var outcome = workflow.Save(() => built, _ => throw new InvalidOperationException("projection failed"));

        outcome.IsSuccessful.Should().BeFalse();
        outcome.ErrorMessage.Should().Be("Options could not be saved: projection failed");
    }

    [Fact]
    public async Task ExportAsync_PickerCancelled_ReturnsSuccessWithoutExporting()
    {
        using var workflow = new ApplicationOptionsWorkflow(store: null, CreateSilentLogger());
        var built = ApplicationOptionsWorkflow.BuildOptions(CreateValidSnapshot(), new LayoutOptions());

        var outcome = await workflow.ExportAsync(() => built, () => Task.FromResult<string?>(null));

        outcome.IsSuccessful.Should().BeTrue();
        outcome.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAsync_BuildThrows_FailsWithoutInvokingPicker()
    {
        using var workflow = new ApplicationOptionsWorkflow(store: null, CreateSilentLogger());
        var pickerInvoked = false;

        var outcome = await workflow.ExportAsync(
            () => throw new InvalidDataException("Font size must be a number."),
            () =>
            {
                pickerInvoked = true;
                return Task.FromResult<string?>("unused.json");
            });

        outcome.IsSuccessful.Should().BeFalse();
        outcome.ErrorMessage.Should().Be("Options export failed: Font size must be a number.");
        pickerInvoked.Should().BeFalse();
    }

    [Fact]
    public void Import_WithoutStore_ReturnsFailedOutcome()
    {
        using var workflow = new ApplicationOptionsWorkflow(store: null, CreateSilentLogger());

        var outcome = workflow.Import("anywhere.json", _ => { });

        outcome.IsSuccessful.Should().BeFalse();
        outcome.ErrorMessage.Should().Be("Options import failed: no options store is configured.");
    }
}

[Trait("Category", "Integration")]
public sealed class ApplicationOptionsWorkflowPersistenceTests
{
    private static ApplicationOptionsSnapshot CreateValidSnapshot() => new()
    {
        HttpVersion = "2.0",
        TlsVersion = "Tls12",
        EnableHttpDiagnostics = false,
        DefaultContentType = "application/json",
        FollowRedirects = true,
        ShowRequestPreviewByDefault = false,
        DefaultRequestUrl = "http://localhost:5000/echo",
        ResponseSaveDefaultFolder = string.Empty,
        ResponseSaveFileNamePattern = "{requestName}",
        DemoServerPort = 5000,
        DemoServerHttpsPort = 5001,
        DemoServerHttpEnabled = true,
        DemoServerHttpsEnabled = false,
        DefaultRequestTimeoutSeconds = 100,
        Theme = "Light",
        FontFamily = "Consolas,Menlo,monospace",
        FontSizeText = "13",
        AutoStartScheduledJobsOnLaunch = true,
        DefaultScheduledJobIntervalSeconds = 60,
        CollectUnhandledExceptions = false
    };

    [Fact]
    public void Save_WithStore_PersistsOptionsToDisk()
    {
        var optionsPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "options.json");
        var store = new ApplicationOptionsStore(optionsPath);
        using var workflow = new ApplicationOptionsWorkflow(store, Serilog.Core.Logger.None);
        var built = ApplicationOptionsWorkflow.BuildOptions(CreateValidSnapshot(), new LayoutOptions());
        ApplicationOptions? applied = null;

        try
        {
            var outcome = workflow.Save(() => built, options => applied = options);

            outcome.IsSuccessful.Should().BeTrue();
            applied.Should().BeSameAs(built);
            store.Load().Appearance.Theme.Should().Be("Light");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(optionsPath)!, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_WithStore_WritesExportFile()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var store = new ApplicationOptionsStore(Path.Join(tempDir, "options.json"));
        using var workflow = new ApplicationOptionsWorkflow(store, Serilog.Core.Logger.None);
        var built = ApplicationOptionsWorkflow.BuildOptions(CreateValidSnapshot(), new LayoutOptions());
        var exportPath = Path.Join(tempDir, "exported-options.json");

        try
        {
            var outcome = await workflow.ExportAsync(() => built, () => Task.FromResult<string?>(exportPath));

            outcome.IsSuccessful.Should().BeTrue();
            File.Exists(exportPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Import_ExportedFile_RoundTripsAndInvokesOnImported()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var store = new ApplicationOptionsStore(Path.Join(tempDir, "options.json"));
        using var workflow = new ApplicationOptionsWorkflow(store, Serilog.Core.Logger.None);
        var built = ApplicationOptionsWorkflow.BuildOptions(CreateValidSnapshot(), new LayoutOptions());
        var importPath = Path.Join(tempDir, "import-options.json");
        store.Export(importPath, built);
        ApplicationOptions? imported = null;

        try
        {
            var outcome = workflow.Import(importPath, options => imported = options);

            outcome.IsSuccessful.Should().BeTrue();
            imported.Should().NotBeNull();
            imported.Http.DefaultRequestTimeoutSeconds.Should().Be(100);
            store.Load().Http.DefaultRequestTimeoutSeconds.Should().Be(100);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Import_InvalidJsonFile_ReturnsFailedOutcome()
    {
        var tempDir = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var store = new ApplicationOptionsStore(Path.Join(tempDir, "options.json"));
        using var workflow = new ApplicationOptionsWorkflow(store, Serilog.Core.Logger.None);
        var importPath = Path.Join(tempDir, "invalid-options.json");
        File.WriteAllText(importPath, "{ not valid json");
        var onImportedInvoked = false;

        try
        {
            var outcome = workflow.Import(importPath, _ => onImportedInvoked = true);

            outcome.IsSuccessful.Should().BeFalse();
            outcome.ErrorMessage.Should().StartWith("Options import failed: ");
            onImportedInvoked.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
