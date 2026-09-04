using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Dependencies;

public interface IUiPathPackageActivityMapper
{
    UiPathPackageMapping ClassifyPackage(string packageName);

    string? MapActivityToPackageFamily(UiPathActivityInfo activity);

    UiPathModernClassicSignal ClassifyActivityMode(UiPathActivityInfo activity);
}
