using System;
using Arbor.HttpClient.Desktop.Features.Variables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Regression tests for <see cref="VariableCompletionData.Complete"/>.
/// These tests require the Avalonia headless platform because they need a real
/// <see cref="AvaloniaEdit.Editing.TextArea"/> and <see cref="AvaloniaEdit.Editing.Caret"/>.
/// </summary>
[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class VariableCompletionDataTests
{
    /// <summary>
    /// Regression test for the crash reported when the user types <c>{{</c> in a header
    /// value field and immediately autocompletes without typing any prefix.
    ///
    /// The completion segment passed by <c>CompletionWindow</c> is an <c>AnchorSegment</c>
    /// whose start anchor uses <c>MovementType = AfterInsertion</c>.  When the prefix is
    /// empty the segment has <c>Length = 0</c>, making the <c>document.Replace</c> a pure
    /// insertion.  After the insertion the start anchor moves from S to S + insertionLength.
    /// Reading <c>completionSegment.Offset</c> after the replace therefore returns the
    /// post-move value, causing caret placement at S + 2 × T which exceeds the document
    /// length and throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public async Task Complete_EmptyPrefixWithAnchorSegment_CaretPlacedAfterInsertionWithoutException()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(() =>
        {
            var editor = new TextEditor();
            var window = new Window { Width = 400, Height = 100, Content = editor };
            window.Show();

            editor.Text = "{{";
            editor.CaretOffset = 2;

            // Create the AnchorSegment exactly as CompletionWindow.CompletionList_InsertionRequested
            // does: AnchorSegment(document, StartOffset, EndOffset - StartOffset).
            // With an empty prefix, StartOffset == EndOffset == 2 → Length = 0.
            // The start anchor's MovementType is AfterInsertion, so after a pure insertion
            // it moves to 2 + insertionLength — which is the source of the original bug.
            var document = editor.Document;
            var segment = new AnchorSegment(document, 2, 0);

            var completionData = new VariableCompletionData("host");

            // Act — must not throw ArgumentOutOfRangeException
            var act = () => completionData.Complete(editor.TextArea, segment, EventArgs.Empty);
            act.Should().NotThrow();

            // Assert: text is "{{host}}" and caret is at the end of the inserted text
            editor.Text.Should().Be("{{host}}");
            editor.CaretOffset.Should().Be("{{host}}".Length);

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    private sealed class TestEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .WithInterFont()
            .LogToTrace();
    }
}
