using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using InControl.ViewModels;
using InControl.ViewModels.ConversationView;

namespace InControl.App.Controls;

/// <summary>
/// Main conversation view displaying messages and input composer.
/// This is the "interaction surface" per UX contract terminology.
/// </summary>
public sealed partial class ConversationView : UserControl
{
    private ConversationViewModel? _viewModel;

    /// <summary>
    /// Raised when the user clicks the speak button on a message.
    /// </summary>
    public event EventHandler<MessageViewModel>? SpeakRequested;

    /// <summary>
    /// Raised when the user clicks stop speaking on a message.
    /// </summary>
    public event EventHandler? StopSpeakRequested;

    /// <summary>
    /// Raised when the user confirms deletion of a message.
    /// </summary>
    public event EventHandler<MessageViewModel>? MessageDeleteRequested;

    public ConversationView()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// The conversation view model.
    /// </summary>
    public ConversationViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = value;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                MessageList.ItemsSource = _viewModel.Messages;
                UpdateViewState();
            }
        }
    }

    /// <summary>
    /// Sets the available models for the input composer's model selector.
    /// Filters out embedding-only models that cannot be used for chat.
    /// </summary>
    public void SetAvailableModels(IEnumerable<string> models)
    {
        InputComposer.SetAvailableModels(models);
    }

    /// <summary>
    /// Gets the input composer control for event wiring.
    /// </summary>
    public InputComposer Composer => InputComposer;

    /// <summary>
    /// Shows the welcome state.
    /// </summary>
    public void ShowWelcome()
    {
        WelcomeState.Visibility = Visibility.Visible;
        EmptySessionState.Visibility = Visibility.Collapsed;
        MessageScrollViewer.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Shows the empty session state.
    /// </summary>
    public void ShowEmptySession()
    {
        WelcomeState.Visibility = Visibility.Collapsed;
        EmptySessionState.Visibility = Visibility.Visible;
        MessageScrollViewer.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Shows the message list.
    /// </summary>
    public void ShowMessages()
    {
        WelcomeState.Visibility = Visibility.Collapsed;
        EmptySessionState.Visibility = Visibility.Collapsed;
        MessageScrollViewer.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Shows the execution status bar.
    /// </summary>
    public void ShowExecutionStatus(string statusText, string elapsedTime)
    {
        ExecutionStatusBar.Visibility = Visibility.Visible;
        ExecutionStatusText.Text = statusText;
        ElapsedTimeText.Text = elapsedTime;
    }

    /// <summary>
    /// Hides the execution status bar.
    /// </summary>
    public void HideExecutionStatus()
    {
        ExecutionStatusBar.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Scrolls to the bottom of the message list.
    /// </summary>
    public void ScrollToBottom()
    {
        MessageScrollViewer.ChangeView(null, MessageScrollViewer.ScrollableHeight, null);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConversationViewModel.ViewState))
        {
            DispatcherQueue.TryEnqueue(UpdateViewState);
        }
        else if (e.PropertyName == nameof(ConversationViewModel.ExecutionState))
        {
            DispatcherQueue.TryEnqueue(UpdateExecutionStatus);
        }
        else if (e.PropertyName == nameof(ConversationViewModel.ElapsedTime))
        {
            DispatcherQueue.TryEnqueue(UpdateElapsedTime);
        }
    }

    private void UpdateViewState()
    {
        if (_viewModel == null) return;

        switch (_viewModel.ViewState)
        {
            case ConversationViewState.Welcome:
                ShowWelcome();
                break;
            case ConversationViewState.EmptySession:
                ShowEmptySession();
                break;
            case ConversationViewState.Messages:
                ShowMessages();
                break;
        }
    }

    private void UpdateExecutionStatus()
    {
        if (_viewModel == null) return;

        if (_viewModel.IsExecuting)
        {
            ShowExecutionStatus(_viewModel.ExecutionStateText, _viewModel.ElapsedTimeText);
        }
        else
        {
            HideExecutionStatus();
        }
    }

    private void UpdateElapsedTime()
    {
        if (_viewModel != null && _viewModel.IsExecuting)
        {
            ElapsedTimeText.Text = _viewModel.ElapsedTimeText;
        }
    }

    private void OnMessageCardLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MessageCard card)
        {
            card.SpeakRequested += (s, msg) => SpeakRequested?.Invoke(this, msg);
            card.StopSpeakRequested += (s, _) => StopSpeakRequested?.Invoke(this, EventArgs.Empty);
            card.DeleteRequested += (s, msg) => MessageDeleteRequested?.Invoke(this, msg);
        }
    }
}
