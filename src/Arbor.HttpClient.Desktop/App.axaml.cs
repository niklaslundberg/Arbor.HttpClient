using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Arbor.HttpClient.Storage.Sqlite;
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
            var databasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Arbor.HttpClient",
                "requests.db");

            var repository = new SqliteRequestHistoryRepository(databasePath);

            var viewModel = new MainWindowViewModel(
                new HttpRequestService(new global::System.Net.Http.HttpClient(), repository),
                repository);

            var window = new MainWindow
            {
                DataContext = viewModel,
            };

            window.Opened += async (_, _) => await InitializeAsync(repository, viewModel);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeAsync(IRequestHistoryRepository repository, MainWindowViewModel viewModel)
    {
        try
        {
            await repository.InitializeAsync();
            await viewModel.LoadHistoryCommand.ExecuteAsync(null);
        }
        catch (Exception exception)
        {
            viewModel.ErrorMessage = exception.Message;
        }
    }
}
