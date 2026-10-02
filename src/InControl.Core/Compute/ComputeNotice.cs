namespace InControl.Core.Compute;

/// <summary>
/// Sentences the operator actually sees. The remote sentence names the machine
/// and says the chat leaves this PC. The tool allowlist is a different decision.
/// </summary>
public static class ComputeNotice
{
    public const string OnThisPc = "On this PC. Prompts stay here.";

    public const string ConnectConsequence = "Connect — this chat will be sent to that machine";

    public const string StayOnThisPc = "Stay on this PC";

    public const string NotTheAllowlist =
        "This choice is separate from Connectivity. Allowing a tool URL does not send the chat to a rented GPU.";

    public const string AddressResets =
        "This address dies when the rental restarts. Paste the new ssh command after a reset.";

    public const string RunPodProxyBlocked =
        "RunPod's ssh.runpod.io proxy is a shell only. It cannot forward a port. Use the direct SSH line: root at the public IP and the mapped port.";

    public const string VastProxyWarning =
        "This Vast host is the proxy address. Port forwarding is documented on the direct public-IP SSH, not on sshNNN.vast.ai. If the tunnel fails, paste that direct line.";

    public const string OllamaPortIsNotSsh =
        "Port 11434 is Ollama on the remote machine, not the SSH port. Use the port from the provider's ssh command.";

    public const string MissingIdentity =
        "Choose the private key file. The key stays on this PC. InControl does not upload it.";

    public const string OllamaStillLocal =
        "Leave Ollama on 127.0.0.1:11434 on that machine. Do not set OLLAMA_HOST=0.0.0.0 and do not publish port 11434.";

    public const string TunnelClosed = "SSH exited before the tunnel was up.";

    public const string TunnelTimedOut = "SSH did not open the local forward in time.";

    public const string HostKeyChanged =
        " The host key changed. A reset rental does that. Forget the saved host key only if you mean to trust this machine.";

    public const string IdentityRedaction = "[identity file]";

    public static string OnRemote(string displayName, string host) =>
        $"This chat is on {displayName} ({host}). Prompts leave this PC.";

    public static string TunnelUp(string displayName, string host) =>
        $"{OnRemote(displayName, host)} {OllamaStillLocal}";
}
