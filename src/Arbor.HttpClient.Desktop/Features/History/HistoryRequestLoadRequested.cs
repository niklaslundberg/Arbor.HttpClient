using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Desktop.Features.History;

/// <summary>
/// Published on the message bus when the user picks a history entry to load into the active
/// request editor. The History panel raises this instead of touching the request editor directly,
/// so it carries no dependency on the request panel; the request feature subscribes and applies
/// the entry.
/// </summary>
public sealed record HistoryRequestLoadRequested(RequestHistoryEntry Entry);
