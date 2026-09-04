namespace RpaDevAssistant.Core.ProjectAssistant;

public interface IUiPathProjectQuestionClassifier
{
    UiPathProjectQuestionIntent Classify(UiPathProjectQuestion request);

    string? DetectActivityName(string question);

    string? DetectWorkflowPath(string question, IEnumerable<string> workflowPaths);
}
