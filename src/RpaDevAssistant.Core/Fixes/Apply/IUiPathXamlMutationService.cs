namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathXamlMutationService
{
    UiPathXamlMutationResult BuildMutation(UiPathXamlMutationRequest request);
}
