using System.ComponentModel;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="RequestTabViewModel"/>.
/// These tests do NOT require the Avalonia headless session — they exercise
/// tab title logic and property-change propagation directly.
/// </summary>
public class RequestTabViewModelTests
{
    private static RequestEditorViewModel CreateEditor() =>
        new(new VariableResolver(), () => []);

    // ── DisplayTitle ────────────────────────────────────────────────────────

    [Fact]
    public void DisplayTitle_ReturnsNew_WhenRequestNameIsEmpty()
    {
        var editor = CreateEditor();
        editor.RequestName = string.Empty;
        var tab = new RequestTabViewModel(editor);

        tab.DisplayTitle.Should().Be("New");
    }

    [Fact]
    public void DisplayTitle_ReturnsNew_WhenRequestNameIsWhitespace()
    {
        var editor = CreateEditor();
        editor.RequestName = "   ";
        var tab = new RequestTabViewModel(editor);

        tab.DisplayTitle.Should().Be("New");
    }

    [Fact]
    public void DisplayTitle_ReturnsRequestName_WhenNameIsSet()
    {
        var editor = CreateEditor();
        editor.RequestName = "Get Users";
        var tab = new RequestTabViewModel(editor);

        tab.DisplayTitle.Should().Be("Get Users");
    }

    [Fact]
    public void DisplayTitle_UpdatesWhenRequestNameChanges()
    {
        var editor = CreateEditor();
        editor.RequestName = string.Empty;
        var tab = new RequestTabViewModel(editor);

        tab.DisplayTitle.Should().Be("New");

        editor.RequestName = "Create Order";

        tab.DisplayTitle.Should().Be("Create Order");
    }

    [Fact]
    public void DisplayTitle_RaisesPropertyChanged_WhenRequestNameChanges()
    {
        var editor = CreateEditor();
        editor.RequestName = "Original";
        var tab = new RequestTabViewModel(editor);

        var changedProperties = new List<string?>();
        tab.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        editor.RequestName = "Updated";

        changedProperties.Should().Contain(nameof(RequestTabViewModel.DisplayTitle));
    }

    [Fact]
    public void DisplayTitle_DoesNotRaisePropertyChanged_WhenUnrelatedPropertyChanges()
    {
        var editor = CreateEditor();
        var tab = new RequestTabViewModel(editor);

        var changedProperties = new List<string?>();
        tab.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        editor.RequestUrl = "http://example.com/other";

        changedProperties.Should().NotContain(nameof(RequestTabViewModel.DisplayTitle));
    }

    [Fact]
    public void DisplayTitle_ChangesFromNameToNew_WhenNameIsCleared()
    {
        var editor = CreateEditor();
        editor.RequestName = "My Request";
        var tab = new RequestTabViewModel(editor);

        editor.RequestName = string.Empty;

        tab.DisplayTitle.Should().Be("New");
    }
}
