using System;
using System.Text;
using System.Text.Json;

namespace Arbor.HttpClient.Core.Environments;

/// <summary>
/// Detects whether a string value is a JSON Web Token and, when so, extracts
/// the <c>exp</c> (expiration) claim as a UTC <see cref="DateTimeOffset"/>.
/// </summary>
public static class JwtExpiryExtractor
{
    /// <summary>
    /// Tries to extract the <c>exp</c> claim from a JWT string.
    /// </summary>
    /// <param name="value">The candidate value (e.g. a variable value typed by the user).</param>
    /// <param name="expiry">
    /// The UTC expiry derived from the JWT <c>exp</c> claim, or <c>null</c> if the value is not a
    /// valid JWT or the token does not contain an <c>exp</c> claim.
    /// </param>
    /// <returns><c>true</c> when a valid JWT with an <c>exp</c> claim was detected; otherwise <c>false</c>.</returns>
    public static bool TryGetExpiry(string? value, out DateTimeOffset? expiry)
    {
        expiry = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // A JWT has exactly three dot-separated segments: header.payload.signature
        var parts = value.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        // Decode the payload (second segment) from base64url.
        var payloadJson = DecodeBase64Url(parts[1]);
        if (payloadJson is null)
        {
            return false;
        }

        // Parse the JSON and look for the "exp" claim (NumericDate = seconds since Unix epoch).
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                return false;
            }

            if (!expElement.TryGetInt64(out var expSeconds))
            {
                return false;
            }

            expiry = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Decodes a base64url-encoded string (no padding required) to a UTF-8 string.
    /// Returns <c>null</c> on any decode error.
    /// </summary>
    private static string? DecodeBase64Url(string base64Url)
    {
        try
        {
            // Convert base64url to standard base64 by replacing characters and adding padding.
            var base64 = base64Url
                .Replace('-', '+')
                .Replace('_', '/');

            // Add required '=' padding.
            var paddingNeeded = base64.Length % 4;
            if (paddingNeeded > 0)
            {
                base64 = base64.PadRight(base64.Length + (4 - paddingNeeded), '=');
            }

            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
