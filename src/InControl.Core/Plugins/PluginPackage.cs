using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InControl.Core.Plugins;

/// <summary>
/// Plugin package format (.icplugin) for distribution.
/// Structure:
///   manifest.json   - Plugin manifest
///   plugin.dll      - Main assembly (optional, for compiled plugins)
///   assets/         - Static assets (icons, templates, etc.)
///   README.md       - Documentation (optional)
///   LICENSE         - License file (required)
///   SIGNATURE       - Package signature (optional, for trusted sources)
/// </summary>
public sealed class PluginPackage
{
    /// <summary>
    /// File extension for plugin packages.
    /// </summary>
    public const string Extension = ".icplugin";

    /// <summary>
    /// Shared JSON options for manifest serialization.
    /// </summary>
    internal static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Maximum package size (50 MB).
    /// </summary>
    public const long MaxPackageSize = 50 * 1024 * 1024;

    /// <summary>
    /// Required files in a valid package.
    /// </summary>
    private static readonly string[] RequiredFiles = ["manifest.json", "LICENSE"];

    /// <summary>
    /// Forbidden file patterns (security).
    /// </summary>
    private static readonly string[] ForbiddenPatterns = [
        "*.exe", "*.bat", "*.cmd", "*.ps1", "*.vbs", "*.js",
        "*.msi", "*.msp", "*.com", "*.scr", "*.pif"
    ];

    public PluginManifest Manifest { get; }
    public string PackagePath { get; }
    public long PackageSize { get; }
    public string PackageHash { get; }
    public bool HasSignature { get; }
    public IReadOnlyList<string> Contents { get; }

    private PluginPackage(
        PluginManifest manifest,
        string packagePath,
        long packageSize,
        string packageHash,
        bool hasSignature,
        IReadOnlyList<string> contents)
    {
        Manifest = manifest;
        PackagePath = packagePath;
        PackageSize = packageSize;
        PackageHash = packageHash;
        HasSignature = hasSignature;
        Contents = contents;
    }

    /// <summary>
    /// Opens and validates a plugin package.
    /// </summary>
    public static async Task<PluginPackageResult> OpenAsync(string packagePath, CancellationToken ct = default)
    {
        if (!File.Exists(packagePath))
        {
            return PluginPackageResult.Failed($"Package not found: {packagePath}");
        }

        if (!packagePath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return PluginPackageResult.Failed($"Invalid package extension. Expected: {Extension}");
        }

        var fileInfo = new FileInfo(packagePath);
        if (fileInfo.Length > MaxPackageSize)
        {
            return PluginPackageResult.Failed($"Package exceeds maximum size of {MaxPackageSize / 1024 / 1024} MB");
        }

        try
        {
            // Compute package hash
            var hash = await ComputeHashAsync(packagePath, ct);

            using var archive = ZipFile.OpenRead(packagePath);

            // Get contents list
            var contents = archive.Entries.Select(e => e.FullName).ToList();

            // Check for required files
            foreach (var required in RequiredFiles)
            {
                if (!contents.Any(c => c.Equals(required, StringComparison.OrdinalIgnoreCase)))
                {
                    return PluginPackageResult.Failed($"Missing required file: {required}");
                }
            }

            // Check for forbidden files
            foreach (var entry in contents)
            {
                foreach (var pattern in ForbiddenPatterns)
                {
                    if (MatchesPattern(entry, pattern))
                    {
                        return PluginPackageResult.Failed($"Forbidden file type: {entry}");
                    }
                }
            }

            // Read and validate manifest
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry == null)
            {
                return PluginPackageResult.Failed("Missing manifest.json");
            }

            PluginManifest manifest;
            using (var stream = manifestEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync(ct);
                var parsed = JsonSerializer.Deserialize<PluginManifest>(json, ManifestJsonOptions);

                if (parsed == null)
                {
                    return PluginPackageResult.Failed("Failed to parse manifest.json");
                }

                manifest = parsed;
            }

            // Validate manifest
            var validator = new ManifestValidator();
            var validation = validator.Validate(manifest);
            if (!validation.IsValid)
            {
                return PluginPackageResult.Failed($"Invalid manifest: {string.Join(", ", validation.Errors)}");
            }

            // Check for signature
            var hasSignature = contents.Any(c => c.Equals("SIGNATURE", StringComparison.OrdinalIgnoreCase));

            var package = new PluginPackage(
                manifest,
                packagePath,
                fileInfo.Length,
                hash,
                hasSignature,
                contents);

            return PluginPackageResult.Succeeded(package);
        }
        catch (InvalidDataException)
        {
            return PluginPackageResult.Failed("Invalid or corrupted package archive");
        }
        catch (Exception ex)
        {
            return PluginPackageResult.Failed($"Failed to open package: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the package to a directory.
    /// </summary>
    public async Task<PluginInstallResult> ExtractToAsync(string targetDirectory, CancellationToken ct = default)
    {
        try
        {
            var pluginDirectory = Path.Combine(targetDirectory, Manifest.Id);

            // Check if already installed
            if (Directory.Exists(pluginDirectory))
            {
                // Check version
                var existingManifestPath = Path.Combine(pluginDirectory, "manifest.json");
                if (File.Exists(existingManifestPath))
                {
                    var existingJson = await File.ReadAllTextAsync(existingManifestPath, ct);
                    var existing = JsonSerializer.Deserialize<PluginManifest>(existingJson, ManifestJsonOptions);

                    if (existing != null && existing.Version == Manifest.Version)
                    {
                        return PluginInstallResult.AlreadyInstalled(Manifest.Id, Manifest.Version);
                    }
                }

                // Remove old version
                Directory.Delete(pluginDirectory, recursive: true);
            }

            // Create directory
            Directory.CreateDirectory(pluginDirectory);

            // Extract
            ZipFile.ExtractToDirectory(PackagePath, pluginDirectory);

            return PluginInstallResult.Succeeded(Manifest.Id, Manifest.Version, pluginDirectory);
        }
        catch (Exception ex)
        {
            return PluginInstallResult.Failed(Manifest.Id, $"Extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a plugin package from a directory.
    /// </summary>
    public static async Task<PluginPackageResult> CreateAsync(
        string sourceDirectory,
        string outputPath,
        CancellationToken ct = default)
    {
        // Validate source directory
        if (!Directory.Exists(sourceDirectory))
        {
            return PluginPackageResult.Failed($"Source directory not found: {sourceDirectory}");
        }

        var manifestPath = Path.Combine(sourceDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return PluginPackageResult.Failed("Source directory must contain manifest.json");
        }

        var licensePath = Path.Combine(sourceDirectory, "LICENSE");
        if (!File.Exists(licensePath))
        {
            return PluginPackageResult.Failed("Source directory must contain LICENSE file");
        }

        try
        {
            // Read and validate manifest
            var json = await File.ReadAllTextAsync(manifestPath, ct);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, ManifestJsonOptions);

            if (manifest == null)
            {
                return PluginPackageResult.Failed("Failed to parse manifest.json");
            }

            var validator = new ManifestValidator();
            var validation = validator.Validate(manifest);
            if (!validation.IsValid)
            {
                return PluginPackageResult.Failed($"Invalid manifest: {string.Join(", ", validation.Errors)}");
            }

            // Check for forbidden files
            var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                foreach (var pattern in ForbiddenPatterns)
                {
                    if (MatchesPattern(relativePath, pattern))
                    {
                        return PluginPackageResult.Failed($"Forbidden file type: {relativePath}");
                    }
                }
            }

            // Ensure output has correct extension
            if (!outputPath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            {
                outputPath = $"{outputPath}{Extension}";
            }

            // Delete existing file
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            // Create package
            ZipFile.CreateFromDirectory(sourceDirectory, outputPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            // Open and return the created package
            return await OpenAsync(outputPath, ct);
        }
        catch (Exception ex)
        {
            return PluginPackageResult.Failed($"Failed to create package: {ex.Message}");
        }
    }

    private static async Task<string> ComputeHashAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern.StartsWith("*."))
        {
            var extension = pattern[1..]; // Remove the *
            return fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }
        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Result of opening a plugin package.
/// </summary>
public sealed record PluginPackageResult(
    bool Success,
    PluginPackage? Package,
    string? Error
)
{
    public static PluginPackageResult Succeeded(PluginPackage package) =>
        new(true, package, null);

    public static PluginPackageResult Failed(string error) =>
        new(false, null, error);
}

/// <summary>
/// Result of installing a plugin.
/// </summary>
public sealed record PluginInstallResult(
    string PluginId,
    bool Success,
    bool WasAlreadyInstalled,
    string? Version,
    string? InstallPath,
    string? Error
)
{
    public static PluginInstallResult Succeeded(string pluginId, string version, string installPath) =>
        new(pluginId, true, false, version, installPath, null);

    public static PluginInstallResult AlreadyInstalled(string pluginId, string version) =>
        new(pluginId, true, true, version, null, null);

    public static PluginInstallResult Failed(string pluginId, string error) =>
        new(pluginId, false, false, null, null, error);
}

/// <summary>
/// Information about an installed plugin.
/// </summary>
public sealed record InstalledPluginInfo(
    string PluginId,
    string Version,
    string InstallPath,
    DateTimeOffset InstalledAt,
    string? PackageHash
);

/// <summary>
/// Manages plugin installation and registry.
/// </summary>
public sealed class PluginInstaller
{
    private readonly string _pluginsDirectory;
    private readonly string _registryPath;
    private readonly Dictionary<string, InstalledPluginInfo> _installedPlugins = new();
    private readonly object _lock = new();

    public PluginInstaller(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        _registryPath = Path.Combine(pluginsDirectory, "registry.json");

        Directory.CreateDirectory(pluginsDirectory);
        LoadRegistry();
    }

    /// <summary>
    /// Gets all installed plugins.
    /// </summary>
    public IReadOnlyList<InstalledPluginInfo> InstalledPlugins
    {
        get
        {
            lock (_lock)
            {
                return _installedPlugins.Values.ToList();
            }
        }
    }

    /// <summary>
    /// Checks if a plugin is installed.
    /// </summary>
    public bool IsInstalled(string pluginId)
    {
        lock (_lock)
        {
            return _installedPlugins.ContainsKey(pluginId);
        }
    }

    /// <summary>
    /// Gets installed plugin info.
    /// </summary>
    public InstalledPluginInfo? GetInstalled(string pluginId)
    {
        lock (_lock)
        {
            return _installedPlugins.TryGetValue(pluginId, out var info) ? info : null;
        }
    }

    /// <summary>
    /// Installs a plugin from a package.
    /// </summary>
    public async Task<PluginInstallResult> InstallAsync(PluginPackage package, CancellationToken ct = default)
    {
        var result = await package.ExtractToAsync(_pluginsDirectory, ct);

        if (result.Success && !result.WasAlreadyInstalled)
        {
            var info = new InstalledPluginInfo(
                package.Manifest.Id,
                package.Manifest.Version,
                result.InstallPath!,
                DateTimeOffset.UtcNow,
                package.PackageHash);

            lock (_lock)
            {
                _installedPlugins[package.Manifest.Id] = info;
            }

            await SaveRegistryAsync(ct);
        }

        return result;
    }

    /// <summary>
    /// Uninstalls a plugin.
    /// </summary>
    public async Task<bool> UninstallAsync(string pluginId, CancellationToken ct = default)
    {
        InstalledPluginInfo? info;
        lock (_lock)
        {
            if (!_installedPlugins.TryGetValue(pluginId, out info))
            {
                return false;
            }

            _installedPlugins.Remove(pluginId);
        }

        try
        {
            if (Directory.Exists(info.InstallPath))
            {
                Directory.Delete(info.InstallPath, recursive: true);
            }

            await SaveRegistryAsync(ct);
            return true;
        }
        catch
        {
            // Restore registry entry on failure
            lock (_lock)
            {
                _installedPlugins[pluginId] = info;
            }
            return false;
        }
    }

    /// <summary>
    /// Loads the manifest for an installed plugin.
    /// </summary>
    public async Task<PluginManifest?> LoadManifestAsync(string pluginId, CancellationToken ct = default)
    {
        InstalledPluginInfo? info;
        lock (_lock)
        {
            if (!_installedPlugins.TryGetValue(pluginId, out info))
            {
                return null;
            }
        }

        var manifestPath = Path.Combine(info.InstallPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        return JsonSerializer.Deserialize<PluginManifest>(json, PluginPackage.ManifestJsonOptions);
    }

    private void LoadRegistry()
    {
        if (!File.Exists(_registryPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_registryPath);
            var entries = JsonSerializer.Deserialize<List<InstalledPluginInfo>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries != null)
            {
                lock (_lock)
                {
                    foreach (var entry in entries)
                    {
                        // Verify installation still exists
                        if (Directory.Exists(entry.InstallPath))
                        {
                            _installedPlugins[entry.PluginId] = entry;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore corrupt registry - will rebuild
        }
    }

    private async Task SaveRegistryAsync(CancellationToken ct)
    {
        List<InstalledPluginInfo> entries;
        lock (_lock)
        {
            entries = _installedPlugins.Values.ToList();
        }

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_registryPath, json, ct);
    }
}
