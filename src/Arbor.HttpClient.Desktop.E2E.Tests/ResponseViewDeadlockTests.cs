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
    private static MainWindowViewModel CreateViewModel()
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", System.Text.Encoding.UTF8, "application/json")
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
    [AvaloniaFact(Timeout = 10_000)]
    public async Task ResponseBody_SetFromBackgroundThread_DoesNotDeadlock()
    {
        using var viewModel = CreateViewModel();

        await Task.Run(() =>
        {
            Dispatcher.UIThread.CheckAccess().Should().BeFalse();
            viewModel.Response.ResponseBody = "{\"test\":\"value\"}";
            viewModel.Response.RawResponseBody = "{\"test\":\"value\"}";
            viewModel.Response.ResponseContentType = "application/json";
        }, TestContext.Current.CancellationToken);

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        viewModel.ResponseBody.Should().Be("{\"test\":\"value\"}");
    }

    /// <summary>
    /// Setting <see cref="MainWindowViewModel.ResponseBody"/> multiple times rapidly from a
    /// background thread must not deadlock even under contention.
    /// </summary>
    [AvaloniaFact(Timeout = 10_000)]
    public async Task ResponseBody_SetRepeatedly_FromBackgroundThread_DoesNotDeadlock()
    {
        using var viewModel = CreateViewModel();

        await Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                viewModel.Response.ResponseBody = $"{{\"iteration\":{i}}}";
                viewModel.Response.ResponseContentType = "application/json";
                await Task.Yield();
            }
        }, TestContext.Current.CancellationToken);

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }
}
