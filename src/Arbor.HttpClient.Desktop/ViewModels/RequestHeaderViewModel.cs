using CommunityToolkit.Mvvm.ComponentModel;

namespace Arbor.HttpClient.Desktop.ViewModels;

public sealed partial class RequestHeaderViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;
}
