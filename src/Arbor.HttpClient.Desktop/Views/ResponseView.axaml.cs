using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    public ResponseView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? GetAppVm() => (DataContext as ResponseViewModel)?.App;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_appVm is not null)
        {
            _appVm.PropertyChanged -= OnAppVmPropertyChanged;
        }

        _appVm = GetAppVm();

        _responseBodyEditor = this.FindControl<TextEditor>("ResponseBodyEditor");

        _registryOptions ??= new RegistryOptions(ThemeName.DarkPlus);

        if (_responseBodyEditor is not null)
        {
            _responseTextMate = _responseBodyEditor.InstallTextMate(_registryOptions);
            _responseTextMate.AppliedTheme += (_, inst) => ApplyThemeColorsToEditor(_responseBodyEditor, inst);
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

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _responseTextMate?.Dispose();
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
}
