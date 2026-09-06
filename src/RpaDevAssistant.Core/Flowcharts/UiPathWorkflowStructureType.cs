namespace RpaDevAssistant.Core.Flowcharts;

public enum UiPathWorkflowStructureType
{
    Sequence,
    Flowchart,
    StateMachine,
    Mixed,
    Unknown
}

public enum UiPathFlowNodeType
{
    Activity,
    Decision,
    Switch,
    Start,
    End,
    Unknown
}

public enum UiPathFlowBranchType
{
    Default,
    True,
    False,
    Case,
    Otherwise,
    Unknown
}

public enum UiPathFlowchartConversionLevel
{
    Safe,
    RequiresReview,
    Complex,
    NotSupported
}

public enum UiPathConversionConfidence
{
    High,
    Medium,
    Low
}

public enum UiPathConversionTransformationType
{
    Preserved,
    WrappedInIf,
    WrappedInSwitch,
    MovedToSequence,
    Unsupported
}
