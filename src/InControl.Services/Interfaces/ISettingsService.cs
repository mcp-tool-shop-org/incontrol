using InControl.Core.Configuration;

namespace InControl.Services.Interfaces;

/// <summary>
/// Service for managing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Event raised when settings change.
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    /// <summary>
    /// Gets the current app options.
    /// </summary>
    AppOptions AppOptions { get; }

    /// <summary>
    /// Gets the current chat options.
    /// </summary>
    ChatOptions ChatOptions { get; }

    /// <summary>
    /// Gets the current inference options.
    /// </summary>
    InferenceOptions InferenceOptions { get; }

    /// <summary>
    /// Gets the current Ollama options.
    /// </summary>
    OllamaOptions OllamaOptions { get; }

    /// <summary>
    /// Gets the current voice options.
    /// </summary>
    VoiceOptions VoiceOptions { get; }

    /// <summary>
    /// Updates app options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAppOptionsAsync(
        Action<AppOptions> configure,
        CancellationToken ct = default);

    /// <summary>
    /// Updates chat options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateChatOptionsAsync(
        Action<ChatOptions> configure,
        CancellationToken ct = default);

    /// <summary>
    /// Updates inference options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateInferenceOptionsAsync(
        Action<InferenceOptions> configure,
        CancellationToken ct = default);

    /// <summary>
    /// Updates Ollama options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateOllamaOptionsAsync(
        Action<OllamaOptions> configure,
        CancellationToken ct = default);

    /// <summary>
    /// Updates voice options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateVoiceOptionsAsync(
        Action<VoiceOptions> configure,
        CancellationToken ct = default);

    /// <summary>
    /// Resets all settings to defaults.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ResetToDefaultsAsync(CancellationToken ct = default);

    /// <summary>
    /// Exports settings to JSON.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON representation of all settings.</returns>
    Task<string> ExportAsync(CancellationToken ct = default);

    /// <summary>
    /// Imports settings from JSON.
    /// </summary>
    /// <param name="json">The JSON to import.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ImportAsync(string json, CancellationToken ct = default);
}

/// <summary>
/// Event args for settings changes.
/// </summary>
public sealed class SettingsChangedEventArgs : EventArgs
{
    /// <summary>
    /// The section that changed.
    /// </summary>
    public required string Section { get; init; }
}
