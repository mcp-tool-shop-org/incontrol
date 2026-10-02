namespace InControl.Core.Exceptions;

/// <summary>
/// Exception thrown when a requested model is not available.
/// </summary>
public class ModelNotFoundException : InControlException
{
    /// <summary>
    /// The model that was not found.
    /// </summary>
    public string Model { get; }

    public ModelNotFoundException(string model)
        : base($"Model '{model}' was not found.", "MODEL_NOT_FOUND")
    {
        Model = model;
    }

    public ModelNotFoundException(string model, Exception innerException)
        : base($"Model '{model}' was not found.", "MODEL_NOT_FOUND", innerException)
    {
        Model = model;
    }
}
