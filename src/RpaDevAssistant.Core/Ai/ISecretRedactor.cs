namespace RpaDevAssistant.Core.Ai;

public interface ISecretRedactor
{
    string? Redact(string key, string? value);
}
