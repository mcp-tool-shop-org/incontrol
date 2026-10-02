namespace InControl.Core.Assistant;

/// <summary>
/// Represents the current operational state of the assistant.
/// All state transitions are explicit and observable.
/// </summary>
public enum AssistantState
{
    /// <summary>
    /// Assistant is idle, waiting for user input.
    /// </summary>
    Idle,

    /// <summary>
    /// Assistant is receiving/processing user input.
    /// </summary>
    Listening,

    /// <summary>
    /// Assistant is analyzing the request and determining response.
    /// </summary>
    Reasoning,

    /// <summary>
    /// Assistant is proposing an action for user approval.
    /// </summary>
    Proposing,

    /// <summary>
    /// Assistant is waiting for user to approve/deny a proposed action.
    /// </summary>
    AwaitingApproval,

    /// <summary>
    /// Assistant is executing an approved action.
    /// </summary>
    Acting,

    /// <summary>
    /// Assistant is blocked and cannot proceed.
    /// </summary>
    Blocked
}

/// <summary>
/// Manages assistant state with transition validation.
/// Prevents hidden or illegal state transitions.
/// </summary>
public sealed class AssistantStateMachine
{
    private AssistantState _currentState = AssistantState.Idle;
    private readonly object _lock = new();
    private readonly List<StateTransition> _history = [];

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Current assistant state.
    /// </summary>
    public AssistantState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    /// <summary>
    /// History of state transitions.
    /// </summary>
    public IReadOnlyList<StateTransition> History
    {
        get
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }
    }

    /// <summary>
    /// Attempts to transition to a new state.
    /// </summary>
    /// <param name="newState">The target state.</param>
    /// <param name="reason">Reason for the transition.</param>
    /// <returns>True if transition was successful, false if illegal.</returns>
    public bool TryTransition(AssistantState newState, string? reason = null)
    {
        lock (_lock)
        {
            if (!IsValidTransition(_currentState, newState))
            {
                return false;
            }

            var transition = new StateTransition(
                From: _currentState,
                To: newState,
                Reason: reason,
                Timestamp: DateTimeOffset.UtcNow
            );

            var previousState = _currentState;
            _currentState = newState;
            _history.Add(transition);

            // Raise event outside of lock to prevent deadlocks
            Task.Run(() => StateChanged?.Invoke(this, new StateChangedEventArgs(previousState, newState, reason)));

            return true;
        }
    }

    /// <summary>
    /// Forces transition to a state (for recovery scenarios).
    /// Logs the forced transition for audit purposes.
    /// </summary>
    public void ForceTransition(AssistantState newState, string reason)
    {
        lock (_lock)
        {
            var transition = new StateTransition(
                From: _currentState,
                To: newState,
                Reason: $"[FORCED] {reason}",
                Timestamp: DateTimeOffset.UtcNow
            );

            var previousState = _currentState;
            _currentState = newState;
            _history.Add(transition);

            Task.Run(() => StateChanged?.Invoke(this, new StateChangedEventArgs(previousState, newState, reason)));
        }
    }

    /// <summary>
    /// Resets to idle state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            if (_currentState != AssistantState.Idle)
            {
                var transition = new StateTransition(
                    From: _currentState,
                    To: AssistantState.Idle,
                    Reason: "Reset",
                    Timestamp: DateTimeOffset.UtcNow
                );

                var previousState = _currentState;
                _currentState = AssistantState.Idle;
                _history.Add(transition);

                Task.Run(() => StateChanged?.Invoke(this, new StateChangedEventArgs(previousState, AssistantState.Idle, "Reset")));
            }
        }
    }

    /// <summary>
    /// Validates whether a state transition is allowed.
    /// </summary>
    public static bool IsValidTransition(AssistantState from, AssistantState to)
    {
        // Same state is always valid (no-op)
        if (from == to)
            return true;

        // Define valid transitions
        return (from, to) switch
        {
            // From Idle
            (AssistantState.Idle, AssistantState.Listening) => true,
            (AssistantState.Idle, AssistantState.Blocked) => true,

            // From Listening
            (AssistantState.Listening, AssistantState.Reasoning) => true,
            (AssistantState.Listening, AssistantState.Idle) => true, // User cancelled
            (AssistantState.Listening, AssistantState.Blocked) => true,

            // From Reasoning
            (AssistantState.Reasoning, AssistantState.Proposing) => true,
            (AssistantState.Reasoning, AssistantState.Acting) => true, // Direct action (no approval needed)
            (AssistantState.Reasoning, AssistantState.Idle) => true, // Simple response
            (AssistantState.Reasoning, AssistantState.Blocked) => true,

            // From Proposing
            (AssistantState.Proposing, AssistantState.AwaitingApproval) => true,
            (AssistantState.Proposing, AssistantState.Idle) => true, // User declined before approval
            (AssistantState.Proposing, AssistantState.Blocked) => true,

            // From AwaitingApproval
            (AssistantState.AwaitingApproval, AssistantState.Acting) => true, // User approved
            (AssistantState.AwaitingApproval, AssistantState.Idle) => true, // User denied
            (AssistantState.AwaitingApproval, AssistantState.Blocked) => true,

            // From Acting
            (AssistantState.Acting, AssistantState.Idle) => true, // Action complete
            (AssistantState.Acting, AssistantState.Blocked) => true, // Action failed

            // From Blocked
            (AssistantState.Blocked, AssistantState.Idle) => true, // Recovered or user reset

            // All other transitions are invalid
            _ => false
        };
    }

    /// <summary>
    /// Gets human-readable description of current state.
    /// </summary>
    public static string GetStateDescription(AssistantState state) => state switch
    {
        AssistantState.Idle => "Ready",
        AssistantState.Listening => "Listening...",
        AssistantState.Reasoning => "Thinking...",
        AssistantState.Proposing => "Suggesting action...",
        AssistantState.AwaitingApproval => "Waiting for approval",
        AssistantState.Acting => "Working...",
        AssistantState.Blocked => "Blocked",
        _ => "Unknown"
    };
}

/// <summary>
/// Records a state transition.
/// </summary>
public sealed record StateTransition(
    AssistantState From,
    AssistantState To,
    string? Reason,
    DateTimeOffset Timestamp
);

/// <summary>
/// Event args for state changes.
/// </summary>
public sealed class StateChangedEventArgs : EventArgs
{
    public AssistantState PreviousState { get; }
    public AssistantState NewState { get; }
    public string? Reason { get; }

    public StateChangedEventArgs(AssistantState previous, AssistantState newState, string? reason)
    {
        PreviousState = previous;
        NewState = newState;
        Reason = reason;
    }
}
