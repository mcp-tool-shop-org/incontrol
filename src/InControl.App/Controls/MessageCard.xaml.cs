using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using InControl.Core.Models;
using InControl.ViewModels;
using InControl.App.Services;

namespace InControl.App.Controls;

/// <summary>
/// Displays a single message card in the conversation.
/// Shows different layouts for user intents vs model output.
/// </summary>
public sealed partial class MessageCard : UserControl
{
    /// <summary>
    /// Raised when the user clicks the speak button on an assistant message.
    /// </summary>
    public event EventHandler<MessageViewModel>? SpeakRequested;

    /// <summary>
    /// Raised when the user clicks stop while speaking.
    /// </summary>
    public event EventHandler? StopSpeakRequested;

    /// <summary>
    /// Raised when the user confirms deletion of a message.
    /// </summary>
    public event EventHandler<MessageViewModel>? DeleteRequested;

    public MessageCard()
    {
        this.InitializeComponent();
        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        // User message context menu
        CopyMenuItem.Click += OnCopyClick;
        CopyAsMarkdownMenuItem.Click += OnCopyAsMarkdownClick;

        DeleteMenuItem.Click += OnDeleteClick;

        // Assistant message context menu (includes report option)
        AssistantCopyMenuItem.Click += OnCopyClick;
        AssistantCopyAsMarkdownMenuItem.Click += OnCopyAsMarkdownClick;
        ReportInappropriateMenuItem.Click += OnReportInappropriateClick;
        AssistantDeleteMenuItem.Click += OnDeleteClick;

        Loaded += OnCardLoaded;
        Unloaded += OnCardUnloaded;
    }

    /// <summary>
    /// The message this card is listening to. Null when it listens to none.
    /// </summary>
    private MessageViewModel? _subscribed;

    private void Subscribe(MessageViewModel message)
    {
        Unsubscribe();
        message.PropertyChanged += OnMessagePropertyChanged;
        _subscribed = message;
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged -= OnMessagePropertyChanged;
            _subscribed = null;
        }
    }

    private void OnCardLoaded(object sender, RoutedEventArgs e)
    {
        // A card that was unloaded stopped listening. Show what the message holds now.
        if (_subscribed is null && Message is { IsAssistant: true })
        {
            UpdateDisplay();
        }
    }

    private void OnCardUnloaded(object sender, RoutedEventArgs e)
    {
        Unsubscribe();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (Message?.Content == null)
        {
            return;
        }

        if (TryCopyText(Message.Content))
        {
            CopyFeedback.ShowCopied();
        }
        else
        {
            CopyFeedback.ShowError("Copy failed");
        }
    }

    private void OnCopyAsMarkdownClick(object sender, RoutedEventArgs e)
    {
        if (Message?.Content == null)
        {
            return;
        }

        // Content may already be markdown. Copy the text that is on the card.
        if (TryCopyText(Message.Content))
        {
            CopyFeedback.ShowSuccess("Copied as Markdown");
        }
        else
        {
            CopyFeedback.ShowError("Copy failed");
        }
    }

    /// <summary>
    /// A busy clipboard throws out of SetContent. That exception is unhandled on the UI
    /// thread and closes the window, so a failed copy has to stay on this click.
    /// </summary>
    private static bool TryCopyText(string text)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async void OnReportInappropriateClick(object sender, RoutedEventArgs e)
    {
        if (Message == null) return;

        var reportDialog = new ContentDialog
        {
            Title = "Report Inappropriate Content",
            XamlRoot = this.XamlRoot,
            PrimaryButtonText = "Submit Report",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        // Create report form
        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(new TextBlock
        {
            Text = "Help us improve by reporting AI-generated content that is inappropriate, harmful, or incorrect.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8
        });

        var reasonCombo = new ComboBox
        {
            Header = "Reason for report",
            PlaceholderText = "Select a reason",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[]
            {
                "Harmful or unsafe content",
                "Factually incorrect information",
                "Biased or discriminatory content",
                "Inappropriate language",
                "Privacy concern",
                "Other"
            }
        };
        panel.Children.Add(reasonCombo);

        var detailsBox = new TextBox
        {
            Header = "Additional details (optional)",
            PlaceholderText = "Provide any additional context...",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            MaxHeight = 150
        };
        panel.Children.Add(detailsBox);

        reportDialog.Content = panel;

        var result = await reportDialog.ShowAsync();

        if (result == ContentDialogResult.Primary && reasonCombo.SelectedItem != null)
        {
            // Save the report
            var report = new ContentReport
            {
                MessageId = Message.Id,
                MessageContent = Message.Content,
                Model = Message.Model ?? "Unknown",
                Reason = reasonCombo.SelectedItem.ToString()!,
                Details = detailsBox.Text,
                ReportedAt = DateTimeOffset.Now
            };

            if (ContentReportService.Instance.SaveReport(report))
            {
                CopyFeedback.ShowSuccess("Report submitted");
            }
            else
            {
                CopyFeedback.ShowError("Report was not saved");
            }
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (Message is null) return;

        var preview = Message.Content.Length > 80
            ? Message.Content[..80] + "..."
            : Message.Content;

        var dialog = new ContentDialog
        {
            Title = "Delete Message",
            Content = $"Are you sure you want to delete this message?\n\n\"{preview}\"",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            DeleteRequested?.Invoke(this, Message);
        }
    }

    /// <summary>
    /// The message view model to display.
    /// </summary>
    public MessageViewModel? Message
    {
        get => (MessageViewModel?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(MessageViewModel),
            typeof(MessageCard),
            new PropertyMetadata(null, OnMessageChanged));

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MessageCard card)
        {
            card.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        Unsubscribe();

        var message = Message;
        if (message == null)
        {
            HideAllCards();
            return;
        }

        switch (message.Role)
        {
            case MessageRole.User:
                ShowUserIntent(message);
                break;
            case MessageRole.Assistant:
                ShowModelOutput(message);
                break;
            case MessageRole.System:
                ShowSystemMessage(message);
                break;
            default:
                HideAllCards();
                break;
        }
    }

    private void ShowUserIntent(MessageViewModel message)
    {
        HideAllCards();
        UserIntentCard.Visibility = Visibility.Visible;
        UserContent.Text = message.Content;
        UserTimestamp.Text = message.TimestampDisplay;
    }

    private void ShowModelOutput(MessageViewModel message)
    {
        HideAllCards();
        ModelOutputCard.Visibility = Visibility.Visible;
        ModelContent.Text = message.Content;
        ModelTimestamp.Text = message.TimestampDisplay;

        // Show model name if available
        if (!string.IsNullOrEmpty(message.Model))
        {
            ModelName.Text = message.Model;
        }
        else
        {
            ModelName.Text = "Model output";
        }

        // Show streaming indicator or footer
        if (message.IsStreaming)
        {
            StreamingIndicator.Visibility = Visibility.Visible;
            ModelFooter.Visibility = Visibility.Collapsed;
        }
        else
        {
            StreamingIndicator.Visibility = Visibility.Collapsed;
            ModelFooter.Visibility = Visibility.Visible;

            // Show token count if available
            if (message.Message.TokenCount.HasValue)
            {
                TokenCountText.Text = $"{message.Message.TokenCount.Value} tokens";
            }

            // Show speak button if voice is available
            UpdateSpeakButton(message);
        }

        // Subscribe to property changes for streaming updates
        Subscribe(message);
    }

    private void ShowSystemMessage(MessageViewModel message)
    {
        HideAllCards();
        SystemMessageCard.Visibility = Visibility.Visible;
        SystemContent.Text = message.Content;
    }

    private void HideAllCards()
    {
        UserIntentCard.Visibility = Visibility.Collapsed;
        ModelOutputCard.Visibility = Visibility.Collapsed;
        SystemMessageCard.Visibility = Visibility.Collapsed;
    }

    private void OnMessagePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not MessageViewModel message || !ReferenceEquals(message, _subscribed)) return;

        DispatcherQueue.TryEnqueue(() =>
        {
            // The card may have moved on to another message before this ran.
            if (!ReferenceEquals(message, _subscribed) || !ReferenceEquals(message, Message))
                return;

            switch (e.PropertyName)
            {
                case nameof(MessageViewModel.Content):
                    if (message.IsAssistant)
                    {
                        ModelContent.Text = message.Content;
                    }
                    break;

                case nameof(MessageViewModel.IsStreaming):
                    if (!message.IsStreaming)
                    {
                        StreamingIndicator.Visibility = Visibility.Collapsed;
                        ModelFooter.Visibility = Visibility.Visible;
                        UpdateSpeakButton(message);
                    }
                    break;

                case nameof(MessageViewModel.IsSpeaking):
                case nameof(MessageViewModel.CanSpeak):
                    UpdateSpeakButton(message);
                    break;
            }
        });
    }

    private void UpdateSpeakButton(MessageViewModel message)
    {
        if (message.CanSpeak)
        {
            SpeakButton.Visibility = Visibility.Visible;
            if (message.IsSpeaking)
            {
                SpeakIcon.Glyph = "\uE71A"; // Stop icon
                ToolTipService.SetToolTip(SpeakButton, "Stop speaking");
            }
            else
            {
                SpeakIcon.Glyph = "\uE767"; // Volume icon
                ToolTipService.SetToolTip(SpeakButton, "Read aloud");
            }
        }
        else
        {
            SpeakButton.Visibility = Visibility.Collapsed;
        }
    }

    private void OnSpeakClicked(object sender, RoutedEventArgs e)
    {
        if (Message is null) return;

        if (Message.IsSpeaking)
        {
            StopSpeakRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            SpeakRequested?.Invoke(this, Message);
        }
    }
}
