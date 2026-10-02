using InControl.Core.Models;

namespace InControl.Inference.Interfaces;

/// <summary>
/// Manages model lifecycle operations.
/// </summary>
public interface IModelManager
{
    /// <summary>
    /// Event raised when model list changes.
    /// </summary>
    event EventHandler<ModelListChangedEventArgs>? ModelsChanged;

    /// <summary>
    /// Event raised during model download progress.
    /// </summary>
    event EventHandler<ModelDownloadProgressEventArgs>? DownloadProgress;

    /// <summary>
    /// Lists all available models.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available models.</returns>
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Pulls (downloads) a model from the registry.
    /// </summary>
    /// <param name="modelId">The model to pull (e.g., "llama3.2:7b").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pulled model info.</returns>
    Task<ModelInfo> PullModelAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a model from local storage.
    /// </summary>
    /// <param name="modelId">The model to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteModelAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Preloads a model into memory for faster first inference.
    /// </summary>
    /// <param name="modelId">The model to preload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PreloadModelAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Unloads a model from memory.
    /// </summary>
    /// <param name="modelId">The model to unload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UnloadModelAsync(string modelId, CancellationToken ct = default);
}

/// <summary>
/// Event args for model list changes.
/// </summary>
public sealed class ModelListChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change.
    /// </summary>
    public required ModelListChangeType ChangeType { get; init; }

    /// <summary>
    /// The affected model (if applicable).
    /// </summary>
    public ModelInfo? Model { get; init; }
}

/// <summary>
/// Type of model list change.
/// </summary>
public enum ModelListChangeType
{
    /// <summary>
    /// A model was added.
    /// </summary>
    Added,

    /// <summary>
    /// A model was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// A model was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// The entire list was refreshed.
    /// </summary>
    Refreshed
}

/// <summary>
/// Event args for model download progress.
/// </summary>
public sealed class ModelDownloadProgressEventArgs : EventArgs
{
    /// <summary>
    /// The model being downloaded.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Current download status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download (if known).
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Download progress as a percentage (0-100).
    /// </summary>
    public double? ProgressPercent => TotalBytes > 0
        ? (double)BytesDownloaded / TotalBytes.Value * 100
        : null;
}
