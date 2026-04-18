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
    private MainWindowViewModel? _appVm;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _responseTextMate;
    private EventHandler<TextMate.Installation>? _appliedThemeHandler;

    public ResponseView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    private MainWindowViewModel? GetAppVm() => (DataContext as ResponseViewModel)?.App;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // Detach all previous wiring before re-wiring
        if (_appVm is not null)
        {
            _appVm.PropertyChanged -= OnAppVmPropertyChanged;
        }

        if (_responseTextMate is not null)
        {
            if (_appliedThemeHandler is not null)
            {
                _responseTextMate.AppliedTheme -= _appliedThemeHandler;
            }
            _responseTextMate.Dispose();
            _responseTextMate = null;
        }

        _appVm = GetAppVm();
        _responseBodyEditor = this.FindControl<TextEditor>("ResponseBodyEditor");

        _registryOptions ??= new RegistryOptions(ThemeName.DarkPlus);

        if (_responseBodyEditor is not null)
        {
            _appliedThemeHandler = (_, inst) => ApplyThemeColorsToEditor(_responseBodyEditor, inst);
            _responseTextMate = _responseBodyEditor.InstallTextMate(_registryOptions);
            _responseTextMate.AppliedTheme += _appliedThemeHandler;
            ApplyTextMateTheme();
        }

        if (_appVm is not null)
        {
            _appVm.PropertyChanged += OnAppVmPropertyChanged;

            if (_responseBodyEditor is not null)
            {
                _responseBodyEditor.Text = _appVm.ResponseBody;
            }
        }
    }

    private void OnAppVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ResponseBody)
            && _responseBodyEditor is not null
            && _appVm is not null
            && _responseBodyEditor.Text != _appVm.ResponseBody)
        {
            _responseBodyEditor.Text = _appVm.ResponseBody;
        }
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

        if (_responseTextMate is not null)
        {
            if (_appliedThemeHandler is not null)
            {
                _responseTextMate.AppliedTheme -= _appliedThemeHandler;
                _appliedThemeHandler = null;
            }
            _responseTextMate.Dispose();
            _responseTextMate = null;
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

    private string GetTextMateThemeName() =>
        (ActualThemeVariant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus).ToString();

    private void ApplyTextMateTheme()
    {
        if (_responseTextMate is null || _registryOptions is null)
        {
            return;
        }

        _responseTextMate.SetTheme(_registryOptions.GetTheme(GetTextMateThemeName()));
    }
}

