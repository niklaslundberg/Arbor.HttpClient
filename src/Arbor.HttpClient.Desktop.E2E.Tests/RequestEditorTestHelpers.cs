using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Shared test helpers for the <c>RequestEditor*Tests</c> classes.
/// </summary>
internal static class RequestEditorTestHelpers
{
    internal static RequestEditorViewModel CreateEditor(
        IReadOnlyList<EnvironmentVariable>? variables = null,
        string? defaultContentType = null)
    {
        var variableList = variables ?? [];
        var editor = new RequestEditorViewModel(
            new VariableResolver(),
            () => variableList);

        if (defaultContentType is { } contentType)
        {
            editor.DefaultContentType = contentType;
        }

        return editor;
    }
}
