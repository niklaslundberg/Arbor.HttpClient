using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace Arbor.HttpClient.Desktop.Demo;

/// <summary>
/// An embedded Kestrel HTTP server that provides demo endpoints for the local demo collection.
/// The server is started and stopped on demand and is never running by default.
/// Supports both HTTP and HTTPS (with a self-signed certificate).
/// Endpoints: <c>/echo</c>, <c>/sse</c>, <c>/ws</c>, <c>/status</c>.
/// </summary>
public sealed class DemoServer : IAsyncDisposable
{
    private WebApplication? _app;
    private X509Certificate2? _selfSignedCert;

    /// <summary>Default HTTP port used when no explicit port is specified.</summary>
    public const int DefaultPort = 5999;

    /// <summary>Default HTTPS port used when no explicit HTTPS port is specified.</summary>
    public const int DefaultHttpsPort = 5998;

    /// <summary>Gets a value indicating whether the demo server is currently running.</summary>
    public bool IsRunning => _app is not null;

    /// <summary>Gets the HTTP port the server is (or was last) started on.</summary>
    public int Port { get; private set; } = DefaultPort;

    /// <summary>Gets the HTTPS port the server is (or was last) started on.</summary>
    public int HttpsPort { get; private set; } = DefaultHttpsPort;

    /// <summary>Gets a value indicating whether HTTP is enabled on the running server.</summary>
    public bool IsHttpEnabled { get; private set; }

    /// <summary>Gets a value indicating whether HTTPS is enabled on the running server.</summary>
    public bool IsHttpsEnabled { get; private set; }

    /// <summary>
    /// Starts the demo server on the specified ports. No-op if already running.
    /// At least one of <paramref name="enableHttp"/> or <paramref name="enableHttps"/> must be <see langword="true"/>.
    /// </summary>
    public async Task StartAsync(
        int httpPort = DefaultPort,
        int httpsPort = DefaultHttpsPort,
        bool enableHttp = true,
        bool enableHttps = false,
        CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        if (!enableHttp && !enableHttps)
        {
            return;
        }

        Port = httpPort;
        HttpsPort = httpsPort;
        IsHttpEnabled = enableHttp;
        IsHttpsEnabled = enableHttps;

        if (enableHttps)
        {
            _selfSignedCert = CreateSelfSignedCertificate();
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            if (enableHttp)
            {
                options.Listen(IPAddress.Loopback, httpPort);
            }

            if (enableHttps && _selfSignedCert is { } cert)
            {
                options.Listen(IPAddress.Loopback, httpsPort, listenOptions =>
                {
                    listenOptions.UseHttps(cert);
                });
            }
        });

        var app = builder.Build();
        app.UseWebSockets();

        // /status — returns a JSON summary of the demo server.
        app.MapGet("/status", (HttpContext context) =>
        {
            context.Response.ContentType = "application/json";
            return Results.Ok(new
            {
                server = "Arbor.HttpClient Demo Server",
                version = "1.0",
                httpPort = enableHttp ? httpPort : (int?)null,
                httpsPort = enableHttps ? httpsPort : (int?)null,
                endpoints = new[] { "/echo", "/sse", "/ws", "/status" }
            });
        });

        // /echo — HTTP echo: reflects the request body back as the response body.
        // Returns 200 OK. When there is no body, returns a JSON summary of the
        // request method, path, and query string.
        app.Map("/echo", async context =>
        {
            context.Response.StatusCode = 200;
            if (context.Request.ContentLength > 0 || context.Request.Headers.TransferEncoding.Count > 0)
            {
                context.Response.ContentType = context.Request.ContentType ?? "application/json";
                await context.Request.Body.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            else
            {
                context.Response.ContentType = "application/json";
                var info = new
                {
                    method = context.Request.Method,
                    path = context.Request.Path.Value,
                    query = context.Request.QueryString.Value,
                    timestamp = DateTimeOffset.UtcNow
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(info), context.RequestAborted);
            }
        });

        // /sse — streams five numbered SSE events 500 ms apart, then closes.
        app.Map("/sse", async context =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";

            for (var i = 1; i <= 5; i++)
            {
                if (context.RequestAborted.IsCancellationRequested)
                {
                    break;
                }

                var payload = JsonSerializer.Serialize(new { @event = i, timestamp = DateTimeOffset.UtcNow });
                await context.Response.WriteAsync($"data: {payload}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);

                try
                {
                    await Task.Delay(500, context.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        // /ws — WebSocket echo: reflects every text frame back to the sender.
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[8192];

            while (ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
                {
                    if (ex is WebSocketException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DemoServer] WebSocket receive ended with an error: {ex.Message}");
                    }
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Echo close", CancellationToken.None);
                    return;
                }

                await ws.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    context.RequestAborted);
            }
        });

        _app = app;
        await app.StartAsync(cancellationToken);
    }

    /// <summary>Stops the demo server gracefully. No-op if not running.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is { } app)
        {
            _app = null;
            IsHttpEnabled = false;
            IsHttpsEnabled = false;
            await app.StopAsync(cancellationToken);
            await app.DisposeAsync();
        }

        _selfSignedCert?.Dispose();
        _selfSignedCert = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();

    /// <summary>
    /// Creates an in-memory self-signed certificate for <c>localhost</c>.
    /// The certificate is valid for one year from the moment it is created.
    /// </summary>
    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        // rsa must remain alive until after CreateSelfSigned() and Export() complete,
        // because CertificateRequest holds a reference to it for signing.
        // After the PFX round-trip the returned certificate owns an independent copy of the key.
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
                false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(1);

        // CreateSelfSigned links the RSA key into the certificate.  Export the certificate
        // as a PFX byte array (which serialises the key) and reload it so the returned
        // X509Certificate2 owns an independent, fully self-contained copy.
        using var tempCert = request.CreateSelfSigned(notBefore, notAfter);
        return X509CertificateLoader.LoadPkcs12(tempCert.Export(X509ContentType.Pfx), null);
        // rsa is disposed here (end of using var scope) — safe because the independent
        // copy embedded in the returned certificate no longer references this RSA object.
    }
}
