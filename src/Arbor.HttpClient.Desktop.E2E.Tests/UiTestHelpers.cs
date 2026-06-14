using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.Demo;
using Arbor.HttpClient.Desktop.Features.Environments;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Logging;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.Options;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;
using Arbor.HttpClient.Desktop.Features.Variables;
using Arbor.HttpClient.Desktop.Localization;
using Arbor.HttpClient.Testing.Fakes;
using Arbor.HttpClient.Testing.Repositories;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using Serilog;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Shared test helpers for the <c>MainWindow*UiTests</c> classes (headless Avalonia integration tests).
/// </summary>
internal static class UiTestHelpers
{
    internal static async Task WaitForUiThreadAsync() =>
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);

    internal static void VerifyTabRealized(TabControl tabControl, string tabHeader)
    {
        var tabItems = tabControl.Items.OfType<TabItem>().ToList();
        var tabItem = tabItems.Single(item => string.Equals(item.Header?.ToString(), tabHeader, StringComparison.Ordinal));
        tabItem.IsVisible.Should().BeTrue();
        tabControl.SelectedIndex = tabItems.IndexOf(tabItem);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);
        tabControl.SelectedItem.Should().Be(tabItem);
    }

    internal static T? FindDockById<T>(IDockable dockable, string id) where T : class, IDockable
    {
        if (dockable is T match && string.Equals(match.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            return match;
        }

        if (dockable is IDock { VisibleDockables: { } visibleDockables })
        {
            foreach (var child in visibleDockables)
            {
                var result = FindDockById<T>(child, id);
                if (result is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    internal sealed class RedirectTestServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loopTask;

        public RedirectTestServer()
        {
            using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            BaseUrl = $"http://127.0.0.1:{port}";
            RedirectUrl = $"{BaseUrl}/redirect";
            FinalUrl = $"{BaseUrl}/final";

            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _loopTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public string BaseUrl { get; }
        public string RedirectUrl { get; }
        public string FinalUrl { get; }
        public int FinalRequestCount { get; private set; }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                using var response = context.Response;
                if (context.Request.Url?.AbsolutePath == "/redirect")
                {
                    response.StatusCode = (int)HttpStatusCode.Redirect;
                    response.RedirectLocation = FinalUrl;
                    response.Close();
                    continue;
                }

                if (context.Request.Url?.AbsolutePath == "/final")
                {
                    FinalRequestCount++;
                    var payload = Encoding.UTF8.GetBytes("redirect-complete");
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "text/plain";
                    response.ContentLength64 = payload.Length;
                    await response.OutputStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                    response.Close();
                    continue;
                }

                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
            }
        }

        public void Dispose()
        {
            using var cancellationTokenSource = _cts;
            cancellationTokenSource.Cancel();
            _listener.Stop();
            _listener.Close();
            try
            {
                _loopTask.GetAwaiter().GetResult();
            }
            catch
            {
                // Best effort stop for tests.
            }
        }
    }

    internal sealed class AsyncStubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            sendAsync(request, cancellationToken);
    }
}
