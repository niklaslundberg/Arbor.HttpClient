using System;
using Arbor.HttpClient.Desktop.Features.Variables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Skia;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class VariableAutoCompleteControllerTests
{
    [Fact]
    public async Task UpdateCompletionWindow_HeaderPrefixTyped_ShowsWellKnownHeaderSuggestions()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestEntryPoint));

        await session.Dispatch(() =>
        {
            var editor = new TextEditor();
            var window = new Window { Width = 400, Height = 100, Content = editor };
            window.Show();

            using var controller = new VariableAutoCompleteController(
                editor,
                getVariableNames: () => [],
                getEnvVariableNames: () => [],
                getPlainSuggestions: () => ["Accept", "Authorization", "Content-Type"]);

            editor.TextArea.PerformTextInput("A");
            editor.TextArea.PerformTextInput("c");

            var completionWindow = controller.CurrentCompletionWindow;
            completionWindow.Should().NotBeNull();
            completionWindow!.IsOpen.Should().BeTrue();
            completionWindow.CompletionList.CompletionData.Select(data => data.Text).Should().Contain("Accept");

            var completionItem = completionWindow.CompletionList.CompletionData.Single(data => data.Text == "Accept");
            completionItem.Complete(
                editor.TextArea,
                new TextSegment
                {
                    StartOffset = completionWindow.StartOffset,
                    Length = completionWindow.EndOffset - completionWindow.StartOffset
                },
                EventArgs.Empty);

            editor.Text.Should().Be("Accept");

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
