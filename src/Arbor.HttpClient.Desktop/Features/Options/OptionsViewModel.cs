using System.Reactive.Linq;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Localization;

namespace Arbor.HttpClient.Desktop.Features.Options;

public sealed partial class OptionsViewModel : Tool
{
    private const string OptionsPageBreadcrumbSeparator = " ›  ";

    public OptionsViewModel(MainWindowViewModel app)
    {
        App = app;
        Id = "options";
        Title = "Options";

        _selectedOptionsPageTitle = this
            .WhenAnyValue(viewModel => viewModel.SelectedOptionsPage)
            .Select(GetOptionsPageTitle)
            .ToProperty(this, viewModel => viewModel.SelectedOptionsPageTitle);

        _selectedOptionsPageBreadcrumb = this
            .WhenAnyValue(viewModel => viewModel.SelectedOptionsPageTitle)
            .Select(title => $"Options{OptionsPageBreadcrumbSeparator}{title}")
            .ToProperty(this, viewModel => viewModel.SelectedOptionsPageBreadcrumb);
    }

    public MainWindowViewModel App { get; }

    [Reactive]
    private string _selectedOptionsPage = "HTTP";

    private readonly ObservableAsPropertyHelper<string> _selectedOptionsPageTitle;
    private readonly ObservableAsPropertyHelper<string> _selectedOptionsPageBreadcrumb;

    public string SelectedOptionsPageTitle => _selectedOptionsPageTitle.Value;

    public string SelectedOptionsPageBreadcrumb => _selectedOptionsPageBreadcrumb.Value;

    private static string GetOptionsPageTitle(string page) => page switch
    {
        "HTTP" => Strings.OptionsNavHttp,
        "ScheduledJobs" => Strings.OptionsNavScheduledJobs,
        "LookAndFeel" => Strings.OptionsNavLookAndFeel,
        "Diagnostics" => Strings.OptionsNavDiagnostics,
        "ManageOptions" => Strings.OptionsNavManageOptions,
        _ => page
    };
}
