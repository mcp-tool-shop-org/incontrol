using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using InControl.Core.Models;

namespace InControl.ViewModels;

/// <summary>
/// ViewModel for displaying a single message.
/// </summary>
public partial class MessageViewModel : ObservableObject
{
    private readonly StringBuilder _contentBuilder = new();

    /// <summary>
    /// The underlying message.
    /// </summary>
    public Message Message { get; private set; }

    /// <summary>
    /// The message ID.
    /// </summary>
    public Guid Id => Message.Id;

    /// <summary>
    /// The message role.
    /// </summary>
    public MessageRole Role => Message.Role;

    /// <summary>
    /// The message content.
    /// </summary>
    [ObservableProperty]
    private string _content;

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt => Message.CreatedAt;

    /// <summary>
    /// The model that generated this message (for assistant messages).
    /// </summary>
    public string? Model => Message.Model;

    /// <summary>
    /// Whether the message is currently being streamed.
    /// </summary>
    [ObservableProperty]
    private bool _isStreaming;

    /// <summary>
    /// Whether this message is currently being spoken aloud.
    /// </summary>
    [ObservableProperty]
    private bool _isSpeaking;

    /// <summary>
    /// Whether this message can be spoken (assistant message, not streaming, voice connected).
    /// </summary>
    [ObservableProperty]
    private bool _canSpeak;

    /// <summary>
    /// Whether this is a user message.
    /// </summary>
    public bool IsUser => Role == MessageRole.User;

    /// <summary>
    /// Whether this is an assistant message.
    /// </summary>
    public bool IsAssistant => Role == MessageRole.Assistant;

    /// <summary>
    /// Whether this is a system message.
    /// </summary>
    public bool IsSystem => Role == MessageRole.System;

    /// <summary>
    /// Display name for the role.
    /// </summary>
    public string RoleDisplay => Role switch
    {
        MessageRole.User => "You",
        MessageRole.Assistant => "Assistant",
        MessageRole.System => "System",
        _ => Role.ToString()
    };

    /// <summary>
    /// Formatted timestamp.
    /// </summary>
    public string TimestampDisplay => CreatedAt.LocalDateTime.ToString("h:mm tt");

    public MessageViewModel(Message message)
    {
        Message = message;
        _content = message.Content;
        _contentBuilder.Append(message.Content);
    }

    /// <summary>
    /// Appends content during streaming.
    /// </summary>
    /// <param name="token">The token to append.</param>
    public void AppendContent(string token)
    {
        _contentBuilder.Append(token);
        Content = _contentBuilder.ToString();

        // Update the underlying message
        Message = Message with { Content = Content };
    }

    /// <summary>
    /// Finalizes the message after streaming completes.
    /// </summary>
    /// <param name="tokenCount">Optional token count.</param>
    public void FinalizeMessage(int? tokenCount = null)
    {
        IsStreaming = false;
        if (tokenCount.HasValue)
        {
            Message = Message with { TokenCount = tokenCount };
        }

        // Enable speak button for assistant messages with content
        if (IsAssistant && !string.IsNullOrWhiteSpace(Content))
        {
            CanSpeak = true;
        }
    }
}
