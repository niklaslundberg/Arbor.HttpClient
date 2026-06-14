using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Arbor.HttpClient.Desktop.Features.Diagnostics;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Serilog;

namespace Arbor.HttpClient.Desktop.Features.Options;

/// <summary>
/// Owns application-options persistence: debounced auto-save scheduling, building a validated
/// <see cref="ApplicationOptions"/> from a UI state snapshot, and the save/export/import flows.
/// Projecting saved options back onto view-model state stays with the host view model and is
/// passed in as a callback so failures surface through the returned outcome.
/// </summary>
public sealed class ApplicationOptionsWorkflow : IDisposable
{
    public static readonly TimeSpan AutoSaveThrottleInterval = TimeSpan.FromSeconds(1);

    private readonly ApplicationOptionsStore? _store;
    private readonly ILogger _debugLogger;
    private readonly Subject<Unit> _autoSaveRequestedSubject = new();
    private readonly IObservable<Unit> _autoSaveRequested;
    private int _autoSaveSuppressionDepth;

    public ApplicationOptionsWorkflow(
        ApplicationOptionsStore? store,
        ILogger debugLogger,
        IScheduler? autoSaveScheduler = null)
    {
        _store = store;
        _debugLogger = debugLogger;
        _autoSaveRequested = _autoSaveRequestedSubject
            .Throttle(AutoSaveThrottleInterval, autoSaveScheduler ?? DefaultScheduler.Instance);
    }

    /// <summary>True when an options store is configured so persistence operations can run.</summary>
    public bool HasStore => _store is not null;

    /// <summary>
    /// Debounced stream of auto-save requests. The subscriber is responsible for marshalling
    /// the actual save onto the UI thread.
    /// </summary>
    public IObservable<Unit> AutoSaveRequested => _autoSaveRequested;

    public bool IsAutoSaveSuppressed => Volatile.Read(ref _autoSaveSuppressionDepth) > 0;

    /// <summary>
    /// Suppresses auto-save queuing until the returned scope is disposed. Scopes may nest;
    /// auto-save resumes when the outermost scope is disposed.
    /// </summary>
    public IDisposable SuppressAutoSave()
    {
        Interlocked.Increment(ref _autoSaveSuppressionDepth);
        return Disposable.Create(() => Interlocked.Decrement(ref _autoSaveSuppressionDepth));
    }

    /// <summary>
    /// Requests a debounced auto-save. No-op while suppressed or when no store is configured.
    /// </summary>
    public void QueueAutoSave()
    {
        if (IsAutoSaveSuppressed || _store is null)
        {
            return;
        }

        _autoSaveRequestedSubject.OnNext(Unit.Default);
    }

    /// <summary>
    /// Builds a validated <see cref="ApplicationOptions"/> from the snapshot. Throws
    /// <see cref="InvalidDataException"/> when the font size is not a number and propagates
    /// validation failures from <see cref="ApplicationOptionsStore.Validate"/>.
    /// </summary>
    public static ApplicationOptions BuildOptions(ApplicationOptionsSnapshot snapshot, LayoutOptions layouts)
    {
        if (!double.TryParse(snapshot.FontSizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize))
        {
            throw new InvalidDataException("Font size must be a number.");
        }

        var options = new ApplicationOptions
        {
            Http = new HttpOptions
            {
                HttpVersion = snapshot.HttpVersion,
                TlsVersion = snapshot.TlsVersion,
                EnableHttpDiagnostics = snapshot.EnableHttpDiagnostics,
                DefaultContentType = snapshot.DefaultContentType,
                FollowRedirects = snapshot.FollowRedirects,
                ShowRequestPreviewByDefault = snapshot.ShowRequestPreviewByDefault,
                DefaultRequestUrl = snapshot.DefaultRequestUrl,
                ResponseSaveDefaultFolder = snapshot.ResponseSaveDefaultFolder,
                ResponseSaveFileNamePattern = snapshot.ResponseSaveFileNamePattern,
                DemoServerPort = snapshot.DemoServerPort,
                DemoServerHttpsPort = snapshot.DemoServerHttpsPort,
                DemoServerHttpEnabled = snapshot.DemoServerHttpEnabled,
                DemoServerHttpsEnabled = snapshot.DemoServerHttpsEnabled,
                DefaultRequestTimeoutSeconds = snapshot.DefaultRequestTimeoutSeconds
            },
            Appearance = new AppearanceOptions
            {
                Theme = snapshot.Theme,
                FontFamily = snapshot.FontFamily,
                FontSize = fontSize
            },
            ScheduledJobs = new ScheduledJobsOptions
            {
                AutoStartOnLaunch = snapshot.AutoStartScheduledJobsOnLaunch,
                DefaultIntervalSeconds = Math.Max(1, snapshot.DefaultScheduledJobIntervalSeconds)
            },
            Layouts = layouts,
            Diagnostics = new DiagnosticsOptions
            {
                CollectUnhandledExceptions = snapshot.CollectUnhandledExceptions
            }
        };

        ApplicationOptionsStore.Validate(options);
        return options;
    }

    /// <summary>
    /// Resolves the default-content-type picker selection for <paramref name="value"/>: when
    /// <paramref name="value"/> matches one of <paramref name="options"/> it becomes the
    /// selected option with an empty custom value, otherwise <paramref name="customOptionLabel"/>
    /// is selected and <paramref name="value"/> becomes the custom value.
    /// </summary>
    public static (string SelectedOption, string CustomValue) ResolveDefaultContentTypeSelection(
        string value,
        IReadOnlyList<string> options,
        string customOptionLabel)
    {
        if (options.Contains(value))
        {
            return (value, string.Empty);
        }

        return (customOptionLabel, value);
    }

    /// <summary>
    /// Builds, persists, and re-applies the current options. <paramref name="onSaved"/> runs
    /// inside the failure boundary so projection errors also surface as a failed outcome.
    /// </summary>
    public OptionsPersistenceOutcome Save(
        Func<ApplicationOptions> buildOptions,
        Action<ApplicationOptions> onSaved)
    {
        try
        {
            var options = buildOptions();
            _store?.Save(options);
            onSaved(options);
            _debugLogger.Information("Saved application options");
            return OptionsPersistenceOutcome.Success();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return OptionsPersistenceOutcome.Failed($"Options could not be saved: {exception.Message}");
        }
    }

    /// <summary>
    /// Builds the current options and exports them to the path produced by
    /// <paramref name="pickExportPath"/>. A null path (picker cancelled) is a silent success.
    /// </summary>
    public async Task<OptionsPersistenceOutcome> ExportAsync(
        Func<ApplicationOptions> buildOptions,
        Func<Task<string?>> pickExportPath)
    {
        try
        {
            var options = buildOptions();
            var exportPath = await pickExportPath();
            if (exportPath is null)
            {
                return OptionsPersistenceOutcome.Success();
            }

            _store?.Export(exportPath, options);
            _debugLogger.Information("Exported options to {Path}", exportPath);
            return OptionsPersistenceOutcome.Success();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return OptionsPersistenceOutcome.Failed($"Options export failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Imports options from <paramref name="path"/>, persists them, and invokes
    /// <paramref name="onImported"/> inside the failure boundary.
    /// </summary>
    public OptionsPersistenceOutcome Import(string path, Action<ApplicationOptions> onImported)
    {
        if (_store is null)
        {
            return OptionsPersistenceOutcome.Failed("Options import failed: no options store is configured.");
        }

        try
        {
            var options = _store.Import(path);
            _store.Save(options);
            onImported(options);
            _debugLogger.Information("Imported options from {Path}", path);
            return OptionsPersistenceOutcome.Success();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return OptionsPersistenceOutcome.Failed($"Options import failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        _autoSaveRequestedSubject.OnCompleted();
        _autoSaveRequestedSubject.Dispose();
    }
}

/// <summary>
/// Immutable snapshot of the option-related view-model state used to build
/// an <see cref="ApplicationOptions"/> instance.
/// </summary>
public sealed record ApplicationOptionsSnapshot
{
    public required string HttpVersion { get; init; }

    public required string TlsVersion { get; init; }

    public required bool EnableHttpDiagnostics { get; init; }

    public required string DefaultContentType { get; init; }

    public required bool FollowRedirects { get; init; }

    public required bool ShowRequestPreviewByDefault { get; init; }

    public required string DefaultRequestUrl { get; init; }

    public required string ResponseSaveDefaultFolder { get; init; }

    public required string ResponseSaveFileNamePattern { get; init; }

    public required int DemoServerPort { get; init; }

    public required int DemoServerHttpsPort { get; init; }

    public required bool DemoServerHttpEnabled { get; init; }

    public required bool DemoServerHttpsEnabled { get; init; }

    public required int DefaultRequestTimeoutSeconds { get; init; }

    public required string Theme { get; init; }

    public required string FontFamily { get; init; }

    public required string FontSizeText { get; init; }

    public required bool AutoStartScheduledJobsOnLaunch { get; init; }

    public required int DefaultScheduledJobIntervalSeconds { get; init; }

    public required bool CollectUnhandledExceptions { get; init; }
}

/// <summary>Result of a save/export/import operation on application options.</summary>
public sealed record OptionsPersistenceOutcome
{
    private OptionsPersistenceOutcome(bool isSuccessful, string errorMessage)
    {
        IsSuccessful = isSuccessful;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccessful { get; }

    public string ErrorMessage { get; }

    public static OptionsPersistenceOutcome Success() => new(true, string.Empty);

    public static OptionsPersistenceOutcome Failed(string errorMessage) => new(false, errorMessage);
}
