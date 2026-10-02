using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using InControl.ViewModels.Onboarding;

namespace InControl.App.Controls;

/// <summary>
/// Setup wizard for first-run onboarding experience.
/// </summary>
public sealed partial class SetupWizard : UserControl
{
    private readonly OnboardingViewModel _viewModel = new();

    public SetupWizard()
    {
        this.InitializeComponent();
        UpdateUI();
    }

    /// <summary>
    /// Event raised when onboarding is complete.
    /// </summary>
    public event EventHandler? OnboardingComplete;

    /// <summary>
    /// Event raised when onboarding is skipped.
    /// </summary>
    public event EventHandler? OnboardingSkipped;

    /// <summary>
    /// Gets the view model.
    /// </summary>
    public OnboardingViewModel ViewModel => _viewModel;

    /// <summary>
    /// Sets the backend connection status.
    /// </summary>
    public void SetBackendConnected(string endpoint)
    {
        _viewModel.DetectedBackend = endpoint;
        _viewModel.IsBackendConnected = true;
        UpdateBackendUI();
    }

    /// <summary>
    /// Sets available models for selection.
    /// </summary>
    public void SetAvailableModels(IEnumerable<string> models)
    {
        ModelSelector.Items.Clear();
        foreach (var model in models)
        {
            ModelSelector.Items.Add(model);
        }
        _viewModel.HasModelsAvailable = ModelSelector.Items.Count > 0;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoBack();
        UpdateUI();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.GoNext();

        if (_viewModel.IsComplete)
        {
            OnboardingComplete?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            UpdateUI();
        }
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Skip();
        OnboardingSkipped?.Invoke(this, EventArgs.Empty);
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is string model)
        {
            _viewModel.SelectedModel = model;
            ReadyModelText.Text = model;
            UpdateNavigationButtons();
        }
    }

    private void UpdateUI()
    {
        // Update header
        StepIndicator.Text = $"Step {_viewModel.StepNumber} of {_viewModel.TotalSteps}";
        StepTitle.Text = _viewModel.StepTitle;
        StepDescription.Text = _viewModel.StepDescription;

        // Update step visibility
        WelcomeContent.Visibility = _viewModel.ShowWelcome ? Visibility.Visible : Visibility.Collapsed;
        BackendCheckContent.Visibility = _viewModel.ShowBackendCheck ? Visibility.Visible : Visibility.Collapsed;
        ModelSelectionContent.Visibility = _viewModel.ShowModelSelection ? Visibility.Visible : Visibility.Collapsed;
        ReadyContent.Visibility = _viewModel.ShowReady ? Visibility.Visible : Visibility.Collapsed;

        UpdateNavigationButtons();
        UpdateBackendUI();
    }

    private void UpdateNavigationButtons()
    {
        // Back button
        BackButton.Visibility = _viewModel.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        SkipButton.Visibility = _viewModel.CanGoBack ? Visibility.Collapsed : Visibility.Visible;

        // Next button
        NextButton.IsEnabled = _viewModel.CanGoNext;
        NextButton.Content = _viewModel.CurrentStep switch
        {
            OnboardingStep.Welcome => "Get Started",
            OnboardingStep.BackendCheck => "Continue",
            OnboardingStep.ModelSelection => "Continue",
            OnboardingStep.Ready => "Start Using InControl",
            _ => "Next"
        };
    }

    private void UpdateBackendUI()
    {
        if (_viewModel.IsBackendConnected)
        {
            BackendSearchRing.Visibility = Visibility.Collapsed;
            BackendConnectedIcon.Visibility = Visibility.Visible;
            BackendStatusText.Text = _viewModel.BackendStatusText;
        }
        else
        {
            BackendSearchRing.Visibility = Visibility.Visible;
            BackendConnectedIcon.Visibility = Visibility.Collapsed;
            BackendStatusText.Text = "Searching for backend...";
        }
    }
}
