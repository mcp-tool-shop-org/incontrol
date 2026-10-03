using Microsoft.Extensions.Options;
using InControl.Core.Compute;
using InControl.Core.Configuration;

namespace InControl.Inference.Ollama;

/// <summary>
/// Ollama on the URL from configuration. Used when no compute session is registered.
/// A connected rental replaces this with <c>ComputeSession</c>.
/// </summary>
public sealed class LocalOllamaEndpoint : IOllamaEndpoint
{
    private readonly IOptions<OllamaOptions> _options;

    public LocalOllamaEndpoint(IOptions<OllamaOptions> options)
    {
        _options = options;
    }

    public string BaseUrl => _options.Value.ResolveBaseUrl();

    public bool PromptsLeaveThisPc => false;

    public string Notice => ComputeNotice.OnThisPc;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}
