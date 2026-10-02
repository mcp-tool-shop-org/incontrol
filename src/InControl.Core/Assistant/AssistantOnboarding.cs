using System.Text.Json;
using System.Text.Json.Serialization;

namespace InControl.Core.Assistant;

/// <summary>
/// Manages the assistant onboarding flow and safety configuration.
/// Ensures users understand and consent to assistant capabilities.
/// </summary>
public sealed class AssistantOnboarding
{
    private readonly string _configPath;
    private OnboardingState _state;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when onboarding state changes.
    /// </summary>
    public event EventHandler<OnboardingStateChangedEventArgs>? StateChanged;

    public AssistantOnboarding(string configPath)
    {
        _configPath = configPath;
        _state = LoadState() ?? OnboardingState.CreateNew();
    }

    /// <summary>
    /// Current onboarding state.
    /// </summary>
    public OnboardingState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Whether onboarding has been completed.
    /// </summary>
    public bool IsComplete => State.CompletedAt.HasValue;

    /// <summary>
    /// Whether the assistant is currently locked.
    /// </summary>
    public bool IsLocked => State.SafetyLock.IsLocked;

    /// <summary>
    /// Gets the current onboarding step.
    /// </summary>
    public OnboardingStep CurrentStep => State.CurrentStep;

    /// <summary>
    /// Starts or restarts the onboarding flow.
    /// </summary>
    public void StartOnboarding()
    {
        lock (_lock)
        {
            _state = _state with
            {
                CurrentStep = OnboardingStep.Welcome,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = null
            };
            SaveState();
        }

        StateChanged?.Invoke(this, new OnboardingStateChangedEventArgs(_state));
    }

    /// <summary>
    /// Advances to the next onboarding step.
    /// </summary>
    public bool AdvanceStep()
    {
        lock (_lock)
        {
            var nextStep = _state.CurrentStep switch
            {
                OnboardingStep.Welcome => OnboardingStep.Capabilities,
                OnboardingStep.Capabilities => OnboardingStep.Limitations,
                OnboardingStep.Limitations => OnboardingStep.PrivacySettings,
                OnboardingStep.PrivacySettings => OnboardingStep.SafetyAcknowledgment,
                OnboardingStep.SafetyAcknowledgment => OnboardingStep.PersonalitySetup,
                OnboardingStep.PersonalitySetup => OnboardingStep.Complete,
                OnboardingStep.Complete => OnboardingStep.Complete,
                _ => OnboardingStep.Welcome
            };

            if (nextStep == _state.CurrentStep)
            {
                return false;
            }

            _state = _state with
            {
                CurrentStep = nextStep,
                CompletedAt = nextStep == OnboardingStep.Complete ? DateTimeOffset.UtcNow : null
            };
            SaveState();
        }

        StateChanged?.Invoke(this, new OnboardingStateChangedEventArgs(_state));
        return true;
    }

    /// <summary>
    /// Records that the user has acknowledged a specific capability.
    /// </summary>
    public void AcknowledgeCapability(string capabilityId)
    {
        lock (_lock)
        {
            if (!_state.AcknowledgedCapabilities.Contains(capabilityId))
            {
                var acknowledged = _state.AcknowledgedCapabilities.ToList();
                acknowledged.Add(capabilityId);
                _state = _state with { AcknowledgedCapabilities = acknowledged };
                SaveState();
            }
        }
    }

    /// <summary>
    /// Records that the user has accepted a safety acknowledgment.
    /// </summary>
    public void AcceptSafetyAcknowledgment(string acknowledgmentId)
    {
        lock (_lock)
        {
            if (!_state.AcceptedSafetyAcknowledgments.Contains(acknowledgmentId))
            {
                var accepted = _state.AcceptedSafetyAcknowledgments.ToList();
                accepted.Add(acknowledgmentId);
                _state = _state with { AcceptedSafetyAcknowledgments = accepted };
                SaveState();
            }
        }
    }

    /// <summary>
    /// Sets the initial personality profile.
    /// </summary>
    public void SetPersonalityProfile(AssistantProfile profile)
    {
        lock (_lock)
        {
            _state = _state with { SelectedProfile = profile };
            SaveState();
        }
    }

    /// <summary>
    /// Enables the safety lock, preventing all assistant actions.
    /// </summary>
    public void EnableSafetyLock(string reason)
    {
        lock (_lock)
        {
            _state = _state with
            {
                SafetyLock = _state.SafetyLock with
                {
                    IsLocked = true,
                    LockedAt = DateTimeOffset.UtcNow,
                    Reason = reason
                }
            };
            SaveState();
        }

        StateChanged?.Invoke(this, new OnboardingStateChangedEventArgs(_state));
    }

    /// <summary>
    /// Disables the safety lock, allowing assistant actions.
    /// Requires explicit confirmation.
    /// </summary>
    public bool DisableSafetyLock(string confirmationCode)
    {
        const string expectedCode = "UNLOCK-ASSISTANT";

        if (confirmationCode != expectedCode)
        {
            return false;
        }

        lock (_lock)
        {
            _state = _state with
            {
                SafetyLock = _state.SafetyLock with
                {
                    IsLocked = false,
                    LockedAt = null,
                    Reason = null,
                    UnlockCount = _state.SafetyLock.UnlockCount + 1,
                    LastUnlockedAt = DateTimeOffset.UtcNow
                }
            };
            SaveState();
        }

        StateChanged?.Invoke(this, new OnboardingStateChangedEventArgs(_state));
        return true;
    }

    /// <summary>
    /// Checks if a capability has been acknowledged.
    /// </summary>
    public bool HasAcknowledgedCapability(string capabilityId)
    {
        lock (_lock)
        {
            return _state.AcknowledgedCapabilities.Contains(capabilityId);
        }
    }

    /// <summary>
    /// Checks if a safety acknowledgment has been accepted.
    /// </summary>
    public bool HasAcceptedSafetyAcknowledgment(string acknowledgmentId)
    {
        lock (_lock)
        {
            return _state.AcceptedSafetyAcknowledgments.Contains(acknowledgmentId);
        }
    }

    /// <summary>
    /// Resets onboarding state completely.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _state = OnboardingState.CreateNew();
            SaveState();
        }

        StateChanged?.Invoke(this, new OnboardingStateChangedEventArgs(_state));
    }

    /// <summary>
    /// Gets the content for the current onboarding step.
    /// </summary>
    public OnboardingStepContent GetCurrentStepContent()
    {
        return GetStepContent(CurrentStep);
    }

    /// <summary>
    /// Gets the content for a specific onboarding step.
    /// </summary>
    public static OnboardingStepContent GetStepContent(OnboardingStep step)
    {
        return step switch
        {
            OnboardingStep.Welcome => new OnboardingStepContent(
                Title: "Welcome to InControl",
                Description: "Your personal assistant that helps you stay in control.",
                Items: [
                    "I'm here to help, not to replace you.",
                    "I'll always explain what I'm about to do.",
                    "I'll never act without your approval.",
                    "Your data stays on your device."
                ],
                ActionLabel: "Let's get started"
            ),

            OnboardingStep.Capabilities => new OnboardingStepContent(
                Title: "What I Can Do",
                Description: "Here's what I'm capable of:",
                Items: [
                    "Remember things you tell me (with your permission)",
                    "Use tools to help with tasks",
                    "Learn your preferences over time",
                    "Explain my reasoning for any action"
                ],
                ActionLabel: "I understand"
            ),

            OnboardingStep.Limitations => new OnboardingStepContent(
                Title: "What I Cannot Do",
                Description: "Important limitations to understand:",
                Items: [
                    "I cannot access the internet without explicit tool permission",
                    "I cannot modify system settings or files",
                    "I cannot make decisions on your behalf",
                    "I cannot guarantee perfect accuracy"
                ],
                ActionLabel: "I understand"
            ),

            OnboardingStep.PrivacySettings => new OnboardingStepContent(
                Title: "Privacy Settings",
                Description: "Choose how I handle your data:",
                Items: [
                    "All data is stored locally on your device",
                    "You control what I remember",
                    "You can delete any memory at any time",
                    "No data is shared without explicit consent"
                ],
                ActionLabel: "Configure privacy"
            ),

            OnboardingStep.SafetyAcknowledgment => new OnboardingStepContent(
                Title: "Safety Acknowledgment",
                Description: "Please acknowledge these safety points:",
                Items: [
                    "I may make mistakes - always verify important information",
                    "I'm a tool, not a replacement for professional advice",
                    "You can stop me at any time with the safety lock",
                    "Report any concerning behavior immediately"
                ],
                ActionLabel: "I acknowledge"
            ),

            OnboardingStep.PersonalitySetup => new OnboardingStepContent(
                Title: "Personalize Your Assistant",
                Description: "Choose how I communicate with you:",
                Items: [
                    "Select a communication style",
                    "Set verbosity preference",
                    "Choose explanation level",
                    "You can change these anytime"
                ],
                ActionLabel: "Complete setup"
            ),

            OnboardingStep.Complete => new OnboardingStepContent(
                Title: "Setup Complete",
                Description: "You're all set!",
                Items: [
                    "I'm ready to help you",
                    "Ask me anything to get started",
                    "Remember: I'll always explain before I act"
                ],
                ActionLabel: "Start using assistant"
            ),

            _ => new OnboardingStepContent(
                Title: "Unknown Step",
                Description: "Something went wrong.",
                Items: [],
                ActionLabel: "Return to start"
            )
        };
    }

    private OnboardingState? LoadState()
    {
        if (!File.Exists(_configPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<OnboardingState>(json, new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            });
        }
        catch
        {
            return null;
        }
    }

    private void SaveState()
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Silently fail - state will be lost but not crash
        }
    }
}

/// <summary>
/// Onboarding steps.
/// </summary>
public enum OnboardingStep
{
    /// <summary>Welcome screen.</summary>
    Welcome,

    /// <summary>Explain capabilities.</summary>
    Capabilities,

    /// <summary>Explain limitations.</summary>
    Limitations,

    /// <summary>Privacy configuration.</summary>
    PrivacySettings,

    /// <summary>Safety acknowledgment.</summary>
    SafetyAcknowledgment,

    /// <summary>Personality setup.</summary>
    PersonalitySetup,

    /// <summary>Onboarding complete.</summary>
    Complete
}

/// <summary>
/// Persistent state for onboarding.
/// </summary>
public sealed record OnboardingState(
    OnboardingStep CurrentStep,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<string> AcknowledgedCapabilities,
    IReadOnlyList<string> AcceptedSafetyAcknowledgments,
    AssistantProfile? SelectedProfile,
    SafetyLockState SafetyLock
)
{
    public static OnboardingState CreateNew() => new(
        CurrentStep: OnboardingStep.Welcome,
        StartedAt: null,
        CompletedAt: null,
        AcknowledgedCapabilities: [],
        AcceptedSafetyAcknowledgments: [],
        SelectedProfile: null,
        SafetyLock: SafetyLockState.CreateUnlocked()
    );
}

/// <summary>
/// Safety lock state.
/// </summary>
public sealed record SafetyLockState(
    bool IsLocked,
    DateTimeOffset? LockedAt,
    string? Reason,
    int UnlockCount,
    DateTimeOffset? LastUnlockedAt
)
{
    public static SafetyLockState CreateUnlocked() => new(
        IsLocked: false,
        LockedAt: null,
        Reason: null,
        UnlockCount: 0,
        LastUnlockedAt: null
    );

    public static SafetyLockState CreateLocked(string reason) => new(
        IsLocked: true,
        LockedAt: DateTimeOffset.UtcNow,
        Reason: reason,
        UnlockCount: 0,
        LastUnlockedAt: null
    );
}

/// <summary>
/// Content for an onboarding step.
/// </summary>
public sealed record OnboardingStepContent(
    string Title,
    string Description,
    IReadOnlyList<string> Items,
    string ActionLabel
);

/// <summary>
/// Event args for onboarding state changes.
/// </summary>
public sealed class OnboardingStateChangedEventArgs : EventArgs
{
    public OnboardingState State { get; }

    public OnboardingStateChangedEventArgs(OnboardingState state)
    {
        State = state;
    }
}

/// <summary>
/// Guards operations based on safety lock status.
/// </summary>
public sealed class SafetyGuard
{
    private readonly AssistantOnboarding _onboarding;

    public SafetyGuard(AssistantOnboarding onboarding)
    {
        _onboarding = onboarding;
    }

    /// <summary>
    /// Checks if an operation is allowed.
    /// </summary>
    public SafetyCheckResult CheckOperation(string operationType)
    {
        if (_onboarding.IsLocked)
        {
            return new SafetyCheckResult(
                Allowed: false,
                Reason: $"Safety lock is enabled: {_onboarding.State.SafetyLock.Reason ?? "No reason provided"}",
                RequiresUnlock: true
            );
        }

        if (!_onboarding.IsComplete)
        {
            return new SafetyCheckResult(
                Allowed: false,
                Reason: "Please complete onboarding before using the assistant",
                RequiresUnlock: false
            );
        }

        return new SafetyCheckResult(
            Allowed: true,
            Reason: null,
            RequiresUnlock: false
        );
    }

    /// <summary>
    /// Executes an operation if allowed.
    /// </summary>
    public async Task<SafetyGuardResult<T>> ExecuteIfAllowedAsync<T>(
        string operationType,
        Func<Task<T>> operation,
        T? blockedValue = default)
    {
        var check = CheckOperation(operationType);

        if (!check.Allowed)
        {
            return new SafetyGuardResult<T>(
                Success: false,
                Value: blockedValue,
                BlockedReason: check.Reason
            );
        }

        var result = await operation();
        return new SafetyGuardResult<T>(
            Success: true,
            Value: result,
            BlockedReason: null
        );
    }
}

/// <summary>
/// Result of a safety check.
/// </summary>
public sealed record SafetyCheckResult(
    bool Allowed,
    string? Reason,
    bool RequiresUnlock
);

/// <summary>
/// Result of a safety-guarded operation.
/// </summary>
public sealed record SafetyGuardResult<T>(
    bool Success,
    T? Value,
    string? BlockedReason
);

/// <summary>
/// Standard capability IDs for acknowledgment.
/// </summary>
public static class CapabilityIds
{
    public const string Memory = "capability.memory";
    public const string Tools = "capability.tools";
    public const string Learning = "capability.learning";
    public const string Explainability = "capability.explainability";
}

/// <summary>
/// Standard safety acknowledgment IDs.
/// </summary>
public static class SafetyAcknowledgmentIds
{
    public const string Fallibility = "safety.fallibility";
    public const string NotProfessionalAdvice = "safety.not-professional-advice";
    public const string SafetyLockAvailable = "safety.lock-available";
    public const string ReportConcerns = "safety.report-concerns";
}
