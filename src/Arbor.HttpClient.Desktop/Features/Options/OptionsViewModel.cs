using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using Arbor.HttpClient.Desktop.Features.Main;

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
        "HTTP" => "HTTP",
        "ScheduledJobs" => "Scheduled Jobs",
        "LookAndFeel" => "Look & Feel",
        _ => SelectedOptionsPage
    };

    public string SelectedOptionsPageBreadcrumb => $"Options{OptionsPageBreadcrumbSeparator}{SelectedOptionsPageTitle}";
}
