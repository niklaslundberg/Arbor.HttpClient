using System;
using Arbor.HttpClient.Desktop.Features.Variables;
using Avalonia.Controls;
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
    [AvaloniaFact(Timeout = 10_000)]
    public Task Text_SetWithNewline_NewlineIsStripped()
    {
        var (box, scope) = CreateBoxInScope();
        using (scope)
        {
            box.Text = "hello\nworld";
            box.Text.Should().Be("helloworld");
        }

        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task Text_SetWithCrLf_NewlineIsStripped()
    {
        var (box, scope) = CreateBoxInScope();
        using (scope)
        {
            box.Text = "hello\r\nworld";
            box.Text.Should().Be("helloworld");
        }

        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task Text_SetWithoutNewline_ValuePreserved()
    {
        var (box, scope) = CreateBoxInScope();
        using (scope)
        {
            box.Text = "https://example.com/api";
            box.Text.Should().Be("https://example.com/api");
        }

        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task Text_SetWithOnlyNewlines_ResultIsEmpty()
    {
        var (box, scope) = CreateBoxInScope();
        using (scope)
        {
            box.Text = "\r\n\r\n";
            box.Text.Should().BeEmpty();
        }

        return Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = 10_000)]
    public Task EditorText_SetWithNewline_NewlineIsStrippedFromEditorAndBoundProperty()
    {
        var (box, scope) = CreateBoxInScope();
        using (scope)
        {
            var innerEditor = box.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();
            innerEditor.Should().NotBeNull("VariableTextBox must contain a nested TextEditor");

            innerEditor.Text = "hello\nworld";

            innerEditor.Text.Should().Be("helloworld", "the editor itself must have no newlines");
            box.Text.Should().Be("helloworld", "the bound Text property must reflect the stripped value");
        }

        return Task.CompletedTask;
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
}
