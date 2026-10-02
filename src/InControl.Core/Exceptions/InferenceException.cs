namespace InControl.Core.Exceptions;

/// <summary>
/// Exception thrown when an LLM inference operation fails.
/// </summary>
public class InferenceException : InControlException
{
    /// <summary>
    /// The model that was being used when the error occurred.
    /// </summary>
    public string? Model { get; }

    /// <summary>
    /// The backend that threw the error (e.g., "Ollama", "LlamaCpp").
    /// </summary>
    public string? Backend { get; }

    public InferenceException(string message) : base(message, "INFERENCE_ERROR")
    {
    }

    public InferenceException(string message, string? model, string? backend)
        : base(message, "INFERENCE_ERROR")
    {
        Model = model;
        Backend = backend;
    }

    public InferenceException(string message, Exception innerException)
        : base(message, "INFERENCE_ERROR", innerException)
    {
    }

    public InferenceException(string message, string? model, string? backend, Exception innerException)
        : base(message, "INFERENCE_ERROR", innerException)
    {
        Model = model;
        Backend = backend;
    }
}
