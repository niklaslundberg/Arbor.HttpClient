using System.Net;
using System.Net.Http;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Skia;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using AwesomeAssertions;
using Serilog;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Tests for the UX 2.2 "Copy / save response shortcuts" feature:
/// Copy body to clipboard, Save body as file, Copy as cURL.
/// </summary>
[Collection("HeadlessAvalonia")]
public class ResponseShortcutsTests
{
    private static MainWindowViewModel CreateViewModel(HttpResponseMessage httpResponse)
    {
        var repository = new InMemoryRequestHistoryRepository();
        var handler = new StubHttpMessageHandler(_ => httpResponse);
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

    [Fact]
    public async Task HasTextResponse_IsFalse_BeforeAnyRequest()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        var result = await session.Dispatch(() =>
        {
            using var viewModel = CreateViewModel(new HttpResponseMessage(HttpStatusCode.OK));
            return Task.FromResult(viewModel.HasTextResponse);
        }, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasTextResponse_BecomesTrue_AfterTextResponse()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        var result = await session.Dispatch(async () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            };
            using var viewModel = CreateViewModel(response);

            viewModel.RequestEditor.RequestUrl = "https://example.com/api";
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            return viewModel.HasTextResponse;
        }, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasTextResponse_RemainsFalse_AfterBinaryResponse()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        var result = await session.Dispatch(async () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0xFF, 0xD8, 0xFF])
            };
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

            using var viewModel = CreateViewModel(response);
            viewModel.RequestEditor.RequestUrl = "https://example.com/image.png";
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            return viewModel.HasTextResponse;
        }, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CopyResponseBodyCommand_CopiesFormattedBodyToClipboard()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        var clipboardText = await session.Dispatch(async () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"hello\":\"world\"}", Encoding.UTF8, "application/json")
            };
            using var viewModel = CreateViewModel(response);

            var window = new MainWindow { DataContext = viewModel };
            window.Show();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

            viewModel.Clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
            viewModel.RequestEditor.RequestUrl = "https://example.com/api";
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            await viewModel.CopyResponseBodyCommand.ExecuteAsync(null);

            var text = await (TopLevel.GetTopLevel(window)?.Clipboard?.TryGetTextAsync()
                             ?? Task.FromResult<string?>(null));
            window.Close();
            return text;
        }, CancellationToken.None);

        clipboardText.Should().NotBeNullOrEmpty();
        clipboardText.Should().Contain("hello");
    }

    [Fact]
    public async Task CopyResponseBodyCommand_DoesNothing_WhenClipboardIsNull()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test body", Encoding.UTF8, "text/plain")
            };
            using var viewModel = CreateViewModel(response);

            viewModel.Clipboard = null;
            viewModel.RequestEditor.RequestUrl = "https://example.com/api";
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            // Should not throw
            await viewModel.CopyResponseBodyCommand.ExecuteAsync(null);

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task CopyCurrentRequestAsCurlCommand_CopiesCurlToClipboard()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        var clipboardText = await session.Dispatch(async () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            using var viewModel = CreateViewModel(response);

            var window = new MainWindow { DataContext = viewModel };
            window.Show();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

            viewModel.Clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
            viewModel.RequestEditor.RequestUrl = "https://example.com/api";
            viewModel.RequestEditor.SelectedMethod = "POST";
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            await viewModel.CopyCurrentRequestAsCurlCommand.ExecuteAsync(null);

            var text = await (TopLevel.GetTopLevel(window)?.Clipboard?.TryGetTextAsync()
                             ?? Task.FromResult<string?>(null));
            window.Close();
            return text;
        }, CancellationToken.None);

        clipboardText.Should().NotBeNullOrEmpty();
        clipboardText.Should().Contain("curl");
        clipboardText.Should().Contain("POST");
        clipboardText.Should().Contain("example.com");
    }

    [Fact]
    public async Task SaveResponseBodyAsFileCommand_DoesNothing_WhenStorageProviderIsNull()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(async () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("body text", Encoding.UTF8, "text/plain")
            };
            using var viewModel = CreateViewModel(response);

            viewModel.StorageProvider = null;
            viewModel.RequestEditor.RequestUrl = "https://example.com/api";
            viewModel.SendRequestCommand.Execute(null);
            await viewModel.SendRequestCommand.ExecutionTask!;

            // Should not throw
            await viewModel.SaveResponseBodyAsFileCommand.ExecuteAsync(null);

            return true;
        }, CancellationToken.None);
    }
}

