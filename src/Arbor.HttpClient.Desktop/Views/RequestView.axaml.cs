using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Arbor.HttpClient.Desktop.ViewModels;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Arbor.HttpClient.Desktop.Views;

public partial class RequestView : UserControl
{
    private TextEditor? _requestBodyEditor;
    private TextEditor? _requestUrlEditor;
    private TextEditor? _requestPreviewEditor;
    private MainWindowViewModel? _appVm;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _requestTextMate;
    private EventHandler<TextMate.Installation>? _appliedThemeHandler;
    private string _requestGrammarScope = string.Empty;
    private readonly VariableTokenColorizer _urlVariableColorizer = new();
    private readonly VariableTokenColorizer _bodyVariableColorizer = new();
    private readonly VariableTokenColorizer _previewVariableColorizer = new();

    public RequestView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private MainWindowViewModel? GetAppVm() => (DataContext as RequestViewModel)?.App;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // Detach all previous wiring before re-wiring
        if (_appVm is not null)
        {
            _appVm.PropertyChanged -= OnAppVmPropertyChanged;
        }

        if (_requestBodyEditor is not null)
        {
            _requestBodyEditor.Document.TextChanged -= OnRequestEditorTextChanged;
        }
        if (_requestUrlEditor is not null)
        {
            _requestUrlEditor.Document.TextChanged -= OnRequestUrlEditorTextChanged;
        }

        if (_requestTextMate is not null)
        {
            if (_appliedThemeHandler is not null)
            {
                _requestTextMate.AppliedTheme -= _appliedThemeHandler;
            }
            _requestTextMate.Dispose();
            _requestTextMate = null;
        }

        _appVm = GetAppVm();
        _requestBodyEditor = this.FindControl<TextEditor>("RequestBodyEditor");
        _requestUrlEditor = this.FindControl<TextEditor>("RequestUrlEditor");
        _requestPreviewEditor = this.FindControl<TextEditor>("RequestPreviewEditor");

        _registryOptions ??= new RegistryOptions(ThemeName.DarkPlus);

        if (_requestBodyEditor is not null)
        {
            _appliedThemeHandler = (_, inst) => ApplyThemeColorsToEditor(_requestBodyEditor, inst);
            _requestTextMate = _requestBodyEditor.InstallTextMate(_registryOptions);
            // ApplyThemeColorsToEditor is called via the AppliedTheme event when InstallTextMate
            // fires its initial theme application using the DarkPlus default above.
            // The correct variant (LightPlus or DarkPlus) is then applied via OnActualThemeVariantChanged
            // once this view is attached to the visual tree and the effective theme is resolved.
            _requestTextMate.AppliedTheme += _appliedThemeHandler;
            _requestBodyEditor.Document.TextChanged += OnRequestEditorTextChanged;
            if (!_requestBodyEditor.TextArea.TextView.LineTransformers.Contains(_bodyVariableColorizer))
            {
                _requestBodyEditor.TextArea.TextView.LineTransformers.Add(_bodyVariableColorizer);
            }
        }

        if (_requestUrlEditor is not null)
        {
            _requestUrlEditor.Document.TextChanged += OnRequestUrlEditorTextChanged;
            if (!_requestUrlEditor.TextArea.TextView.LineTransformers.Contains(_urlVariableColorizer))
            {
                _requestUrlEditor.TextArea.TextView.LineTransformers.Add(_urlVariableColorizer);
            }
        }

        if (_requestPreviewEditor is not null)
        {
            if (!_requestPreviewEditor.TextArea.TextView.LineTransformers.Contains(_previewVariableColorizer))
            {
                _requestPreviewEditor.TextArea.TextView.LineTransformers.Add(_previewVariableColorizer);
            }
        }

        if (_appVm is not null)
        {
            _appVm.PropertyChanged += OnAppVmPropertyChanged;

            if (_requestBodyEditor is not null)
            {
                ApplyEditorFont(_requestBodyEditor, _appVm);
                _requestBodyEditor.Text = _appVm.RequestBody;
                ApplyGrammarForContent(_requestTextMate, _appVm.RequestBody, ref _requestGrammarScope);
            }

            if (_requestUrlEditor is not null)
            {
                ApplyEditorFont(_requestUrlEditor, _appVm);
                _requestUrlEditor.Text = _appVm.RequestUrl;
            }

            if (_requestPreviewEditor is not null)
            {
                ApplyEditorFont(_requestPreviewEditor, _appVm);
                _requestPreviewEditor.Text = _appVm.RequestPreview;
            }
        }

        ApplyVariableColorTheme();
    }

    private void OnRequestEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_appVm is not null && _requestBodyEditor is not null
            && _appVm.RequestBody != _requestBodyEditor.Text)
        {
            _appVm.RequestBody = _requestBodyEditor.Text;
        }

        ApplyGrammarForContent(_requestTextMate, _requestBodyEditor?.Text ?? string.Empty, ref _requestGrammarScope);
    }

    private void OnRequestUrlEditorTextChanged(object? sender, EventArgs e)
    {
        if (_appVm is not null && _requestUrlEditor is not null
            && _appVm.RequestUrl != _requestUrlEditor.Text)
        {
            _appVm.RequestUrl = _requestUrlEditor.Text;
        }
    }

    private void OnAppVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.RequestBody)
            && _requestBodyEditor is not null
            && _appVm is not null
            && _requestBodyEditor.Text != _appVm.RequestBody)
        {
            _requestBodyEditor.Text = _appVm.RequestBody;
            ApplyGrammarForContent(_requestTextMate, _appVm.RequestBody, ref _requestGrammarScope);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.RequestUrl)
            && _requestUrlEditor is not null
            && _appVm is not null
            && _requestUrlEditor.Text != _appVm.RequestUrl)
        {
            _requestUrlEditor.Text = _appVm.RequestUrl;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.RequestPreview)
            && _requestPreviewEditor is not null
            && _appVm is not null
            && _requestPreviewEditor.Text != _appVm.RequestPreview)
        {
            _requestPreviewEditor.Text = _appVm.RequestPreview;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.ContentType))
        {
            _requestGrammarScope = string.Empty;
            ApplyGrammarForContent(_requestTextMate, _requestBodyEditor?.Text ?? string.Empty, ref _requestGrammarScope);
        }

        if ((e.PropertyName == nameof(MainWindowViewModel.UiFontFamily)
             || e.PropertyName == nameof(MainWindowViewModel.UiFontSize))
            && _appVm is not null)
        {
            if (_requestUrlEditor is not null)
            {
                ApplyEditorFont(_requestUrlEditor, _appVm);
            }

            if (_requestBodyEditor is not null)
            {
                ApplyEditorFont(_requestBodyEditor, _appVm);
            }

            if (_requestPreviewEditor is not null)
            {
                ApplyEditorFont(_requestPreviewEditor, _appVm);
            }
        }
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyTextMateTheme();
        ApplyVariableColorTheme();
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (_appVm is not null)
        {
            _appVm.PropertyChanged -= OnAppVmPropertyChanged;
            _appVm = null;
        }

        if (_requestBodyEditor is not null)
        {
            _requestBodyEditor.Document.TextChanged -= OnRequestEditorTextChanged;
            _requestBodyEditor = null;
        }

        if (_requestUrlEditor is not null)
        {
            _requestUrlEditor.Document.TextChanged -= OnRequestUrlEditorTextChanged;
            _requestUrlEditor = null;
        }

        _requestPreviewEditor = null;

        if (_requestTextMate is not null)
        {
            if (_appliedThemeHandler is not null)
            {
                _requestTextMate.AppliedTheme -= _appliedThemeHandler;
                _appliedThemeHandler = null;
            }
            _requestTextMate.Dispose();
            _requestTextMate = null;
        }
    }

    private void ApplyGrammarForContent(TextMate.Installation? installation, string content, ref string currentScope)
    {
        if (installation is null || _registryOptions is null)
        {
            return;
        }

        var explicitContentType = _appVm?.ContentType ?? string.Empty;
        var ext = !string.IsNullOrEmpty(explicitContentType)
            ? MainWindowViewModel.ExtensionFromContentType(explicitContentType)
            : MainWindowViewModel.DetectExtensionFromContent(content);

        var language = _registryOptions.GetLanguageByExtension(ext);
        var newScope = language is not null ? _registryOptions.GetScopeByLanguageId(language.Id) : string.Empty;

        if (newScope == currentScope)
        {
            return;
        }

        currentScope = newScope;
        installation.SetGrammar(string.IsNullOrEmpty(newScope) ? null : newScope);
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

    private void ApplyVariableColorTheme()
    {
        IBrush brush = Brushes.MediumPurple;
        if (Application.Current?.TryGetResource("MethodPatchBrush", out var resource) == true &&
            resource is IBrush resourceBrush)
        {
            brush = resourceBrush;
        }

        _urlVariableColorizer.SetForeground(brush);
        _bodyVariableColorizer.SetForeground(brush);
        _previewVariableColorizer.SetForeground(brush);

        _requestUrlEditor?.TextArea.TextView.Redraw();
        _requestBodyEditor?.TextArea.TextView.Redraw();
        _requestPreviewEditor?.TextArea.TextView.Redraw();
    }

    private void ApplyTextMateTheme()
    {
        if (_requestTextMate is null || _registryOptions is null)
        {
            return;
        }

        var themeName = ActualThemeVariant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;
        var theme = _registryOptions.GetTheme(themeName.ToString());
        if (theme is null)
        {
            return;
        }

        _requestTextMate.SetTheme(theme);
    }

    private sealed partial class VariableTokenColorizer : DocumentColorizingTransformer
    {
        [GeneratedRegex(@"\{\{[^}]+\}\}", RegexOptions.Compiled)]
        private static partial Regex VariableTokenRegex();

        private IBrush _foreground = Brushes.MediumPurple;

        public void SetForeground(IBrush foreground) => _foreground = foreground;

        protected override void ColorizeLine(DocumentLine line)
        {
            var lineText = CurrentContext.Document.GetText(line);
            foreach (Match match in VariableTokenRegex().Matches(lineText))
            {
                var startOffset = line.Offset + match.Index;
                var endOffset = startOffset + match.Length;

                ChangeLinePart(startOffset, endOffset, element =>
                    element.TextRunProperties.SetForegroundBrush(_foreground));
            }
        }
    }
}

