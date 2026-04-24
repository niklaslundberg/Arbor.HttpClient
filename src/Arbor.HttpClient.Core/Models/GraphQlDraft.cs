namespace Arbor.HttpClient.Core.Models;

/// <summary>
/// Encapsulates the inputs for a GraphQL request: the query document, optional variables
/// (as a JSON string), and an optional operation name for documents that contain more than
/// one operation.
/// </summary>
public sealed record GraphQlDraft(
    string Url,
    string Query,
    string? VariablesJson,
    string? OperationName,
    IReadOnlyList<RequestHeader>? Headers = null);
