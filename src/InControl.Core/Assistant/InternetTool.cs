using InControl.Core.Connectivity;
using InControl.Core.Errors;

namespace InControl.Core.Assistant;

/// <summary>
/// Assistant tool for making governed internet requests.
/// Internet access is exposed as a tool, not a capability.
/// The assistant must propose internet use, explain why, and await approval.
/// </summary>
public sealed class InternetTool : IAssistantTool
{
    private readonly ConnectivityManager _connectivity;
    private readonly IInternetToolPermissions _permissions;

    public InternetTool(ConnectivityManager connectivity, IInternetToolPermissions permissions)
    {
        _connectivity = connectivity;
        _permissions = permissions;
    }

    public string Id => "internet.request";
    public string Name => "Internet Request";
    public string Description => "Make a network request to fetch data from the internet";
    public ToolRiskLevel RiskLevel => ToolRiskLevel.High;
    public bool IsReadOnly => true;
    public bool RequiresNetwork => true;

    public IReadOnlyList<ToolParameter> Parameters => new List<ToolParameter>
    {
        new("endpoint", "The URL to request", ParameterType.Url, true),
        new("method", "HTTP method (GET, POST)", ParameterType.String, true),
        new("purpose", "Why this request is needed", ParameterType.String, true),
        new("data_sent", "Data being sent (for POST)", ParameterType.String, false),
        new("expected_data", "What data is expected back", ParameterType.String, true),
        new("retention", "How long the data will be kept", ParameterType.String, true)
    };

    public async Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;

        // Extract parameters
        if (!context.Parameters.TryGetValue("endpoint", out var endpointObj) ||
            endpointObj is not string endpoint)
        {
            return ToolResult.Failed(CreateError("Missing required parameter: endpoint"), TimeSpan.Zero);
        }

        if (!context.Parameters.TryGetValue("method", out var methodObj) ||
            methodObj is not string method)
        {
            return ToolResult.Failed(CreateError("Missing required parameter: method"), TimeSpan.Zero);
        }

        if (!context.Parameters.TryGetValue("purpose", out var purposeObj) ||
            purposeObj is not string purpose)
        {
            return ToolResult.Failed(CreateError("Missing required parameter: purpose"), TimeSpan.Zero);
        }

        if (!context.Parameters.TryGetValue("expected_data", out var expectedObj) ||
            expectedObj is not string expectedData)
        {
            return ToolResult.Failed(CreateError("Missing required parameter: expected_data"), TimeSpan.Zero);
        }

        if (!context.Parameters.TryGetValue("retention", out var retentionObj) ||
            retentionObj is not string retention)
        {
            return ToolResult.Failed(CreateError("Missing required parameter: retention"), TimeSpan.Zero);
        }

        var dataSent = context.Parameters.TryGetValue("data_sent", out var dataObj) && dataObj is string data
            ? data
            : null;

        // Check connectivity mode
        if (_connectivity.Mode == ConnectivityMode.OfflineOnly)
        {
            return ToolResult.Failed(
                CreateError("Internet access is disabled. The operator has set the app to offline-only mode."),
                TimeSpan.FromMilliseconds(1));
        }

        // Check per-tool permissions
        var permission = await _permissions.CheckPermissionAsync(endpoint, purpose, ct);
        if (!permission.Allowed)
        {
            return ToolResult.Failed(
                CreateError($"Internet access denied: {permission.Reason}"),
                TimeSpan.FromMilliseconds(1));
        }

        // Create the intent declaration for audit
        var intent = $"Purpose: {purpose}. Expected data: {expectedData}. Retention: {retention}";

        // Create the network request
        var request = new NetworkRequest(
            Endpoint: endpoint,
            Method: method.ToUpperInvariant(),
            Intent: intent,
            DataSent: dataSent,
            RequestedAt: DateTimeOffset.UtcNow
        );

        // Execute the request
        var response = await _connectivity.RequestAsync(request, ct);
        var duration = DateTimeOffset.UtcNow - startTime;

        if (response == null)
        {
            return ToolResult.Failed(
                CreateError("Request blocked by connectivity policy"),
                duration);
        }

        if (!response.IsSuccess)
        {
            return ToolResult.Failed(
                CreateError($"Request failed: {response.Error ?? "Unknown error"} (HTTP {response.StatusCode})"),
                duration);
        }

        return ToolResult.Succeeded(response.Data ?? "Request completed successfully", duration);
    }

    private static InControlError CreateError(string message) => new()
    {
        Code = ErrorCode.ToolExecutionFailed,
        Message = message
    };
}

/// <summary>
/// Permissions checker for internet tool usage.
/// </summary>
public interface IInternetToolPermissions
{
    Task<InternetPermissionResult> CheckPermissionAsync(
        string endpoint,
        string purpose,
        CancellationToken ct = default);
}

/// <summary>
/// Result of an internet permission check.
/// </summary>
public sealed record InternetPermissionResult(
    bool Allowed,
    string? Reason
);

/// <summary>
/// Default implementation of internet tool permissions.
/// Uses a simple allow/deny list with operator approval.
/// </summary>
public sealed class InternetToolPermissions : IInternetToolPermissions
{
    private readonly Dictionary<string, PermissionRule> _rules = new();
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when permission is requested for an unknown endpoint.
    /// </summary>
    public event EventHandler<InternetPermissionRequestEventArgs>? PermissionRequested;

    public Task<InternetPermissionResult> CheckPermissionAsync(
        string endpoint,
        string purpose,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            // Check for matching rules
            foreach (var (pattern, rule) in _rules)
            {
                if (MatchesPattern(endpoint, pattern))
                {
                    return Task.FromResult(new InternetPermissionResult(
                        rule.Permission == ToolPermission.AlwaysAllow,
                        rule.Permission == ToolPermission.AlwaysAllow
                            ? null
                            : $"Endpoint denied by rule: {pattern}"
                    ));
                }
            }

            // No matching rule - need operator approval
            PermissionRequested?.Invoke(this, new InternetPermissionRequestEventArgs(endpoint, purpose));

            return Task.FromResult(new InternetPermissionResult(
                false,
                "Endpoint not in approved list. Operator approval required."
            ));
        }
    }

    /// <summary>
    /// Adds a permission rule for an endpoint pattern.
    /// </summary>
    public void SetRule(string endpointPattern, ToolPermission permission, string? notes = null)
    {
        lock (_lock)
        {
            _rules[endpointPattern] = new PermissionRule(permission, notes);
        }
    }

    /// <summary>
    /// Removes a permission rule.
    /// </summary>
    public void RemoveRule(string endpointPattern)
    {
        lock (_lock)
        {
            _rules.Remove(endpointPattern);
        }
    }

    /// <summary>
    /// Gets all permission rules.
    /// </summary>
    public IReadOnlyDictionary<string, PermissionRule> GetRules()
    {
        lock (_lock)
        {
            return _rules.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    /// <summary>
    /// Clears all permission rules.
    /// </summary>
    public void ClearRules()
    {
        lock (_lock)
        {
            _rules.Clear();
        }
    }

    private static bool MatchesPattern(string endpoint, string pattern)
    {
        // Simple prefix matching
        // Pattern: "https://api.example.com" matches "https://api.example.com/users"
        return endpoint.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A permission rule for an endpoint.
/// </summary>
public sealed record PermissionRule(
    ToolPermission Permission,
    string? Notes
);

/// <summary>
/// Event args for permission requests.
/// </summary>
public sealed class InternetPermissionRequestEventArgs : EventArgs
{
    public string Endpoint { get; }
    public string Purpose { get; }

    public InternetPermissionRequestEventArgs(string endpoint, string purpose)
    {
        Endpoint = endpoint;
        Purpose = purpose;
    }
}

/// <summary>
/// Declaration of what an internet tool does.
/// Must be provided for every tool that uses the network.
/// </summary>
public sealed record InternetToolDeclaration(
    string ToolId,
    IReadOnlyList<string> Endpoints,
    IReadOnlyList<string> DataSent,
    IReadOnlyList<string> DataReceived,
    string RetentionPolicy
)
{
    /// <summary>
    /// Formats the declaration for display to the operator.
    /// </summary>
    public string ToDisplayString()
    {
        var lines = new List<string>
        {
            $"Tool: {ToolId}",
            "",
            "Endpoints accessed:",
        };

        foreach (var endpoint in Endpoints)
        {
            lines.Add($"  - {endpoint}");
        }

        lines.Add("");
        lines.Add("Data sent:");
        foreach (var data in DataSent)
        {
            lines.Add($"  - {data}");
        }

        lines.Add("");
        lines.Add("Data received:");
        foreach (var data in DataReceived)
        {
            lines.Add($"  - {data}");
        }

        lines.Add("");
        lines.Add($"Retention: {RetentionPolicy}");

        return string.Join(Environment.NewLine, lines);
    }
}
