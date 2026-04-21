using CommunityToolkit.Mvvm.ComponentModel;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class EnvironmentVariableViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _value;

    public EnvironmentVariableViewModel(string name, string value, bool isEnabled = true)
    {
        _isEnabled = isEnabled;
        _name = name;
        _value = value;
    }
}
