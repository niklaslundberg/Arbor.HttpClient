using System;
using System.Collections.Generic;
using Arbor.HttpClient.Desktop.Features.Main;
using Arbor.HttpClient.Desktop.Features.Variables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Arbor.HttpClient.Desktop.Features.Variables;

/// <summary>
/// A single-line text input that highlights <c>{{variable}}</c> and <c>{{env:variable}}</c> tokens
/// with distinct syntax coloring while otherwise appearing identical to a Fluent <c>TextBox</c>.
/// Use <see cref="TextProperty"/> with <c>Mode=TwoWay</c> for data binding.
/// </summary>
public sealed class VariableTextBox : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<VariableTextBox, string>(nameof(Text), defaultValue: string.Empty);

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<VariableTextBox, string>(nameof(PlaceholderText), defaultValue: string.Empty);

    public static readonly StyledProperty<MainWindowViewModel?> AppViewModelProperty =
        AvaloniaProperty.Register<VariableTextBox, MainWindowViewModel?>(nameof(AppViewModel));

    private readonly AvaloniaEdit.TextEditor _editor;
    private readonly TextBlock _placeholder;
    private readonly VariableTokenColorizer _colorizer = new();
    private VariableAutoCompleteController? _autoCompleteController;
    private readonly Border _border;
    private bool _updatingText;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public MainWindowViewModel? AppViewModel
    {
        get => GetValue(AppViewModelProperty);
        set => SetValue(AppViewModelProperty, value);
    }

    public VariableTextBox()
    {
        _editor = new AvaloniaEdit.TextEditor
        {
            ShowLineNumbers = false,
            WordWrap = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Padding = new Thickness(5, 6, 5, 6),
            Background = Brushes.Transparent
        };
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.TextArea.Background = Brushes.Transparent;
        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        _editor.Document.TextChanged += OnEditorTextChanged;

        _placeholder = new TextBlock
        {
            Margin = new Thickness(7, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = 0.5,
            IsVisible = true
        };

        var layer = new Grid();
        layer.Children.Add(_placeholder);
        layer.Children.Add(_editor);

        _border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Child = layer
        };

        Content = _border;

        ActualThemeVariantChanged += (_, _) => { ApplyBrushes(); ApplyFont(); };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _autoCompleteController ??= new VariableAutoCompleteController(_editor, GetVariableNames, GetEnvVariableNames);
        ApplyFont();
        ApplyBrushes();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _autoCompleteController?.Dispose();
        _autoCompleteController = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty && !_updatingText)
        {
            var text = GetValue(TextProperty) ?? string.Empty;
            if (_editor.Text != text)
            {
                _editor.Text = text;
            }
            _placeholder.IsVisible = string.IsNullOrEmpty(text);
        }
        else if (change.Property == PlaceholderTextProperty)
        {
            _placeholder.Text = GetValue(PlaceholderTextProperty);
        }
        else if (change.Property == FontFamilyProperty)
        {
            _editor.FontFamily = FontFamily;
            _placeholder.FontFamily = FontFamily;
        }
        else if (change.Property == FontSizeProperty)
        {
            _editor.FontSize = FontSize;
            _placeholder.FontSize = FontSize;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _updatingText = true;
        try
        {
            var text = _editor.Text;
            SetValue(TextProperty, text);
            _placeholder.IsVisible = string.IsNullOrEmpty(text);
        }
        finally
        {
            _updatingText = false;
        }
    }

    private void ApplyFont()
    {
        _editor.FontFamily = FontFamily;
        _editor.FontSize = FontSize;
        _placeholder.FontFamily = FontFamily;
        _placeholder.FontSize = FontSize;
    }

    private void ApplyBrushes()
    {
        var theme = ActualThemeVariant;
        IBrush bracketBrush = Brushes.Orange;
        IBrush nameBrush = Brushes.MediumPurple;
        IBrush envPrefixBrush = Brushes.SteelBlue;

        if (Application.Current?.TryGetResource("VariableBracketBrush", theme, out var b) == true && b is IBrush bb)
        {
            bracketBrush = bb;
        }

        if (Application.Current?.TryGetResource("VariableNameBrush", theme, out var n) == true && n is IBrush nb)
        {
            nameBrush = nb;
        }

        if (Application.Current?.TryGetResource("EnvVariablePrefixBrush", theme, out var ep) == true && ep is IBrush epb)
        {
            envPrefixBrush = epb;
        }

        _colorizer.SetBrushes(bracketBrush, nameBrush, envPrefixBrush);
        _editor.TextArea.TextView.Redraw();

        if (Application.Current?.TryGetResource("PanelBorderBrush", theme, out var borderResource) == true &&
            borderResource is IBrush borderBrush)
        {
            _border.BorderBrush = borderBrush;
        }

        if (Application.Current?.TryGetResource("SurfaceBackgroundBrush", theme, out var bgResource) == true &&
            bgResource is IBrush bgBrush)
        {
            _border.Background = bgBrush;
        }
    }

    private IReadOnlyList<string> GetVariableNames() =>
        VariableNameHelper.ExtractDistinctNames(AppViewModel?.ActiveEnvironmentVariables);

    private static IReadOnlyList<string> GetEnvVariableNames() =>
        VariableNameHelper.GetSystemEnvironmentVariableNames();
}

