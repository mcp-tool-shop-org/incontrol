using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace InControl.Core.Policy;

/// <summary>
/// A complete policy document that can be loaded from JSON.
/// Versioned schema with explicit rules for all governance categories.
/// </summary>
public sealed record PolicyDocument
{
    /// <summary>
    /// Schema version for backward compatibility.
    /// Format: major.minor (e.g., "1.0")
    /// </summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = "https://incontrol.dev/schemas/policy/v1.0";

    /// <summary>
    /// Policy format version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Optional policy identifier for tracking.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Human-readable name for this policy.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Description of what this policy enforces.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// When this policy was created.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTimeOffset? Created { get; init; }

    /// <summary>
    /// When this policy was last modified.
    /// </summary>
    [JsonPropertyName("modified")]
    public DateTimeOffset? Modified { get; init; }

    /// <summary>
    /// If true, lower-precedence policies cannot override rules in this document.
    /// Typically used by organization policies.
    /// </summary>
    [JsonPropertyName("locked")]
    public bool Locked { get; init; }

    /// <summary>
    /// Tool usage rules.
    /// </summary>
    [JsonPropertyName("tools")]
    public ToolPolicyRules? Tools { get; init; }

    /// <summary>
    /// Plugin loading and execution rules.
    /// </summary>
    [JsonPropertyName("plugins")]
    public PluginPolicyRules? Plugins { get; init; }

    /// <summary>
    /// Memory and retention rules.
    /// </summary>
    [JsonPropertyName("memory")]
    public MemoryPolicyRules? Memory { get; init; }

    /// <summary>
    /// Connectivity mode rules.
    /// </summary>
    [JsonPropertyName("connectivity")]
    public ConnectivityPolicyRules? Connectivity { get; init; }

    /// <summary>
    /// Update and release channel rules.
    /// </summary>
    [JsonPropertyName("updates")]
    public UpdatePolicyRules? Updates { get; init; }
}

#region Tool Policy Rules

/// <summary>
/// Rules governing tool usage.
/// </summary>
public sealed record ToolPolicyRules
{
    /// <summary>
    /// Default behavior for tools not explicitly listed.
    /// </summary>
    [JsonPropertyName("default")]
    public PolicyDecision Default { get; init; } = PolicyDecision.Allow;

    /// <summary>
    /// Tools that are explicitly allowed.
    /// </summary>
    [JsonPropertyName("allow")]
    public List<string>? Allow { get; init; }

    /// <summary>
    /// Tools that are explicitly denied.
    /// </summary>
    [JsonPropertyName("deny")]
    public List<string>? Deny { get; init; }

    /// <summary>
    /// Tools that require operator approval.
    /// </summary>
    [JsonPropertyName("requireApproval")]
    public List<string>? RequireApproval { get; init; }

    /// <summary>
    /// Detailed rules with conditions and constraints.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<ToolRule>? Rules { get; init; }
}

/// <summary>
/// A detailed tool rule with optional conditions and constraints.
/// </summary>
public sealed record ToolRule
{
    /// <summary>
    /// Unique identifier for this rule.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Tool ID pattern (supports * wildcards).
    /// </summary>
    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    /// <summary>
    /// The decision for matching tools.
    /// </summary>
    [JsonPropertyName("decision")]
    public required PolicyDecision Decision { get; init; }

    /// <summary>
    /// Human-readable explanation shown to operators.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Constraints applied when decision is AllowWithConstraints.
    /// </summary>
    [JsonPropertyName("constraints")]
    public Dictionary<string, object>? Constraints { get; init; }

    /// <summary>
    /// Optional conditions that must be met for this rule to apply.
    /// </summary>
    [JsonPropertyName("conditions")]
    public RuleConditions? Conditions { get; init; }
}

#endregion

#region Plugin Policy Rules

/// <summary>
/// Rules governing plugin loading and execution.
/// </summary>
public sealed record PluginPolicyRules
{
    /// <summary>
    /// Whether plugins are enabled at all.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Default behavior for plugins not explicitly listed.
    /// </summary>
    [JsonPropertyName("default")]
    public PolicyDecision Default { get; init; } = PolicyDecision.AllowWithApproval;

    /// <summary>
    /// Maximum allowed risk level for plugins.
    /// </summary>
    [JsonPropertyName("maxRiskLevel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PluginRiskLevelPolicy MaxRiskLevel { get; init; } = PluginRiskLevelPolicy.Network;

    /// <summary>
    /// Plugin IDs that are explicitly allowed.
    /// </summary>
    [JsonPropertyName("allow")]
    public List<string>? Allow { get; init; }

    /// <summary>
    /// Plugin IDs that are explicitly denied.
    /// </summary>
    [JsonPropertyName("deny")]
    public List<string>? Deny { get; init; }

    /// <summary>
    /// Trusted plugin authors (plugins from these authors are auto-approved).
    /// </summary>
    [JsonPropertyName("trustedAuthors")]
    public List<string>? TrustedAuthors { get; init; }

    /// <summary>
    /// Detailed rules for plugins.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<PluginRule>? Rules { get; init; }
}

/// <summary>
/// Risk level limits for plugin policy (mirrors PluginRiskLevel).
/// </summary>
public enum PluginRiskLevelPolicy
{
    /// <summary>
    /// Only read-only plugins allowed.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Plugins that modify local state allowed.
    /// </summary>
    LocalMutation,

    /// <summary>
    /// Network-accessing plugins allowed.
    /// </summary>
    Network
}

/// <summary>
/// A detailed plugin rule.
/// </summary>
public sealed record PluginRule
{
    /// <summary>
    /// Unique identifier for this rule.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Plugin ID pattern (supports * wildcards).
    /// </summary>
    [JsonPropertyName("plugin")]
    public required string Plugin { get; init; }

    /// <summary>
    /// The decision for matching plugins.
    /// </summary>
    [JsonPropertyName("decision")]
    public required PolicyDecision Decision { get; init; }

    /// <summary>
    /// Human-readable explanation.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

#endregion

#region Memory Policy Rules

/// <summary>
/// Rules governing memory and retention.
/// </summary>
public sealed record MemoryPolicyRules
{
    /// <summary>
    /// Whether memory is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Maximum retention period in days (0 = unlimited).
    /// </summary>
    [JsonPropertyName("maxRetentionDays")]
    public int MaxRetentionDays { get; init; } = 0;

    /// <summary>
    /// Maximum number of memories to store.
    /// </summary>
    [JsonPropertyName("maxMemories")]
    public int MaxMemories { get; init; } = 10000;

    /// <summary>
    /// Whether to encrypt memory at rest.
    /// </summary>
    [JsonPropertyName("encryptAtRest")]
    public bool EncryptAtRest { get; init; } = true;

    /// <summary>
    /// Whether automatic memory formation is allowed.
    /// </summary>
    [JsonPropertyName("autoFormation")]
    public bool AutoFormation { get; init; } = true;

    /// <summary>
    /// Categories of content that should not be memorized.
    /// </summary>
    [JsonPropertyName("excludeCategories")]
    public List<string>? ExcludeCategories { get; init; }

    /// <summary>
    /// Whether memory export is allowed.
    /// </summary>
    [JsonPropertyName("allowExport")]
    public bool AllowExport { get; init; } = true;

    /// <summary>
    /// Whether memory import is allowed.
    /// </summary>
    [JsonPropertyName("allowImport")]
    public bool AllowImport { get; init; } = true;
}

#endregion

#region Connectivity Policy Rules

/// <summary>
/// Rules governing connectivity modes.
/// </summary>
public sealed record ConnectivityPolicyRules
{
    /// <summary>
    /// Allowed connectivity modes.
    /// </summary>
    [JsonPropertyName("allowedModes")]
    public List<string>? AllowedModes { get; init; }

    /// <summary>
    /// The default connectivity mode.
    /// </summary>
    [JsonPropertyName("defaultMode")]
    public string? DefaultMode { get; init; }

    /// <summary>
    /// Whether operators can change connectivity mode.
    /// </summary>
    [JsonPropertyName("allowModeChange")]
    public bool AllowModeChange { get; init; } = true;

    /// <summary>
    /// Allowed outbound domains (for network modes).
    /// Empty = all allowed. Null = use defaults.
    /// </summary>
    [JsonPropertyName("allowedDomains")]
    public List<string>? AllowedDomains { get; init; }

    /// <summary>
    /// Blocked outbound domains.
    /// </summary>
    [JsonPropertyName("blockedDomains")]
    public List<string>? BlockedDomains { get; init; }

    /// <summary>
    /// Whether telemetry is allowed.
    /// </summary>
    [JsonPropertyName("allowTelemetry")]
    public bool AllowTelemetry { get; init; } = true;
}

#endregion

#region Update Policy Rules

/// <summary>
/// Rules governing updates and release channels.
/// </summary>
public sealed record UpdatePolicyRules
{
    /// <summary>
    /// Whether automatic updates are enabled.
    /// </summary>
    [JsonPropertyName("autoUpdate")]
    public bool AutoUpdate { get; init; } = true;

    /// <summary>
    /// Allowed release channels.
    /// </summary>
    [JsonPropertyName("allowedChannels")]
    public List<string>? AllowedChannels { get; init; }

    /// <summary>
    /// The required release channel.
    /// </summary>
    [JsonPropertyName("requiredChannel")]
    public string? RequiredChannel { get; init; }

    /// <summary>
    /// Whether to defer updates by days.
    /// </summary>
    [JsonPropertyName("deferDays")]
    public int DeferDays { get; init; } = 0;

    /// <summary>
    /// Whether to check for updates on startup.
    /// </summary>
    [JsonPropertyName("checkOnStartup")]
    public bool CheckOnStartup { get; init; } = true;

    /// <summary>
    /// Minimum version required.
    /// </summary>
    [JsonPropertyName("minimumVersion")]
    public string? MinimumVersion { get; init; }
}

#endregion

#region Rule Conditions

/// <summary>
/// Conditions that must be met for a rule to apply.
/// </summary>
public sealed record RuleConditions
{
    /// <summary>
    /// Required connectivity mode.
    /// </summary>
    [JsonPropertyName("connectivityMode")]
    public string? ConnectivityMode { get; init; }

    /// <summary>
    /// Time of day range when rule applies (e.g., "09:00-17:00").
    /// </summary>
    [JsonPropertyName("timeRange")]
    public string? TimeRange { get; init; }

    /// <summary>
    /// Days of week when rule applies (0=Sunday).
    /// </summary>
    [JsonPropertyName("daysOfWeek")]
    public List<int>? DaysOfWeek { get; init; }
}

#endregion

#region Policy Serialization

/// <summary>
/// JSON serialization options for policy documents.
/// </summary>
public static class PolicyJsonOptions
{
    /// <summary>
    /// Default options for reading policy files.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
}

/// <summary>
/// Policy document loader and serializer.
/// </summary>
public static class PolicySerializer
{
    /// <summary>
    /// Loads a policy document from a file path.
    /// </summary>
    public static async Task<PolicyLoadResult> LoadFromFileAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(path))
            {
                return PolicyLoadResult.NotFound(path);
            }

            var json = await File.ReadAllTextAsync(path, ct);
            return LoadFromJson(json, path);
        }
        catch (UnauthorizedAccessException)
        {
            return PolicyLoadResult.Failed(path, "Access denied");
        }
        catch (IOException ex)
        {
            return PolicyLoadResult.Failed(path, $"IO error: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads a policy document from JSON string.
    /// </summary>
    public static PolicyLoadResult LoadFromJson(string json, string? sourcePath = null)
    {
        try
        {
            var document = JsonSerializer.Deserialize<PolicyDocument>(json, PolicyJsonOptions.Default);
            if (document == null)
            {
                return PolicyLoadResult.Failed(sourcePath, "Deserialized to null");
            }

            // Validate the document
            var validation = PolicyValidator.Validate(document);
            if (!validation.IsValid)
            {
                return PolicyLoadResult.Invalid(sourcePath, validation.Errors);
            }

            return PolicyLoadResult.Success(document, sourcePath);
        }
        catch (JsonException ex)
        {
            return PolicyLoadResult.Failed(sourcePath, $"JSON parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Serializes a policy document to JSON.
    /// </summary>
    public static string ToJson(PolicyDocument document)
    {
        return JsonSerializer.Serialize(document, PolicyJsonOptions.Default);
    }

    /// <summary>
    /// Saves a policy document to a file.
    /// </summary>
    public static async Task SaveToFileAsync(PolicyDocument document, string path, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = ToJson(document);
        await File.WriteAllTextAsync(path, json, ct);
    }
}

/// <summary>
/// Result of loading a policy document.
/// </summary>
public sealed record PolicyLoadResult
{
    /// <summary>
    /// Whether loading succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The loaded document if successful.
    /// </summary>
    public PolicyDocument? Document { get; init; }

    /// <summary>
    /// Path the policy was loaded from.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Error message if loading failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Validation errors if document was invalid.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    /// <summary>
    /// Whether the file was not found (vs other errors).
    /// </summary>
    public bool FileNotFound { get; init; }

    public static PolicyLoadResult Success(PolicyDocument document, string? sourcePath) =>
        new() { IsSuccess = true, Document = document, SourcePath = sourcePath };

    public static PolicyLoadResult NotFound(string? path) =>
        new() { IsSuccess = false, SourcePath = path, FileNotFound = true, Error = "File not found" };

    public static PolicyLoadResult Failed(string? path, string error) =>
        new() { IsSuccess = false, SourcePath = path, Error = error };

    public static PolicyLoadResult Invalid(string? path, IReadOnlyList<string> errors) =>
        new() { IsSuccess = false, SourcePath = path, Error = "Validation failed", ValidationErrors = errors };
}

#endregion

#region Policy Validation

/// <summary>
/// Validates policy documents.
/// </summary>
public static class PolicyValidator
{
    private static readonly Regex VersionRegex = new(@"^\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex RuleIdRegex = new(@"^[a-z0-9][a-z0-9._-]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates a policy document.
    /// </summary>
    public static PolicyValidationResult Validate(PolicyDocument document)
    {
        var errors = new List<string>();

        // Version validation
        if (string.IsNullOrWhiteSpace(document.Version))
        {
            errors.Add("Version is required");
        }
        else if (!VersionRegex.IsMatch(document.Version))
        {
            errors.Add($"Invalid version format: {document.Version} (expected X.Y)");
        }

        // Validate tool rules
        if (document.Tools?.Rules != null)
        {
            ValidateToolRules(document.Tools.Rules, errors);
        }

        // Validate plugin rules
        if (document.Plugins?.Rules != null)
        {
            ValidatePluginRules(document.Plugins.Rules, errors);
        }

        // Validate memory rules
        if (document.Memory != null)
        {
            ValidateMemoryRules(document.Memory, errors);
        }

        // Validate connectivity rules
        if (document.Connectivity != null)
        {
            ValidateConnectivityRules(document.Connectivity, errors);
        }

        // Validate update rules
        if (document.Updates != null)
        {
            ValidateUpdateRules(document.Updates, errors);
        }

        // Check for conflicting rules
        ValidateNoConflicts(document, errors);

        return new PolicyValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateToolRules(List<ToolRule> rules, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                errors.Add("Tool rule missing ID");
            }
            else if (!RuleIdRegex.IsMatch(rule.Id))
            {
                errors.Add($"Invalid tool rule ID format: {rule.Id}");
            }
            else if (!ids.Add(rule.Id))
            {
                errors.Add($"Duplicate tool rule ID: {rule.Id}");
            }

            if (string.IsNullOrWhiteSpace(rule.Tool))
            {
                errors.Add($"Tool rule {rule.Id} missing tool pattern");
            }

            if (rule.Decision == PolicyDecision.AllowWithConstraints && (rule.Constraints == null || rule.Constraints.Count == 0))
            {
                errors.Add($"Tool rule {rule.Id} has AllowWithConstraints but no constraints defined");
            }
        }
    }

    private static void ValidatePluginRules(List<PluginRule> rules, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                errors.Add("Plugin rule missing ID");
            }
            else if (!RuleIdRegex.IsMatch(rule.Id))
            {
                errors.Add($"Invalid plugin rule ID format: {rule.Id}");
            }
            else if (!ids.Add(rule.Id))
            {
                errors.Add($"Duplicate plugin rule ID: {rule.Id}");
            }

            if (string.IsNullOrWhiteSpace(rule.Plugin))
            {
                errors.Add($"Plugin rule {rule.Id} missing plugin pattern");
            }
        }
    }

    private static void ValidateMemoryRules(MemoryPolicyRules memory, List<string> errors)
    {
        if (memory.MaxRetentionDays < 0)
        {
            errors.Add("Memory maxRetentionDays cannot be negative");
        }

        if (memory.MaxMemories < 0)
        {
            errors.Add("Memory maxMemories cannot be negative");
        }
    }

    private static void ValidateConnectivityRules(ConnectivityPolicyRules connectivity, List<string> errors)
    {
        var validModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "online", "offline", "local", "hybrid"
        };

        if (connectivity.AllowedModes != null)
        {
            foreach (var mode in connectivity.AllowedModes)
            {
                if (!validModes.Contains(mode))
                {
                    errors.Add($"Invalid connectivity mode: {mode}");
                }
            }
        }

        if (connectivity.DefaultMode != null && !validModes.Contains(connectivity.DefaultMode))
        {
            errors.Add($"Invalid default connectivity mode: {connectivity.DefaultMode}");
        }
    }

    private static void ValidateUpdateRules(UpdatePolicyRules updates, List<string> errors)
    {
        var validChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "stable", "beta", "dev", "canary"
        };

        if (updates.AllowedChannels != null)
        {
            foreach (var channel in updates.AllowedChannels)
            {
                if (!validChannels.Contains(channel))
                {
                    errors.Add($"Invalid update channel: {channel}");
                }
            }
        }

        if (updates.RequiredChannel != null && !validChannels.Contains(updates.RequiredChannel))
        {
            errors.Add($"Invalid required update channel: {updates.RequiredChannel}");
        }

        if (updates.DeferDays < 0 || updates.DeferDays > 365)
        {
            errors.Add($"Invalid deferDays: {updates.DeferDays} (must be 0-365)");
        }

        if (updates.MinimumVersion != null && !System.Version.TryParse(updates.MinimumVersion, out _))
        {
            errors.Add($"Invalid minimumVersion format: {updates.MinimumVersion}");
        }
    }

    private static void ValidateNoConflicts(PolicyDocument document, List<string> errors)
    {
        // Check for tool ID appearing in both allow and deny
        if (document.Tools?.Allow != null && document.Tools?.Deny != null)
        {
            var conflicts = document.Tools.Allow.Intersect(document.Tools.Deny, StringComparer.OrdinalIgnoreCase);
            foreach (var conflict in conflicts)
            {
                errors.Add($"Tool '{conflict}' appears in both allow and deny lists");
            }
        }

        // Check for plugin ID appearing in both allow and deny
        if (document.Plugins?.Allow != null && document.Plugins?.Deny != null)
        {
            var conflicts = document.Plugins.Allow.Intersect(document.Plugins.Deny, StringComparer.OrdinalIgnoreCase);
            foreach (var conflict in conflicts)
            {
                errors.Add($"Plugin '{conflict}' appears in both allow and deny lists");
            }
        }

        // Check for domain appearing in both allowed and blocked
        if (document.Connectivity?.AllowedDomains != null && document.Connectivity?.BlockedDomains != null)
        {
            var conflicts = document.Connectivity.AllowedDomains.Intersect(document.Connectivity.BlockedDomains, StringComparer.OrdinalIgnoreCase);
            foreach (var conflict in conflicts)
            {
                errors.Add($"Domain '{conflict}' appears in both allowed and blocked lists");
            }
        }
    }
}

/// <summary>
/// Result of policy validation.
/// </summary>
public sealed record PolicyValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PolicyValidationResult Valid { get; } = new(true, []);
}

#endregion
