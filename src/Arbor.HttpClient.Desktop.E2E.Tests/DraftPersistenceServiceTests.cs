using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Core.Services;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using Arbor.HttpClient.Desktop.ViewModels;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Unit tests for <see cref="DraftPersistenceService"/>.
/// These tests do NOT require the Avalonia headless session — all file I/O and
/// editor-state capture/restore logic is exercised directly.
/// </summary>
public class DraftPersistenceServiceTests
{
    private static RequestEditorViewModel CreateEditor() =>
        new(new VariableResolver(), () => []);

    private static string CreateTempDraftsFolder() =>
        Path.Join(Path.GetTempPath(), $"arbor-drafts-{Guid.NewGuid():N}");

    // ── LoadDraft ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadDraftAsync_ReturnsNull_WhenDraftFileDoesNotExist()
    {
        var folder = CreateTempDraftsFolder();
        var service = new DraftPersistenceService(folder);

        var result = await service.LoadDraftAsync();

        result.Should().BeNull();
    }

    [Fact]
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

    [Fact]
    public async Task SaveAndLoadDraftAsync_ShouldRoundtripAllFields()
    {
        var folder = CreateTempDraftsFolder();
        var service = new DraftPersistenceService(folder);
        var savedAt = DateTimeOffset.UtcNow;

        var draft = new DraftState
        {
            RequestName = "My Request",
            Method = "POST",
            Url = "http://localhost:5000/data",
            Body = "{\"key\":\"value\"}",
            FollowRedirects = false,
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
        loaded!.RequestName.Should().Be("My Request");
        loaded.Method.Should().Be("POST");
        loaded.Url.Should().Be("http://localhost:5000/data");
        loaded.Body.Should().Be("{\"key\":\"value\"}");
        loaded.FollowRedirects.Should().BeFalse();
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

    [Fact]
    public async Task ClearDraft_DeletesDraftFile()
    {
        var folder = CreateTempDraftsFolder();
        var service = new DraftPersistenceService(folder);
        await service.SaveDraftAsync(new DraftState());

        service.ClearDraft();

        File.Exists(service.DraftFilePath).Should().BeFalse();
    }

    [Fact]
    public void ClearDraft_DoesNotThrow_WhenFileDoesNotExist()
    {
        var folder = CreateTempDraftsFolder();
        var service = new DraftPersistenceService(folder);

        var action = service.ClearDraft;

        action.Should().NotThrow();
    }

    // ── CaptureFromEditor ─────────────────────────────────────────────────────

    [Fact]
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
        editor.RequestNotes = "my note";
        editor.RequestHeaders.Add(new RequestHeaderViewModel { Name = "Accept", Value = "application/json", IsEnabled = true });

        var draft = DraftPersistenceService.CaptureFromEditor(editor);

        draft.RequestName.Should().Be("Test");
        draft.Method.Should().Be("DELETE");
        draft.Url.Should().Be("http://localhost:5000/items/1");
        draft.RequestNotes.Should().Be("my note");
        draft.Headers.Should().ContainSingle(h => h.Name == "Accept" && h.Value == "application/json");
        draft.SavedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── RestoreToEditor ───────────────────────────────────────────────────────

    [Fact]
    public void RestoreToEditor_PopulatesAllEditorFields()
    {
        var editor = CreateEditor();
        var draft = new DraftState
        {
            RequestName = "Restored",
            Method = "PATCH",
            Url = "http://localhost:5000/update",
            Body = "body-content",
            FollowRedirects = false,
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
        editor.SelectedHttpVersionOption.Should().Be("2.0");
        editor.SelectedContentTypeOption.Should().Be("application/xml");
        editor.RequestNotes.Should().Be("restored note");
        editor.SelectedRequestType.Should().Be(RequestType.Http);
        editor.RequestHeaders.Should().ContainSingle();
        editor.RequestHeaders[0].Name.Should().Be("X-Api-Version");
        editor.RequestHeaders[0].Value.Should().Be("2");
        editor.RequestHeaders[0].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void RestoreToEditor_IgnoresUnknownRequestType()
    {
        var editor = CreateEditor();
        var draft = new DraftState { RequestType = "UnknownProtocol" };

        // Should not throw; RequestType stays at its default.
        var action = () => DraftPersistenceService.RestoreToEditor(draft, editor);

        action.Should().NotThrow();
        editor.SelectedRequestType.Should().Be(RequestType.Http);
    }

    // ── SaveDraftAsync creates folder if missing ──────────────────────────────

    [Fact]
    public async Task SaveDraftAsync_CreatesDirectoryIfMissing()
    {
        var folder = CreateTempDraftsFolder(); // folder does not exist yet
        var service = new DraftPersistenceService(folder);

        await service.SaveDraftAsync(new DraftState { Url = "http://localhost:5000" });

        Directory.Exists(folder).Should().BeTrue();
        File.Exists(service.DraftFilePath).Should().BeTrue();
    }

    // ── CaptureFromEditor → RestoreToEditor round-trip ───────────────────────

    [Fact]
    public void CaptureAndRestore_PreservesEditorState()
    {
        var original = CreateEditor();
        original.RequestName = "Round-trip";
        original.SelectedMethod = "PUT";
        original.RequestUrl = "http://localhost:5000";
        original.RequestBody = "{}";
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
        restored.SelectedAuthModeOption.Should().Be(original.SelectedAuthModeOption);
        restored.AuthBearerToken.Should().Be(original.AuthBearerToken);
        restored.RequestHeaders.Should().ContainSingle(h => h.Name == "X-Foo" && h.Value == "bar");
    }
}
