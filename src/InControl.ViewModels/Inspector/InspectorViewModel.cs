using System.Collections.ObjectModel;
using System.ComponentModel;
using InControl.Core.UX;
using InControl.ViewModels.Input;

namespace InControl.ViewModels.Inspector;

/// <summary>
/// ViewModel for the right inspector panel.
/// Displays run statistics and context items.
/// </summary>
public sealed class InspectorViewModel : INotifyPropertyChanged
{
    private InspectorTab _activeTab = InspectorTab.Run;
    private RunStatistics _runStats = new();
    private bool _isVisible = true;

    public InspectorViewModel()
    {
        ContextItems = new ObservableCollection<ContextItemViewModel>();
    }

    /// <summary>
    /// The currently active tab.
    /// </summary>
    public InspectorTab ActiveTab
    {
        get => _activeTab;
        set
        {
            if (_activeTab != value)
            {
                _activeTab = value;
                OnPropertyChanged(nameof(ActiveTab));
                OnPropertyChanged(nameof(ShowRunTab));
                OnPropertyChanged(nameof(ShowContextTab));
            }
        }
    }

    /// <summary>
    /// Whether to show the Run tab content.
    /// </summary>
    public bool ShowRunTab => _activeTab == InspectorTab.Run;

    /// <summary>
    /// Whether to show the Context tab content.
    /// </summary>
    public bool ShowContextTab => _activeTab == InspectorTab.Context;

    /// <summary>
    /// Whether the inspector panel is visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    /// <summary>
    /// The current run statistics.
    /// </summary>
    public RunStatistics RunStats
    {
        get => _runStats;
        set
        {
            _runStats = value;
            OnPropertyChanged(nameof(RunStats));
        }
    }

    /// <summary>
    /// Context items attached to the current session.
    /// </summary>
    public ObservableCollection<ContextItemViewModel> ContextItems { get; }

    /// <summary>
    /// Whether there are any context items.
    /// </summary>
    public bool HasContextItems => ContextItems.Count > 0;

    /// <summary>
    /// Updates the run statistics.
    /// </summary>
    public void UpdateRunStats(RunStatistics stats)
    {
        RunStats = stats;
    }

    /// <summary>
    /// Clears the run statistics.
    /// </summary>
    public void ClearRunStats()
    {
        RunStats = new RunStatistics();
    }

    /// <summary>
    /// Adds a context item.
    /// </summary>
    public void AddContextItem(ContextItemViewModel item)
    {
        ContextItems.Add(item);
        OnPropertyChanged(nameof(HasContextItems));
    }

    /// <summary>
    /// Removes a context item.
    /// </summary>
    public void RemoveContextItem(ContextItemViewModel item)
    {
        ContextItems.Remove(item);
        OnPropertyChanged(nameof(HasContextItems));
    }

    /// <summary>
    /// Clears all context items.
    /// </summary>
    public void ClearContextItems()
    {
        ContextItems.Clear();
        OnPropertyChanged(nameof(HasContextItems));
    }

    /// <summary>
    /// Toggles the inspector visibility.
    /// </summary>
    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// The inspector tabs.
/// </summary>
public enum InspectorTab
{
    /// <summary>
    /// Run statistics tab.
    /// </summary>
    Run,

    /// <summary>
    /// Context items tab.
    /// </summary>
    Context
}

/// <summary>
/// Statistics from a model run.
/// </summary>
public sealed class RunStatistics : INotifyPropertyChanged
{
    private string _deviceName = "—";
    private string _modelName = "—";
    private TimeSpan _latency;
    private int _tokensIn;
    private int _tokensOut;
    private long _memoryUsedBytes;
    private ExecutionState _state = ExecutionState.Idle;

    /// <summary>
    /// The device name (e.g., "NVIDIA RTX 5080").
    /// </summary>
    public string DeviceName
    {
        get => _deviceName;
        set
        {
            if (_deviceName != value)
            {
                _deviceName = value;
                OnPropertyChanged(nameof(DeviceName));
            }
        }
    }

    /// <summary>
    /// The model name.
    /// </summary>
    public string ModelName
    {
        get => _modelName;
        set
        {
            if (_modelName != value)
            {
                _modelName = value;
                OnPropertyChanged(nameof(ModelName));
            }
        }
    }

    /// <summary>
    /// Time to first token.
    /// </summary>
    public TimeSpan Latency
    {
        get => _latency;
        set
        {
            if (_latency != value)
            {
                _latency = value;
                OnPropertyChanged(nameof(Latency));
                OnPropertyChanged(nameof(LatencyText));
            }
        }
    }

    /// <summary>
    /// Formatted latency text.
    /// </summary>
    public string LatencyText
    {
        get
        {
            if (_latency == TimeSpan.Zero)
                return "—";
            if (_latency.TotalMilliseconds < 1000)
                return $"{_latency.TotalMilliseconds:F0}ms";
            return $"{_latency.TotalSeconds:F2}s";
        }
    }

    /// <summary>
    /// Number of input tokens.
    /// </summary>
    public int TokensIn
    {
        get => _tokensIn;
        set
        {
            if (_tokensIn != value)
            {
                _tokensIn = value;
                OnPropertyChanged(nameof(TokensIn));
                OnPropertyChanged(nameof(TokensInText));
            }
        }
    }

    /// <summary>
    /// Formatted tokens in text.
    /// </summary>
    public string TokensInText => _tokensIn > 0 ? _tokensIn.ToString("N0") : "—";

    /// <summary>
    /// Number of output tokens.
    /// </summary>
    public int TokensOut
    {
        get => _tokensOut;
        set
        {
            if (_tokensOut != value)
            {
                _tokensOut = value;
                OnPropertyChanged(nameof(TokensOut));
                OnPropertyChanged(nameof(TokensOutText));
                OnPropertyChanged(nameof(TokensPerSecond));
            }
        }
    }

    /// <summary>
    /// Formatted tokens out text.
    /// </summary>
    public string TokensOutText => _tokensOut > 0 ? _tokensOut.ToString("N0") : "—";

    /// <summary>
    /// Tokens per second (output throughput).
    /// </summary>
    public double TokensPerSecond
    {
        get
        {
            if (_latency == TimeSpan.Zero || _tokensOut == 0)
                return 0;
            return _tokensOut / _latency.TotalSeconds;
        }
    }

    /// <summary>
    /// Memory used in bytes.
    /// </summary>
    public long MemoryUsedBytes
    {
        get => _memoryUsedBytes;
        set
        {
            if (_memoryUsedBytes != value)
            {
                _memoryUsedBytes = value;
                OnPropertyChanged(nameof(MemoryUsedBytes));
                OnPropertyChanged(nameof(MemoryText));
            }
        }
    }

    /// <summary>
    /// Formatted memory text.
    /// </summary>
    public string MemoryText
    {
        get
        {
            if (_memoryUsedBytes == 0)
                return "—";
            if (_memoryUsedBytes < 1024 * 1024)
                return $"{_memoryUsedBytes / 1024.0:F1} KB";
            if (_memoryUsedBytes < 1024 * 1024 * 1024)
                return $"{_memoryUsedBytes / (1024.0 * 1024.0):F1} MB";
            return $"{_memoryUsedBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// The execution state of this run.
    /// </summary>
    public ExecutionState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(StateText));
            }
        }
    }

    /// <summary>
    /// Display text for the state.
    /// </summary>
    public string StateText => _state.ToDisplayText();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
