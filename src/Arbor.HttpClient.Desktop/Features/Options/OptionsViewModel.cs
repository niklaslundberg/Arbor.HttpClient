using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
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
    }

    public MainWindowViewModel App { get; }

    [ObservableProperty]
    private string _selectedOptionsPage = "HTTP";

    partial void OnSelectedOptionsPageChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedOptionsPageTitle));
        OnPropertyChanged(nameof(SelectedOptionsPageBreadcrumb));
    }

    public string SelectedOptionsPageTitle => SelectedOptionsPage switch
    {
        "HTTP" => Strings.OptionsNavHttp,
        "ScheduledJobs" => Strings.OptionsNavScheduledJobs,
        "LookAndFeel" => Strings.OptionsNavLookAndFeel,
        "Diagnostics" => Strings.OptionsNavDiagnostics,
        "ManageOptions" => Strings.OptionsNavManageOptions,
        _ => SelectedOptionsPage
    };

    public string SelectedOptionsPageBreadcrumb => $"Options{OptionsPageBreadcrumbSeparator}{SelectedOptionsPageTitle}";
}
