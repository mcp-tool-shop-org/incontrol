using System.IO.Compression;
using System.Text.Json;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// Tests for plugin packaging and installation.
/// </summary>
public class PluginPackageTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _pluginsDir;

    public PluginPackageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PluginPackageTests_{Guid.NewGuid():N}");
        _pluginsDir = Path.Combine(_testDir, "plugins");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_pluginsDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region Package Opening Tests

    [Fact]
    public async Task OpenAsync_ValidPackage_Succeeds()
    {
        var packagePath = await CreateTestPackageAsync("test-plugin", "1.0.0");

        var result = await PluginPackage.OpenAsync(packagePath);

        Assert.True(result.Success);
        Assert.NotNull(result.Package);
        Assert.Equal("test-plugin", result.Package.Manifest.Id);
        Assert.Equal("1.0.0", result.Package.Manifest.Version);
        Assert.NotEmpty(result.Package.PackageHash);
    }

    [Fact]
    public async Task OpenAsync_MissingFile_Fails()
    {
        var result = await PluginPackage.OpenAsync("/nonexistent/package.icplugin");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task OpenAsync_WrongExtension_Fails()
    {
        var wrongPath = Path.Combine(_testDir, "test.zip");
        File.WriteAllText(wrongPath, "test");

        var result = await PluginPackage.OpenAsync(wrongPath);

        Assert.False(result.Success);
        Assert.Contains("extension", result.Error);
    }

    [Fact]
    public async Task OpenAsync_MissingManifest_Fails()
    {
        var packagePath = Path.Combine(_testDir, "no-manifest.icplugin");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            var licenseEntry = archive.CreateEntry("LICENSE");
            using var writer = new StreamWriter(licenseEntry.Open());
            writer.Write("MIT License");
        }

        var result = await PluginPackage.OpenAsync(packagePath);

        Assert.False(result.Success);
        Assert.Contains("manifest", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenAsync_MissingLicense_Fails()
    {
        var packagePath = Path.Combine(_testDir, "no-license.icplugin");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifestEntry.Open());
            writer.Write(CreateManifestJson("test", "1.0.0"));
        }

        var result = await PluginPackage.OpenAsync(packagePath);

        Assert.False(result.Success);
        Assert.Contains("LICENSE", result.Error);
    }

    [Fact]
    public async Task OpenAsync_ForbiddenFileType_Fails()
    {
        var packagePath = Path.Combine(_testDir, "dangerous.icplugin");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            AddManifestAndLicense(archive, "test", "1.0.0");

            // Add forbidden file
            var exeEntry = archive.CreateEntry("malware.exe");
            using var writer = new StreamWriter(exeEntry.Open());
            writer.Write("bad stuff");
        }

        var result = await PluginPackage.OpenAsync(packagePath);

        Assert.False(result.Success);
        Assert.Contains("Forbidden", result.Error);
    }

    [Fact]
    public async Task OpenAsync_InvalidManifest_Fails()
    {
        var packagePath = Path.Combine(_testDir, "bad-manifest.icplugin");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                // Invalid: id is empty string
                writer.Write("{ \"id\": \"\", \"name\": \"Bad\", \"version\": \"1.0.0\", \"author\": \"Test\", \"description\": \"Test\", \"riskLevel\": \"ReadOnly\" }");
            }

            var licenseEntry = archive.CreateEntry("LICENSE");
            using (var licenseWriter = new StreamWriter(licenseEntry.Open()))
            {
                licenseWriter.Write("MIT License");
            }
        }

        var result = await PluginPackage.OpenAsync(packagePath);

        Assert.False(result.Success);
        // Fails on either parse failure or validation
        Assert.NotNull(result.Error);
        Assert.True(result.Error.Contains("manifest", StringComparison.OrdinalIgnoreCase) ||
                    result.Error.Contains("parse", StringComparison.OrdinalIgnoreCase) ||
                    result.Error.Contains("Invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenAsync_CorruptedArchive_Fails()
    {
        var packagePath = Path.Combine(_testDir, "corrupt.icplugin");
        await File.WriteAllTextAsync(packagePath, "not a zip file");

        var result = await PluginPackage.OpenAsync(packagePath);

        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Error);
    }

    [Fact]
    public async Task OpenAsync_WithSignature_DetectsSignature()
    {
        var packagePath = Path.Combine(_testDir, "signed.icplugin");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            AddManifestAndLicense(archive, "signed-plugin", "1.0.0");

            var signatureEntry = archive.CreateEntry("SIGNATURE");
            using var writer = new StreamWriter(signatureEntry.Open());
            writer.Write("signature-data");
        }

        var result = await PluginPackage.OpenAsync(packagePath);

        Assert.True(result.Success);
        Assert.True(result.Package!.HasSignature);
    }

    #endregion

    #region Package Creation Tests

    [Fact]
    public async Task CreateAsync_ValidSource_Succeeds()
    {
        var sourceDir = await CreateSourceDirectoryAsync("create-test", "2.0.0");
        var outputPath = Path.Combine(_testDir, "output.icplugin");

        var result = await PluginPackage.CreateAsync(sourceDir, outputPath);

        Assert.True(result.Success);
        Assert.NotNull(result.Package);
        Assert.True(File.Exists(outputPath));
        Assert.Equal("create-test", result.Package.Manifest.Id);
    }

    [Fact]
    public async Task CreateAsync_MissingSource_Fails()
    {
        var result = await PluginPackage.CreateAsync("/nonexistent", "output.icplugin");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        // Directory does not exist, so we get the "not found" or "must contain" error
    }

    [Fact]
    public async Task CreateAsync_MissingManifest_Fails()
    {
        var sourceDir = Path.Combine(_testDir, "no-manifest-source");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "LICENSE"), "MIT");

        var result = await PluginPackage.CreateAsync(sourceDir, "output.icplugin");

        Assert.False(result.Success);
        Assert.Contains("manifest", result.Error);
    }

    [Fact]
    public async Task CreateAsync_MissingLicense_Fails()
    {
        var sourceDir = Path.Combine(_testDir, "no-license-source");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(
            Path.Combine(sourceDir, "manifest.json"),
            CreateManifestJson("test", "1.0.0"));

        var result = await PluginPackage.CreateAsync(sourceDir, "output.icplugin");

        Assert.False(result.Success);
        Assert.Contains("LICENSE", result.Error);
    }

    [Fact]
    public async Task CreateAsync_ForbiddenFile_Fails()
    {
        var sourceDir = await CreateSourceDirectoryAsync("forbidden-test", "1.0.0");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "script.bat"), "echo bad");

        var result = await PluginPackage.CreateAsync(sourceDir, "output.icplugin");

        Assert.False(result.Success);
        Assert.Contains("Forbidden", result.Error);
    }

    [Fact]
    public async Task CreateAsync_AddsExtensionIfMissing()
    {
        var sourceDir = await CreateSourceDirectoryAsync("ext-test", "1.0.0");
        var outputPath = Path.Combine(_testDir, "no-ext");

        var result = await PluginPackage.CreateAsync(sourceDir, outputPath);

        Assert.True(result.Success);
        Assert.True(File.Exists(outputPath + PluginPackage.Extension));
    }

    #endregion

    #region Installation Tests

    [Fact]
    public async Task ExtractToAsync_InstallsPlugin()
    {
        var packagePath = await CreateTestPackageAsync("install-test", "1.0.0");
        var packageResult = await PluginPackage.OpenAsync(packagePath);
        Assert.True(packageResult.Success);

        var result = await packageResult.Package!.ExtractToAsync(_pluginsDir);

        Assert.True(result.Success);
        Assert.False(result.WasAlreadyInstalled);
        Assert.Equal("install-test", result.PluginId);
        Assert.NotNull(result.InstallPath);
        Assert.True(Directory.Exists(result.InstallPath));
        Assert.True(File.Exists(Path.Combine(result.InstallPath, "manifest.json")));
    }

    [Fact]
    public async Task ExtractToAsync_SameVersion_ReportsAlreadyInstalled()
    {
        var packagePath = await CreateTestPackageAsync("already-test", "1.0.0");
        var packageResult = await PluginPackage.OpenAsync(packagePath);

        // Install first time
        var result1 = await packageResult.Package!.ExtractToAsync(_pluginsDir);
        Assert.True(result1.Success);
        Assert.False(result1.WasAlreadyInstalled);

        // Install again
        var result2 = await packageResult.Package!.ExtractToAsync(_pluginsDir);
        Assert.True(result2.Success);
        Assert.True(result2.WasAlreadyInstalled);
    }

    [Fact]
    public async Task ExtractToAsync_NewerVersion_Upgrades()
    {
        // Install v1.0.0
        var package1Path = await CreateTestPackageAsync("upgrade-test", "1.0.0");
        var package1Result = await PluginPackage.OpenAsync(package1Path);
        await package1Result.Package!.ExtractToAsync(_pluginsDir);

        // Install v2.0.0
        var package2Path = await CreateTestPackageAsync("upgrade-test", "2.0.0");
        var package2Result = await PluginPackage.OpenAsync(package2Path);
        var result = await package2Result.Package!.ExtractToAsync(_pluginsDir);

        Assert.True(result.Success);
        Assert.False(result.WasAlreadyInstalled);
        Assert.Equal("2.0.0", result.Version);
    }

    #endregion

    #region PluginInstaller Tests

    [Fact]
    public async Task Installer_InstallAsync_RegistersPlugin()
    {
        var installer = new PluginInstaller(_pluginsDir);
        var packagePath = await CreateTestPackageAsync("installer-test", "1.0.0");
        var packageResult = await PluginPackage.OpenAsync(packagePath);

        var result = await installer.InstallAsync(packageResult.Package!);

        Assert.True(result.Success);
        Assert.True(installer.IsInstalled("installer-test"));
        Assert.NotNull(installer.GetInstalled("installer-test"));
    }

    [Fact]
    public async Task Installer_UninstallAsync_RemovesPlugin()
    {
        var installer = new PluginInstaller(_pluginsDir);
        var packagePath = await CreateTestPackageAsync("uninstall-test", "1.0.0");
        var packageResult = await PluginPackage.OpenAsync(packagePath);
        await installer.InstallAsync(packageResult.Package!);

        var success = await installer.UninstallAsync("uninstall-test");

        Assert.True(success);
        Assert.False(installer.IsInstalled("uninstall-test"));
    }

    [Fact]
    public async Task Installer_LoadManifestAsync_ReturnsManifest()
    {
        var installer = new PluginInstaller(_pluginsDir);
        var packagePath = await CreateTestPackageAsync("manifest-test", "1.5.0");
        var packageResult = await PluginPackage.OpenAsync(packagePath);
        await installer.InstallAsync(packageResult.Package!);

        var manifest = await installer.LoadManifestAsync("manifest-test");

        Assert.NotNull(manifest);
        Assert.Equal("manifest-test", manifest.Id);
        Assert.Equal("1.5.0", manifest.Version);
    }

    [Fact]
    public async Task Installer_PersistsRegistry()
    {
        // Install with first installer instance
        var installer1 = new PluginInstaller(_pluginsDir);
        var packagePath = await CreateTestPackageAsync("persist-test", "1.0.0");
        var packageResult = await PluginPackage.OpenAsync(packagePath);
        await installer1.InstallAsync(packageResult.Package!);

        // Create new installer instance - should load from registry
        var installer2 = new PluginInstaller(_pluginsDir);

        Assert.True(installer2.IsInstalled("persist-test"));
        var info = installer2.GetInstalled("persist-test");
        Assert.NotNull(info);
        Assert.Equal("1.0.0", info.Version);
    }

    [Fact]
    public void Installer_InstalledPlugins_ReturnsAll()
    {
        var installer = new PluginInstaller(_pluginsDir);

        var plugins = installer.InstalledPlugins;

        Assert.NotNull(plugins);
        // Initially empty
        Assert.Empty(plugins);
    }

    #endregion

    #region Helper Methods

    private async Task<string> CreateTestPackageAsync(string pluginId, string version)
    {
        var packagePath = Path.Combine(_testDir, $"{pluginId}-{version}.icplugin");

        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            AddManifestAndLicense(archive, pluginId, version);

            // Add a README
            var readmeEntry = archive.CreateEntry("README.md");
            using var readmeWriter = new StreamWriter(readmeEntry.Open());
            await readmeWriter.WriteAsync($"# {pluginId}\n\nVersion {version}");
        }

        return packagePath;
    }

    private async Task<string> CreateSourceDirectoryAsync(string pluginId, string version)
    {
        var sourceDir = Path.Combine(_testDir, $"source-{pluginId}");
        Directory.CreateDirectory(sourceDir);

        await File.WriteAllTextAsync(
            Path.Combine(sourceDir, "manifest.json"),
            CreateManifestJson(pluginId, version));

        await File.WriteAllTextAsync(
            Path.Combine(sourceDir, "LICENSE"),
            "MIT License\n\nCopyright (c) 2025 Test");

        await File.WriteAllTextAsync(
            Path.Combine(sourceDir, "README.md"),
            $"# {pluginId}\n\nTest plugin.");

        return sourceDir;
    }

    private void AddManifestAndLicense(ZipArchive archive, string pluginId, string version)
    {
        var manifestEntry = archive.CreateEntry("manifest.json");
        using (var writer = new StreamWriter(manifestEntry.Open()))
        {
            writer.Write(CreateManifestJson(pluginId, version));
        }

        var licenseEntry = archive.CreateEntry("LICENSE");
        using (var licenseWriter = new StreamWriter(licenseEntry.Open()))
        {
            licenseWriter.Write("MIT License\n\nCopyright (c) 2025 Test");
        }
    }

    private string CreateManifestJson(string pluginId, string version)
    {
        var manifest = new PluginManifest
        {
            Id = pluginId,
            Name = $"Test Plugin {pluginId}",
            Version = version,
            Author = "Test Author",
            Description = "A test plugin for unit tests",
            RiskLevel = PluginRiskLevel.ReadOnly,
            Permissions = [],
            Capabilities = []
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });
    }

    #endregion
}
