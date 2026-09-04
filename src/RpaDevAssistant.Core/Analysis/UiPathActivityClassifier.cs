using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis;

public static class UiPathActivityClassifier
{
    private static readonly HashSet<string> ContainerActivityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Activity",
        "Sequence",
        "Flowchart",
        "TryCatch",
        "Catch",
        "Finally",
        "If",
        "While",
        "DoWhile",
        "ForEach",
        "Parallel",
        "Pick",
        "Switch"
    };

    public static bool IsExecutable(UiPathActivityInfo activity)
    {
        return !ContainerActivityNames.Contains(NormalizeActivityName(activity.Name));
    }

    public static string NormalizeActivityName(string activityName)
    {
        return activityName.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
    }

    public static bool IsNamed(UiPathActivityInfo activity, params string[] names)
    {
        var normalizedName = NormalizeActivityName(activity.Name);
        return names.Any(name => normalizedName.Equals(NormalizeActivityName(name), StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<UiPathActivityInfo> DescendantsOf(UiPathWorkflowAnalysis workflow, UiPathActivityInfo parent)
    {
        var activitiesByParentId = workflow.Activities
            .Where(activity => activity.ParentActivityId is not null)
            .GroupBy(activity => activity.ParentActivityId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var descendants = new List<UiPathActivityInfo>();
        var pending = new Stack<UiPathActivityInfo>();
        if (activitiesByParentId.TryGetValue(parent.ActivityId, out var children))
        {
            foreach (var child in children.Reverse())
            {
                pending.Push(child);
            }
        }

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            descendants.Add(current);

            if (!activitiesByParentId.TryGetValue(current.ActivityId, out var currentChildren))
            {
                continue;
            }

            foreach (var child in currentChildren.Reverse())
            {
                pending.Push(child);
            }
        }

        return descendants;
    }
}
