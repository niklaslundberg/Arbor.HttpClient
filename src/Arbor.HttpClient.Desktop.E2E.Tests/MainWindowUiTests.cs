using System.Net;
using System.Net.Http;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using AwesomeAssertions;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class MainWindowUiTests
{
    [Fact]
    public async Task SendButton_ShouldUpdateResponseStatusAndBody()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var repository = new InMemoryRequestHistoryRepository();
            var handler = new StubMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    ReasonPhrase = "Accepted",
                    Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
                });

            var viewModel = new MainWindowViewModel(new HttpRequestService(new global::System.Net.Http.HttpClient(handler), repository), repository)
            {
                RequestName = "UI Test",
                RequestUrl = "https://example.com/api",
                SelectedMethod = "GET"
            };

            var window = new MainWindow { DataContext = viewModel };
            window.Show();

            var sendButton = window.FindControl<Button>("SendButton");
            sendButton.Should().NotBeNull();

            sendButton!.Command!.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            viewModel.ResponseStatus.Should().Be("202 Accepted");
            viewModel.ResponseBody.Should().Contain("ok");
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var screenshot = window.GetLastRenderedFrame() ?? window.CaptureRenderedFrame();
            var screenshotPath = Path.Combine(Path.GetTempPath(), "arbor-httpclient-ui.png");
            screenshot?.Save(screenshotPath);

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    private sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .WithInterFont()
            .LogToTrace();
    }

    private sealed class InMemoryRequestHistoryRepository : IRequestHistoryRepository
    {
        private readonly List<SavedRequest> _items = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default)
        {
            _items.Add(request);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SavedRequest>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SavedRequest>>(_items.Take(limit).ToList());
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
}
