namespace InControl.Core.Assistant;

/// <summary>
/// Handles consent flow for memory operations.
/// Memory writes require explicit justification surfaced to user.
/// </summary>
public sealed class MemoryConsentManager
{
    private readonly AssistantMemoryStore _store;
    private readonly List<MemoryConsentRequest> _pendingRequests = [];
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when a memory write request needs approval.
    /// </summary>
    public event EventHandler<MemoryConsentRequestEventArgs>? ConsentRequested;

    /// <summary>
    /// Event raised when a consent decision is made.
    /// </summary>
    public event EventHandler<ConsentDecisionEventArgs>? ConsentDecided;

    public MemoryConsentManager(AssistantMemoryStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Pending memory requests awaiting user approval.
    /// </summary>
    public IReadOnlyList<MemoryConsentRequest> PendingRequests
    {
        get
        {
            lock (_lock)
            {
                return _pendingRequests.ToList();
            }
        }
    }

    /// <summary>
    /// Requests permission to remember something.
    /// Returns immediately; user approval happens asynchronously.
    /// </summary>
    public MemoryConsentRequest RequestRemember(
        MemoryType type,
        string key,
        string value,
        string justification,
        MemorySource source = MemorySource.Inferred,
        double confidence = 0.8)
    {
        var request = new MemoryConsentRequest(
            Id: Guid.NewGuid(),
            Type: type,
            Key: key,
            Value: value,
            Justification: justification,
            Source: source,
            Confidence: confidence,
            RequestedAt: DateTimeOffset.UtcNow,
            Status: ConsentStatus.Pending
        );

        lock (_lock)
        {
            _pendingRequests.Add(request);
        }

        ConsentRequested?.Invoke(this, new MemoryConsentRequestEventArgs(request));

        return request;
    }

    /// <summary>
    /// User approves a memory request.
    /// </summary>
    public bool Approve(Guid requestId, MemoryScope scope = MemoryScope.User)
    {
        MemoryConsentRequest? request;
        lock (_lock)
        {
            request = _pendingRequests.FirstOrDefault(r => r.Id == requestId);
            if (request == null)
                return false;

            _pendingRequests.Remove(request);
        }

        // Create and store the memory
        var memory = AssistantMemoryItem.Create(
            request.Type,
            scope,
            request.Source,
            request.Key,
            request.Value,
            request.Justification,
            request.Confidence
        );

        _store.Add(memory);

        ConsentDecided?.Invoke(this, new ConsentDecisionEventArgs(request, ConsentDecision.Approved, memory));

        return true;
    }

    /// <summary>
    /// User denies a memory request.
    /// </summary>
    public bool Deny(Guid requestId, string? reason = null)
    {
        MemoryConsentRequest? request;
        lock (_lock)
        {
            request = _pendingRequests.FirstOrDefault(r => r.Id == requestId);
            if (request == null)
                return false;

            _pendingRequests.Remove(request);
        }

        ConsentDecided?.Invoke(this, new ConsentDecisionEventArgs(request, ConsentDecision.Denied, null, reason));

        return true;
    }

    /// <summary>
    /// Dismisses all pending requests.
    /// </summary>
    public int DismissAll()
    {
        int count;
        lock (_lock)
        {
            count = _pendingRequests.Count;
            _pendingRequests.Clear();
        }
        return count;
    }

    /// <summary>
    /// Directly adds a memory with explicit user action (no consent needed).
    /// Use this when user explicitly says "remember this".
    /// </summary>
    public AssistantMemoryItem RememberExplicit(
        MemoryType type,
        string key,
        string value,
        MemoryScope scope = MemoryScope.User)
    {
        var memory = AssistantMemoryItem.Create(
            type,
            scope,
            MemorySource.ExplicitUser,
            key,
            value,
            "User explicitly requested to remember this",
            confidence: 1.0
        );

        _store.Add(memory);

        return memory;
    }
}

/// <summary>
/// A request to remember something, awaiting user approval.
/// </summary>
public sealed record MemoryConsentRequest(
    Guid Id,
    MemoryType Type,
    string Key,
    string Value,
    string Justification,
    MemorySource Source,
    double Confidence,
    DateTimeOffset RequestedAt,
    ConsentStatus Status
);

/// <summary>
/// Status of a consent request.
/// </summary>
public enum ConsentStatus
{
    Pending,
    Approved,
    Denied,
    Expired
}

/// <summary>
/// User's decision on a consent request.
/// </summary>
public enum ConsentDecision
{
    Approved,
    Denied
}

/// <summary>
/// Event args for consent requests.
/// </summary>
public sealed class MemoryConsentRequestEventArgs : EventArgs
{
    public MemoryConsentRequest Request { get; }

    public MemoryConsentRequestEventArgs(MemoryConsentRequest request)
    {
        Request = request;
    }
}

/// <summary>
/// Event args for consent decisions.
/// </summary>
public sealed class ConsentDecisionEventArgs : EventArgs
{
    public MemoryConsentRequest Request { get; }
    public ConsentDecision Decision { get; }
    public AssistantMemoryItem? CreatedMemory { get; }
    public string? DenialReason { get; }

    public ConsentDecisionEventArgs(
        MemoryConsentRequest request,
        ConsentDecision decision,
        AssistantMemoryItem? createdMemory,
        string? denialReason = null)
    {
        Request = request;
        Decision = decision;
        CreatedMemory = createdMemory;
        DenialReason = denialReason;
    }
}

/// <summary>
/// Labels for memory confidence levels.
/// </summary>
public static class ConfidenceLabels
{
    /// <summary>
    /// Gets human-readable label for confidence level.
    /// </summary>
    public static string GetLabel(double confidence) => confidence switch
    {
        >= 1.0 => "Confirmed",
        >= 0.9 => "High confidence",
        >= 0.7 => "Likely",
        >= 0.5 => "Possible",
        _ => "Uncertain"
    };

    /// <summary>
    /// Gets confidence level description.
    /// </summary>
    public static string GetDescription(double confidence) => confidence switch
    {
        >= 1.0 => "User explicitly confirmed this information",
        >= 0.9 => "Very likely based on clear evidence",
        >= 0.7 => "Probably correct based on context",
        >= 0.5 => "May be correct but uncertain",
        _ => "Low confidence, please verify"
    };
}
