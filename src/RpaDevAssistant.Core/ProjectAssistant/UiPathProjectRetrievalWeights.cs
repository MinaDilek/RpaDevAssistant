namespace RpaDevAssistant.Core.ProjectAssistant;

public static class UiPathProjectRetrievalWeights
{
    public const double ExactActivityMatch = 10;
    public const double WorkflowPathMatch = 8;
    public const double RuleIdMatch = 10;
    public const double DisplayNameContainsTerm = 5;
    public const double MessageContainsTerm = 3;
    public const double PropertyContainsTerm = 2;
    public const double DependencyMatch = 4;
    public const double PreferredWorkflowBoost = 4;
}
