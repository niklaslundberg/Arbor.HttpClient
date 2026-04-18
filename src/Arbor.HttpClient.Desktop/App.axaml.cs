using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Arbor.HttpClient.Storage.Sqlite;
using Serilog;
using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;

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

            // Repositories
            var historyRepository = new SqliteRequestHistoryRepository(dbPath);
            var collectionRepository = new SqliteCollectionRepository(connectionString);
            var environmentRepository = new SqliteEnvironmentRepository(connectionString);
            var scheduledJobRepository = new SqliteScheduledJobRepository(connectionString);
            var optionsStore = new ApplicationOptionsStore(optionsPath);
            ApplicationOptions currentOptions;
            try
            {
                currentOptions = optionsStore.Load();
            }
            catch
            {
                currentOptions = new ApplicationOptions();
            }

            // Services
            var httpClient = new global::System.Net.Http.HttpClient();
            var httpRequestService = new HttpRequestService(httpClient, historyRepository);
            httpRequestService.SetHttpClientFactory(() => CreateHttpClient(currentOptions.Http));
            var scheduledJobService = new ScheduledJobService(httpRequestService, Log.Logger);

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
                optionsStore,
                currentOptions,
                updatedOptions => currentOptions = updatedOptions);

            var window = new MainWindow
            {
                DataContext = viewModel,
            };

            window.Opened += async (_, _) => await InitializeAsync(historyRepository, collectionRepository, environmentRepository, scheduledJobRepository, viewModel);
            window.Closed += (_, _) =>
            {
                viewModel.Dispose();
                logWindowViewModel.Dispose();
                Log.CloseAndFlush();
            };
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

    private static global::System.Net.Http.HttpClient CreateHttpClient(HttpOptions options)
    {
        const SslProtocols tls10 = (SslProtocols)192;
        const SslProtocols tls11 = (SslProtocols)768;

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = options.FollowRedirects,
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = options.TlsVersion switch
                {
                    "Tls10" => tls10,
                    "Tls11" => tls11,
                    "Tls12" => SslProtocols.Tls12,
                    "Tls13" => SslProtocols.Tls13,
                    _ => SslProtocols.None
                }
            }
        };

        return new global::System.Net.Http.HttpClient(handler, disposeHandler: true);
    }
}
