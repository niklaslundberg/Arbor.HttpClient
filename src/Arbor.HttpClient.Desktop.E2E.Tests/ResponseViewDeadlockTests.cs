using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.Threading;
using Serilog;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Regression tests for the deadlock between the AvaloniaEdit document lock and the
/// TextMateSharp tokenizer listener lock when ViewModel properties are mutated from a
/// background thread and the <see cref="ResponseView"/> tries to update editor state
/// without first marshalling to the UI thread.
/// </summary>
[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class ResponseViewDeadlockTests
{
    private sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .LogToTrace();
    }

    private static MainWindowViewModel CreateViewModel()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("{\"ok\":true}", System.Text.Encoding.UTF8, "application/json")
        });
        var httpRequestService = new HttpRequestService(new System.Net.Http.HttpClient(handler), repository);
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();
        var scheduledJobService = new ScheduledJobService(httpRequestService, logger);
        var logWindowViewModel = new LogWindowViewModel(inMemorySink);

        return new MainWindowViewModel(
            httpRequestService,
            repository,
            new InMemoryCollectionRepository(),
            new InMemoryEnvironmentRepository(),
            new InMemoryScheduledJobRepository(),
            scheduledJobService,
            logWindowViewModel);
    }

    /// <summary>
    /// Setting <see cref="MainWindowViewModel.ResponseBody"/> from a background thread
    /// must not deadlock. Before the fix, the <c>OnAppVmPropertyChanged</c> handler in
    /// <see cref="ResponseView"/> would directly mutate AvaloniaEdit document state
    /// from the worker thread, triggering a circular lock dependency with the TextMateSharp
    /// tokenizer thread.
    /// </summary>
    [Fact]
    public async Task ResponseBody_SetFromBackgroundThread_DoesNotDeadlock()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        // The test completes successfully if it does not hang. A timeout is used to detect deadlocks.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await session.Dispatch(async () =>
        {
            using var viewModel = CreateViewModel();

            // Simulate a background thread (e.g. an async HTTP response handler) setting
            // ResponseBody without being on the UI thread.
            await Task.Run(() =>
            {
                // Must not be on the UI thread.
                Dispatcher.UIThread.CheckAccess().Should().BeFalse();
                viewModel.ResponseBody = "{\"test\":\"value\"}";
                viewModel.RawResponseBody = "{\"test\":\"value\"}";
                viewModel.ResponseContentType = "application/json";
            }, cts.Token);

            // Allow any posted dispatcher callbacks to run so property-changed handlers
            // that were marshalled via Dispatcher.UIThread.Post can execute.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

            viewModel.ResponseBody.Should().Be("{\"test\":\"value\"}");
            return true;
        }, cts.Token);
    }

    /// <summary>
    /// Setting <see cref="MainWindowViewModel.ResponseBody"/> multiple times rapidly from a
    /// background thread must not deadlock even under contention.
    /// </summary>
    [Fact]
    public async Task ResponseBody_SetRepeatedly_FromBackgroundThread_DoesNotDeadlock()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await session.Dispatch(async () =>
        {
            using var viewModel = CreateViewModel();

            // Fire many rapid updates from a background thread to stress the dispatch path.
            await Task.Run(async () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    viewModel.ResponseBody = $"{{\"iteration\":{i}}}";
                    viewModel.ResponseContentType = "application/json";
                    await Task.Yield();
                }
            }, cts.Token);

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

            return true;
        }, cts.Token);
    }
}
