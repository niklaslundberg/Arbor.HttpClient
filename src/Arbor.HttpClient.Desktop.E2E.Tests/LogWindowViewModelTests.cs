using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.ViewModels;
using AwesomeAssertions;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class LogWindowViewModelTests
{
    [Fact]
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
}
