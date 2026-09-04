namespace RpaDevAssistant.Core.Analysis;

public interface IUiPathRuleCatalogProvider
{
    IReadOnlyList<UiPathRuleCatalogItem> GetRules(string? locale = null);
}
