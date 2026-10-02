namespace InControl.Core.Models;

/// <summary>
/// The role of a message sender in a conversation.
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// System prompt that sets the behavior of the assistant.
    /// </summary>
    System,

    /// <summary>
    /// Message from the user.
    /// </summary>
    User,

    /// <summary>
    /// Response from the AI assistant.
    /// </summary>
    Assistant
}
