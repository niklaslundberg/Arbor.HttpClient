using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Arbor.HttpClient.Desktop.Features.Variables;

/// <summary>
/// Colors <c>{{variableName}}</c> and <c>{{env:variableName}}</c> tokens with distinct brushes:
/// one for the <c>{{</c> and <c>}}</c> brackets, one for the variable name, and a third for the
/// <c>env:</c> prefix in system environment variable references.
/// </summary>
internal sealed partial class VariableTokenColorizer : DocumentColorizingTransformer
{
    // Matches {{env:varName}} — groups: (1) {{ (2) env: (3) varName (4) }}
    [GeneratedRegex(@"(\{\{)(env:)([^}]*)(\}\})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EnvTokenRegex();

    // Matches regular {{varName}} — groups: (1) {{ (2) varName (3) }}
    // Uses a negative look-ahead so it does not match {{env:...}} again.
    [GeneratedRegex(@"(\{\{)(?!env:)([^}]+)(\}\})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex VariableTokenRegex();

    private IBrush _bracketBrush = Brushes.Orange;
    private IBrush _nameBrush = Brushes.MediumPurple;
    private IBrush _envPrefixBrush = Brushes.SteelBlue;

    public void SetBrushes(IBrush bracketBrush, IBrush nameBrush, IBrush envPrefixBrush)
    {
        _bracketBrush = bracketBrush;
        _nameBrush = nameBrush;
        _envPrefixBrush = envPrefixBrush;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineText = CurrentContext.Document.GetText(line);

        foreach (Match match in EnvTokenRegex().Matches(lineText))
        {
            // Group 1: {{ — opening bracket
            ColorGroup(line, match.Groups[1], _bracketBrush);
            // Group 2: env: — prefix
            ColorGroup(line, match.Groups[2], _envPrefixBrush);
            // Group 3: variable name
            ColorGroup(line, match.Groups[3], _nameBrush);
            // Group 4: }} — closing bracket
            ColorGroup(line, match.Groups[4], _bracketBrush);
        }

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
