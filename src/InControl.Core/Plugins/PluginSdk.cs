namespace InControl.Core.Plugins;

/// <summary>
/// Base class for implementing plugins.
/// Provides common functionality and lifecycle management.
/// </summary>
public abstract class PluginBase : IPluginInstance, IAsyncDisposable
{
    private IPluginContext? _context;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Gets the plugin context after initialization.
    /// </summary>
    protected IPluginContext Context => _context ?? throw new InvalidOperationException("Plugin not initialized");

    /// <summary>
    /// Gets whether the plugin has been initialized.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Gets the plugin manifest.
    /// </summary>
    public PluginManifest Manifest => Context.Manifest;

    /// <summary>
    /// Gets the file access API.
    /// </summary>
    protected IPluginFileAccess Files => Context.Files;

    /// <summary>
    /// Gets the network access API.
    /// </summary>
    protected IPluginNetworkAccess Network => Context.Network;

    /// <summary>
    /// Gets the memory access API.
    /// </summary>
    protected IPluginMemoryAccess Memory => Context.Memory;

    /// <summary>
    /// Gets the plugin storage API.
    /// </summary>
    protected IPluginStorage Storage => Context.Storage;

    /// <inheritdoc />
    public IReadOnlyList<PluginCapability> GetCapabilities() => Manifest.Capabilities;

    /// <inheritdoc />
    public async Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_initialized)
        {
            throw new InvalidOperationException("Plugin already initialized");
        }

        _context = context;
        await OnInitializeAsync(ct);
        _initialized = true;
    }

    /// <inheritdoc />
    public async Task<PluginActionResult> ExecuteAsync(
        string actionId,
        IReadOnlyDictionary<string, object?> parameters,
        IPluginContext context,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            return PluginActionResult.Failed("Plugin not initialized");
        }

        try
        {
            return await OnExecuteAsync(actionId, parameters, ct);
        }
        catch (OperationCanceledException)
        {
            return PluginActionResult.Failed("Operation cancelled");
        }
        catch (Exception ex)
        {
            return PluginActionResult.Failed($"Execution error: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when the plugin is initialized.
    /// Override to perform setup tasks.
    /// </summary>
    protected virtual Task OnInitializeAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Called when an action is executed.
    /// Override to handle plugin actions.
    /// </summary>
    protected abstract Task<PluginActionResult> OnExecuteAsync(
        string actionId,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken ct);

    /// <summary>
    /// Called when the plugin is being disposed.
    /// Override to perform cleanup tasks.
    /// </summary>
    protected virtual ValueTask OnDisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await OnDisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Builder for creating plugin manifests programmatically.
/// </summary>
public sealed class PluginManifestBuilder
{
    private string _id = "";
    private string _name = "";
    private string _version = "";
    private string _author = "";
    private string _description = "";
    private string? _minAppVersion;
    private string? _homepage;
    private string? _license;
    private string? _entryPoint;
    private string? _icon;
    private PluginRiskLevel _riskLevel = PluginRiskLevel.ReadOnly;
    private NetworkIntent? _networkIntent;
    private readonly List<PluginPermission> _permissions = [];
    private readonly List<PluginCapability> _capabilities = [];

    /// <summary>
    /// Sets the plugin ID.
    /// </summary>
    public PluginManifestBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the plugin name.
    /// </summary>
    public PluginManifestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the plugin version.
    /// </summary>
    public PluginManifestBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the plugin author.
    /// </summary>
    public PluginManifestBuilder WithAuthor(string author)
    {
        _author = author;
        return this;
    }

    /// <summary>
    /// Sets the plugin description.
    /// </summary>
    public PluginManifestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the minimum app version required.
    /// </summary>
    public PluginManifestBuilder WithMinAppVersion(string version)
    {
        _minAppVersion = version;
        return this;
    }

    /// <summary>
    /// Sets the plugin homepage.
    /// </summary>
    public PluginManifestBuilder WithHomepage(string url)
    {
        _homepage = url;
        return this;
    }

    /// <summary>
    /// Sets the plugin license.
    /// </summary>
    public PluginManifestBuilder WithLicense(string license)
    {
        _license = license;
        return this;
    }

    /// <summary>
    /// Sets the entry point assembly.
    /// </summary>
    public PluginManifestBuilder WithEntryPoint(string entryPoint)
    {
        _entryPoint = entryPoint;
        return this;
    }

    /// <summary>
    /// Sets the plugin icon path.
    /// </summary>
    public PluginManifestBuilder WithIcon(string iconPath)
    {
        _icon = iconPath;
        return this;
    }

    /// <summary>
    /// Sets the risk level.
    /// </summary>
    public PluginManifestBuilder WithRiskLevel(PluginRiskLevel level)
    {
        _riskLevel = level;
        return this;
    }

    /// <summary>
    /// Adds a file permission.
    /// </summary>
    public PluginManifestBuilder AddFilePermission(string scope, PermissionAccess access, string reason)
    {
        _permissions.Add(new PluginPermission
        {
            Type = PermissionType.File,
            Access = access,
            Scope = scope,
            Reason = reason
        });
        return this;
    }

    /// <summary>
    /// Adds a network permission with intent.
    /// </summary>
    public PluginManifestBuilder AddNetworkPermission(string endpoint, string description, IEnumerable<string> dataTypes)
    {
        _permissions.Add(new PluginPermission
        {
            Type = PermissionType.Network,
            Access = PermissionAccess.Read,
            Scope = endpoint,
            Reason = description
        });

        _networkIntent = new NetworkIntent
        {
            Endpoints = [endpoint],
            DataSent = dataTypes.ToList(),
            DataReceived = []
        };

        return this;
    }

    /// <summary>
    /// Adds a memory permission.
    /// </summary>
    public PluginManifestBuilder AddMemoryPermission(PermissionAccess access, string reason)
    {
        _permissions.Add(new PluginPermission
        {
            Type = PermissionType.Memory,
            Access = access,
            Reason = reason
        });
        return this;
    }

    /// <summary>
    /// Adds a capability (tool).
    /// </summary>
    public PluginManifestBuilder AddCapability(
        string toolId,
        string name,
        string description,
        bool modifiesState = false,
        IEnumerable<PluginCapabilityParameter>? parameters = null)
    {
        _capabilities.Add(new PluginCapability
        {
            ToolId = toolId,
            Name = name,
            Description = description,
            ModifiesState = modifiesState,
            Parameters = parameters?.ToList() ?? []
        });
        return this;
    }

    /// <summary>
    /// Builds the manifest.
    /// </summary>
    public PluginManifest Build()
    {
        var manifest = new PluginManifest
        {
            Id = _id,
            Name = _name,
            Version = _version,
            Author = _author,
            Description = _description,
            MinAppVersion = _minAppVersion,
            Homepage = _homepage,
            License = _license,
            EntryPoint = _entryPoint,
            IconPath = _icon,
            RiskLevel = _riskLevel,
            NetworkIntent = _networkIntent,
            Permissions = _permissions.ToList(),
            Capabilities = _capabilities.ToList()
        };

        // Validate before returning
        var validator = new ManifestValidator();
        var result = validator.Validate(manifest);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid manifest: {string.Join(", ", result.Errors)}");
        }

        return manifest;
    }
}

/// <summary>
/// Helper for creating capability parameters.
/// </summary>
public static class CapabilityParameter
{
    /// <summary>
    /// Creates a required string parameter.
    /// </summary>
    public static PluginCapabilityParameter RequiredString(string name, string description) =>
        new()
        {
            Name = name,
            Description = description,
            Type = "string",
            Required = true
        };

    /// <summary>
    /// Creates an optional string parameter.
    /// </summary>
    public static PluginCapabilityParameter OptionalString(string name, string description, string? defaultValue = null) =>
        new()
        {
            Name = name,
            Description = description,
            Type = "string",
            Required = false,
            Default = defaultValue
        };

    /// <summary>
    /// Creates a required number parameter.
    /// </summary>
    public static PluginCapabilityParameter RequiredNumber(string name, string description) =>
        new()
        {
            Name = name,
            Description = description,
            Type = "number",
            Required = true
        };

    /// <summary>
    /// Creates an optional number parameter.
    /// </summary>
    public static PluginCapabilityParameter OptionalNumber(string name, string description, double? defaultValue = null) =>
        new()
        {
            Name = name,
            Description = description,
            Type = "number",
            Required = false,
            Default = defaultValue
        };

    /// <summary>
    /// Creates a required boolean parameter.
    /// </summary>
    public static PluginCapabilityParameter RequiredBool(string name, string description) =>
        new()
        {
            Name = name,
            Description = description,
            Type = "boolean",
            Required = true
        };

    /// <summary>
    /// Creates an optional boolean parameter.
    /// </summary>
    public static PluginCapabilityParameter OptionalBool(string name, string description, bool defaultValue = false) =>
        new()
        {
            Name = name,
            Description = description,
            Type = "boolean",
            Required = false,
            Default = defaultValue
        };

    /// <summary>
    /// Creates an enum parameter with allowed values.
    /// </summary>
    public static PluginCapabilityParameter Enum(string name, string description, IEnumerable<string> allowedValues, bool required = true) =>
        new()
        {
            Name = name,
            Description = description,
            Type = "string",
            Required = required,
            Enum = allowedValues.ToList()
        };
}

/// <summary>
/// Test doubles for plugin development.
/// </summary>
public static class PluginTestHelpers
{
    /// <summary>
    /// Creates a mock plugin context for testing.
    /// </summary>
    public static IPluginContext CreateTestContext(PluginManifest manifest) =>
        new TestPluginContext(manifest);

    /// <summary>
    /// Creates a minimal test manifest.
    /// </summary>
    public static PluginManifest CreateTestManifest(string id = "test-plugin", string version = "1.0.0") =>
        new PluginManifestBuilder()
            .WithId(id)
            .WithName($"Test Plugin: {id}")
            .WithVersion(version)
            .WithAuthor("Test Author")
            .WithDescription("A plugin created for testing")
            .WithRiskLevel(PluginRiskLevel.ReadOnly)
            .Build();

    private sealed class TestPluginContext : IPluginContext
    {
        public PluginManifest Manifest { get; }
        public IPluginFileAccess Files { get; }
        public IPluginNetworkAccess Network { get; }
        public IPluginMemoryAccess Memory { get; }
        public IPluginStorage Storage { get; }

        public TestPluginContext(PluginManifest manifest)
        {
            Manifest = manifest;
            Files = new TestFileAccess();
            Network = new TestNetworkAccess();
            Memory = new TestMemoryAccess();
            Storage = new TestStorage();
        }

        public bool HasPermission(PermissionType type, PermissionAccess access, string? scope = null)
        {
            // Test context always grants permissions
            return true;
        }

        public void Dispose() { }
    }

    private sealed class TestFileAccess : IPluginFileAccess
    {
        private readonly Dictionary<string, string> _files = new();

        public bool IsPathPermitted(string path, PermissionAccess access) => true;

        public Task<PluginFileResult> ReadAsync(string path, CancellationToken ct = default)
        {
            if (_files.TryGetValue(path, out var content))
                return Task.FromResult(PluginFileResult.Succeeded(content));
            return Task.FromResult(PluginFileResult.Failed("File not found"));
        }

        public Task<PluginFileResult> WriteAsync(string path, string content, CancellationToken ct = default)
        {
            _files[path] = content;
            return Task.FromResult(PluginFileResult.Succeeded(null));
        }

        public Task<PluginFileListResult> ListAsync(string path, CancellationToken ct = default) =>
            Task.FromResult(PluginFileListResult.Succeeded(_files.Keys.ToList()));
    }

    private sealed class TestNetworkAccess : IPluginNetworkAccess
    {
        public bool IsAvailable => true;
        public bool IsEndpointPermitted(string endpoint) => true;

        public Task<PluginNetworkResult> RequestAsync(
            string endpoint, string method, string? body, string intent, CancellationToken ct = default) =>
            Task.FromResult(PluginNetworkResult.Succeeded(200, "{}"));
    }

    private sealed class TestMemoryAccess : IPluginMemoryAccess
    {
        private readonly Dictionary<string, string> _store = new();

        public Task<bool> StoreAsync(string key, string content, CancellationToken ct = default)
        {
            _store[key] = content;
            return Task.FromResult(true);
        }

        public Task<string?> RetrieveAsync(string key, CancellationToken ct = default)
        {
            _store.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task<IReadOnlyList<string>> SearchAsync(string query, int limit = 10, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(_store.Values.Where(v => v.Contains(query)).Take(limit).ToList());
    }

    private sealed class TestStorage : IPluginStorage
    {
        private readonly Dictionary<string, string> _store = new();

        public Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
        {
            _store.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task<bool> RemoveAsync(string key, CancellationToken ct = default)
        {
            return Task.FromResult(_store.Remove(key));
        }

        public Task<IReadOnlyList<string>> ListKeysAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(_store.Keys.ToList());

        public Task ClearAsync(CancellationToken ct = default)
        {
            _store.Clear();
            return Task.CompletedTask;
        }
    }
}
