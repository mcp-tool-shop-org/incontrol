namespace InControl.Core.Compute;

/// <summary>
/// Proves the local forward reaches an Ollama, not merely an open TCP port.
/// </summary>
public interface IOllamaReadyProbe
{
    /// <summary>
    /// The Ollama version at <paramref name="baseUrl"/>, or null when it did not answer as Ollama.
    /// </summary>
    Task<string?> VersionAsync(string baseUrl, CancellationToken cancellationToken);
}
