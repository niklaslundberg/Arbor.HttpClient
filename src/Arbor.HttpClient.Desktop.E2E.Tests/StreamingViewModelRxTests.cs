using System.Net;
using System.Text;
using Arbor.HttpClient.Desktop.Features.Sse;
using Arbor.HttpClient.Desktop.Features.WebSocket;
using Arbor.HttpClient.Testing.Fakes;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class StreamingViewModelRxTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public async Task ConnectAsync_SseViewModel_ShouldPublishToEventReceivedObservable()
    {
        var ssePayload = "data: rx-event\n\n";
        var responseBytes = Encoding.UTF8.GetBytes(ssePayload);

        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseBytes)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return response;
        });

        using var httpClient = new System.Net.Http.HttpClient(handler);
        using var logger = new LoggerConfiguration().CreateLogger();
        using var viewModel = new SseViewModel(httpClient, logger);
        var publishedEvent = new TaskCompletionSource<Arbor.HttpClient.Core.Sse.SseEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = viewModel.EventReceivedObservable.Subscribe(evt => publishedEvent.TrySetResult(evt));

        await viewModel.ConnectAsync("http://localhost:5000/sse", cancellationToken: TestContext.Current.CancellationToken);

        var observedEvent = await publishedEvent.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        observedEvent.Data.Should().Be("rx-event");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void Dispose_WebSocketViewModel_ShouldCompleteObservableStreams()
    {
        using var logger = new LoggerConfiguration().CreateLogger();
        using var viewModel = new WebSocketViewModel(logger);
        var messageStreamCompleted = false;
        var disconnectedStreamCompleted = false;

        using var messageSubscription = viewModel.MessageReceivedObservable.Subscribe(_ => { }, () => messageStreamCompleted = true);
        using var disconnectedSubscription = viewModel.DisconnectedObservable.Subscribe(_ => { }, () => disconnectedStreamCompleted = true);

        viewModel.Dispose();

        messageStreamCompleted.Should().BeTrue();
        disconnectedStreamCompleted.Should().BeTrue();
    }
}
