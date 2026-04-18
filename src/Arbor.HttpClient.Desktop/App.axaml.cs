using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.Logging;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Arbor.HttpClient.Storage.Sqlite;
using Serilog;
using System;
using System.IO;
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

            // Services
            var httpClient = new global::System.Net.Http.HttpClient();
            var httpRequestService = new HttpRequestService(httpClient, historyRepository);
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
                logWindowViewModel);

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
}
