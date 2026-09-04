namespace RpaDevAssistant.Core.Localization;

public interface IRpaDevAssistantLocalizer
{
    string NormalizeLocale(string? locale);

    string Get(string key, string? locale = null, IReadOnlyDictionary<string, string?>? values = null, string? fallback = null);
}
