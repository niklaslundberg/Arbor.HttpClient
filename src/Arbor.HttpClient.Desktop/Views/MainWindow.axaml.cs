using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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
            _requestTextMate.AppliedTheme += (_, installation) => ApplyThemeColorsToEditor(_requestBodyEditor, installation);
            _requestBodyEditor.Document.TextChanged += OnRequestEditorTextChanged;
        }

        if (_responseBodyEditor is not null)
        {
            _responseTextMate = _responseBodyEditor.InstallTextMate(_registryOptions);
            _responseTextMate.AppliedTheme += (_, installation) => ApplyThemeColorsToEditor(_responseBodyEditor, installation);
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

    private static void ApplyThemeColorsToEditor(TextEditor editor, TextMate.Installation installation)
    {
        if (TryGetThemeBrush(installation, "editor.background", out var background))
        {
            editor.Background = background;
            editor.TextArea.Background = background;
        }

        if (TryGetThemeBrush(installation, "editor.foreground", out var foreground))
        {
            editor.Foreground = foreground;
        }

        if (TryGetThemeBrush(installation, "editor.selectionBackground", out var selectionBrush))
        {
            editor.TextArea.SelectionBrush = selectionBrush;
        }
        else if (Application.Current?.TryGetResource("TextAreaSelectionBrush", out var resourceObj) == true && resourceObj is IBrush brush)
        {
            editor.TextArea.SelectionBrush = brush;
        }

        if (TryGetThemeBrush(installation, "editor.lineHighlightBackground", out var lineHighlight))
        {
            editor.TextArea.TextView.CurrentLineBackground = lineHighlight;
            editor.TextArea.TextView.CurrentLineBorder = new Pen(lineHighlight);
        }
        else
        {
            // Restore the built-in default highlight colors defined by AvaloniaEdit
            editor.TextArea.TextView.SetDefaultHighlightLineColors();
        }

        if (TryGetThemeBrush(installation, "editorLineNumber.foreground", out var lineNumberForeground))
        {
            editor.LineNumbersForeground = lineNumberForeground;
        }
        else
        {
            editor.LineNumbersForeground = editor.Foreground;
        }
    }

    private static bool TryGetThemeBrush(TextMate.Installation installation, string key, out IBrush brush)
    {
        brush = Brushes.Transparent;
        if (!installation.TryGetThemeColor(key, out var colorString))
        {
            return false;
        }

        if (!Color.TryParse(colorString, out var color))
        {
            return false;
        }

        brush = new SolidColorBrush(color);
        return true;
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

        if (e.PropertyName == nameof(MainWindowViewModel.ContentType))
        {
            // ContentType changed explicitly — force grammar re-detection for the request editor
            _requestGrammarScope = string.Empty;
            ApplyGrammarForContent(_requestTextMate, _requestBodyEditor?.Text ?? string.Empty, ref _requestGrammarScope);
        }
    }

    private void ApplyGrammarForContent(TextMate.Installation? installation, string content, ref string currentScope)
    {
        if (installation is null || _registryOptions is null)
        {
            return;
        }

        // Prefer explicit ContentType from ViewModel; fall back to body heuristics
        var explicitContentType = _viewModel?.ContentType ?? string.Empty;
        var ext = !string.IsNullOrEmpty(explicitContentType)
            ? MainWindowViewModel.ExtensionFromContentType(explicitContentType)
            : DetectExtension(content);

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
