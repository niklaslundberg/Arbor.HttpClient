using System;
using Arbor.HttpClient.Desktop.Features.Variables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;

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

        await session.Dispatch(() =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                box.Text = "hello\nworld";
                box.Text.Should().Be("helloworld");
            }
            return true;
        }, CancellationToken.None);
    }

    /// <summary>
    /// CR+LF sequences (Windows-style paste) must also be stripped.
    /// </summary>
    [Fact]
    public async Task Text_SetWithCrLf_NewlineIsStripped()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(() =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                box.Text = "hello\r\nworld";
                box.Text.Should().Be("helloworld");
            }
            return true;
        }, CancellationToken.None);
    }

    /// <summary>
    /// When text without newlines is set the value is preserved unchanged.
    /// </summary>
    [Fact]
    public async Task Text_SetWithoutNewline_ValuePreserved()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(() =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                box.Text = "https://example.com/api";
                box.Text.Should().Be("https://example.com/api");
            }
            return true;
        }, CancellationToken.None);
    }

    /// <summary>
    /// When text with only newlines is set the result must be an empty string.
    /// </summary>
    [Fact]
    public async Task Text_SetWithOnlyNewlines_ResultIsEmpty()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(() =>
        {
            var (box, scope) = CreateBoxInScope();
            using (scope)
            {
                box.Text = "\r\n\r\n";
                box.Text.Should().BeEmpty();
            }
            return true;
        }, CancellationToken.None);
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
