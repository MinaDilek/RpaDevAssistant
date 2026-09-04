namespace RpaDevAssistant.Core.Localization;

public static class SupportedLocale
{
    public const string English = "en";

    public const string Turkish = "tr";

    public static string Normalize(string? locale)
    {
        if (string.Equals(locale, Turkish, StringComparison.OrdinalIgnoreCase)
            || string.Equals(locale, "tr-TR", StringComparison.OrdinalIgnoreCase))
        {
            return Turkish;
        }

        return English;
    }
}
