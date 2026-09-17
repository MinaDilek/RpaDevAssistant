namespace RpaDevAssistant.Core.Dependencies;

public interface IUiPathPackageMetadataProvider
{
    Task<UiPathPackageMetadata> GetMetadataAsync(
        string packageName,
        string? declaredVersion,
        CancellationToken cancellationToken = default);
}
