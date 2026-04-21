using System;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace Arbor.HttpClient.Desktop.Views;

internal sealed class VariableCompletionData(string variableName) : ICompletionData
{
    public string Text => variableName;
    public object Content => variableName;
    public object Description => $"Insert {{{{{variableName}}}}}";
    public double Priority => 0d;
    public Avalonia.Media.IImage? Image => null;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var document = textArea.Document;
        var insertionText = VariableCompletionEngine.BuildInsertionText(document.Text, completionSegment.EndOffset, variableName);
        document.Replace(completionSegment.Offset, completionSegment.Length, insertionText);
        textArea.Caret.Offset = completionSegment.Offset + insertionText.Length;
    }
}
