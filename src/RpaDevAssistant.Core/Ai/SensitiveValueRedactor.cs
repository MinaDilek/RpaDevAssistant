namespace RpaDevAssistant.Core.Ai;

public sealed class SensitiveValueRedactor : ISecretRedactor
{
    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "pwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "clientsecret",
        "connectionstring",
        "connection_string"
    ];

    public string? Redact(string key, string? value)
    {
        if (SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            return "[REDACTED]";
        }

        return value;
    }
}
