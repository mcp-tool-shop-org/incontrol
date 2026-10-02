using System.ComponentModel;
using System.Diagnostics;
using InControl.Core.UX;

namespace InControl.ViewModels.Execution;

/// <summary>
/// ViewModel for tracking execution timing and state.
/// Per UX contract: Waiting must feel bounded - always show elapsed time during execution.
/// </summary>
public sealed class ExecutionTimerViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Stopwatch _stopwatch = new();
    private Timer? _updateTimer;
    private ExecutionState _state = ExecutionState.Idle;
    private TimeSpan _elapsedTime;
    private bool _disposed;

    /// <summary>
    /// The current execution state.
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
                OnPropertyChanged(nameof(CapsuleText));
                OnPropertyChanged(nameof(IsExecuting));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(AllowsInput));
                OnPropertyChanged(nameof(ShowTimer));
            }
        }
    }

    /// <summary>
    /// Display text for the current state.
    /// </summary>
    public string StateText => _state.ToDisplayText();

    /// <summary>
    /// Short capsule text for the state.
    /// </summary>
    public string CapsuleText => _state.ToCapsuleText();

    /// <summary>
    /// Whether execution is in progress.
    /// </summary>
    public bool IsExecuting => _state.IsExecuting();

    /// <summary>
    /// Whether execution can be cancelled.
    /// </summary>
    public bool CanCancel => _state.CanCancel();

    /// <summary>
    /// Whether input is allowed.
    /// </summary>
    public bool AllowsInput => _state.AllowsInput();

    /// <summary>
    /// Whether to show the timer.
    /// </summary>
    public bool ShowTimer => IsExecuting;

    /// <summary>
    /// The elapsed time since execution started.
    /// </summary>
    public TimeSpan ElapsedTime
    {
        get => _elapsedTime;
        private set
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
    /// Starts execution with the specified initial state.
    /// </summary>
    public void Start(ExecutionState initialState = ExecutionState.Initializing)
    {
        State = initialState;
        _stopwatch.Restart();
        ElapsedTime = TimeSpan.Zero;

        // Start update timer
        _updateTimer?.Dispose();
        _updateTimer = new Timer(
            _ => UpdateElapsedTime(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Transitions to a new execution state.
    /// </summary>
    public void TransitionTo(ExecutionState newState)
    {
        State = newState;

        if (!newState.IsExecuting())
        {
            Stop();
        }
    }

    /// <summary>
    /// Stops execution tracking.
    /// </summary>
    public void Stop()
    {
        _stopwatch.Stop();
        _updateTimer?.Dispose();
        _updateTimer = null;
        UpdateElapsedTime(); // Final update
    }

    /// <summary>
    /// Resets to idle state.
    /// </summary>
    public void Reset()
    {
        Stop();
        State = ExecutionState.Idle;
        _stopwatch.Reset();
        ElapsedTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Completes execution successfully.
    /// </summary>
    public void Complete()
    {
        TransitionTo(ExecutionState.Complete);
    }

    /// <summary>
    /// Marks execution as cancelled.
    /// </summary>
    public void Cancel()
    {
        TransitionTo(ExecutionState.Cancelled);
    }

    /// <summary>
    /// Marks execution as having an issue.
    /// </summary>
    public void SetIssue()
    {
        TransitionTo(ExecutionState.Issue);
    }

    private void UpdateElapsedTime()
    {
        ElapsedTime = _stopwatch.Elapsed;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _updateTimer?.Dispose();
            _updateTimer = null;
            _disposed = true;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
