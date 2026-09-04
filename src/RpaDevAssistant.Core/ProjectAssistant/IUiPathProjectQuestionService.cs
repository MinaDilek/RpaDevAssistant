namespace RpaDevAssistant.Core.ProjectAssistant;

public interface IUiPathProjectQuestionService
{
    Task<UiPathProjectAnswer> AskAsync(UiPathProjectQuestion request, CancellationToken cancellationToken);
}
