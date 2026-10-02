using System.Text.Json;
using System.Text.Json.Serialization;
using InControl.Core.Errors;

namespace InControl.Core.Plugins;

/// <summary>
/// Complete manifest describing a plugin's identity, capabilities, and requirements.
/// Every plugin must provide a valid manifest to load.
/// </summary>
public sealed record PluginManifest
{
    /// <summary>
    /// Unique identifier for the plugin (e.g., "com.example.weather-tool").
    /// Must be lowercase alphanumeric with dots and hyphens only.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Semantic version string (e.g., "1.0.0").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Human-readable name for display.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Plugin author or organization.
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// Description of what the plugin does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Minimum InControl version required.
    /// </summary>
    public string? MinAppVersion { get; init; }

    /// <summary>
    /// Plugin homepage or repository URL.
    /// </summary>
    public string? Homepage { get; init; }

    /// <summary>
    /// License identifier (e.g., "MIT", "Apache-2.0").
    /// </summary>
    public string? License { get; init; }

    /// <summary>
    /// Permissions required by this plugin.
    /// </summary>
    public IReadOnlyList<PluginPermission> Permissions { get; init; } = [];

    /// <summary>
    /// Capabilities (tools) provided by this plugin.
    /// </summary>
    public IReadOnlyList<PluginCapability> Capabilities { get; init; } = [];

    /// <summary>
    /// Risk level classification.
    /// </summary>
    public PluginRiskLevel RiskLevel { get; init; } = PluginRiskLevel.ReadOnly;

    /// <summary>
    /// Network intent declaration (required if requesting network permission).
    /// </summary>
    public NetworkIntent? NetworkIntent { get; init; }

    /// <summary>
    /// Entry point assembly name within the plugin package.
    /// </summary>
    public string? EntryPoint { get; init; }

    /// <summary>
    /// Plugin icon path relative to package root.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Tags for categorization and search.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// A specific permission requested by a plugin.
/// </summary>
public sealed record PluginPermission
{
    /// <summary>
    /// Permission type (file, memory, network, ui).
    /// </summary>
    public required PermissionType Type { get; init; }

    /// <summary>
    /// Access level (read, write, execute).
    /// </summary>
    public required PermissionAccess Access { get; init; }

    /// <summary>
    /// Resource scope (e.g., path pattern, endpoint pattern).
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Human-readable reason for this permission.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Whether this permission is optional (plugin works without it).
    /// </summary>
    public bool Optional { get; init; }

    /// <summary>
    /// Creates a display-friendly string for this permission.
    /// </summary>
    public string ToDisplayString()
    {
        var access = Access.ToString().ToLowerInvariant();
        var scope = string.IsNullOrEmpty(Scope) ? "" : $" ({Scope})";
        return $"{Type}:{access}{scope}";
    }
}

/// <summary>
/// Types of permissions a plugin can request.
/// </summary>
public enum PermissionType
{
    /// <summary>
    /// Access to local files through FileStore.
    /// </summary>
    File,

    /// <summary>
    /// Access to assistant memory system.
    /// </summary>
    Memory,

    /// <summary>
    /// Network access through ConnectivityManager.
    /// </summary>
    Network,

    /// <summary>
    /// Ability to display UI panels.
    /// </summary>
    UI,

    /// <summary>
    /// Access to conversation context.
    /// </summary>
    Conversation,

    /// <summary>
    /// Access to settings (read-only).
    /// </summary>
    Settings
}

/// <summary>
/// Access levels for permissions.
/// </summary>
public enum PermissionAccess
{
    /// <summary>
    /// Read-only access.
    /// </summary>
    Read,

    /// <summary>
    /// Read and write access.
    /// </summary>
    Write,

    /// <summary>
    /// Execute/invoke access.
    /// </summary>
    Execute
}

/// <summary>
/// A capability (tool) provided by a plugin.
/// </summary>
public sealed record PluginCapability
{
    /// <summary>
    /// Tool identifier (must be unique within the plugin).
    /// </summary>
    public required string ToolId { get; init; }

    /// <summary>
    /// Human-readable tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what the tool does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Whether this tool requires network access.
    /// </summary>
    public bool RequiresNetwork { get; init; }

    /// <summary>
    /// Whether this tool modifies local state.
    /// </summary>
    public bool ModifiesState { get; init; }

    /// <summary>
    /// Parameters accepted by this capability.
    /// </summary>
    public IReadOnlyList<PluginCapabilityParameter> Parameters { get; init; } = [];
}

/// <summary>
/// Parameter definition for a plugin capability.
/// </summary>
public sealed record PluginCapabilityParameter
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parameter description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Parameter type (string, number, boolean, object, array).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Default value if not provided.
    /// </summary>
    public object? Default { get; init; }

    /// <summary>
    /// Allowed values for enum-style parameters.
    /// </summary>
    public IReadOnlyList<string>? Enum { get; init; }
}

/// <summary>
/// Risk level classification for plugins.
/// </summary>
public enum PluginRiskLevel
{
    /// <summary>
    /// Level 1: Read-only operations, no side effects.
    /// </summary>
    ReadOnly = 1,

    /// <summary>
    /// Level 2: Can modify local data (files, memory).
    /// </summary>
    LocalMutation = 2,

    /// <summary>
    /// Level 3: Can access network through ConnectivityManager.
    /// </summary>
    Network = 3,

    /// <summary>
    /// Level 4: System-adjacent (reserved, not available in Phase 8).
    /// </summary>
    SystemAdjacent = 4
}

/// <summary>
/// Declaration of network intent for transparency.
/// Required for any plugin requesting network permissions.
/// </summary>
public sealed record NetworkIntent
{
    /// <summary>
    /// Specific endpoints this plugin will access.
    /// </summary>
    public required IReadOnlyList<string> Endpoints { get; init; }

    /// <summary>
    /// Description of data sent to these endpoints.
    /// </summary>
    public IReadOnlyList<string> DataSent { get; init; } = [];

    /// <summary>
    /// Description of data received from these endpoints.
    /// </summary>
    public IReadOnlyList<string> DataReceived { get; init; } = [];

    /// <summary>
    /// Data retention policy.
    /// </summary>
    public string Retention { get; init; } = "session";

    /// <summary>
    /// Purpose of network access.
    /// </summary>
    public string? Purpose { get; init; }
}

/// <summary>
/// Result of manifest validation.
/// </summary>
public sealed record ManifestValidationResult
{
    /// <summary>
    /// Whether the manifest is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors found.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings (non-fatal).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ManifestValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ManifestValidationResult Failed(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };

    /// <summary>
    /// Creates a result with warnings but still valid.
    /// </summary>
    public static ManifestValidationResult SuccessWithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = warnings
    };
}

/// <summary>
/// Validates plugin manifests against the extensibility charter rules.
/// </summary>
public sealed class ManifestValidator
{
    private static readonly char[] ValidIdChars = "abcdefghijklmnopqrstuvwxyz0123456789.-".ToCharArray();

    /// <summary>
    /// Validates a plugin manifest.
    /// </summary>
    public ManifestValidationResult Validate(PluginManifest manifest)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (string.IsNullOrWhiteSpace(manifest.Id))
            errors.Add("Plugin ID is required");
        else if (!IsValidId(manifest.Id))
            errors.Add("Plugin ID must be lowercase alphanumeric with dots and hyphens only");

        if (string.IsNullOrWhiteSpace(manifest.Version))
            errors.Add("Version is required");
        else if (!IsValidVersion(manifest.Version))
            errors.Add("Version must be a valid semantic version (e.g., 1.0.0)");

        if (string.IsNullOrWhiteSpace(manifest.Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(manifest.Author))
            errors.Add("Author is required");

        if (string.IsNullOrWhiteSpace(manifest.Description))
            errors.Add("Description is required");

        // Permission validation
        ValidatePermissions(manifest, errors, warnings);

        // Capability validation
        ValidateCapabilities(manifest, errors, warnings);

        // Risk level consistency
        ValidateRiskLevel(manifest, errors, warnings);

        // Network intent validation
        ValidateNetworkIntent(manifest, errors, warnings);

        if (errors.Count > 0)
            return new ManifestValidationResult { IsValid = false, Errors = errors, Warnings = warnings };

        if (warnings.Count > 0)
            return new ManifestValidationResult { IsValid = true, Warnings = warnings };

        return ManifestValidationResult.Success();
    }

    private static bool IsValidId(string id)
    {
        return !string.IsNullOrEmpty(id) &&
               id.All(c => ValidIdChars.Contains(c)) &&
               !id.StartsWith('.') &&
               !id.EndsWith('.') &&
               !id.Contains("..");
    }

    private static bool IsValidVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length < 2 || parts.Length > 4)
            return false;

        return parts.All(p => int.TryParse(p.Split('-')[0], out _));
    }

    private void ValidatePermissions(PluginManifest manifest, List<string> errors, List<string> warnings)
    {
        var seenPermissions = new HashSet<string>();

        foreach (var permission in manifest.Permissions)
        {
            var key = $"{permission.Type}:{permission.Access}:{permission.Scope}";
            if (!seenPermissions.Add(key))
            {
                warnings.Add($"Duplicate permission: {permission.ToDisplayString()}");
                continue;
            }

            // File permissions need scope
            if (permission.Type == PermissionType.File && string.IsNullOrEmpty(permission.Scope))
            {
                errors.Add("File permissions require a scope (path pattern)");
            }

            // Network permissions need scope
            if (permission.Type == PermissionType.Network && string.IsNullOrEmpty(permission.Scope))
            {
                errors.Add("Network permissions require a scope (endpoint pattern)");
            }

            // No wildcards for sensitive permissions
            if (permission.Type == PermissionType.File &&
                permission.Access == PermissionAccess.Write &&
                permission.Scope == "*")
            {
                errors.Add("Wildcard write access to files is not permitted");
            }

            // Reason recommended for non-obvious permissions
            if (permission.Type is PermissionType.Network or PermissionType.File &&
                permission.Access == PermissionAccess.Write &&
                string.IsNullOrEmpty(permission.Reason))
            {
                warnings.Add($"Permission '{permission.ToDisplayString()}' should include a reason");
            }
        }
    }

    private void ValidateCapabilities(PluginManifest manifest, List<string> errors, List<string> warnings)
    {
        var seenTools = new HashSet<string>();

        foreach (var capability in manifest.Capabilities)
        {
            if (string.IsNullOrWhiteSpace(capability.ToolId))
            {
                errors.Add("Capability tool ID is required");
                continue;
            }

            if (!seenTools.Add(capability.ToolId))
            {
                errors.Add($"Duplicate capability tool ID: {capability.ToolId}");
            }

            if (string.IsNullOrWhiteSpace(capability.Name))
            {
                errors.Add($"Capability '{capability.ToolId}' requires a name");
            }

            if (string.IsNullOrWhiteSpace(capability.Description))
            {
                warnings.Add($"Capability '{capability.ToolId}' should have a description");
            }

            // Network-requiring tools need network permission
            if (capability.RequiresNetwork)
            {
                var hasNetworkPermission = manifest.Permissions
                    .Any(p => p.Type == PermissionType.Network);

                if (!hasNetworkPermission)
                {
                    errors.Add($"Capability '{capability.ToolId}' requires network but no network permission declared");
                }
            }

            // State-modifying tools need appropriate permission
            if (capability.ModifiesState)
            {
                var hasWritePermission = manifest.Permissions
                    .Any(p => p.Access is PermissionAccess.Write or PermissionAccess.Execute);

                if (!hasWritePermission)
                {
                    errors.Add($"Capability '{capability.ToolId}' modifies state but no write permission declared");
                }
            }
        }

        if (manifest.Capabilities.Count == 0)
        {
            warnings.Add("Plugin declares no capabilities");
        }
    }

    private void ValidateRiskLevel(PluginManifest manifest, List<string> errors, List<string> warnings)
    {
        var calculatedRisk = CalculateMinimumRiskLevel(manifest);

        if (manifest.RiskLevel < calculatedRisk)
        {
            errors.Add($"Declared risk level ({manifest.RiskLevel}) is lower than required by permissions ({calculatedRisk})");
        }

        if (manifest.RiskLevel > calculatedRisk)
        {
            warnings.Add($"Declared risk level ({manifest.RiskLevel}) is higher than necessary ({calculatedRisk})");
        }

        if (manifest.RiskLevel == PluginRiskLevel.SystemAdjacent)
        {
            errors.Add("SystemAdjacent risk level is not available in Phase 8");
        }
    }

    private static PluginRiskLevel CalculateMinimumRiskLevel(PluginManifest manifest)
    {
        var hasNetwork = manifest.Permissions.Any(p => p.Type == PermissionType.Network);
        var hasWrite = manifest.Permissions.Any(p => p.Access is PermissionAccess.Write or PermissionAccess.Execute);

        if (hasNetwork) return PluginRiskLevel.Network;
        if (hasWrite) return PluginRiskLevel.LocalMutation;
        return PluginRiskLevel.ReadOnly;
    }

    private void ValidateNetworkIntent(PluginManifest manifest, List<string> errors, List<string> warnings)
    {
        var hasNetworkPermission = manifest.Permissions.Any(p => p.Type == PermissionType.Network);

        if (hasNetworkPermission && manifest.NetworkIntent == null)
        {
            errors.Add("Network permission requires NetworkIntent declaration");
        }

        if (!hasNetworkPermission && manifest.NetworkIntent != null)
        {
            warnings.Add("NetworkIntent declared but no network permission requested");
        }

        if (manifest.NetworkIntent != null)
        {
            if (manifest.NetworkIntent.Endpoints.Count == 0)
            {
                errors.Add("NetworkIntent must declare at least one endpoint");
            }

            foreach (var endpoint in manifest.NetworkIntent.Endpoints)
            {
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                {
                    errors.Add($"Invalid endpoint URL: {endpoint}");
                }
                else if (uri.Scheme != "https")
                {
                    warnings.Add($"Endpoint '{endpoint}' uses non-HTTPS scheme");
                }
            }

            // Verify network permissions match intent
            var permittedEndpoints = manifest.Permissions
                .Where(p => p.Type == PermissionType.Network)
                .Select(p => p.Scope)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet();

            foreach (var endpoint in manifest.NetworkIntent.Endpoints)
            {
                var baseUrl = GetBaseUrl(endpoint);
                if (!permittedEndpoints.Any(p => endpoint.StartsWith(p!) || baseUrl == p))
                {
                    errors.Add($"NetworkIntent endpoint '{endpoint}' not covered by network permissions");
                }
            }
        }
    }

    private static string GetBaseUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Host}";
        }
        return url;
    }
}

/// <summary>
/// Serialization helpers for plugin manifests.
/// </summary>
public static class ManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    /// <summary>
    /// Serializes a manifest to JSON.
    /// </summary>
    public static string Serialize(PluginManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, Options);
    }

    /// <summary>
    /// Deserializes a manifest from JSON.
    /// </summary>
    public static PluginManifest? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<PluginManifest>(json, Options);
    }

    /// <summary>
    /// Tries to deserialize a manifest from JSON.
    /// </summary>
    public static bool TryDeserialize(string json, out PluginManifest? manifest, out string? error)
    {
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(json, Options);
            error = null;
            return manifest != null;
        }
        catch (JsonException ex)
        {
            manifest = null;
            error = ex.Message;
            return false;
        }
    }
}
