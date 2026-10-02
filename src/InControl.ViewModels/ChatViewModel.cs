using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InControl.Core.Configuration;
using InControl.Core.Models;
using InControl.Services.Interfaces;
using InControl.Services.Voice;

namespace InControl.ViewModels;

/// <summary>
/// ViewModel for the main chat interface.
/// </summary>
public partial class ChatViewModel : ViewModelBase
{
    private readonly IChatService _chatService;
    private readonly IVoiceService _voiceService;
    private readonly IOptions<VoiceOptions> _voiceOptions;
    private CancellationTokenSource? _generationCts;

    /// <summary>
    /// The current conversation.
    /// </summary>
    [ObservableProperty]
    private Conversation? _currentConversation;

    /// <summary>
    /// Messages in the current conversation.
    /// </summary>
    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    /// <summary>
    /// The current user input.
    /// </summary>
    [ObservableProperty]
    private string _inputText = string.Empty;

    /// <summary>
    /// Indicates whether a response is being generated.
    /// </summary>
    [ObservableProperty]
    private bool _isGenerating;

    /// <summary>
    /// The currently selected model.
    /// </summary>
    [ObservableProperty]
    private string? _selectedModel;

    /// <summary>
    /// Available models.
    /// </summary>
    public ObservableCollection<ModelInfo> AvailableModels { get; } = [];

    /// <summary>
    /// Current generation status text.
    /// </summary>
    [ObservableProperty]
    private string? _statusText;

    /// <summary>
    /// Tokens per second during generation.
    /// </summary>
    [ObservableProperty]
    private double? _tokensPerSecond;

    public ChatViewModel(
        IChatService chatService,
        IVoiceService voiceService,
        IOptions<VoiceOptions> voiceOptions,
        ILogger<ChatViewModel> logger)
        : base(logger)
    {
        _chatService = chatService;
        _voiceService = voiceService;
        _voiceOptions = voiceOptions;
    }

    /// <summary>
    /// The voice service, exposed for UI binding.
    /// </summary>
    public IVoiceService VoiceService => _voiceService;

    /// <summary>
    /// Indicates whether a message can be sent.
    /// </summary>
    public bool CanSend => !string.IsNullOrWhiteSpace(InputText) && !IsGenerating;

    /// <summary>
    /// Sends the current input message.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (CurrentConversation is null)
        {
            await CreateNewConversationAsync();
        }

        if (CurrentConversation is null)
        {
            SetError("Failed to create conversation");
            return;
        }

        var message = InputText.Trim();
        InputText = string.Empty;

        // Add user message to UI
        Messages.Add(new MessageViewModel(Message.User(message)));

        // Create placeholder for assistant response
        var assistantMessage = new MessageViewModel(Message.Assistant(string.Empty, SelectedModel))
        {
            IsStreaming = true
        };
        Messages.Add(assistantMessage);

        IsGenerating = true;
        _generationCts = new CancellationTokenSource();

        try
        {
            await foreach (var token in _chatService.SendMessageAsync(
                CurrentConversation.Id,
                message,
                _generationCts.Token))
            {
                assistantMessage.AppendContent(token);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Generation stopped";
        }
        catch (Exception ex)
        {
            SetError(ex, "Failed to generate response");
        }
        finally
        {
            assistantMessage.IsStreaming = false;
            IsGenerating = false;

            // Auto-speak if enabled and voice engine connected
            AutoSpeakIfEnabled(assistantMessage.Content);

            _generationCts?.Dispose();
            _generationCts = null;
        }
    }

    /// <summary>
    /// Stops the current generation.
    /// </summary>
    [RelayCommand]
    private void StopGeneration()
    {
        if (CurrentConversation is not null)
        {
            _chatService.StopGeneration(CurrentConversation.Id);
        }
        _generationCts?.Cancel();
    }

    /// <summary>
    /// Speaks the given text using the voice service.
    /// </summary>
    /// <param name="text">Text to speak.</param>
    [RelayCommand]
    private async Task SpeakTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        await _voiceService.SpeakAsync(text);
    }

    /// <summary>
    /// Stops current speech.
    /// </summary>
    [RelayCommand]
    private async Task StopSpeakingAsync()
    {
        await _voiceService.StopSpeakingAsync();
    }

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    [RelayCommand]
    private async Task CreateNewConversationAsync()
    {
        await ExecuteAsync(async () =>
        {
            Messages.Clear();
            CurrentConversation = await _chatService.CreateConversationAsync(
                model: SelectedModel);
        });
    }

    /// <summary>
    /// Loads an existing conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID to load.</param>
    [RelayCommand]
    private async Task LoadConversationAsync(Guid conversationId)
    {
        await ExecuteAsync(async () =>
        {
            var conversation = await _chatService.GetConversationAsync(conversationId);
            if (conversation is null)
            {
                SetError("Conversation not found");
                return;
            }

            CurrentConversation = conversation;
            Messages.Clear();

            foreach (var message in conversation.Messages)
            {
                Messages.Add(new MessageViewModel(message));
            }
        });
    }

    /// <summary>
    /// Regenerates the last assistant response.
    /// </summary>
    [RelayCommand]
    private async Task RegenerateAsync()
    {
        if (CurrentConversation is null || Messages.Count == 0) return;

        // Remove the last assistant message
        if (Messages[^1].Role == MessageRole.Assistant)
        {
            Messages.RemoveAt(Messages.Count - 1);
        }

        // Create new placeholder
        var assistantMessage = new MessageViewModel(Message.Assistant(string.Empty, SelectedModel))
        {
            IsStreaming = true
        };
        Messages.Add(assistantMessage);

        IsGenerating = true;
        _generationCts = new CancellationTokenSource();

        try
        {
            await foreach (var token in _chatService.RegenerateLastResponseAsync(
                CurrentConversation.Id,
                _generationCts.Token))
            {
                assistantMessage.AppendContent(token);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Generation stopped";
        }
        catch (Exception ex)
        {
            SetError(ex, "Failed to regenerate response");
        }
        finally
        {
            assistantMessage.IsStreaming = false;
            IsGenerating = false;

            // Auto-speak if enabled and voice engine connected
            AutoSpeakIfEnabled(assistantMessage.Content);

            _generationCts?.Dispose();
            _generationCts = null;
        }
    }

    private void AutoSpeakIfEnabled(string? content)
    {
        if (_voiceOptions.Value.AutoSpeak
            && _voiceService.ConnectionState == VoiceConnectionState.Connected
            && !string.IsNullOrWhiteSpace(content))
        {
            // Fire-and-forget — speaking should not block the UI
            _ = _voiceService.SpeakAsync(content);
        }
    }

    partial void OnInputTextChanged(string value)
    {
        SendCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsGeneratingChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
    }
}
