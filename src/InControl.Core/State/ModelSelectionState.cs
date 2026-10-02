namespace InControl.Core.State;

/// <summary>
/// Represents the current model selection state.
/// </summary>
public sealed record ModelSelectionState
{
    /// <summary>
    /// The currently selected model name.
    /// </summary>
    public required string SelectedModel { get; init; }

    /// <summary>
    /// Available models that can be selected.
    /// </summary>
    public IReadOnlyList<string> AvailableModels { get; init; } = [];

    /// <summary>
    /// The backend being used (e.g., "Ollama", "LlamaCpp").
    /// </summary>
    public required string Backend { get; init; }

    /// <summary>
    /// When the model list was last refreshed.
    /// </summary>
    public DateTimeOffset? LastRefreshed { get; init; }

    /// <summary>
    /// Creates a default state with no model selected.
    /// </summary>
    public static ModelSelectionState Default(string backend = "Ollama") => new()
    {
        SelectedModel = string.Empty,
        Backend = backend,
        AvailableModels = []
    };

    /// <summary>
    /// Creates state with a selected model.
    /// </summary>
    public static ModelSelectionState WithModel(
        string model,
        string backend = "Ollama",
        IEnumerable<string>? availableModels = null) => new()
    {
        SelectedModel = model,
        Backend = backend,
        AvailableModels = availableModels?.ToList() ?? [model],
        LastRefreshed = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Returns state with a different model selected.
    /// </summary>
    public ModelSelectionState SelectModel(string model) => this with
    {
        SelectedModel = model
    };

    /// <summary>
    /// Returns state with updated available models.
    /// </summary>
    public ModelSelectionState WithAvailableModels(IEnumerable<string> models) => this with
    {
        AvailableModels = models.ToList(),
        LastRefreshed = DateTimeOffset.UtcNow
    };
}
