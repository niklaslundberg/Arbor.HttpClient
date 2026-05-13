using Arbor.HttpClient.Desktop.Features.Logging;
using Avalonia.Threading;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class LogWindowViewModelTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public void Constructor_ShouldRouteExistingEntriesToMatchingTabs()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        logger.ForContext("LogTab", LogTab.ScheduledLive).Information("scheduled");
        logger.ForContext("LogTab", LogTab.HttpDiagnostics).Information("diagnostics");
        logger.ForContext("LogTab", LogTab.HttpRequests).Information("request");
        logger.ForContext("LogTab", LogTab.Debug).Information("debug");

        using var viewModel = new LogWindowViewModel(sink);

        viewModel.Entries.Should().ContainSingle(entry => entry.Message.Contains("scheduled"));
        viewModel.HttpDiagnosticsEntries.Should().ContainSingle(entry => entry.Message.Contains("diagnostics"));
        viewModel.HttpRequestEntries.Should().ContainSingle(entry => entry.Message.Contains("request"));
        viewModel.DebugEntries.Should().ContainSingle(entry => entry.Message.Contains("debug"));
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Emit_ShouldPublishToEventAndObservableEndpoints()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        LogEntry? eventEntry = null;
        sink.EntryAdded += (_, entry) => eventEntry = entry;

        var observableEntryCompletion = new TaskCompletionSource<LogEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = sink.EntryAddedObservable.Subscribe(entry => observableEntryCompletion.TrySetResult(entry));

        logger.ForContext("LogTab", LogTab.HttpRequests).Information("reactive");

        var observableEntry = await observableEntryCompletion.Task.WaitAsync(TimeSpan.FromSeconds(2));

        eventEntry.Should().NotBeNull();
        eventEntry!.Tab.Should().Be(LogTab.HttpRequests);
        observableEntry.Tab.Should().Be(LogTab.HttpRequests);
        observableEntry.Message.Should().Contain("reactive");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task Constructor_WhenNewEntryIsEmitted_ShouldRouteViaObservableEndpoint()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        using var viewModel = new LogWindowViewModel(sink);

        logger.ForContext("LogTab", LogTab.HttpDiagnostics).Information("diagnostics-rx");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline &&
               !viewModel.HttpDiagnosticsEntries.Any(entry => entry.Message.Contains("diagnostics-rx", StringComparison.Ordinal)))
        {
            await Dispatcher.UIThread.InvokeAsync(() => { });
            await Task.Delay(10);
        }

        viewModel.HttpDiagnosticsEntries.Should().ContainSingle(entry => entry.Message.Contains("diagnostics-rx"));
    }
}
