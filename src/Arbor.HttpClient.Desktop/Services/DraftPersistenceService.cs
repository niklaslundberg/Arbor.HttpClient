using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.ViewModels;

namespace Arbor.HttpClient.Desktop.Services;

/// <summary>
/// Saves and restores request editor state to/from a JSON draft file in
/// <c>drafts/draft.json</c> under the given folder.
/// The auto-save loop itself is driven by the caller (see
/// <see cref="MainWindowViewModel"/>) using a <see cref="System.Threading.PeriodicTimer"/>
/// so that this service has no dependency on Avalonia and remains fully unit-testable.
/// </summary>
public sealed class DraftPersistenceService(string draftsFolder)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal string DraftFilePath => Path.Join(draftsFolder, "draft.json");

    /// <summary>
    /// Reads the persisted draft file and returns the deserialised state,
    /// or <see langword="null"/> if the file does not exist or cannot be parsed.
    /// </summary>
    public DraftState? LoadDraft()
    {
        var path = DraftFilePath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<DraftState>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Serialises <paramref name="state"/> and writes it to the draft file atomically.</summary>
    public void SaveDraft(DraftState state)
    {
        Directory.CreateDirectory(draftsFolder);
        var json = JsonSerializer.Serialize(state, SerializerOptions);
        var draftFilePath = DraftFilePath;
        var tempFilePath = Path.Join(draftsFolder, Path.GetRandomFileName());
        try
        {
            File.WriteAllText(tempFilePath, json);
            if (File.Exists(draftFilePath))
            {
                File.Replace(tempFilePath, draftFilePath, null);
            }
            else
            {
                File.Move(tempFilePath, draftFilePath);
            }
        }
        catch
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (IOException)
            {
                // best-effort cleanup of temp file
            }

            throw;
        }
    }

    /// <summary>Deletes the draft file if it exists (best-effort; ignores errors).</summary>
    public void ClearDraft()
    {
        try
        {
            if (File.Exists(DraftFilePath))
            {
                File.Delete(DraftFilePath);
            }
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
        catch (UnauthorizedAccessException)
        {
            // best-effort cleanup
        }
    }

    /// <summary>
    /// Captures the current state of <paramref name="editor"/> into a <see cref="DraftState"/>.
    /// Must be called on the UI thread (or a thread where observable property reads are safe).
    /// </summary>
    public static DraftState CaptureFromEditor(RequestEditorViewModel editor)
    {
        var headers = editor.RequestHeaders
            .Select(h => new DraftHeaderDto
            {
                Name = h.Name,
                Value = h.Value,
                IsEnabled = h.IsEnabled
            })
            .ToList();

        return new DraftState
        {
            RequestName = editor.RequestName,
            Method = editor.SelectedMethod,
            Url = editor.RequestUrl,
            Body = editor.RequestBody,
            FollowRedirects = editor.FollowRedirectsForRequest,
            HttpVersion = editor.SelectedHttpVersionOption,
            ContentTypeOption = editor.SelectedContentTypeOption,
            CustomContentType = editor.CustomContentType,
            AuthMode = editor.SelectedAuthModeOption,
            AuthBearerToken = editor.AuthBearerToken,
            AuthBasicUsername = editor.AuthBasicUsername,
            AuthBasicPassword = editor.AuthBasicPassword,
            AuthApiKey = editor.AuthApiKey,
            AuthOAuth2AccessToken = editor.AuthOAuth2AccessToken,
            RequestNotes = editor.RequestNotes,
            RequestType = editor.SelectedRequestType.ToString(),
            Headers = headers,
            SavedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Applies the saved <paramref name="state"/> back to <paramref name="editor"/>.
    /// Must be called on the UI thread.
    /// </summary>
    public static void RestoreToEditor(DraftState state, RequestEditorViewModel editor)
    {
        editor.RequestName = state.RequestName;
        editor.SelectedMethod = state.Method;
        editor.RequestUrl = state.Url;
        editor.RequestBody = state.Body;
        editor.FollowRedirectsForRequest = state.FollowRedirects;
        editor.SelectedHttpVersionOption = state.HttpVersion;
        editor.SelectedContentTypeOption = state.ContentTypeOption;
        editor.CustomContentType = state.CustomContentType;
        editor.SelectedAuthModeOption = state.AuthMode;
        editor.AuthBearerToken = state.AuthBearerToken;
        editor.AuthBasicUsername = state.AuthBasicUsername;
        editor.AuthBasicPassword = state.AuthBasicPassword;
        editor.AuthApiKey = state.AuthApiKey;
        editor.AuthOAuth2AccessToken = state.AuthOAuth2AccessToken;
        editor.RequestNotes = state.RequestNotes;

        if (Enum.TryParse<RequestType>(state.RequestType, out var requestType))
        {
            editor.SelectedRequestType = requestType;
        }

        editor.RequestHeaders.Clear();
        foreach (var h in state.Headers)
        {
            editor.RequestHeaders.Add(new RequestHeaderViewModel
            {
                Name = h.Name,
                Value = h.Value,
                IsEnabled = h.IsEnabled
            });
        }
    }
}
