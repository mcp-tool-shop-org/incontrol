using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using InControl.Core.Models;
using InControl.Services.Interfaces;

namespace InControl.ViewModels;

/// <summary>
/// ViewModel for the conversation list sidebar.
/// </summary>
public partial class ConversationListViewModel : ViewModelBase
{
    private readonly IChatService _chatService;

    /// <summary>
    /// All conversations.
    /// </summary>
    public ObservableCollection<ConversationItemViewModel> Conversations { get; } = [];

    /// <summary>
    /// The currently selected conversation.
    /// </summary>
    [ObservableProperty]
    private ConversationItemViewModel? _selectedConversation;

    /// <summary>
    /// Filter text for searching conversations.
    /// </summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// Event raised when a conversation is selected.
    /// </summary>
    public event EventHandler<Guid>? ConversationSelected;

    public ConversationListViewModel(
        IChatService chatService,
        ILogger<ConversationListViewModel> logger)
        : base(logger)
    {
        _chatService = chatService;

        _chatService.ConversationCreated += OnConversationCreated;
        _chatService.ConversationUpdated += OnConversationUpdated;
        _chatService.ConversationDeleted += OnConversationDeleted;
    }

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        await LoadConversationsAsync();
    }

    /// <summary>
    /// Loads all conversations.
    /// </summary>
    [RelayCommand]
    private async Task LoadConversationsAsync()
    {
        await ExecuteAsync(async () =>
        {
            var conversations = await _chatService.GetConversationsAsync();

            Conversations.Clear();
            foreach (var conv in conversations)
            {
                Conversations.Add(new ConversationItemViewModel(conv));
            }
        });
    }

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    [RelayCommand]
    private async Task CreateNewAsync()
    {
        await ExecuteAsync(async () =>
        {
            var conversation = await _chatService.CreateConversationAsync();
            var viewModel = new ConversationItemViewModel(conversation);

            Conversations.Insert(0, viewModel);
            SelectedConversation = viewModel;

            ConversationSelected?.Invoke(this, conversation.Id);
        });
    }

    /// <summary>
    /// Deletes a conversation.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync(ConversationItemViewModel item)
    {
        await ExecuteAsync(async () =>
        {
            await _chatService.DeleteConversationAsync(item.Id);
            Conversations.Remove(item);

            if (SelectedConversation == item)
            {
                SelectedConversation = Conversations.FirstOrDefault();
            }
        });
    }

    partial void OnSelectedConversationChanged(ConversationItemViewModel? value)
    {
        if (value is not null)
        {
            ConversationSelected?.Invoke(this, value.Id);
        }
    }

    private void OnConversationCreated(object? sender, ConversationEventArgs e)
    {
        var existing = Conversations.FirstOrDefault(c => c.Id == e.Conversation.Id);
        if (existing is null)
        {
            Conversations.Insert(0, new ConversationItemViewModel(e.Conversation));
        }
    }

    private void OnConversationUpdated(object? sender, ConversationEventArgs e)
    {
        var existing = Conversations.FirstOrDefault(c => c.Id == e.Conversation.Id);
        if (existing is not null)
        {
            existing.Update(e.Conversation);
        }
    }

    private void OnConversationDeleted(object? sender, ConversationEventArgs e)
    {
        var existing = Conversations.FirstOrDefault(c => c.Id == e.Conversation.Id);
        if (existing is not null)
        {
            Conversations.Remove(existing);
        }
    }
}

/// <summary>
/// ViewModel for a conversation list item.
/// </summary>
public partial class ConversationItemViewModel : ObservableObject
{
    /// <summary>
    /// The conversation ID.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The conversation title.
    /// </summary>
    [ObservableProperty]
    private string _title;

    /// <summary>
    /// When the conversation was last modified.
    /// </summary>
    [ObservableProperty]
    private DateTimeOffset _modifiedAt;

    /// <summary>
    /// The model used for this conversation.
    /// </summary>
    [ObservableProperty]
    private string? _model;

    /// <summary>
    /// Number of messages in the conversation.
    /// </summary>
    [ObservableProperty]
    private int _messageCount;

    /// <summary>
    /// Formatted modification time.
    /// </summary>
    public string ModifiedDisplay => FormatModifiedTime(ModifiedAt);

    public ConversationItemViewModel(Conversation conversation)
    {
        Id = conversation.Id;
        _title = conversation.Title;
        _modifiedAt = conversation.ModifiedAt;
        _model = conversation.Model;
        _messageCount = conversation.Messages.Count;
    }

    /// <summary>
    /// Updates the view model from a conversation.
    /// </summary>
    public void Update(Conversation conversation)
    {
        Title = conversation.Title;
        ModifiedAt = conversation.ModifiedAt;
        Model = conversation.Model;
        MessageCount = conversation.Messages.Count;
    }

    private static string FormatModifiedTime(DateTimeOffset time)
    {
        var now = DateTimeOffset.Now;
        var diff = now - time;

        return diff.TotalMinutes switch
        {
            < 1 => "Just now",
            < 60 => $"{(int)diff.TotalMinutes}m ago",
            < 1440 => $"{(int)diff.TotalHours}h ago",
            < 10080 => $"{(int)diff.TotalDays}d ago",
            _ => time.LocalDateTime.ToString("MMM d")
        };
    }

    partial void OnModifiedAtChanged(DateTimeOffset value)
    {
        OnPropertyChanged(nameof(ModifiedDisplay));
    }
}
