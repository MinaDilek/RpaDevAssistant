namespace RpaDevAssistant.Core.Analysis.Profiles;

public sealed class UnknownRuleProfileException : Exception
{
    public UnknownRuleProfileException(string profileId)
        : base($"Rule profile '{profileId}' was not found.")
    {
        ProfileId = profileId;
    }

    public string ProfileId { get; }
}
