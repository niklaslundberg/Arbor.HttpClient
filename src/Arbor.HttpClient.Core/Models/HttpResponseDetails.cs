namespace Arbor.HttpClient.Core.Models;

public sealed record HttpResponseDetails(int StatusCode, string ReasonPhrase, string Body);
