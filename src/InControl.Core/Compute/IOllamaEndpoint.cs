namespace InControl.Core.Compute;

/// <summary>
/// Where the Ollama HTTP client should send the chat. Local is the default.
/// A remote endpoint is a loopback URL on this PC that an SSH forward owns.
/// </summary>
public interface IOllamaEndpoint
{
    string BaseUrl { get; }

    bool PromptsLeaveThisPc { get; }

    string Notice { get; }

    event EventHandler? Changed;
}
