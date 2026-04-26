using Arbor.HttpClient.Desktop.Views;

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
}
