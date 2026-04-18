using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Arbor.HttpClient.Desktop.Views;

/// <summary>
/// Colors <c>{{variableName}}</c> tokens with two distinct brushes:
/// one for the <c>{{</c> and <c>}}</c> brackets and another for the name between them.
/// </summary>
internal sealed partial class VariableTokenColorizer : DocumentColorizingTransformer
{
    // Capture groups: (1) opening bracket, (2) variable name, (3) closing bracket
    [GeneratedRegex(@"(\{\{)([^}]+)(\}\})", RegexOptions.Compiled)]
    private static partial Regex VariableTokenRegex();

    private IBrush _bracketBrush = Brushes.Orange;
    private IBrush _nameBrush = Brushes.MediumPurple;

    public void SetBrushes(IBrush bracketBrush, IBrush nameBrush)
    {
        _bracketBrush = bracketBrush;
        _nameBrush = nameBrush;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineText = CurrentContext.Document.GetText(line);
        foreach (Match match in VariableTokenRegex().Matches(lineText))
        {
            // Group 1: {{ — opening bracket
            ColorGroup(line, match.Groups[1], _bracketBrush);
            // Group 2: variable name
            ColorGroup(line, match.Groups[2], _nameBrush);
            // Group 3: }} — closing bracket
            ColorGroup(line, match.Groups[3], _bracketBrush);
        }
    }

    private void ColorGroup(DocumentLine line, Group group, IBrush brush)
    {
        var start = line.Offset + group.Index;
        var end = start + group.Length;
        ChangeLinePart(start, end, element =>
            element.TextRunProperties.SetForegroundBrush(brush));
    }
}
