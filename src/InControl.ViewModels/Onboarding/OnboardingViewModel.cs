using System.ComponentModel;

namespace InControl.ViewModels.Onboarding;

/// <summary>
/// ViewModel for first-run onboarding experience.
/// Guides users through initial setup without overwhelming them.
/// </summary>
public sealed class OnboardingViewModel : INotifyPropertyChanged
{
    private OnboardingStep _currentStep = OnboardingStep.Welcome;
    private bool _isComplete;
    private string _detectedBackend = string.Empty;
    private bool _isBackendConnected;
    private string _selectedModel = string.Empty;
    private bool _hasModelsAvailable;

    /// <summary>
    /// The current onboarding step.
    /// </summary>
    public OnboardingStep CurrentStep
    {
        get => _currentStep;
        set
        {
            if (_currentStep != value)
            {
                _currentStep = value;
                OnPropertyChanged(nameof(CurrentStep));
                OnPropertyChanged(nameof(StepNumber));
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(StepDescription));
                OnPropertyChanged(nameof(ShowWelcome));
                OnPropertyChanged(nameof(ShowBackendCheck));
                OnPropertyChanged(nameof(ShowModelSelection));
                OnPropertyChanged(nameof(ShowReady));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    /// <summary>
    /// Current step number (1-based).
    /// </summary>
    public int StepNumber => (int)_currentStep + 1;

    /// <summary>
    /// Total number of steps.
    /// </summary>
    public int TotalSteps => 4;

    /// <summary>
    /// Title for the current step.
    /// </summary>
    public string StepTitle => _currentStep switch
    {
        OnboardingStep.Welcome => "Welcome to InControl",
        OnboardingStep.BackendCheck => "Connect to Backend",
        OnboardingStep.ModelSelection => "Select a Model",
        OnboardingStep.Ready => "Ready to Go",
        _ => string.Empty
    };

    /// <summary>
    /// Description for the current step.
    /// </summary>
    public string StepDescription => _currentStep switch
    {
        OnboardingStep.Welcome => "A local AI workstation for your RTX GPU. Everything runs on your machine.",
        OnboardingStep.BackendCheck => "InControl connects to a local inference backend to run models.",
        OnboardingStep.ModelSelection => "Choose a model to get started. You can change this anytime.",
        OnboardingStep.Ready => "You're all set. Start a conversation to begin.",
        _ => string.Empty
    };

    /// <summary>
    /// Whether to show the welcome step.
    /// </summary>
    public bool ShowWelcome => _currentStep == OnboardingStep.Welcome;

    /// <summary>
    /// Whether to show the backend check step.
    /// </summary>
    public bool ShowBackendCheck => _currentStep == OnboardingStep.BackendCheck;

    /// <summary>
    /// Whether to show the model selection step.
    /// </summary>
    public bool ShowModelSelection => _currentStep == OnboardingStep.ModelSelection;

    /// <summary>
    /// Whether to show the ready step.
    /// </summary>
    public bool ShowReady => _currentStep == OnboardingStep.Ready;

    /// <summary>
    /// Whether onboarding is complete.
    /// </summary>
    public bool IsComplete
    {
        get => _isComplete;
        private set
        {
            if (_isComplete != value)
            {
                _isComplete = value;
                OnPropertyChanged(nameof(IsComplete));
            }
        }
    }

    /// <summary>
    /// The detected backend endpoint.
    /// </summary>
    public string DetectedBackend
    {
        get => _detectedBackend;
        set
        {
            if (_detectedBackend != value)
            {
                _detectedBackend = value;
                OnPropertyChanged(nameof(DetectedBackend));
                OnPropertyChanged(nameof(HasDetectedBackend));
            }
        }
    }

    /// <summary>
    /// Whether a backend has been detected.
    /// </summary>
    public bool HasDetectedBackend => !string.IsNullOrEmpty(_detectedBackend);

    /// <summary>
    /// Whether the backend is connected.
    /// </summary>
    public bool IsBackendConnected
    {
        get => _isBackendConnected;
        set
        {
            if (_isBackendConnected != value)
            {
                _isBackendConnected = value;
                OnPropertyChanged(nameof(IsBackendConnected));
                OnPropertyChanged(nameof(BackendStatusText));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    /// <summary>
    /// Status text for the backend connection.
    /// </summary>
    public string BackendStatusText => _isBackendConnected
        ? $"Connected to {_detectedBackend}"
        : "Searching for backend...";

    /// <summary>
    /// The selected model name.
    /// </summary>
    public string SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (_selectedModel != value)
            {
                _selectedModel = value;
                OnPropertyChanged(nameof(SelectedModel));
                OnPropertyChanged(nameof(HasSelectedModel));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    /// <summary>
    /// Whether a model has been selected.
    /// </summary>
    public bool HasSelectedModel => !string.IsNullOrEmpty(_selectedModel);

    /// <summary>
    /// Whether models are available to select.
    /// </summary>
    public bool HasModelsAvailable
    {
        get => _hasModelsAvailable;
        set
        {
            if (_hasModelsAvailable != value)
            {
                _hasModelsAvailable = value;
                OnPropertyChanged(nameof(HasModelsAvailable));
            }
        }
    }

    /// <summary>
    /// Whether the user can go back to the previous step.
    /// </summary>
    public bool CanGoBack => _currentStep > OnboardingStep.Welcome;

    /// <summary>
    /// Whether the user can proceed to the next step.
    /// </summary>
    public bool CanGoNext => _currentStep switch
    {
        OnboardingStep.Welcome => true,
        OnboardingStep.BackendCheck => _isBackendConnected,
        OnboardingStep.ModelSelection => HasSelectedModel,
        OnboardingStep.Ready => true,
        _ => false
    };

    /// <summary>
    /// Advances to the next step.
    /// </summary>
    public void GoNext()
    {
        if (!CanGoNext) return;

        if (_currentStep == OnboardingStep.Ready)
        {
            Complete();
            return;
        }

        CurrentStep = _currentStep + 1;
    }

    /// <summary>
    /// Returns to the previous step.
    /// </summary>
    public void GoBack()
    {
        if (!CanGoBack) return;
        CurrentStep = _currentStep - 1;
    }

    /// <summary>
    /// Completes the onboarding process.
    /// </summary>
    public void Complete()
    {
        IsComplete = true;
    }

    /// <summary>
    /// Skips onboarding entirely.
    /// </summary>
    public void Skip()
    {
        IsComplete = true;
    }

    /// <summary>
    /// Resets onboarding to the beginning.
    /// </summary>
    public void Reset()
    {
        CurrentStep = OnboardingStep.Welcome;
        IsComplete = false;
        DetectedBackend = string.Empty;
        IsBackendConnected = false;
        SelectedModel = string.Empty;
        HasModelsAvailable = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// The steps in the onboarding process.
/// </summary>
public enum OnboardingStep
{
    /// <summary>
    /// Welcome introduction.
    /// </summary>
    Welcome,

    /// <summary>
    /// Backend connection check.
    /// </summary>
    BackendCheck,

    /// <summary>
    /// Model selection.
    /// </summary>
    ModelSelection,

    /// <summary>
    /// Ready to use.
    /// </summary>
    Ready
}
