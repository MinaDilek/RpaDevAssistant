using System.Globalization;
using System.Text.RegularExpressions;

namespace RpaDevAssistant.Core.Analysis;

public enum UiPathExpressionKind
{
    LiteralString,
    LiteralNumber,
    BooleanLiteral,
    Expression,
    VariableReference,
    ConfigReference,
    Unknown
}

public sealed record UiPathExpressionClassification(
    UiPathExpressionKind Kind,
    string? LiteralValue,
    bool IsHardCodedLiteral);

public interface IUiPathExpressionClassifier
{
    UiPathExpressionClassification Classify(string? value);

    bool TryParseLiteralNumber(string? value, out double number);

    bool TryParseTimeoutMilliseconds(string? value, out int milliseconds);
}

public sealed class UiPathExpressionClassifier : IUiPathExpressionClassifier
{
    private static readonly Regex VariableReferenceRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?$",
        RegexOptions.Compiled);

    public UiPathExpressionClassification Classify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new UiPathExpressionClassification(UiPathExpressionKind.Unknown, null, false);
        }

        var trimmed = value.Trim();
        var unwrapped = UnwrapExpression(trimmed);

        if (IsConfigReference(unwrapped))
        {
            return new UiPathExpressionClassification(UiPathExpressionKind.ConfigReference, null, false);
        }

        if (bool.TryParse(unwrapped, out _))
        {
            return new UiPathExpressionClassification(UiPathExpressionKind.BooleanLiteral, unwrapped, true);
        }

        if (TryParseLiteralNumber(unwrapped, out _))
        {
            return new UiPathExpressionClassification(UiPathExpressionKind.LiteralNumber, unwrapped, true);
        }

        if (IsQuotedString(unwrapped))
        {
            return new UiPathExpressionClassification(
                UiPathExpressionKind.LiteralString,
                Unquote(unwrapped),
                true);
        }

        if (LooksLikeExpression(unwrapped))
        {
            return new UiPathExpressionClassification(UiPathExpressionKind.Expression, null, false);
        }

        if (VariableReferenceRegex.IsMatch(unwrapped))
        {
            return new UiPathExpressionClassification(UiPathExpressionKind.VariableReference, null, false);
        }

        return new UiPathExpressionClassification(UiPathExpressionKind.LiteralString, unwrapped, true);
    }

    public bool TryParseLiteralNumber(string? value, out double number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = UnwrapExpression(value.Trim());
        return double.TryParse(
            trimmed,
            NumberStyles.Integer | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out number);
    }

    public bool TryParseTimeoutMilliseconds(string? value, out int milliseconds)
    {
        milliseconds = 0;
        if (TryParseLiteralNumber(value, out var numeric))
        {
            milliseconds = Convert.ToInt32(numeric, CultureInfo.InvariantCulture);
            return true;
        }

        var classification = Classify(value);
        if (classification.IsHardCodedLiteral &&
            TimeSpan.TryParse(classification.LiteralValue, CultureInfo.InvariantCulture, out var timeSpan))
        {
            milliseconds = Convert.ToInt32(timeSpan.TotalMilliseconds, CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    public static string UnwrapExpression(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']'
            ? trimmed[1..^1].Trim()
            : trimmed;
    }

    public static string Unquote(string value)
    {
        var trimmed = value.Trim();
        return IsQuotedString(trimmed)
            ? trimmed[1..^1]
            : trimmed;
    }

    private static bool IsConfigReference(string value)
    {
        return value.Contains("Config(", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("in_Config(", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeExpression(string value)
    {
        return value.Contains('(') ||
            value.Contains('+') ||
            value.Contains('&') ||
            value.Contains("$\"",
                StringComparison.Ordinal) ||
            value.Contains("Path.",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Environment.",
                StringComparison.OrdinalIgnoreCase) ||
            value.Contains("New ",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQuotedString(string value)
    {
        return value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''));
    }
}
