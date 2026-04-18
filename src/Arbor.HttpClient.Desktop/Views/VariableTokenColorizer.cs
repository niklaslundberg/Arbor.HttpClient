using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Arbor.HttpClient.Desktop.Views;

/// <summary>
/// Colors <c>{{variableName}}</c> tokens with the configured foreground brush.
/// </summary>
internal sealed partial class VariableTokenColorizer : DocumentColorizingTransformer
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
