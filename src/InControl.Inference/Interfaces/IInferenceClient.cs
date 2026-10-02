using InControl.Core.Models;

namespace InControl.Inference.Interfaces;

/// <summary>
/// Abstraction for LLM inference backends.
/// </summary>
public interface IInferenceClient
{
    /// <summary>
    /// Gets the name of this inference backend.
    /// </summary>
    string BackendName { get; }

    /// <summary>
    /// Checks if the inference backend is available and responsive.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the backend is available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets detailed health information about the backend.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Health check result.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists available models.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available models.</returns>
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets information about a specific model.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Model information, or null if not found.</returns>
    Task<ModelInfo?> GetModelAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Streams chat completion tokens as they are generated.
    /// </summary>
    /// <param name="request">The chat request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of generated tokens.</returns>
    IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a complete chat response (non-streaming).
    /// </summary>
    /// <param name="request">The chat request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete response.</returns>
    Task<ChatResponse> ChatAsync(
        ChatRequest request,
        CancellationToken ct = default);
}
