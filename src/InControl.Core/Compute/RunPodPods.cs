using System.Net;
using System.Text.Json;

namespace InControl.Core.Compute;

/// <summary>
/// One pod from RunPod's list. <see cref="SshCommand"/> is the direct public-IP login,
/// never the <c>ssh.runpod.io</c> proxy. Empty when this pod must not be dialed.
/// </summary>
public sealed record RunPodPod(
    string Id,
    string Name,
    string Host,
    int SshPort,
    bool OllamaPublished,
    string? Refusal)
{
    public bool CanOffer =>
        Refusal is null && !OllamaPublished && Host.Length > 0 && SshPort is >= 1 and <= 65535;

    public string SshCommand => CanOffer ? $"ssh -p {SshPort} root@{Host}" : "";

    public string Label => string.IsNullOrEmpty(Name) ? Id : $"{Name} ({Id})";
}

/// <summary>
/// Result of asking RunPod which pods already exist. Lookup does not create or stop a pod.
/// </summary>
public sealed record RunPodLookup(bool Reached, string Message, IReadOnlyList<RunPodPod> Pods);

/// <summary>
/// Lists pods. The API key stays in the environment and is never written to config.
/// </summary>
public interface IRunPodPods
{
    Task<RunPodLookup> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads a RunPod <c>GET /pods</c> body. The direct SSH endpoint is the public IP
/// plus the host port mapped to container port 22. A published 11434 is refused.
/// </summary>
public static class RunPodPodList
{
    /// <summary>
    /// Pods in the body. Null when the body is not a pod list.
    /// </summary>
    public static IReadOnlyList<RunPodPod>? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("pods", out var wrapped)
                && wrapped.ValueKind == JsonValueKind.Array)
            {
                root = wrapped;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var pods = new List<RunPodPod>();
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var pod = ReadOne(item);
                if (pod is not null)
                {
                    pods.Add(pod);
                }
            }

            return pods;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Summarize(IReadOnlyList<RunPodPod> pods)
    {
        var offerable = pods.Where(static pod => pod.CanOffer).ToList();
        if (offerable.Count == 1)
        {
            return $"Filled the command for {offerable[0].Label}. Connect sends the chat. Lookup does not start a pod.";
        }

        if (offerable.Count > 1)
        {
            return $"{offerable.Count} pods have a direct SSH address. Pick one. Connect sends the chat. Lookup does not start a pod.";
        }

        var published = pods.Where(static pod => pod.OllamaPublished).Select(static pod => pod.Label).ToList();
        if (published.Count > 0)
        {
            return ComputeNotice.RunPodOllamaPublished + " " + string.Join(", ", published) + ".";
        }

        return ComputeNotice.RunPodLookupEmpty;
    }

    private static RunPodPod? ReadOne(JsonElement item)
    {
        var id = StringOrEmpty(item, "id");
        if (id.Length == 0)
        {
            return null;
        }

        var status = StringOrEmpty(item, "desiredStatus");
        if (string.Equals(status, "TERMINATED", StringComparison.Ordinal))
        {
            return null;
        }

        var name = CleanName(StringOrEmpty(item, "name"), id);
        var published = PublishesOllama(item);
        var host = StringOrEmpty(item, "publicIp");
        var sshPort = MappedSshPort(item);

        if (published)
        {
            return new RunPodPod(id, name, host, sshPort, true, ComputeNotice.RunPodOllamaPublished);
        }

        if (host.Length == 0 || sshPort == 0)
        {
            return new RunPodPod(id, name, host, sshPort, false, ComputeNotice.RunPodLookupEmpty);
        }

        var endpoint = new SshEndpoint
        {
            DisplayName = name,
            User = "root",
            Host = host,
            SshPort = sshPort,
            IdentityFile = ""
        };
        var inspection = SshEndpointInspector.Inspect(endpoint, requireIdentity: false);
        if (!inspection.CanDial)
        {
            return new RunPodPod(id, name, host, sshPort, false, inspection.Error);
        }

        return new RunPodPod(id, name, host, sshPort, false, null);
    }

    private static bool PublishesOllama(JsonElement item)
    {
        if (!item.TryGetProperty("ports", out var ports) || ports.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var port in ports.EnumerateArray())
        {
            if (port.ValueKind == JsonValueKind.String
                && port.GetString() is { } text
                && text.StartsWith("11434", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int MappedSshPort(JsonElement item)
    {
        if (!item.TryGetProperty("portMappings", out var mappings) || mappings.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        if (!mappings.TryGetProperty("22", out var mapped))
        {
            return 0;
        }

        if (mapped.ValueKind == JsonValueKind.Number && mapped.TryGetInt32(out var port))
        {
            return port;
        }

        if (mapped.ValueKind == JsonValueKind.String && int.TryParse(mapped.GetString(), out port))
        {
            return port;
        }

        return 0;
    }

    private static string StringOrEmpty(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return "";
        }

        return value.GetString()?.Trim() ?? "";
    }

    private static string CleanName(string name, string id)
    {
        var cleaned = new string(name.Where(static ch => !char.IsControl(ch)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = id;
        }

        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }
}

/// <summary>
/// Calls RunPod's REST list. The key is read at call time from <c>RUNPOD_API_KEY</c>
/// unless a test supplies another source. It is sent only to <c>https://rest.runpod.io</c>.
/// </summary>
public sealed class RunPodPodClient : IRunPodPods
{
    public const string PodsUrl = "https://rest.runpod.io/v1/pods?includeMachine=true";

    private readonly Func<string?> _key;
    private readonly HttpMessageHandler? _handler;

    public RunPodPodClient(Func<string?>? key = null, HttpMessageHandler? handler = null)
    {
        _key = key ?? (() => Environment.GetEnvironmentVariable("RUNPOD_API_KEY"));
        _handler = handler;
    }

    public async Task<RunPodLookup> ListAsync(CancellationToken cancellationToken = default)
    {
        var key = _key()?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return new RunPodLookup(false, ComputeNotice.RunPodKeyMissing, []);
        }

        var ownsHandler = _handler is null;
        var handler = _handler ?? new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler, disposeHandler: ownsHandler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, PodsUrl);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return new RunPodLookup(false, Safe($"RunPod returned HTTP {(int)response.StatusCode}.", key), []);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = RunPodPodList.Read(body);
            if (parsed is null)
            {
                return new RunPodLookup(false, Safe("RunPod's pod list could not be read.", key), []);
            }

            var pods = parsed.Select(pod => pod with
            {
                Id = Safe(pod.Id, key),
                Name = Safe(pod.Name, key),
                Host = Safe(pod.Host, key),
                Refusal = pod.Refusal is null ? null : Safe(pod.Refusal, key)
            }).ToList();
            return new RunPodLookup(true, Safe(RunPodPodList.Summarize(pods), key), pods);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new RunPodLookup(false, Safe("RunPod could not be reached.", key), []);
        }
    }

    private static string Safe(string text, string key) =>
        string.IsNullOrEmpty(key) ? text : text.Replace(key, "[api key]", StringComparison.Ordinal);
}
