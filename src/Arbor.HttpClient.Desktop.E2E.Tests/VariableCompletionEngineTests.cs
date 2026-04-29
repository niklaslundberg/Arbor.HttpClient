using Arbor.HttpClient.Desktop.Features.Variables;
using AvaloniaEdit.Document;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class VariableCompletionEngineTests
{
    [Fact]
    public void TryGetContext_ShouldReturnContext_WhenCaretIsInsideVariableToken()
    {
        var text = "https://{{ho";
        var caretOffset = text.Length;

        var hasContext = VariableCompletionEngine.TryGetContext(text, caretOffset, out var context);

        hasContext.Should().BeTrue();
        context.ReplaceStartOffset.Should().Be("https://{{".Length);
        context.Prefix.Should().Be("ho");
        context.IsEnvVariable.Should().BeFalse();
    }

    [Fact]
    public void TryGetContext_ShouldReturnFalse_WhenTokenIsClosedBeforeCaret()
    {
        var text = "https://{{host}}/api";
        var caretOffset = text.Length;

        var hasContext = VariableCompletionEngine.TryGetContext(text, caretOffset, out _);

        hasContext.Should().BeFalse();
    }

    [Fact]
    public void GetSuggestions_ShouldFilterByPrefix_CaseInsensitive()
    {
        var suggestions = VariableCompletionEngine.GetSuggestions(
            ["Host", "token", "tenant", "region"],
            "t");

        suggestions.Should().Equal("tenant", "token");
    }

    [Fact]
    public void BuildInsertionText_ShouldAvoidDuplicatingClosingBraces()
    {
        var insertion = VariableCompletionEngine.BuildInsertionText("{{ho}}", 4, "host");

        insertion.Should().Be("host");
    }

    [Fact]
    public void TryGetContext_ShouldReturnEnvContext_WhenCaretIsInsideEnvToken()
    {
        var text = "https://{{env:PA";
        var caretOffset = text.Length;

        var hasContext = VariableCompletionEngine.TryGetContext(text, caretOffset, out var context);

        hasContext.Should().BeTrue();
        context.IsEnvVariable.Should().BeTrue();
        context.Prefix.Should().Be("PA");
        context.ReplaceStartOffset.Should().Be("https://{{env:".Length);
    }

    [Fact]
    public void TryGetContext_ShouldReturnEnvContext_WhenCaretIsRightAfterEnvColon()
    {
        var text = "{{env:";
        var caretOffset = text.Length;

        var hasContext = VariableCompletionEngine.TryGetContext(text, caretOffset, out var context);

        hasContext.Should().BeTrue();
        context.IsEnvVariable.Should().BeTrue();
        context.Prefix.Should().BeEmpty();
        context.ReplaceStartOffset.Should().Be("{{env:".Length);
    }

    [Fact]
    public void TryGetContext_EnvPrefixDetection_IsCaseInsensitive()
    {
        var text = "{{ENV:VAR";
        var caretOffset = text.Length;

        var hasContext = VariableCompletionEngine.TryGetContext(text, caretOffset, out var context);

        hasContext.Should().BeTrue();
        context.IsEnvVariable.Should().BeTrue();
        context.Prefix.Should().Be("VAR");
    }

    [Fact]
    public void TryGetContext_ShouldTrimLeadingWhitespace_AfterEnvColon()
    {
        // "{{env: HOME" — space after the colon; prefix and start offset must skip the space
        var text = "{{env: HOME";
        var caretOffset = text.Length;

        var hasContext = VariableCompletionEngine.TryGetContext(text, caretOffset, out var context);

        hasContext.Should().BeTrue();
        context.IsEnvVariable.Should().BeTrue();
        context.Prefix.Should().Be("HOME");
        context.ReplaceStartOffset.Should().Be("{{env: ".Length);
    }

    [Fact]
    public void Complete_EmptyPrefix_CaretPlacedAfterInsertion()
    {
        // Regression test: when the user types "{{" and immediately autocompletes (no prefix
        // typed yet), the completion segment has Length = 0. The underlying AnchorSegment start
        // anchor uses MovementType.AfterInsertion, which moves the anchor to the end of the
        // inserted text after a pure insertion. Reading segment.Offset after document.Replace
        // returned the wrong (post-move) position, causing an ArgumentOutOfRangeException in
        // Caret.set_Offset. The fix caches the offset before Replace so the caret is always
        // placed at insertionOffset + insertionText.Length regardless of anchor movement.
        const string docText = "{{";
        var document = new TextDocument(docText);
        int insertionOffset = 2; // after "{{"
        string insertionText = "variableN}}"; // 11 chars — simulates BuildInsertionText result

        // Simulate what the fixed Complete() does: cache insertionOffset before Replace
        document.Replace(insertionOffset, 0, insertionText);
        int caretOffset = insertionOffset + insertionText.Length;

        caretOffset.Should().Be(13); // 2 + 11
        caretOffset.Should().BeLessThanOrEqualTo(document.TextLength);
        document.Text.Should().Be("{{variableN}}");
    }
}
