namespace InControl.Core.Compute;

/// <summary>
/// The dial fields parsed from a provider's ssh command. The private key path is optional
/// because some consoles print it separately.
/// </summary>
public sealed record SshConnectFields(string User, string Host, int SshPort, string? IdentityFile);
