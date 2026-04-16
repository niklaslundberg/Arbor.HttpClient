using Avalonia.Controls;
using Arbor.HttpClient.Desktop.ViewModels;
using AvaloniaEdit;

namespace Arbor.HttpClient.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private TextEditor? _requestBodyEditor;
    private TextEditor? _responseBodyEditor;
    private MainWindowViewModel? _viewModel;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // Unsubscribe from old VM
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        // Locate the named editors now that the AXAML tree is built
        _requestBodyEditor = this.FindControl<TextEditor>("RequestBodyEditor");
        _responseBodyEditor = this.FindControl<TextEditor>("ResponseBodyEditor");

        if (_requestBodyEditor is not null)
        {
            _requestBodyEditor.Document.TextChanged += OnRequestEditorTextChanged;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Sync initial values
            if (_requestBodyEditor is not null)
                _requestBodyEditor.Text = _viewModel.RequestBody;
            if (_responseBodyEditor is not null)
                _responseBodyEditor.Text = _viewModel.ResponseBody;
        }
    }

    private void OnRequestEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null && _requestBodyEditor is not null
            && _viewModel.RequestBody != _requestBodyEditor.Text)
        {
            _viewModel.RequestBody = _requestBodyEditor.Text;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.RequestBody)
            && _requestBodyEditor is not null
            && _viewModel is not null
            && _requestBodyEditor.Text != _viewModel.RequestBody)
        {
            _requestBodyEditor.Text = _viewModel.RequestBody;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.ResponseBody)
            && _responseBodyEditor is not null
            && _viewModel is not null
            && _responseBodyEditor.Text != _viewModel.ResponseBody)
        {
            _responseBodyEditor.Text = _viewModel.ResponseBody;
        }
    }
}
