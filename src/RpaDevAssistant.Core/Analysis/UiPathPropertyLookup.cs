using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis;

public static class UiPathPropertyLookup
{
    public static bool TryGet(UiPathActivityInfo activity, out string? value, params string[] names)
    {
        foreach (var name in names)
        {
            var property = activity.Properties.FirstOrDefault(
                candidate => candidate.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(property.Key))
            {
                value = property.Value;
                return true;
            }

            var argument = activity.Arguments.FirstOrDefault(
                candidate => candidate.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(argument.Key))
            {
                value = argument.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    public static IEnumerable<KeyValuePair<string, string?>> AllProperties(UiPathActivityInfo activity)
    {
        foreach (var property in activity.Properties)
        {
            yield return property;
        }

        foreach (var argument in activity.Arguments)
        {
            yield return argument;
        }
    }
}
