namespace Arbor.HttpClient.Core.Models;

public sealed record HttpResponseDetails(
    int StatusCode,
    string ReasonPhrase,
    string Body,
    IReadOnlyList<(string Name, string Value)> Headers,
    byte[]? BodyBytes = null,
    double ElapsedMilliseconds = 0);
