using System.Text.RegularExpressions;

namespace RpaDevAssistant.Core.Analysis;

public sealed record UiPathSelectorAttribute(string Name, string Value);

public sealed record UiPathSelectorAnalysis
{
    public bool IsLiteral { get; init; }

    public int Length { get; init; }

    public bool ContainsIdx { get; init; }

    public IReadOnlyList<string> PotentialDynamicAttributes { get; init; } = [];

    public IReadOnlyList<UiPathSelectorAttribute> Attributes { get; init; } = [];

    public string? LiteralValue { get; init; }
}

public interface IUiPathSelectorAnalyzer
{
    UiPathSelectorAnalysis Analyze(string? selector);
}

public sealed class UiPathSelectorAnalyzer : IUiPathSelectorAnalyzer
{
    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_:][A-Za-z0-9_.:-]*)\s*=\s*['""](?<value>[^'""]*)['""]",
        RegexOptions.Compiled);

    private static readonly Regex GeneratedValueRegex = new(
        @"^(?:[0-9]{6,}|[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12})$",
        RegexOptions.Compiled);

    public UiPathSelectorAnalysis Analyze(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return new UiPathSelectorAnalysis();
        }

        var literal = UiPathExpressionClassifier.Unquote(
            UiPathExpressionClassifier.UnwrapExpression(selector.Trim()));
        var isLiteral = !literal.Contains('+') &&
            !literal.Contains("Config(", StringComparison.OrdinalIgnoreCase) &&
            !literal.Contains("Path.", StringComparison.OrdinalIgnoreCase);

        var attributes = AttributeRegex.Matches(literal)
            .Select(match => new UiPathSelectorAttribute(
                match.Groups["name"].Value,
                match.Groups["value"].Value))
            .ToArray();

        var dynamicAttributes = attributes
            .Where(attribute => IsPotentiallyDynamic(attribute.Name, attribute.Value))
            .Select(attribute => $"{attribute.Name}={attribute.Value}")
            .ToArray();

        return new UiPathSelectorAnalysis
        {
            IsLiteral = isLiteral,
            Length = literal.Length,
            ContainsIdx = Regex.IsMatch(literal, @"\bidx\s*=", RegexOptions.IgnoreCase),
            PotentialDynamicAttributes = dynamicAttributes,
            Attributes = attributes,
            LiteralValue = literal
        };
    }

    private static bool IsPotentiallyDynamic(string name, string value)
    {
        return (name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ctrlid", StringComparison.OrdinalIgnoreCase)) &&
            GeneratedValueRegex.IsMatch(value);
    }
}
