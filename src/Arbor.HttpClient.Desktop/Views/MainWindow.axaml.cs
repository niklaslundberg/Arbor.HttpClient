using Avalonia.Controls;
using Arbor.HttpClient.Desktop.ViewModels;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Arbor.HttpClient.Desktop.Views;

public partial class MainWindow : Window
{
    private TextEditor? _requestBodyEditor;
    private TextEditor? _responseBodyEditor;
    private MainWindowViewModel? _viewModel;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _requestTextMate;
    private TextMate.Installation? _responseTextMate;
    private string _requestGrammarScope = string.Empty;
    private string _responseGrammarScope = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        _requestBodyEditor = this.FindControl<TextEditor>("RequestBodyEditor");
        _responseBodyEditor = this.FindControl<TextEditor>("ResponseBodyEditor");

        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        if (_requestBodyEditor is not null)
        {
            _requestTextMate = _requestBodyEditor.InstallTextMate(_registryOptions);
            _requestBodyEditor.Document.TextChanged += OnRequestEditorTextChanged;
        }

        if (_responseBodyEditor is not null)
        {
            _responseTextMate = _responseBodyEditor.InstallTextMate(_registryOptions);
        }

        if (_viewModel is not null)
        {
            _viewModel.StorageProvider = StorageProvider;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            if (_requestBodyEditor is not null)
            {
                _requestBodyEditor.Text = _viewModel.RequestBody;
                ApplyGrammarForContent(_requestTextMate, _viewModel.RequestBody, ref _requestGrammarScope);
            }

            if (_responseBodyEditor is not null)
            {
                _responseBodyEditor.Text = _viewModel.ResponseBody;
                ApplyGrammarForContent(_responseTextMate, _viewModel.ResponseBody, ref _responseGrammarScope);
            }
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
        _requestTextMate?.Dispose();
        _responseTextMate?.Dispose();
    }

    private void OnRequestEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null && _requestBodyEditor is not null
            && _viewModel.RequestBody != _requestBodyEditor.Text)
        {
            _viewModel.RequestBody = _requestBodyEditor.Text;
        }

        ApplyGrammarForContent(_requestTextMate, _requestBodyEditor?.Text ?? string.Empty, ref _requestGrammarScope);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.RequestBody)
            && _requestBodyEditor is not null
            && _viewModel is not null
            && _requestBodyEditor.Text != _viewModel.RequestBody)
        {
            _requestBodyEditor.Text = _viewModel.RequestBody;
            ApplyGrammarForContent(_requestTextMate, _viewModel.RequestBody, ref _requestGrammarScope);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.ResponseBody)
            && _responseBodyEditor is not null
            && _viewModel is not null
            && _responseBodyEditor.Text != _viewModel.ResponseBody)
        {
            _responseBodyEditor.Text = _viewModel.ResponseBody;
            ApplyGrammarForContent(_responseTextMate, _viewModel.ResponseBody, ref _responseGrammarScope);
        }
    }

    private void ApplyGrammarForContent(TextMate.Installation? installation, string content, ref string currentScope)
    {
        if (installation is null || _registryOptions is null)
        {
            return;
        }

        var ext = DetectExtension(content);
        var language = _registryOptions.GetLanguageByExtension(ext);
        var newScope = language is not null ? _registryOptions.GetScopeByLanguageId(language.Id) : string.Empty;

        if (newScope == currentScope)
        {
            return;
        }

        currentScope = newScope;
        installation.SetGrammar(string.IsNullOrEmpty(newScope) ? null : newScope);
    }

    private static string DetectExtension(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return ".json";
        }

        if (trimmed.StartsWith('<'))
        {
            return ".xml";
        }

        return ".txt";
    }
}
