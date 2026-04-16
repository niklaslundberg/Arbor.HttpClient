using CommunityToolkit.Mvvm.ComponentModel;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class EnvironmentVariableViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _value;

    public EnvironmentVariableViewModel(string name, string value)
    {
        _name = name;
        _value = value;
    }
}
