using InControl.Core.Errors;

namespace InControl.Core.Assistant;

/// <summary>
/// Defines a tool that the assistant can use.
/// All tools must be explicitly registered and declare their capabilities.
/// </summary>
public interface IAssistantTool
{
    /// <summary>
    /// Unique identifier for this tool.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what this tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Risk level of this tool.
    /// </summary>
    ToolRiskLevel RiskLevel { get; }

    /// <summary>
    /// Whether this tool is read-only (safe) or can mutate state.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Whether this tool requires network access.
    /// </summary>
    bool RequiresNetwork { get; }

    /// <summary>
    /// Input parameters this tool accepts.
    /// </summary>
    IReadOnlyList<ToolParameter> Parameters { get; }

    /// <summary>
    /// Executes the tool with given parameters.
    /// </summary>
    Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken ct = default);
}

/// <summary>
/// Risk level classification for tools.
/// </summary>
public enum ToolRiskLevel
{
    /// <summary>
    /// Safe, read-only operations.
    /// </summary>
    Low,

    /// <summary>
    /// May modify local state.
    /// </summary>
    Medium,

    /// <summary>
    /// May have significant effects (network, file system).
    /// </summary>
    High,

    /// <summary>
    /// Potentially destructive or irreversible.
    /// </summary>
    Critical
}

/// <summary>
/// Describes a parameter accepted by a tool.
/// </summary>
public sealed record ToolParameter(
    string Name,
    string Description,
    ParameterType Type,
    bool Required,
    object? DefaultValue = null
);

/// <summary>
/// Parameter types for tool inputs.
/// </summary>
public enum ParameterType
{
    String,
    Integer,
    Boolean,
    FilePath,
    Url,
    Json
}

/// <summary>
/// Context for tool execution.
/// </summary>
public sealed record ToolExecutionContext(
    IReadOnlyDictionary<string, object?> Parameters,
    Guid InvocationId,
    DateTimeOffset RequestedAt
);

/// <summary>
/// Result of tool execution.
/// </summary>
public sealed record ToolResult
{
    public bool Success { get; init; }
    public string? Output { get; init; }
    public InControlError? Error { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public TimeSpan Duration { get; init; }

    public static ToolResult Succeeded(string output, TimeSpan duration) => new()
    {
        Success = true,
        Output = output,
        CompletedAt = DateTimeOffset.UtcNow,
        Duration = duration
    };

    public static ToolResult Failed(InControlError error, TimeSpan duration) => new()
    {
        Success = false,
        Error = error,
        CompletedAt = DateTimeOffset.UtcNow,
        Duration = duration
    };
}

/// <summary>
/// Registry of available assistant tools.
/// No tool can execute outside this registry.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IAssistantTool> _tools = [];
    private readonly Dictionary<string, ToolPermission> _permissions = [];
    private readonly List<ToolInvocationRecord> _auditLog = [];
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when a tool is invoked.
    /// </summary>
    public event EventHandler<ToolInvokedEventArgs>? ToolInvoked;

    /// <summary>
    /// All registered tools.
    /// </summary>
    public IReadOnlyList<IAssistantTool> Tools
    {
        get
        {
            lock (_lock)
            {
                return _tools.Values.ToList();
            }
        }
    }

    /// <summary>
    /// Audit log of all tool invocations.
    /// </summary>
    public IReadOnlyList<ToolInvocationRecord> AuditLog
    {
        get
        {
            lock (_lock)
            {
                return _auditLog.ToList();
            }
        }
    }

    /// <summary>
    /// Registers a tool.
    /// </summary>
    public void Register(IAssistantTool tool)
    {
        lock (_lock)
        {
            _tools[tool.Id] = tool;
            // Default permission based on risk level
            _permissions[tool.Id] = tool.RiskLevel switch
            {
                ToolRiskLevel.Low => ToolPermission.AlwaysAllow,
                ToolRiskLevel.Medium => ToolPermission.AskOnce,
                ToolRiskLevel.High => ToolPermission.AlwaysAsk,
                ToolRiskLevel.Critical => ToolPermission.AlwaysAsk,
                _ => ToolPermission.AlwaysAsk
            };
        }
    }

    /// <summary>
    /// Unregisters a tool.
    /// </summary>
    public bool Unregister(string toolId)
    {
        lock (_lock)
        {
            _permissions.Remove(toolId);
            return _tools.Remove(toolId);
        }
    }

    /// <summary>
    /// Gets a tool by ID.
    /// </summary>
    public IAssistantTool? GetTool(string toolId)
    {
        lock (_lock)
        {
            return _tools.TryGetValue(toolId, out var tool) ? tool : null;
        }
    }

    /// <summary>
    /// Gets the permission setting for a tool.
    /// </summary>
    public ToolPermission GetPermission(string toolId)
    {
        lock (_lock)
        {
            return _permissions.TryGetValue(toolId, out var permission)
                ? permission
                : ToolPermission.AlwaysAsk;
        }
    }

    /// <summary>
    /// Sets the permission setting for a tool.
    /// </summary>
    public void SetPermission(string toolId, ToolPermission permission)
    {
        lock (_lock)
        {
            if (_tools.ContainsKey(toolId))
            {
                _permissions[toolId] = permission;
            }
        }
    }

    /// <summary>
    /// Checks if a tool requires approval before execution.
    /// </summary>
    public bool RequiresApproval(string toolId)
    {
        lock (_lock)
        {
            if (!_permissions.TryGetValue(toolId, out var permission))
                return true;

            return permission != ToolPermission.AlwaysAllow;
        }
    }

    /// <summary>
    /// Executes a tool and logs the invocation.
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(
        string toolId,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        IAssistantTool? tool;
        lock (_lock)
        {
            if (!_tools.TryGetValue(toolId, out tool))
            {
                return ToolResult.Failed(
                    InControlError.Create(ErrorCode.InvalidOperation, $"Tool not found: {toolId}"),
                    TimeSpan.Zero
                );
            }
        }

        var invocationId = Guid.NewGuid();
        var context = new ToolExecutionContext(parameters, invocationId, DateTimeOffset.UtcNow);

        var startTime = DateTimeOffset.UtcNow;
        ToolResult result;

        try
        {
            result = await tool.ExecuteAsync(context, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = ToolResult.Failed(
                InControlError.Cancelled($"Tool execution: {toolId}"),
                DateTimeOffset.UtcNow - startTime
            );
        }
        catch (Exception ex)
        {
            result = ToolResult.Failed(
                InControlError.FromException(ex),
                DateTimeOffset.UtcNow - startTime
            );
        }

        // Record in audit log
        var record = new ToolInvocationRecord(
            InvocationId: invocationId,
            ToolId: toolId,
            Parameters: parameters,
            Result: result,
            InvokedAt: startTime
        );

        lock (_lock)
        {
            _auditLog.Add(record);

            if (_auditLog.Count > 10_000)
            {
                _auditLog.RemoveRange(0, 1_000);
            }
        }

        ToolInvoked?.Invoke(this, new ToolInvokedEventArgs(record));

        return result;
    }

    /// <summary>
    /// Clears the audit log.
    /// </summary>
    public void ClearAuditLog()
    {
        lock (_lock)
        {
            _auditLog.Clear();
        }
    }
}

/// <summary>
/// Permission setting for a tool.
/// </summary>
public enum ToolPermission
{
    /// <summary>
    /// Always allow without asking.
    /// </summary>
    AlwaysAllow,

    /// <summary>
    /// Ask once, then remember choice.
    /// </summary>
    AskOnce,

    /// <summary>
    /// Always ask before execution.
    /// </summary>
    AlwaysAsk,

    /// <summary>
    /// Never allow (disabled).
    /// </summary>
    Disabled
}

/// <summary>
/// Record of a tool invocation for audit purposes.
/// </summary>
public sealed record ToolInvocationRecord(
    Guid InvocationId,
    string ToolId,
    IReadOnlyDictionary<string, object?> Parameters,
    ToolResult Result,
    DateTimeOffset InvokedAt
);

/// <summary>
/// Event args for tool invocation.
/// </summary>
public sealed class ToolInvokedEventArgs : EventArgs
{
    public ToolInvocationRecord Record { get; }

    public ToolInvokedEventArgs(ToolInvocationRecord record)
    {
        Record = record;
    }
}
