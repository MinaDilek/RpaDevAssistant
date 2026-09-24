namespace RpaDevAssistant.Core.Modules;

public interface IUiPathRuleModuleService
{
    UiPathRuleModule Export(string moduleId, string name, string version, string? publisher = null, string? description = null);
    UiPathRuleModuleImportResult Import(UiPathRuleModule module, bool overwrite = false);
}
