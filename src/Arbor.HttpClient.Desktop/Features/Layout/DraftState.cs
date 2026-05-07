
namespace Arbor.HttpClient.Desktop.Features.Layout;

/// <summary>
/// Serialisation model for auto-saved request editor state.
/// Persisted to the <c>drafts/</c> folder in the application data directory
/// by <see cref="Services.DraftPersistenceService"/>.
/// </summary>
public sealed class DraftState
{
    public string RequestName { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public string Url { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool FollowRedirects { get; init; } = true;
    public string RequestTimeoutSecondsText { get; init; } = string.Empty;
    public string HttpVersion { get; init; } = "1.1";
    public string ContentTypeOption { get; init; } = "(none)";
    public string CustomContentType { get; init; } = string.Empty;
    public string AuthMode { get; init; } = "None";
    public string AuthBearerToken { get; init; } = string.Empty;
    public string AuthBasicUsername { get; init; } = string.Empty;
    public string AuthBasicPassword { get; init; } = string.Empty;
    public string AuthApiKey { get; init; } = string.Empty;
    public string AuthOAuth2AccessToken { get; init; } = string.Empty;
    public string RequestNotes { get; init; } = string.Empty;
    public string RequestType { get; init; } = "Http";
    public List<DraftHeaderDto> Headers { get; init; } = [];

    /// <summary>UTC timestamp of when the draft was last saved.</summary>
    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;
}
