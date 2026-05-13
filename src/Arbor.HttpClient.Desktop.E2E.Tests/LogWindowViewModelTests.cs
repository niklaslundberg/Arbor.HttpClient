using Arbor.HttpClient.Desktop.Features.Logging;
using Avalonia.Threading;
using Serilog;
using System.Collections.Specialized;

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

        var observableEntryReceived = new TaskCompletionSource<LogEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = sink.EntryAddedObservable.Subscribe(entry => observableEntryReceived.TrySetResult(entry));

        logger.ForContext("LogTab", LogTab.HttpRequests).Information("reactive");

        var observableEntry = await observableEntryReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

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

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NotifyCollectionChangedEventHandler handler = (_, _) =>
        {
            if (viewModel.HttpDiagnosticsEntries.Any(entry =>
                    entry.Message.Contains("diagnostics-rx", StringComparison.Ordinal)))
            {
                completion.TrySetResult();
            }
        };
        viewModel.HttpDiagnosticsEntries.CollectionChanged += handler;

        logger.ForContext("LogTab", LogTab.HttpDiagnostics).Information("diagnostics-rx");
        await Dispatcher.UIThread.InvokeAsync(() => { });
        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            viewModel.HttpDiagnosticsEntries.CollectionChanged -= handler;
        }

        viewModel.HttpDiagnosticsEntries.Should().ContainSingle(entry => entry.Message.Contains("diagnostics-rx"));
    }
}
