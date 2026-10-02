using System.Collections.ObjectModel;
using System.ComponentModel;
using InControl.Core.UX;

namespace InControl.ViewModels.Input;

/// <summary>
/// ViewModel for the input composer control panel.
/// Manages intent input, model selection, and execution state.
/// </summary>
public sealed class InputComposerViewModel : INotifyPropertyChanged
{
    private string _intentText = string.Empty;
    private string? _selectedModel;
    private ExecutionState _executionState = ExecutionState.Idle;
    private int _contextItemCount;
    private TimeSpan _elapsedTime;

    public InputComposerViewModel()
    {
        AvailableModels = new ObservableCollection<string>();
        ContextItems = new ObservableCollection<ContextItemViewModel>();
    }

    /// <summary>
    /// The user's intent text (prompt).
    /// </summary>
    public string IntentText
    {
        get => _intentText;
        set
        {
            if (_intentText != value)
            {
                _intentText = value;
                OnPropertyChanged(nameof(IntentText));
                OnPropertyChanged(nameof(CharacterCount));
                OnPropertyChanged(nameof(CanRun));
            }
        }
    }

    /// <summary>
    /// The character count display text.
    /// </summary>
    public string CharacterCount => $"{_intentText.Length} characters";

    /// <summary>
    /// Available models for selection.
    /// </summary>
    public ObservableCollection<string> AvailableModels { get; }

    /// <summary>
    /// The currently selected model.
    /// </summary>
    public string? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (_selectedModel != value)
            {
                _selectedModel = value;
                OnPropertyChanged(nameof(SelectedModel));
                OnPropertyChanged(nameof(HasSelectedModel));
                OnPropertyChanged(nameof(CanRun));
            }
        }
    }

    /// <summary>
    /// Whether a model is selected.
    /// </summary>
    public bool HasSelectedModel => !string.IsNullOrEmpty(_selectedModel);

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
                OnPropertyChanged(nameof(AllowsInput));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(ShowCancelButton));
                OnPropertyChanged(nameof(ShowRunButton));
            }
        }
    }

    /// <summary>
    /// Display text for the current execution state.
    /// </summary>
    public string ExecutionStateText => _executionState.ToDisplayText();

    /// <summary>
    /// Whether execution is in progress.
    /// </summary>
    public bool IsExecuting => _executionState.IsExecuting();

    /// <summary>
    /// Whether input is allowed.
    /// </summary>
    public bool AllowsInput => _executionState.AllowsInput();

    /// <summary>
    /// Whether execution can be cancelled.
    /// </summary>
    public bool CanCancel => _executionState.CanCancel();

    /// <summary>
    /// Whether a run can be initiated.
    /// </summary>
    public bool CanRun =>
        !string.IsNullOrWhiteSpace(_intentText) &&
        HasSelectedModel &&
        AllowsInput;

    /// <summary>
    /// Whether to show the cancel button.
    /// </summary>
    public bool ShowCancelButton => IsExecuting;

    /// <summary>
    /// Whether to show the run button.
    /// </summary>
    public bool ShowRunButton => !IsExecuting;

    /// <summary>
    /// Context items attached to this input.
    /// </summary>
    public ObservableCollection<ContextItemViewModel> ContextItems { get; }

    /// <summary>
    /// The number of context items attached.
    /// </summary>
    public int ContextItemCount
    {
        get => _contextItemCount;
        private set
        {
            if (_contextItemCount != value)
            {
                _contextItemCount = value;
                OnPropertyChanged(nameof(ContextItemCount));
                OnPropertyChanged(nameof(HasContextItems));
            }
        }
    }

    /// <summary>
    /// Whether there are context items attached.
    /// </summary>
    public bool HasContextItems => _contextItemCount > 0;

    /// <summary>
    /// Elapsed time for current execution.
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
    /// Sets the available models.
    /// </summary>
    public void SetAvailableModels(IEnumerable<string> models)
    {
        AvailableModels.Clear();
        foreach (var model in models)
        {
            AvailableModels.Add(model);
        }

        // Auto-select first model if none selected
        if (SelectedModel == null && AvailableModels.Count > 0)
        {
            SelectedModel = AvailableModels[0];
        }
    }

    /// <summary>
    /// Adds a context item.
    /// </summary>
    public void AddContextItem(ContextItemViewModel item)
    {
        ContextItems.Add(item);
        ContextItemCount = ContextItems.Count;
    }

    /// <summary>
    /// Removes a context item.
    /// </summary>
    public void RemoveContextItem(ContextItemViewModel item)
    {
        ContextItems.Remove(item);
        ContextItemCount = ContextItems.Count;
    }

    /// <summary>
    /// Clears all context items.
    /// </summary>
    public void ClearContextItems()
    {
        ContextItems.Clear();
        ContextItemCount = 0;
    }

    /// <summary>
    /// Resets the composer to initial state.
    /// </summary>
    public void Reset()
    {
        IntentText = string.Empty;
        ExecutionState = ExecutionState.Idle;
        ElapsedTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Clears only the intent text, preserving model and context.
    /// </summary>
    public void ClearIntent()
    {
        IntentText = string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// ViewModel for a context item attached to the input.
/// </summary>
public sealed class ContextItemViewModel : INotifyPropertyChanged
{
    private readonly string _id;
    private string _name;
    private ContextItemType _type;
    private long _sizeBytes;

    public ContextItemViewModel(string name, ContextItemType type, long sizeBytes = 0)
    {
        _id = Guid.NewGuid().ToString();
        _name = name;
        _type = type;
        _sizeBytes = sizeBytes;
    }

    /// <summary>
    /// Unique identifier for this context item.
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// Display name for the context item.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    /// <summary>
    /// The type of context item.
    /// </summary>
    public ContextItemType Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(TypeIcon));
            }
        }
    }

    /// <summary>
    /// Size in bytes (for files).
    /// </summary>
    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (_sizeBytes != value)
            {
                _sizeBytes = value;
                OnPropertyChanged(nameof(SizeBytes));
                OnPropertyChanged(nameof(SizeText));
            }
        }
    }

    /// <summary>
    /// Formatted size text.
    /// </summary>
    public string SizeText
    {
        get
        {
            if (_sizeBytes < 1024)
                return $"{_sizeBytes} B";
            if (_sizeBytes < 1024 * 1024)
                return $"{_sizeBytes / 1024.0:F1} KB";
            return $"{_sizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>
    /// Icon glyph for the context type.
    /// </summary>
    public string TypeIcon => _type switch
    {
        ContextItemType.File => "\uE8A5",      // Document icon
        ContextItemType.PreviousOutput => "\uE8C8", // Copy icon
        ContextItemType.Image => "\uE8B9",    // Image icon
        ContextItemType.Code => "\uE943",     // Code icon
        _ => "\uE8F1"                          // Attach icon
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Types of context items that can be attached.
/// </summary>
public enum ContextItemType
{
    /// <summary>
    /// A file from the filesystem.
    /// </summary>
    File,

    /// <summary>
    /// Output from a previous run.
    /// </summary>
    PreviousOutput,

    /// <summary>
    /// An image file.
    /// </summary>
    Image,

    /// <summary>
    /// A code snippet.
    /// </summary>
    Code
}
