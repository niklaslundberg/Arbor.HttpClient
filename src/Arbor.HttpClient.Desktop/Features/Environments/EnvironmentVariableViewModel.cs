using CommunityToolkit.Mvvm.ComponentModel;
using Arbor.HttpClient.Desktop.Shared;

namespace Arbor.HttpClient.Desktop.Features.Environments;

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
