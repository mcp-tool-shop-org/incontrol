using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using OllamaSharp;

namespace InControl.App.Pages;

/// <summary>
/// Model Manager page - first-class screen for managing AI models via Ollama.
/// Allows users to view, pull, and manage Ollama models.
/// </summary>
public sealed partial class ModelManagerPage : UserControl
{
    private readonly ObservableCollection<OllamaModelInfo> _models = new();
    private OllamaApiClient? _ollamaClient;
    private bool _isConnected;

    public ModelManagerPage()
    {
        this.InitializeComponent();
        SetupEventHandlers();
        _ = InitializeOllamaAsync();
    }

    /// <summary>
    /// Event raised when user wants to go back.
    /// </summary>
    public event EventHandler? BackRequested;

    /// <summary>
    /// Event raised when a model is selected as default.
    /// </summary>
    public event EventHandler<string>? ModelSelected;

    private void SetupEventHandlers()
    {
        BackButton.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        RefreshOllamaButton.Click += async (s, e) => await RefreshModelsAsync();
        PullModelButton.Click += OnPullModelClick;
        EmptyPullButton.Click += OnPullModelClick;
        EmptyRefreshButton.Click += async (s, e) => await RefreshModelsAsync();
        InstallOllamaButton.Click += (s, e) => OpenUrl("https://ollama.com/download");
        DefaultModelSelector.SelectionChanged += OnDefaultModelChanged;
        ModelsListView.ItemsSource = _models;

        // Quick pull buttons
        PullLlama32Button.Click += async (s, e) => await PullModelAsync("llama3.2");
        PullMistralButton.Click += async (s, e) => await PullModelAsync("mistral");
        PullCodegemmaButton.Click += async (s, e) => await PullModelAsync("codegemma");

        // External links
        OpenOllamaDocsButton.Click += (s, e) => OpenUrl("https://ollama.com/docs");
        OpenOllamaLibraryButton.Click += (s, e) => OpenUrl("https://ollama.com/library");
    }

    private async Task InitializeOllamaAsync()
    {
        try
        {
            _ollamaClient = new OllamaApiClient("http://localhost:11434");

            // Check connection by getting version
            var version = await _ollamaClient.GetVersionAsync();
            _isConnected = true;

            OllamaStatusIndicator.Fill = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
            OllamaStatusText.Text = "Connected";
            OllamaVersionText.Text = version?.ToString() ?? "Unknown";

            await RefreshModelsAsync();
        }
        catch (Exception)
        {
            _isConnected = false;
            SetDisconnectedState();
        }
    }

    private void SetDisconnectedState()
    {
        OllamaStatusIndicator.Fill = new SolidColorBrush(Microsoft.UI.Colors.Orange);
        OllamaStatusText.Text = "Not running";
        OllamaVersionText.Text = "--";

        EmptyStateTitle.Text = "Ollama Required";
        EmptyStateDescription.Text = "InControl uses Ollama to run AI models locally on your computer. Ollama is free and runs in the background.";

        // Show setup instructions and Install button, hide Pull Model button
        SetupInstructionsPanel.Visibility = Visibility.Visible;
        InstallOllamaButton.Visibility = Visibility.Visible;
        EmptyPullButton.Visibility = Visibility.Collapsed;

        EmptyState.Visibility = Visibility.Visible;
        ModelsListView.Visibility = Visibility.Collapsed;
    }

    private async Task RefreshModelsAsync()
    {
        if (_ollamaClient == null) return;

        try
        {
            LoadingOverlay.Show("Loading models...", "Fetching from Ollama");

            var models = await _ollamaClient.ListLocalModelsAsync();

            _models.Clear();
            foreach (var model in models)
            {
                _models.Add(new OllamaModelInfo
                {
                    Name = model.Name,
                    Size = FormatSize(model.Size),
                    ModifiedAt = model.ModifiedAt.ToString("MMM d, yyyy"),
                    Family = model.Details?.Family ?? "",
                    ParameterSize = model.Details?.ParameterSize ?? "",
                    Quantization = model.Details?.QuantizationLevel ?? ""
                });
            }

            UpdateModelCount();
            UpdateEmptyState();
            UpdateDefaultSelector();

            LoadingOverlay.Hide();
            OperationFeedback.ShowSuccess($"Found {_models.Count} models");
        }
        catch (Exception ex)
        {
            LoadingOverlay.Hide();
            OperationFeedback.ShowError($"Failed to load models: {ex.Message}");
            SetDisconnectedState();
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private void UpdateModelCount()
    {
        var count = _models.Count;
        ModelCountText.Text = count == 1 ? "1 model" : $"{count} models";
    }

    private void UpdateEmptyState()
    {
        var hasModels = _models.Count > 0;
        EmptyState.Visibility = hasModels ? Visibility.Collapsed : Visibility.Visible;
        ModelsListView.Visibility = hasModels ? Visibility.Visible : Visibility.Collapsed;

        if (!hasModels && _isConnected)
        {
            EmptyStateTitle.Text = "No models installed";
            EmptyStateDescription.Text = "Pull a model from the Ollama library to get started.";

            // Hide setup instructions, show Pull Model button (Ollama is connected)
            SetupInstructionsPanel.Visibility = Visibility.Collapsed;
            InstallOllamaButton.Visibility = Visibility.Collapsed;
            EmptyPullButton.Visibility = Visibility.Visible;
        }
    }

    private void UpdateDefaultSelector()
    {
        DefaultModelSelector.Items.Clear();
        foreach (var model in _models)
        {
            DefaultModelSelector.Items.Add(model.Name);
        }

        if (DefaultModelSelector.Items.Count > 0)
        {
            DefaultModelSelector.SelectedIndex = 0;
        }
    }

    private async void OnPullModelClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Pull Model",
            Content = CreatePullModelContent(),
            PrimaryButtonText = "Pull",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (dialog.Content is StackPanel panel &&
                panel.Children.OfType<TextBox>().FirstOrDefault() is TextBox modelNameBox &&
                !string.IsNullOrWhiteSpace(modelNameBox.Text))
            {
                await PullModelAsync(modelNameBox.Text.Trim());
            }
        }
    }

    private UIElement CreatePullModelContent()
    {
        var panel = new StackPanel { Spacing = 16 };

        panel.Children.Add(new TextBlock
        {
            Text = "Enter the model name to pull from the Ollama library.",
            TextWrapping = TextWrapping.Wrap
        });

        var modelNameBox = new TextBox
        {
            PlaceholderText = "e.g., llama3.2, mistral, codellama",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(modelNameBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Browse available models at ollama.com/library",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });

        return panel;
    }

    private async Task PullModelAsync(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;

        // Check if Ollama is connected before attempting to pull
        if (!_isConnected || _ollamaClient == null)
        {
            await ShowOllamaNotInstalledDialogAsync();
            return;
        }

        try
        {
            LoadingOverlay.Show($"Pulling {modelName}...", "This may take a while for large models");

            await foreach (var status in _ollamaClient.PullModelAsync(modelName))
            {
                if (status != null)
                {
                    var progressText = status.Completed > 0 && status.Total > 0
                        ? $"{status.Completed * 100 / status.Total}%"
                        : status.Status ?? "Downloading...";
                    LoadingOverlay.Show($"Pulling {modelName}...", progressText);
                }
            }

            LoadingOverlay.Hide();
            OperationFeedback.ShowSuccess($"Successfully pulled {modelName}");

            // Refresh model list
            await RefreshModelsAsync();
        }
        catch (HttpRequestException)
        {
            // Connection error - Ollama is not running
            LoadingOverlay.Hide();
            _isConnected = false;
            SetDisconnectedState();
            await ShowOllamaNotInstalledDialogAsync();
        }
        catch (Exception ex)
        {
            LoadingOverlay.Hide();
            // Check if this is a connection-related error
            if (ex.InnerException is System.Net.Sockets.SocketException ||
                ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("refused", StringComparison.OrdinalIgnoreCase))
            {
                _isConnected = false;
                SetDisconnectedState();
                await ShowOllamaNotInstalledDialogAsync();
            }
            else
            {
                OperationFeedback.ShowError($"Failed to pull {modelName}: {ex.Message}");
            }
        }
    }

    private async Task ShowOllamaNotInstalledDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Ollama Required",
            XamlRoot = this.XamlRoot,
            PrimaryButtonText = "Download Ollama",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary
        };

        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(new TextBlock
        {
            Text = "InControl requires Ollama to run AI models locally on your computer.",
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Ollama is a free, open-source tool that manages local AI models. Once installed, InControl will automatically connect to it.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });

        var stepsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
        stepsPanel.Children.Add(new TextBlock
        {
            Text = "Setup steps:",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        stepsPanel.Children.Add(new TextBlock { Text = "1. Download and install Ollama from ollama.com" });
        stepsPanel.Children.Add(new TextBlock { Text = "2. Run Ollama (it runs in the background)" });
        stepsPanel.Children.Add(new TextBlock { Text = "3. Return to InControl and click Refresh" });
        panel.Children.Add(stepsPanel);

        dialog.Content = panel;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            OpenUrl("https://ollama.com/download");
        }
    }

    private void OnDefaultModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DefaultModelSelector.SelectedItem is string modelName)
        {
            ModelSelected?.Invoke(this, modelName);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
}

/// <summary>
/// Represents Ollama model information for the UI.
/// </summary>
public class OllamaModelInfo
{
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string ModifiedAt { get; set; } = "";
    public string Family { get; set; } = "";
    public string ParameterSize { get; set; } = "";
    public string Quantization { get; set; } = "";

    public string Status => "Ready";
    public string Parameters => string.IsNullOrEmpty(ParameterSize) ? Family : $"{ParameterSize} • {Family}";
}
