namespace InControl.Core.Configuration;

/// <summary>
/// Configuration options for chat behavior.
/// </summary>
public sealed class ChatOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Chat";

    /// <summary>
    /// Default system prompt for new conversations.
    /// </summary>
    public string? DefaultSystemPrompt { get; set; }

    /// <summary>
    /// Whether to stream responses as they generate.
    /// </summary>
    public bool StreamResponses { get; set; } = true;

    /// <summary>
    /// Whether to auto-generate conversation titles.
    /// </summary>
    public bool AutoGenerateTitles { get; set; } = true;

    /// <summary>
    /// Maximum number of messages to include in context.
    /// </summary>
    public int MaxContextMessages { get; set; } = 50;

    /// <summary>
    /// Whether to save conversations automatically.
    /// </summary>
    public bool AutoSave { get; set; } = true;

    /// <summary>
    /// Auto-save interval in seconds.
    /// </summary>
    public int AutoSaveIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to show token counts in the UI.
    /// </summary>
    public bool ShowTokenCounts { get; set; } = true;

    /// <summary>
    /// Whether to show generation speed in the UI.
    /// </summary>
    public bool ShowGenerationSpeed { get; set; } = true;
}
