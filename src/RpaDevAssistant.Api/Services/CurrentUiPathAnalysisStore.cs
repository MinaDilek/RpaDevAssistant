using System.Collections.Concurrent;
using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Api.Services;

public interface ICurrentUiPathAnalysisStore
{
    void Set(UiPathProjectAnalysisResult analysis);
    bool TryGet(string projectPath, out UiPathProjectAnalysisResult? analysis);
}

public sealed class CurrentUiPathAnalysisStore : ICurrentUiPathAnalysisStore
{
    private readonly ConcurrentDictionary<string, UiPathProjectAnalysisResult> analyses = new(StringComparer.OrdinalIgnoreCase);

    public void Set(UiPathProjectAnalysisResult analysis) => analyses[Normalize(analysis.ProjectPath)] = analysis;

    public bool TryGet(string projectPath, out UiPathProjectAnalysisResult? analysis)
    {
        try
        {
            return analyses.TryGetValue(Normalize(projectPath), out analysis);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            analysis = null;
            return false;
        }
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
