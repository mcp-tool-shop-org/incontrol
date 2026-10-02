namespace InControl.Core.Compute;

/// <summary>
/// Outcome of asking to move the chat, including the sentence to show the operator.
/// </summary>
public sealed record ComputeConnectResult(bool Connected, string Message)
{
    public static ComputeConnectResult Ok(string message) => new(true, message);

    public static ComputeConnectResult Fail(string message) => new(false, message);
}
