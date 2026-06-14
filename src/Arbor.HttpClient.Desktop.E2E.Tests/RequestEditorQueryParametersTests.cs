using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.Variables;
using static Arbor.HttpClient.Desktop.E2E.Tests.RequestEditorTestHelpers;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Focused unit tests for <see cref="RequestEditorViewModel"/>: URL/query-parameter synchronization.
/// These tests do NOT require the Avalonia headless session.
/// </summary>
public class RequestEditorQueryParametersTests
{
    [AvaloniaFact(Timeout = 10_000)]
    public void QueryParameters_ArePopulated_WhenUrlContainsQueryString()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/search?q=hello&page=2";

        editor.RequestQueryParameters.Should().HaveCount(3);
        editor.RequestQueryParameters[0].Key.Should().Be("q");
        editor.RequestQueryParameters[0].Value.Should().Be("hello");
        editor.RequestQueryParameters[1].Key.Should().Be("page");
        editor.RequestQueryParameters[1].Value.Should().Be("2");
        editor.RequestQueryParameters[2].Key.Should().BeEmpty();
        editor.RequestQueryParameters[2].IsEnabled.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void QueryParameters_AreCleared_WhenUrlHasNoQuery()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/search?q=hello";
        editor.RequestUrl = "http://localhost:5000/search";

        editor.RequestQueryParameters.Should().HaveCount(1);
        editor.RequestQueryParameters[0].Key.Should().BeEmpty();
        editor.RequestQueryParameters[0].IsEnabled.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void Url_IsUpdated_WhenQueryParameterValueChanges()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/items?foo=bar";

        editor.RequestQueryParameters[0].Value = "baz";

        editor.RequestUrl.Should().Contain("foo=baz");
        editor.RequestUrl.Should().NotContain("foo=bar");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void Url_IsUpdated_WhenDisabledQueryParameterIsExcluded()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/items?a=1&b=2";

        editor.RequestQueryParameters[0].IsEnabled = false;

        editor.RequestUrl.Should().NotContain("a=1");
        editor.RequestUrl.Should().Contain("b=2");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void AddQueryParameter_AddsEmptyEntryToCollection()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/items";
        editor.AddQueryParameterCommand.Execute().Subscribe();

        editor.RequestQueryParameters.Should().HaveCount(1);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RemoveQueryParameter_RemovesEntry()
    {
        var editor = CreateEditor();
        editor.RequestUrl = "http://localhost:5000/items?x=1";
        var param = editor.RequestQueryParameters[0];
        editor.RemoveQueryParameterCommand.Execute(param).Subscribe();

        editor.RequestQueryParameters.Should().HaveCount(1);
        editor.RequestQueryParameters[0].Key.Should().BeEmpty();
        editor.RequestQueryParameters[0].IsEnabled.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void PlaceholderQueryParameter_AutoEnablesAndAppendsNewPlaceholder_WhenKeyIsTyped()
    {
        var editor = CreateEditor();
        var placeholder = editor.RequestQueryParameters[0];
        placeholder.Key.Should().BeEmpty();
        placeholder.IsEnabled.Should().BeFalse();

        placeholder.Key = "page";

        placeholder.IsEnabled.Should().BeTrue();
        editor.RequestQueryParameters.Should().HaveCount(2);
        editor.RequestQueryParameters[^1].Key.Should().BeEmpty();
        editor.RequestQueryParameters[^1].IsEnabled.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void PlaceholderQueryParameter_CannotBeEnabled_WhenKeyIsBlank()
    {
        var editor = CreateEditor();
        var placeholder = editor.RequestQueryParameters[0];

        placeholder.IsEnabled = true;

        editor.RequestQueryParameters.Should().HaveCount(1);
        editor.RequestQueryParameters[0].Key.Should().BeEmpty();
        editor.RequestQueryParameters[0].IsEnabled.Should().BeFalse();
    }
}
