namespace InControl.Core.Assistant;

/// <summary>
/// Defines the assistant's personality and behavioral configuration.
/// Immutable by design — personality cannot be changed at runtime by the assistant.
/// </summary>
public sealed record AssistantProfile
{
    /// <summary>
    /// Communication tone setting.
    /// </summary>
    public required Tone Tone { get; init; }

    /// <summary>
    /// How verbose the assistant should be in responses.
    /// </summary>
    public required Verbosity Verbosity { get; init; }

    /// <summary>
    /// How much the assistant should explain its reasoning.
    /// </summary>
    public required ExplanationLevel ExplanationLevel { get; init; }

    /// <summary>
    /// How cautious the assistant should be with suggestions.
    /// </summary>
    public required RiskTolerance RiskTolerance { get; init; }

    /// <summary>
    /// Default professional assistant profile.
    /// Calm, concise, explains when asked, moderate caution.
    /// </summary>
    public static AssistantProfile Default { get; } = new()
    {
        Tone = Tone.Professional,
        Verbosity = Verbosity.Concise,
        ExplanationLevel = ExplanationLevel.OnRequest,
        RiskTolerance = RiskTolerance.Moderate
    };

    /// <summary>
    /// Minimal assistant profile for advanced users.
    /// Brief responses, minimal explanation, lower caution.
    /// </summary>
    public static AssistantProfile Minimal { get; } = new()
    {
        Tone = Tone.Professional,
        Verbosity = Verbosity.Brief,
        ExplanationLevel = ExplanationLevel.Minimal,
        RiskTolerance = RiskTolerance.Low
    };

    /// <summary>
    /// Detailed assistant profile for users who want more context.
    /// Detailed responses, proactive explanation, high caution.
    /// </summary>
    public static AssistantProfile Detailed { get; } = new()
    {
        Tone = Tone.Professional,
        Verbosity = Verbosity.Detailed,
        ExplanationLevel = ExplanationLevel.Proactive,
        RiskTolerance = RiskTolerance.High
    };
}

/// <summary>
/// Communication tone for the assistant.
/// </summary>
public enum Tone
{
    /// <summary>
    /// Professional, calm, helpful. Default.
    /// </summary>
    Professional,

    /// <summary>
    /// Friendly but still professional.
    /// </summary>
    Friendly,

    /// <summary>
    /// Direct and to the point.
    /// </summary>
    Direct
}

/// <summary>
/// How verbose the assistant should be.
/// </summary>
public enum Verbosity
{
    /// <summary>
    /// Very short responses, essential info only.
    /// </summary>
    Brief,

    /// <summary>
    /// Information-dense but not verbose. Default.
    /// </summary>
    Concise,

    /// <summary>
    /// More context and explanation included.
    /// </summary>
    Detailed
}

/// <summary>
/// How much the assistant explains its reasoning.
/// </summary>
public enum ExplanationLevel
{
    /// <summary>
    /// Minimal explanation unless critical.
    /// </summary>
    Minimal,

    /// <summary>
    /// Explains when asked or for important decisions. Default.
    /// </summary>
    OnRequest,

    /// <summary>
    /// Proactively explains reasoning.
    /// </summary>
    Proactive
}

/// <summary>
/// How cautious the assistant should be.
/// </summary>
public enum RiskTolerance
{
    /// <summary>
    /// Fewer warnings, trusts user judgment.
    /// </summary>
    Low,

    /// <summary>
    /// Balanced caution. Default.
    /// </summary>
    Moderate,

    /// <summary>
    /// More warnings, extra confirmation for risky actions.
    /// </summary>
    High
}
