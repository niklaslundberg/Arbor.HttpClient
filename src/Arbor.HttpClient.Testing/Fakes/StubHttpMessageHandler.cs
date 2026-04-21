using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Arbor.HttpClient.Testing.Fakes;

/// <summary>
/// Stub HTTP message handler for testing HTTP requests.
/// Allows custom response logic via a delegate.
/// </summary>
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => Task.FromResult(send(request));
}
