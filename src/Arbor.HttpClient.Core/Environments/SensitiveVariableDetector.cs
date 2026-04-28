namespace Arbor.HttpClient.Core.Environments;

/// <summary>
/// Detects whether a variable name matches common sensitive-data patterns such as
/// passwords, tokens, secrets, and API keys.
/// </summary>
public static class SensitiveVariableDetector
{
    // Patterns whose presence (case-insensitive) in a variable name suggests sensitive content.
    private static readonly string[] SensitiveKeywords =
    [
        "password", "passwd", "pwd",
        "token", "access_token", "refresh_token",
        "secret",
        "apikey", "api_key", "api-key",
        "auth", "authorization",
        "credential",
        "private_key", "privatekey",
        "client_secret", "clientsecret",
        "bearer",
        "passphrase",
        "signing_key",
        "encryption_key",
    ];

    /// <summary>
    /// Returns <c>true</c> when <paramref name="variableName"/> contains at least one sensitive keyword.
    /// The comparison is case-insensitive.
    /// </summary>
    public static bool IsSensitive(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        foreach (var keyword in SensitiveKeywords)
        {
            if (variableName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
