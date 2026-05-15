using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Unit tests for <see cref="DraftPersistenceService"/>.
/// These tests do NOT require the Avalonia headless session — all file I/O and
/// editor-state capture/restore logic is exercised directly.
/// </summary>
[Trait("Category", "Integration")]
public class DraftPersistenceServiceTests
{
    private static RequestEditorViewModel CreateEditor() =>
        new(new VariableResolver(), () => []);

    private static string CreateTempDraftsFolder() =>
        Path.Join(Path.GetTempPath(), $"arbor-drafts-{Guid.NewGuid():N}");

    // ── LoadDraft ─────────────────────────────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadDraftAsync_ReturnsNull_WhenDraftFileDoesNotExist()
    {
        var folder = CreateTempDraftsFolder();
        var service = new DraftPersistenceService(folder);

        var result = await service.LoadDraftAsync();

        result.Should().BeNull();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task LoadDraftAsync_ReturnsNull_WhenDraftFileIsCorrupted()
    {
        var folder = CreateTempDraftsFolder();
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Join(folder, "draft.json"), "not valid json{{{{");
        var service = new DraftPersistenceService(folder);

        var result = await service.LoadDraftAsync();

        result.Should().BeNull();
    }

    // ── SaveDraftAsync / LoadDraftAsync round-trip ────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SaveAndLoadDraftAsync_ShouldRoundtripAllFields()
    {
        var folder = CreateTempDraftsFolder();
        var service = new DraftPersistenceService(folder);
        var savedAt = DateTimeOffset.UtcNow;

        var draft = new RequestEditorSnapshot
        {
            RequestName = "My Request",
            Method = "POST",
            Url = "http://localhost:5000/data",
            Body = "{\"key\":\"value\"}",
            FollowRedirects = false,
            ValidateUrlBeforeSend = false,
            PrettyPrintRequestBody = true,
            PrettyPrintRequestBodyUseIndentation = false,
            TlsVersionOverrideOption = "Tls12",
            HttpVersion = "2.0",
            ContentTypeOption = "application/json",
            CustomContentType = string.Empty,
            AuthMode = "Bearer Token",
            AuthBearerToken = "secret-token",
            AuthBasicUsername = string.Empty,
            AuthBasicPassword = string.Empty,
            AuthApiKey = string.Empty,
            AuthOAuth2AccessToken = string.Empty,
            RequestNotes = "Some notes",
            RequestType = "Http",
            Headers = [new DraftHeaderDto { Name = "X-Custom", Value = "val", IsEnabled = true }],
            SavedAt = savedAt
        };

        await service.SaveDraftAsync(draft);
        var loaded = await service.LoadDraftAsync();

        loaded.Should().NotBeNull();
        loaded.RequestName.Should().Be("My Request");
        loaded.Method.Should().Be("POST");
        loaded.Url.Should().Be("http://localhost:5000/data");
        loaded.Body.Should().Be("{\"key\":\"value\"}");
        loaded.FollowRedirects.Should().BeFalse();
        loaded.ValidateUrlBeforeSend.Should().BeFalse();
        loaded.PrettyPrintRequestBody.Should().BeTrue();
        loaded.PrettyPrintRequestBodyUseIndentation.Should().BeFalse();
        loaded.TlsVersionOverrideOption.Should().Be("Tls12");
        loaded.HttpVersion.Should().Be("2.0");
        loaded.ContentTypeOption.Should().Be("application/json");
        loaded.AuthMode.Should().Be("Bearer Token");
        loaded.AuthBearerToken.Should().Be("secret-token");
        loaded.RequestNotes.Should().Be("Some notes");
        loaded.RequestType.Should().Be("Http");
        loaded.Headers.Should().ContainSingle();
        loaded.Headers[0].Name.Should().Be("X-Custom");
        loaded.Headers[0].Value.Should().Be("val");
        loaded.Headers[0].IsEnabled.Should().BeTrue();
        loaded.SavedAt.Should().BeCloseTo(savedAt, TimeSpan.FromSeconds(1));
    }

    // ── ClearDraft ────────────────────────────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ClearDraft_DeletesDraftFile()
    {
        var folder = CreateTempDraftsFolder();
        var service = new DraftPersistenceService(folder);
        await service.SaveDraftAsync(new RequestEditorSnapshot());

        service.ClearDraft();

        File.Exists(service.DraftFilePath).Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void ClearDraft_DoesNotThrow_WhenFileDoesNotExist()
    {
        var folder = CreateTempDraftsFolder();
        var service = new DraftPersistenceService(folder);

        var action = service.ClearDraft;

        action.Should().NotThrow();
    }

    // ── CaptureFromEditor ─────────────────────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public void CaptureFromEditor_ReflectsEditorState()
    {
        var editor = CreateEditor();
        editor.RequestName = "Test";
        editor.SelectedMethod = "DELETE";
        editor.RequestUrl = "http://localhost:5000/items/1";
        editor.RequestBody = string.Empty;
        editor.SelectedHttpVersionOption = "1.1";
        editor.SelectedContentTypeOption = "(none)";
        editor.SelectedAuthModeOption = "None";
        editor.ValidateUrlBeforeSend = false;
        editor.PrettyPrintRequestBody = true;
        editor.PrettyPrintRequestBodyUseIndentation = false;
        editor.SelectedTlsVersionOverrideOption = "Tls13";
        editor.RequestNotes = "my note";
        editor.RequestHeaders.Add(new RequestHeaderViewModel { Name = "Accept", Value = "application/json", IsEnabled = true });

        var draft = DraftPersistenceService.CaptureFromEditor(editor);

        draft.RequestName.Should().Be("Test");
        draft.Method.Should().Be("DELETE");
        draft.Url.Should().Be("http://localhost:5000/items/1");
        draft.ValidateUrlBeforeSend.Should().BeFalse();
        draft.PrettyPrintRequestBody.Should().BeTrue();
        draft.PrettyPrintRequestBodyUseIndentation.Should().BeFalse();
        draft.TlsVersionOverrideOption.Should().Be("Tls13");
        draft.RequestNotes.Should().Be("my note");
        draft.Headers.Should().ContainSingle(h => h.Name == "Accept" && h.Value == "application/json");
        draft.SavedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── RestoreToEditor ───────────────────────────────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public void RestoreToEditor_PopulatesAllEditorFields()
    {
        var editor = CreateEditor();
        var draft = new RequestEditorSnapshot
        {
            RequestName = "Restored",
            Method = "PATCH",
            Url = "http://localhost:5000/update",
            Body = "body-content",
            FollowRedirects = false,
            ValidateUrlBeforeSend = false,
            PrettyPrintRequestBody = true,
            PrettyPrintRequestBodyUseIndentation = false,
            TlsVersionOverrideOption = "Tls12",
            HttpVersion = "2.0",
            ContentTypeOption = "application/xml",
            CustomContentType = string.Empty,
            AuthMode = "None",
            AuthBearerToken = string.Empty,
            AuthBasicUsername = string.Empty,
            AuthBasicPassword = string.Empty,
            AuthApiKey = string.Empty,
            AuthOAuth2AccessToken = string.Empty,
            RequestNotes = "restored note",
            RequestType = "Http",
            Headers = [new DraftHeaderDto { Name = "X-Api-Version", Value = "2", IsEnabled = false }]
        };

        DraftPersistenceService.RestoreToEditor(draft, editor);

        editor.RequestName.Should().Be("Restored");
        editor.SelectedMethod.Should().Be("PATCH");
        editor.RequestUrl.Should().Be("http://localhost:5000/update");
        editor.RequestBody.Should().Be("body-content");
        editor.FollowRedirectsForRequest.Should().BeFalse();
        editor.ValidateUrlBeforeSend.Should().BeFalse();
        editor.PrettyPrintRequestBody.Should().BeTrue();
        editor.PrettyPrintRequestBodyUseIndentation.Should().BeFalse();
        editor.SelectedTlsVersionOverrideOption.Should().Be("Tls12");
        editor.SelectedHttpVersionOption.Should().Be("2.0");
        editor.SelectedContentTypeOption.Should().Be("application/xml");
        editor.RequestNotes.Should().Be("restored note");
        editor.SelectedRequestType.Should().Be(RequestType.Http);
        editor.RequestHeaders.Where(h => !string.IsNullOrEmpty(h.Name)).Should().ContainSingle();
        editor.RequestHeaders[0].Name.Should().Be("X-Api-Version");
        editor.RequestHeaders[0].Value.Should().Be("2");
        editor.RequestHeaders[0].IsEnabled.Should().BeFalse();
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RestoreToEditor_IgnoresUnknownRequestType()
    {
        var editor = CreateEditor();
        var draft = new RequestEditorSnapshot { RequestType = "UnknownProtocol" };

        // Should not throw; RequestType stays at its default.
        var action = () => DraftPersistenceService.RestoreToEditor(draft, editor);

        action.Should().NotThrow();
        editor.SelectedRequestType.Should().Be(RequestType.Http);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void RestoreToEditor_UsesDefaultTlsOverride_WhenSavedValueIsUnknown()
    {
        var editor = CreateEditor();
        var draft = new RequestEditorSnapshot { TlsVersionOverrideOption = "Tls99" };

        DraftPersistenceService.RestoreToEditor(draft, editor);

        editor.SelectedTlsVersionOverrideOption.Should().Be(RequestEditorViewModel.DefaultTlsVersionOverrideOption);
    }

    // ── SaveDraftAsync creates folder if missing ──────────────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public async Task SaveDraftAsync_CreatesDirectoryIfMissing()
    {
        var folder = CreateTempDraftsFolder(); // folder does not exist yet
        var service = new DraftPersistenceService(folder);

        await service.SaveDraftAsync(new RequestEditorSnapshot { Url = "http://localhost:5000" });

        Directory.Exists(folder).Should().BeTrue();
        File.Exists(service.DraftFilePath).Should().BeTrue();
    }

    // ── CaptureFromEditor → RestoreToEditor round-trip ───────────────────────

    [AvaloniaFact(Timeout = 10_000)]
    public void CaptureAndRestore_PreservesEditorState()
    {
        var original = CreateEditor();
        original.RequestName = "Round-trip";
        original.SelectedMethod = "PUT";
        original.RequestUrl = "http://localhost:5000";
        original.RequestBody = "{}";
        original.PrettyPrintRequestBody = true;
        original.PrettyPrintRequestBodyUseIndentation = false;
        original.SelectedAuthModeOption = RequestEditorViewModel.AuthBearerOption;
        original.AuthBearerToken = "tok123";
        original.RequestHeaders.Add(new RequestHeaderViewModel { Name = "X-Foo", Value = "bar", IsEnabled = true });

        var draft = DraftPersistenceService.CaptureFromEditor(original);

        var restored = CreateEditor();
        DraftPersistenceService.RestoreToEditor(draft, restored);

        restored.RequestName.Should().Be(original.RequestName);
        restored.SelectedMethod.Should().Be(original.SelectedMethod);
        restored.RequestUrl.Should().Be(original.RequestUrl);
        restored.RequestBody.Should().Be(original.RequestBody);
        restored.PrettyPrintRequestBody.Should().BeTrue();
        restored.PrettyPrintRequestBodyUseIndentation.Should().BeFalse();
        restored.SelectedAuthModeOption.Should().Be(original.SelectedAuthModeOption);
        restored.AuthBearerToken.Should().Be(original.AuthBearerToken);
        restored.RequestHeaders.Should().ContainSingle(h => h.Name == "X-Foo" && h.Value == "bar");
    }
}

