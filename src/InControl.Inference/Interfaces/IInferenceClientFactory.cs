namespace InControl.Inference.Interfaces;

/// <summary>
/// Factory for creating inference clients based on configuration.
/// </summary>
public interface IInferenceClientFactory
{
    /// <summary>
    /// Gets the currently configured inference client.
    /// </summary>
    /// <returns>The active inference client.</returns>
    IInferenceClient GetClient();

    /// <summary>
    /// Gets an inference client for a specific backend.
    /// </summary>
    /// <param name="backendName">The backend name (e.g., "Ollama", "LlamaCpp").</param>
    /// <returns>The inference client for the specified backend.</returns>
    /// <exception cref="ArgumentException">If the backend is not supported.</exception>
    IInferenceClient GetClient(string backendName);

    /// <summary>
    /// Gets all available backend names.
    /// </summary>
    /// <returns>List of supported backend names.</returns>
    IReadOnlyList<string> GetAvailableBackends();

    /// <summary>
    /// Checks if a backend is supported.
    /// </summary>
    /// <param name="backendName">The backend name to check.</param>
    /// <returns>True if the backend is supported.</returns>
    bool IsBackendSupported(string backendName);
}
