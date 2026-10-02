using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using InControl.Core.UX;

namespace InControl.App.Controls;

/// <summary>
/// Displays execution state with elapsed time and cancel option.
/// Per UX contract: Always show what is happening, make waiting feel bounded and cancellable.
/// </summary>
public sealed partial class ExecutionIndicator : UserControl
{
    private ExecutionState _state = ExecutionState.Idle;
    private DispatcherTimer? _timer;
    private Stopwatch? _stopwatch;

    public ExecutionIndicator()
    {
        this.InitializeComponent();
        CancelButton.Click += OnCancelClick;
    }

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
                UpdateDisplay();
            }
        }
    }

    /// <summary>
    /// Event raised when cancel is requested.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Starts execution tracking with the timer.
    /// </summary>
    public void StartTracking()
    {
        _stopwatch = Stopwatch.StartNew();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        UpdateElapsedTime();
    }

    /// <summary>
    /// Stops execution tracking.
    /// </summary>
    public void StopTracking()
    {
        _timer?.Stop();
        _timer = null;
        _stopwatch?.Stop();
    }

    /// <summary>
    /// Gets the elapsed time since tracking started.
    /// </summary>
    public TimeSpan ElapsedTime => _stopwatch?.Elapsed ?? TimeSpan.Zero;

    private void OnTimerTick(object? sender, object e)
    {
        UpdateElapsedTime();
    }

    private void UpdateElapsedTime()
    {
        if (_stopwatch == null) return;

        var elapsed = _stopwatch.Elapsed;
        ElapsedTimeText.Text = FormatElapsedTime(elapsed);
    }

    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 1)
            return "< 1s";
        if (elapsed.TotalMinutes < 1)
            return $"{elapsed.Seconds}s";
        return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
    }

    private void UpdateDisplay()
    {
        // Always visible when not idle
        RootGrid.Visibility = _state != ExecutionState.Idle
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Update state text
        StateText.Text = _state.ToDisplayText();

        // Update icon and progress based on state
        var isExecuting = _state.IsExecuting();

        ProgressIndicator.IsActive = isExecuting;
        ProgressIndicator.Visibility = isExecuting ? Visibility.Visible : Visibility.Collapsed;

        StateIcon.Visibility = !isExecuting ? Visibility.Visible : Visibility.Collapsed;
        StateIcon.Glyph = GetStateGlyph(_state);
        StateIcon.Foreground = GetStateForeground(_state);

        // Show elapsed time during execution
        ElapsedTimeText.Visibility = isExecuting ? Visibility.Visible : Visibility.Collapsed;

        // Show cancel button when cancellable
        CancelButton.Visibility = _state.CanCancel() ? Visibility.Visible : Visibility.Collapsed;

        // Start/stop tracking
        if (isExecuting && _timer == null)
        {
            StartTracking();
        }
        else if (!isExecuting && _timer != null)
        {
            StopTracking();
        }
    }

    private static string GetStateGlyph(ExecutionState state) => state switch
    {
        ExecutionState.Complete => "\uE73E",    // Checkmark
        ExecutionState.Cancelled => "\uE711",   // Cancel
        ExecutionState.Issue => "\uE7BA",       // Warning
        _ => "\uE946"                           // Info
    };

    private Brush GetStateForeground(ExecutionState state)
    {
        var resourceKey = state switch
        {
            ExecutionState.Complete => "SystemFillColorSuccessBrush",
            ExecutionState.Issue => "SystemFillColorCriticalBrush",
            ExecutionState.Cancelled => "SystemFillColorNeutralBrush",
            _ => "TextFillColorSecondaryBrush"
        };

        if (Resources.TryGetValue(resourceKey, out var brush) && brush is Brush b)
            return b;

        return (Brush)App.Current.Resources["TextFillColorSecondaryBrush"];
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
