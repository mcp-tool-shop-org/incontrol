namespace InControl.Core.UX;

/// <summary>
/// Represents the current execution state of the application.
/// Used for UI status display and state management.
/// </summary>
public enum ExecutionState
{
    /// <summary>
    /// No execution in progress. Ready for user input.
    /// </summary>
    Idle,

    /// <summary>
    /// Preparing to execute. Loading model or initializing.
    /// </summary>
    Initializing,

    /// <summary>
    /// Loading model into memory.
    /// </summary>
    LoadingModel,

    /// <summary>
    /// Actively running inference.
    /// </summary>
    Running,

    /// <summary>
    /// Receiving streaming output from model.
    /// </summary>
    Streaming,

    /// <summary>
    /// Completing the run and finalizing output.
    /// </summary>
    Completing,

    /// <summary>
    /// Execution completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// Execution was cancelled by the user.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Execution encountered an issue.
    /// </summary>
    Issue
}

/// <summary>
/// Extension methods for ExecutionState.
/// </summary>
public static class ExecutionStateExtensions
{
    /// <summary>
    /// Gets the display text for the execution state.
    /// </summary>
    public static string ToDisplayText(this ExecutionState state) => state switch
    {
        ExecutionState.Idle => "Idle",
        ExecutionState.Initializing => "Initializing...",
        ExecutionState.LoadingModel => "Loading model...",
        ExecutionState.Running => "Running inference...",
        ExecutionState.Streaming => "Receiving output...",
        ExecutionState.Completing => "Completing run...",
        ExecutionState.Complete => "Complete",
        ExecutionState.Cancelled => "Cancelled",
        ExecutionState.Issue => "Issue",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the status capsule text (short form).
    /// </summary>
    public static string ToCapsuleText(this ExecutionState state) => state switch
    {
        ExecutionState.Idle => "Idle",
        ExecutionState.Initializing or ExecutionState.LoadingModel => "Loading",
        ExecutionState.Running or ExecutionState.Streaming or ExecutionState.Completing => "Running",
        ExecutionState.Complete => "Done",
        ExecutionState.Cancelled => "Cancelled",
        ExecutionState.Issue => "Issue",
        _ => "—"
    };

    /// <summary>
    /// Whether the state represents active execution.
    /// </summary>
    public static bool IsExecuting(this ExecutionState state) => state switch
    {
        ExecutionState.Initializing or
        ExecutionState.LoadingModel or
        ExecutionState.Running or
        ExecutionState.Streaming or
        ExecutionState.Completing => true,
        _ => false
    };

    /// <summary>
    /// Whether the state allows user input.
    /// </summary>
    public static bool AllowsInput(this ExecutionState state) => state switch
    {
        ExecutionState.Idle or
        ExecutionState.Complete or
        ExecutionState.Cancelled or
        ExecutionState.Issue => true,
        _ => false
    };

    /// <summary>
    /// Whether the state can be cancelled.
    /// </summary>
    public static bool CanCancel(this ExecutionState state) => state.IsExecuting();
}
