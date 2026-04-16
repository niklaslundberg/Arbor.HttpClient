using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.ViewModels;
using Arbor.HttpClient.Desktop.Views;
using Arbor.HttpClient.Storage.Sqlite;
using System;
using System.IO;
using System.Net.Http;

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
            repository.InitializeAsync().GetAwaiter().GetResult();

            var viewModel = new MainWindowViewModel(
                new HttpRequestService(new global::System.Net.Http.HttpClient(), repository),
                repository);

            viewModel.LoadHistoryCommand.Execute(null);
            viewModel.LoadHistoryCommand.ExecutionTask?.GetAwaiter().GetResult();

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
