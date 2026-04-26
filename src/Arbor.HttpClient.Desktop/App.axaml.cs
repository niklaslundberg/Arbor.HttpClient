using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Arbor.HttpClient.Storage.Sqlite;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;

namespace Arbor.HttpClient.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Arbor.HttpClient",
                "requests.db");

            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
            var optionsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Arbor.HttpClient",
                "options.json");

            // Logging
            var inMemorySink = new InMemorySink();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Sink(inMemorySink)
                .CreateLogger();
            var diagnosticsLogger = Log.Logger.ForContext("LogTab", LogTab.HttpDiagnostics);

            // Repositories
            var historyRepository = new SqliteRequestHistoryRepository(dbPath);
            var collectionRepository = new SqliteCollectionRepository(connectionString);
            var environmentRepository = new SqliteEnvironmentRepository(connectionString);
            var scheduledJobRepository = new SqliteScheduledJobRepository(connectionString);
            var optionsStore = new ApplicationOptionsStore(optionsPath);
            var draftsFolder = Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Arbor.HttpClient",
                "drafts");
            var draftPersistenceService = new DraftPersistenceService(draftsFolder);
            ApplicationOptions currentOptions;
            try
            {
                currentOptions = optionsStore.Load();
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Failed to load application options; using defaults");
                currentOptions = new ApplicationOptions();
            }

            // Services
            var sharedCookieContainer = new System.Net.CookieContainer();
            var httpClient = new global::System.Net.Http.HttpClient();
            var httpRequestService = new HttpRequestService(httpClient, historyRepository);
            httpRequestService.SetHttpDiagnosticsObserver(diagnostics =>
                diagnosticsLogger.Information(
                    "HTTP diagnostics {Method} {Url} | Requested HTTP/{RequestedHttpVersion} | Response HTTP/{ResponseHttpVersion} | DNS ({DnsLookupMs:F1} ms): {DnsLookup} | TLS ({TlsMs:F1} ms): {TlsNegotiation} | Timings ms => headers: {HeadersMs:F1}, body: {BodyMs:F1}, total: {TotalMs:F1}",
                    diagnostics.Method,
                    diagnostics.Url,
                    diagnostics.RequestedHttpVersion,
                    diagnostics.ResponseHttpVersion,
                    diagnostics.DnsLookupMilliseconds,
                    diagnostics.DnsLookup,
                    diagnostics.TlsNegotiationMilliseconds,
                    diagnostics.TlsNegotiation,
                    diagnostics.ResponseHeadersMilliseconds,
                    diagnostics.ResponseBodyMilliseconds,
                    diagnostics.TotalMilliseconds));
            httpRequestService.SetHttpDiagnosticsEnabled(currentOptions.Http.EnableHttpDiagnostics);
            var configuredHttpClient = CreateHttpClient(currentOptions.Http, cookieContainer: sharedCookieContainer);
            var inverseRedirectHttpClient = CreateHttpClient(currentOptions.Http, !currentOptions.Http.FollowRedirects, cookieContainer: sharedCookieContainer);
            var retiredHttpClients = new List<global::System.Net.Http.HttpClient>();
            httpRequestService.SetHttpClientFactory(followRedirectsOverride =>
            {
                if (followRedirectsOverride is null || followRedirectsOverride == currentOptions.Http.FollowRedirects)
                {
                    return configuredHttpClient;
                }

                return inverseRedirectHttpClient;
            });
            var scheduledJobService = new ScheduledJobService(httpRequestService, Log.Logger);
            var demoServer = new DemoServer();

            // ViewModels
            var logWindowViewModel = new LogWindowViewModel(inMemorySink);
            var viewModel = new MainWindowViewModel(
                httpRequestService,
                historyRepository,
                collectionRepository,
                environmentRepository,
                scheduledJobRepository,
                scheduledJobService,
                logWindowViewModel,
                Log.Logger,
                optionsStore,
                currentOptions,
                updatedOptions =>
                {
                    currentOptions = updatedOptions;
                    retiredHttpClients.Add(configuredHttpClient);
                    retiredHttpClients.Add(inverseRedirectHttpClient);
                    configuredHttpClient = CreateHttpClient(currentOptions.Http, cookieContainer: sharedCookieContainer);
                    inverseRedirectHttpClient = CreateHttpClient(currentOptions.Http, !currentOptions.Http.FollowRedirects, cookieContainer: sharedCookieContainer);
                    httpRequestService.SetHttpDiagnosticsEnabled(currentOptions.Http.EnableHttpDiagnostics);
                },
                cookieContainer: sharedCookieContainer,
                draftPersistenceService: draftPersistenceService,
                demoServer: demoServer);

            var window = new MainWindow
            {
                DataContext = viewModel,
            };

            window.Opened += async (_, _) => await InitializeAsync(historyRepository, collectionRepository, environmentRepository, scheduledJobRepository, viewModel);
            window.Closed += (_, _) => DisposeResources();

            async void DisposeResources()
            {
                // Layout was already persisted and floating windows closed in MainWindow.OnClosing.
                viewModel.Dispose();
                logWindowViewModel.Dispose();
                configuredHttpClient.Dispose();
                foreach (var retiredHttpClient in retiredHttpClients)
                {
                    retiredHttpClient.Dispose();
                }
                httpClient.Dispose();
                await demoServer.DisposeAsync();
                Log.CloseAndFlush();
            }
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeAsync(
        IRequestHistoryRepository historyRepository,
        ICollectionRepository collectionRepository,
        IEnvironmentRepository environmentRepository,
        IScheduledJobRepository scheduledJobRepository,
        MainWindowViewModel viewModel)
    {
        try
        {
            await historyRepository.InitializeAsync();
            await collectionRepository.InitializeAsync();
            await environmentRepository.InitializeAsync();
            await scheduledJobRepository.InitializeAsync();
            await viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            viewModel.ErrorMessage = exception.Message;
        }
    }

    private static global::System.Net.Http.HttpClient CreateHttpClient(HttpOptions options, bool? followRedirectsOverride = null, System.Net.CookieContainer? cookieContainer = null)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = followRedirectsOverride ?? options.FollowRedirects,
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = options.TlsVersion switch
                {
                    // TLS 1.0 and 1.1 are cryptographically broken and disabled by default in modern
                    // operating systems. They are exposed here exclusively for testing HTTP clients
                    // against legacy servers that cannot be upgraded. Never use these in production.
#pragma warning disable SYSLIB0039
                    "Tls10" => SslProtocols.Tls,
                    "Tls11" => SslProtocols.Tls11,
#pragma warning restore SYSLIB0039
                    "Tls12" => SslProtocols.Tls12,
                    "Tls13" => SslProtocols.Tls13,
                    _ => SslProtocols.None
                }
            }
        };

        if (cookieContainer is { } cookies)
        {
            handler.UseCookies = true;
            handler.CookieContainer = cookies;
        }

        return new global::System.Net.Http.HttpClient(handler, disposeHandler: true);
    }
}
