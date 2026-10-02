using System.Collections.ObjectModel;
using System.ComponentModel;
using InControl.Core.Models;
using InControl.Core.UX;

namespace InControl.ViewModels.ConversationView;

/// <summary>
/// ViewModel for the conversation view (interaction surface).
/// Manages the message list and conversation state.
/// </summary>
public sealed class ConversationViewModel : INotifyPropertyChanged
{
    private Core.Models.Conversation? _conversation;
    private ExecutionState _executionState = ExecutionState.Idle;
    private MessageViewModel? _streamingMessage;
    private TimeSpan _elapsedTime;
    private string? _currentModel;
    private ConversationViewState _viewState = ConversationViewState.Welcome;

    public ConversationViewModel()
    {
        Messages = new ObservableCollection<MessageViewModel>();
    }

    /// <summary>
    /// The messages in this conversation.
    /// </summary>
    public ObservableCollection<MessageViewModel> Messages { get; }

    /// <summary>
    /// The current view state.
    /// </summary>
    public ConversationViewState ViewState
    {
        get => _viewState;
        private set
        {
            if (_viewState != value)
            {
                _viewState = value;
                OnPropertyChanged(nameof(ViewState));
                OnPropertyChanged(nameof(ShowWelcome));
                OnPropertyChanged(nameof(ShowEmptySession));
                OnPropertyChanged(nameof(ShowMessages));
            }
        }
    }

    /// <summary>
    /// Whether to show the welcome state.
    /// </summary>
    public bool ShowWelcome => _viewState == ConversationViewState.Welcome;

    /// <summary>
    /// Whether to show the empty session state.
    /// </summary>
    public bool ShowEmptySession => _viewState == ConversationViewState.EmptySession;

    /// <summary>
    /// Whether to show the message list.
    /// </summary>
    public bool ShowMessages => _viewState == ConversationViewState.Messages;

    /// <summary>
    /// The current execution state.
    /// </summary>
    public ExecutionState ExecutionState
    {
        get => _executionState;
        set
        {
            if (_executionState != value)
            {
                _executionState = value;
                OnPropertyChanged(nameof(ExecutionState));
                OnPropertyChanged(nameof(ExecutionStateText));
                OnPropertyChanged(nameof(IsExecuting));
                OnPropertyChanged(nameof(ShowExecutionIndicator));
            }
        }
    }

    /// <summary>
    /// Display text for execution state.
    /// </summary>
    public string ExecutionStateText => _executionState.ToDisplayText();

    /// <summary>
    /// Whether execution is in progress.
    /// </summary>
    public bool IsExecuting => _executionState.IsExecuting();

    /// <summary>
    /// Whether to show the execution indicator.
    /// </summary>
    public bool ShowExecutionIndicator => IsExecuting;

    /// <summary>
    /// The elapsed time for the current run.
    /// </summary>
    public TimeSpan ElapsedTime
    {
        get => _elapsedTime;
        set
        {
            if (_elapsedTime != value)
            {
                _elapsedTime = value;
                OnPropertyChanged(nameof(ElapsedTime));
                OnPropertyChanged(nameof(ElapsedTimeText));
            }
        }
    }

    /// <summary>
    /// Formatted elapsed time text.
    /// </summary>
    public string ElapsedTimeText
    {
        get
        {
            if (_elapsedTime.TotalSeconds < 1)
                return "< 1s";
            if (_elapsedTime.TotalMinutes < 1)
                return $"{_elapsedTime.Seconds}s";
            return $"{(int)_elapsedTime.TotalMinutes}m {_elapsedTime.Seconds}s";
        }
    }

    /// <summary>
    /// The current model being used.
    /// </summary>
    public string? CurrentModel
    {
        get => _currentModel;
        set
        {
            if (_currentModel != value)
            {
                _currentModel = value;
                OnPropertyChanged(nameof(CurrentModel));
            }
        }
    }

    /// <summary>
    /// Whether there are any messages.
    /// </summary>
    public bool HasMessages => Messages.Count > 0;

    /// <summary>
    /// The session title (from underlying conversation).
    /// </summary>
    public string SessionTitle => _conversation?.Title ?? UXStrings.Session.EmptyTitle;

    /// <summary>
    /// Loads a conversation into the view.
    /// </summary>
    public void LoadConversation(Core.Models.Conversation conversation)
    {
        _conversation = conversation;
        Messages.Clear();

        foreach (var message in conversation.Messages)
        {
            Messages.Add(new MessageViewModel(message));
        }

        UpdateViewState();
        OnPropertyChanged(nameof(SessionTitle));
        OnPropertyChanged(nameof(HasMessages));
    }

    /// <summary>
    /// Clears the current conversation.
    /// </summary>
    public void ClearConversation()
    {
        _conversation = null;
        Messages.Clear();
        ViewState = ConversationViewState.Welcome;
        ExecutionState = ExecutionState.Idle;
        ElapsedTime = TimeSpan.Zero;

        OnPropertyChanged(nameof(SessionTitle));
        OnPropertyChanged(nameof(HasMessages));
    }

    /// <summary>
    /// Adds a user intent to the conversation.
    /// </summary>
    public void AddUserIntent(string content)
    {
        var message = Message.User(content);
        Messages.Add(new MessageViewModel(message));
        UpdateViewState();
        OnPropertyChanged(nameof(HasMessages));
    }

    /// <summary>
    /// Begins streaming model output.
    /// </summary>
    public void BeginModelOutput(string? model = null)
    {
        var placeholderMessage = Message.Assistant(string.Empty, model);
        _streamingMessage = new MessageViewModel(placeholderMessage) { IsStreaming = true };
        Messages.Add(_streamingMessage);
        UpdateViewState();
    }

    /// <summary>
    /// Appends content to the streaming output.
    /// </summary>
    public void AppendToModelOutput(string chunk)
    {
        _streamingMessage?.AppendContent(chunk);
    }

    /// <summary>
    /// Completes the streaming model output.
    /// </summary>
    public void CompleteModelOutput(Message finalMessage)
    {
        if (_streamingMessage != null)
        {
            var index = Messages.IndexOf(_streamingMessage);
            if (index >= 0)
            {
                Messages[index] = new MessageViewModel(finalMessage);
            }
            _streamingMessage = null;
        }

        OnPropertyChanged(nameof(HasMessages));
    }

    /// <summary>
    /// Finalizes the current streaming message in place (sets IsStreaming=false).
    /// Use this when you've already appended all tokens and just need to stop the spinner.
    /// </summary>
    public void FinalizeModelOutput()
    {
        if (_streamingMessage != null)
        {
            _streamingMessage.FinalizeMessage();
            _streamingMessage = null;
        }

        OnPropertyChanged(nameof(HasMessages));
    }

    /// <summary>
    /// Cancels the current streaming output.
    /// </summary>
    public void CancelModelOutput()
    {
        if (_streamingMessage != null)
        {
            Messages.Remove(_streamingMessage);
            _streamingMessage = null;
        }
    }

    /// <summary>
    /// Removes a message from the UI collection by its ID.
    /// </summary>
    public void RemoveMessage(Guid messageId)
    {
        var toRemove = Messages.FirstOrDefault(m => m.Id == messageId);
        if (toRemove != null)
        {
            Messages.Remove(toRemove);
            UpdateViewState();
            OnPropertyChanged(nameof(HasMessages));
        }
    }

    /// <summary>
    /// Gets the underlying conversation.
    /// </summary>
    public Core.Models.Conversation? GetConversation() => _conversation;

    private void UpdateViewState()
    {
        if (_conversation == null)
        {
            ViewState = ConversationViewState.Welcome;
        }
        else if (Messages.Count == 0)
        {
            ViewState = ConversationViewState.EmptySession;
        }
        else
        {
            ViewState = ConversationViewState.Messages;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// The visual state of the conversation view.
/// </summary>
public enum ConversationViewState
{
    /// <summary>
    /// No session is active. Show welcome message.
    /// </summary>
    Welcome,

    /// <summary>
    /// A session is active but has no messages.
    /// </summary>
    EmptySession,

    /// <summary>
    /// A session is active with messages.
    /// </summary>
    Messages
}
