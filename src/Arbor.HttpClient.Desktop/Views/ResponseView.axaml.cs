using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Arbor.HttpClient.Desktop.ViewModels;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Arbor.HttpClient.Desktop.Views;

public partial class ResponseView : UserControl
{
    private TextEditor? _responseBodyEditor;
    private TextEditor? _rawResponseBodyEditor;
    private MainWindowViewModel? _appVm;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _responseTextMate;
    private TextMate.Installation? _rawResponseTextMate;
    private EventHandler<TextMate.Installation>? _responseAppliedThemeHandler;
    private EventHandler<TextMate.Installation>? _rawAppliedThemeHandler;
    private string _responseGrammarScope = string.Empty;
    private string _rawResponseGrammarScope = string.Empty;

    public ResponseView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private MainWindowViewModel? GetAppVm() => (DataContext as ResponseViewModel)?.App;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_appVm is not null)
        {
            _appVm.PropertyChanged -= OnAppVmPropertyChanged;
        }

        DisposeTextMateInstallations();

        _appVm = GetAppVm();
        _responseBodyEditor = this.FindControl<TextEditor>("ResponseBodyEditor");
        _rawResponseBodyEditor = this.FindControl<TextEditor>("RawResponseBodyEditor");

        _registryOptions ??= new RegistryOptions(ThemeName.DarkPlus);

        if (_responseBodyEditor is not null)
        {
            _responseAppliedThemeHandler = (_, inst) => ApplyThemeColorsToEditor(_responseBodyEditor, inst);
            _responseTextMate = _responseBodyEditor.InstallTextMate(_registryOptions);
            _responseTextMate.AppliedTheme += _responseAppliedThemeHandler;
        }

        if (_rawResponseBodyEditor is not null)
        {
            _rawAppliedThemeHandler = (_, inst) => ApplyThemeColorsToEditor(_rawResponseBodyEditor, inst);
            _rawResponseTextMate = _rawResponseBodyEditor.InstallTextMate(_registryOptions);
            _rawResponseTextMate.AppliedTheme += _rawAppliedThemeHandler;
        }

        if (_appVm is not null)
        {
            _appVm.PropertyChanged += OnAppVmPropertyChanged;
            UpdateEditorsFromViewModel();
            ApplyEditorFonts();
        }
    }

    private void OnAppVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.ResponseBody)
            or nameof(MainWindowViewModel.RawResponseBody)
            or nameof(MainWindowViewModel.ResponseContentType)
            or nameof(MainWindowViewModel.IsBinaryResponse)
            or nameof(MainWindowViewModel.ResponseBodyTabLabel))
        {
            UpdateEditorsFromViewModel();
        }

        if (e.PropertyName is nameof(MainWindowViewModel.UiFontFamily)
            or nameof(MainWindowViewModel.UiFontSize))
        {
            ApplyEditorFonts();
        }
    }

    private void UpdateEditorsFromViewModel()
    {
        if (_appVm is null)
        {
            return;
        }

        if (_responseBodyEditor is not null && _responseBodyEditor.Text != _appVm.ResponseBody)
        {
            _responseBodyEditor.Text = _appVm.ResponseBody;
        }

        if (_rawResponseBodyEditor is not null && _rawResponseBodyEditor.Text != _appVm.RawResponseBody)
        {
            _rawResponseBodyEditor.Text = _appVm.RawResponseBody;
        }

        var ext = !string.IsNullOrWhiteSpace(_appVm.ResponseContentType)
            ? MainWindowViewModel.ExtensionFromContentType(_appVm.ResponseContentType)
            : MainWindowViewModel.DetectExtensionFromContent(_appVm.ResponseBody);

        ApplyGrammarForContent(_responseTextMate, _registryOptions, ext, ref _responseGrammarScope);

        var rawExt = !string.IsNullOrWhiteSpace(_appVm.ResponseContentType)
            ? MainWindowViewModel.ExtensionFromContentType(_appVm.ResponseContentType)
            : MainWindowViewModel.DetectExtensionFromContent(_appVm.RawResponseBody);
        ApplyGrammarForContent(_rawResponseTextMate, _registryOptions, rawExt, ref _rawResponseGrammarScope);
    }

    private void ApplyEditorFonts()
    {
        if (_appVm is null)
        {
            return;
        }

        if (_responseBodyEditor is not null)
        {
            ApplyEditorFont(_responseBodyEditor, _appVm);
        }

        if (_rawResponseBodyEditor is not null)
        {
            ApplyEditorFont(_rawResponseBodyEditor, _appVm);
        }
    }

    private static void ApplyGrammarForContent(TextMate.Installation? installation, RegistryOptions? registryOptions, string extension, ref string currentScope)
    {
        if (installation is null || registryOptions is null)
        {
            return;
        }

        var language = registryOptions.GetLanguageByExtension(extension);
        var newScope = language is not null ? registryOptions.GetScopeByLanguageId(language.Id) : string.Empty;

        if (newScope == currentScope)
        {
            return;
        }

        currentScope = newScope;
        installation.SetGrammar(string.IsNullOrEmpty(newScope) ? null : newScope);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) =>
        ApplyTextMateTheme();

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (_appVm is not null)
        {
            _appVm.PropertyChanged -= OnAppVmPropertyChanged;
            _appVm = null;
        }

        _responseBodyEditor = null;
        _rawResponseBodyEditor = null;

        DisposeTextMateInstallations();
    }

    private void DisposeTextMateInstallations()
    {
        if (_responseTextMate is not null)
        {
            if (_responseAppliedThemeHandler is not null)
            {
                _responseTextMate.AppliedTheme -= _responseAppliedThemeHandler;
                _responseAppliedThemeHandler = null;
            }

            _responseTextMate.Dispose();
            _responseTextMate = null;
        }

        if (_rawResponseTextMate is not null)
        {
            if (_rawAppliedThemeHandler is not null)
            {
                _rawResponseTextMate.AppliedTheme -= _rawAppliedThemeHandler;
                _rawAppliedThemeHandler = null;
            }

            _rawResponseTextMate.Dispose();
            _rawResponseTextMate = null;
        }
    }

    private static void ApplyEditorFont(TextEditor editor, MainWindowViewModel appVm)
    {
        editor.FontFamily = new FontFamily(appVm.UiFontFamily);
        editor.FontSize = appVm.UiFontSize;
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

    private void ApplyTextMateTheme()
    {
        if (_registryOptions is null)
        {
            return;
        }

        var themeName = ActualThemeVariant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;
        var theme = _registryOptions.GetTheme(themeName.ToString());
        if (theme is null)
        {
            return;
        }

        _responseTextMate?.SetTheme(theme);
        _rawResponseTextMate?.SetTheme(theme);
    }
}
