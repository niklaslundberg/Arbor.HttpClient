using System;
using Arbor.HttpClient.Desktop.Features.Variables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.VisualTree;
using AvaloniaEdit;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Tests for <see cref="VariableTextBox"/> newline-stripping behaviour.
/// Newlines must never appear in single-line fields such as query parameters,
/// header key/value, and auth fields.
/// </summary>
[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class VariableTextBoxTests
{
    /// <summary>
    /// When the <see cref="VariableTextBox.Text"/> property is set to a value that
    /// contains a newline the newline must be stripped before it reaches the editor.
    /// </summary>
    [Fact]
    public async Task Text_SetWithNewline_NewlineIsStripped()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await HeadlessTestTimeout.DispatchAsync(session, () =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                box.Text = "hello\nworld";
                box.Text.Should().Be("helloworld");
            }
            return true;
        });
    }

    /// <summary>
    /// CR+LF sequences (Windows-style paste) must also be stripped.
    /// </summary>
    [Fact]
    public async Task Text_SetWithCrLf_NewlineIsStripped()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await HeadlessTestTimeout.DispatchAsync(session, () =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                box.Text = "hello\r\nworld";
                box.Text.Should().Be("helloworld");
            }
            return true;
        });
    }

    /// <summary>
    /// When text without newlines is set the value is preserved unchanged.
    /// </summary>
    [Fact]
    public async Task Text_SetWithoutNewline_ValuePreserved()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await HeadlessTestTimeout.DispatchAsync(session, () =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                box.Text = "https://example.com/api";
                box.Text.Should().Be("https://example.com/api");
            }
            return true;
        });
    }

    /// <summary>
    /// When text with only newlines is set the result must be an empty string.
    /// </summary>
    [Fact]
    public async Task Text_SetWithOnlyNewlines_ResultIsEmpty()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await HeadlessTestTimeout.DispatchAsync(session, () =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                box.Text = "\r\n\r\n";
                box.Text.Should().BeEmpty();
            }
            return true;
        });
    }

    /// <summary>
    /// When text containing a newline is typed into (or pasted into) the inner
    /// <c>AvaloniaEdit.TextEditor</c> directly, the <c>OnEditorTextChanged</c> handler must
    /// strip the newline from both the editor text and the bound <see cref="VariableTextBox.Text"/>.
    /// </summary>
    [Fact]
    public async Task EditorText_SetWithNewline_NewlineIsStrippedFromEditorAndBoundProperty()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await HeadlessTestTimeout.DispatchAsync(session, () =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                var innerEditor = box.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();
                innerEditor.Should().NotBeNull("VariableTextBox must contain a nested TextEditor");

                innerEditor!.Text = "hello\nworld";

                innerEditor.Text.Should().Be("helloworld", "the editor itself must have no newlines");
                box.Text.Should().Be("helloworld", "the bound Text property must reflect the stripped value");
            }
            return true;
        });
    }

    private static (VariableTextBox box, Window window) CreateBoxInWindow()
    {
        var box = new VariableTextBox();
        var window = new Window { Width = 400, Height = 100, Content = box };
        window.Show();
        return (box, window);
    }

    private sealed class WindowScope : IDisposable
    {
        private readonly Window _window;
        internal WindowScope(Window window) => _window = window;
        public void Dispose() => _window.Close();
    }

    private static (VariableTextBox box, IDisposable scope) CreateBoxInScope()
    {
        var (box, window) = CreateBoxInWindow();
        return (box, new WindowScope(window));
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
