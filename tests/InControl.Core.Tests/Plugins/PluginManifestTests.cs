using FluentAssertions;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

public class ManifestValidatorTests
{
    private readonly ManifestValidator _validator = new();

    [Fact]
    public void Validate_ValidMinimalManifest_Succeeds()
    {
        var manifest = CreateValidManifest();

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingId_Fails()
    {
        var manifest = CreateValidManifest() with { Id = "" };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ID"));
    }

    [Fact]
    public void Validate_InvalidIdFormat_Fails()
    {
        var manifest = CreateValidManifest() with { Id = "Invalid_Plugin_ID" };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("lowercase"));
    }

    [Fact]
    public void Validate_MissingVersion_Fails()
    {
        var manifest = CreateValidManifest() with { Version = "" };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Version"));
    }

    [Fact]
    public void Validate_InvalidVersion_Fails()
    {
        var manifest = CreateValidManifest() with { Version = "not-a-version" };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("semantic version"));
    }

    [Fact]
    public void Validate_MissingName_Fails()
    {
        var manifest = CreateValidManifest() with { Name = "" };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Name"));
    }

    [Fact]
    public void Validate_MissingAuthor_Fails()
    {
        var manifest = CreateValidManifest() with { Author = "" };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Author"));
    }

    [Fact]
    public void Validate_MissingDescription_Fails()
    {
        var manifest = CreateValidManifest() with { Description = "" };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Description"));
    }

    [Fact]
    public void Validate_FilePermissionWithoutScope_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Permissions = new List<PluginPermission>
            {
                new() { Type = PermissionType.File, Access = PermissionAccess.Read }
            }
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("scope"));
    }

    [Fact]
    public void Validate_NetworkPermissionWithoutScope_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Permissions = new List<PluginPermission>
            {
                new() { Type = PermissionType.Network, Access = PermissionAccess.Read }
            },
            RiskLevel = PluginRiskLevel.Network
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("scope"));
    }

    [Fact]
    public void Validate_WildcardWriteAccess_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Permissions = new List<PluginPermission>
            {
                new() { Type = PermissionType.File, Access = PermissionAccess.Write, Scope = "*" }
            },
            RiskLevel = PluginRiskLevel.LocalMutation
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Wildcard"));
    }

    [Fact]
    public void Validate_NetworkCapabilityWithoutPermission_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Capabilities = new List<PluginCapability>
            {
                new()
                {
                    ToolId = "fetch-data",
                    Name = "Fetch Data",
                    Description = "Fetches data from the internet",
                    RequiresNetwork = true
                }
            }
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("network permission"));
    }

    [Fact]
    public void Validate_StateModifyingCapabilityWithoutWritePermission_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Capabilities = new List<PluginCapability>
            {
                new()
                {
                    ToolId = "modify-files",
                    Name = "Modify Files",
                    Description = "Modifies local files",
                    ModifiesState = true
                }
            }
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("write permission"));
    }

    [Fact]
    public void Validate_DeclaredRiskLowerThanRequired_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Permissions = new List<PluginPermission>
            {
                new()
                {
                    Type = PermissionType.Network,
                    Access = PermissionAccess.Read,
                    Scope = "https://api.example.com"
                }
            },
            RiskLevel = PluginRiskLevel.ReadOnly, // Should be Network
            NetworkIntent = new NetworkIntent
            {
                Endpoints = new[] { "https://api.example.com" }
            }
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("risk level"));
    }

    [Fact]
    public void Validate_SystemAdjacentRiskLevel_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            RiskLevel = PluginRiskLevel.SystemAdjacent
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Phase 8"));
    }

    [Fact]
    public void Validate_NetworkPermissionWithoutIntent_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Permissions = new List<PluginPermission>
            {
                new()
                {
                    Type = PermissionType.Network,
                    Access = PermissionAccess.Read,
                    Scope = "https://api.example.com"
                }
            },
            RiskLevel = PluginRiskLevel.Network,
            NetworkIntent = null
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NetworkIntent"));
    }

    [Fact]
    public void Validate_NetworkIntentEndpointNotInPermissions_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Permissions = new List<PluginPermission>
            {
                new()
                {
                    Type = PermissionType.Network,
                    Access = PermissionAccess.Read,
                    Scope = "https://api.example.com"
                }
            },
            RiskLevel = PluginRiskLevel.Network,
            NetworkIntent = new NetworkIntent
            {
                Endpoints = new[] { "https://different.example.com" }
            }
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not covered"));
    }

    [Fact]
    public void Validate_DuplicateCapabilityIds_Fails()
    {
        var manifest = CreateValidManifest() with
        {
            Capabilities = new List<PluginCapability>
            {
                new() { ToolId = "same-id", Name = "Tool 1", Description = "First" },
                new() { ToolId = "same-id", Name = "Tool 2", Description = "Second" }
            }
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Validate_NoCapabilities_WarnsButPasses()
    {
        var manifest = CreateValidManifest() with
        {
            Capabilities = new List<PluginCapability>()
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("no capabilities"));
    }

    [Fact]
    public void Validate_ValidFullManifest_Succeeds()
    {
        var manifest = new PluginManifest
        {
            Id = "com.example.weather-tool",
            Version = "1.0.0",
            Name = "Weather Tool",
            Author = "Example Developer",
            Description = "Fetches weather data for a given location",
            License = "MIT",
            Homepage = "https://github.com/example/weather-tool",
            RiskLevel = PluginRiskLevel.Network,
            Permissions = new List<PluginPermission>
            {
                new()
                {
                    Type = PermissionType.Network,
                    Access = PermissionAccess.Read,
                    Scope = "https://api.weather.com",
                    Reason = "Fetch weather data"
                }
            },
            Capabilities = new List<PluginCapability>
            {
                new()
                {
                    ToolId = "get-weather",
                    Name = "Get Weather",
                    Description = "Fetches current weather for a location",
                    RequiresNetwork = true
                }
            },
            NetworkIntent = new NetworkIntent
            {
                Endpoints = new[] { "https://api.weather.com/current" },
                DataSent = new[] { "Location name" },
                DataReceived = new[] { "Temperature", "Conditions" },
                Retention = "session",
                Purpose = "Display current weather to user"
            },
            Tags = new[] { "weather", "utility" }
        };

        var result = _validator.Validate(manifest);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static PluginManifest CreateValidManifest()
    {
        return new PluginManifest
        {
            Id = "com.test.sample-plugin",
            Version = "1.0.0",
            Name = "Sample Plugin",
            Author = "Test Author",
            Description = "A test plugin for validation"
        };
    }
}

public class ManifestSerializerTests
{
    [Fact]
    public void Serialize_AndDeserialize_RoundTrips()
    {
        var original = new PluginManifest
        {
            Id = "com.test.round-trip",
            Version = "2.1.0",
            Name = "Round Trip Test",
            Author = "Tester",
            Description = "Tests serialization",
            RiskLevel = PluginRiskLevel.LocalMutation,
            Permissions = new List<PluginPermission>
            {
                new()
                {
                    Type = PermissionType.File,
                    Access = PermissionAccess.Write,
                    Scope = "/documents",
                    Reason = "Save files"
                }
            },
            Capabilities = new List<PluginCapability>
            {
                new()
                {
                    ToolId = "save-file",
                    Name = "Save File",
                    Description = "Saves content to a file",
                    ModifiesState = true
                }
            }
        };

        var json = ManifestSerializer.Serialize(original);
        var deserialized = ManifestSerializer.Deserialize(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Version.Should().Be(original.Version);
        deserialized.RiskLevel.Should().Be(original.RiskLevel);
        deserialized.Permissions.Should().HaveCount(1);
        deserialized.Capabilities.Should().HaveCount(1);
    }

    [Fact]
    public void TryDeserialize_InvalidJson_ReturnsFalse()
    {
        var result = ManifestSerializer.TryDeserialize("not valid json", out var manifest, out var error);

        result.Should().BeFalse();
        manifest.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Serialize_UsesSnakeCase()
    {
        var manifest = new PluginManifest
        {
            Id = "test",
            Version = "1.0.0",
            Name = "Test",
            Author = "Test",
            Description = "Test",
            RiskLevel = PluginRiskLevel.ReadOnly
        };

        var json = ManifestSerializer.Serialize(manifest);

        json.Should().Contain("risk_level");
        json.Should().Contain("read_only");
    }
}

public class PluginPermissionTests
{
    [Fact]
    public void ToDisplayString_FormatsCorrectly()
    {
        var permission = new PluginPermission
        {
            Type = PermissionType.File,
            Access = PermissionAccess.Read,
            Scope = "/documents"
        };

        var display = permission.ToDisplayString();

        display.Should().Be("File:read (/documents)");
    }

    [Fact]
    public void ToDisplayString_WithoutScope_FormatsCorrectly()
    {
        var permission = new PluginPermission
        {
            Type = PermissionType.Memory,
            Access = PermissionAccess.Write
        };

        var display = permission.ToDisplayString();

        display.Should().Be("Memory:write");
    }
}

public class PluginRiskLevelTests
{
    [Theory]
    [InlineData(PluginRiskLevel.ReadOnly, 1)]
    [InlineData(PluginRiskLevel.LocalMutation, 2)]
    [InlineData(PluginRiskLevel.Network, 3)]
    [InlineData(PluginRiskLevel.SystemAdjacent, 4)]
    public void RiskLevel_HasCorrectValues(PluginRiskLevel level, int expected)
    {
        ((int)level).Should().Be(expected);
    }

    [Fact]
    public void RiskLevel_CanCompare()
    {
        (PluginRiskLevel.ReadOnly < PluginRiskLevel.LocalMutation).Should().BeTrue();
        (PluginRiskLevel.LocalMutation < PluginRiskLevel.Network).Should().BeTrue();
        (PluginRiskLevel.Network < PluginRiskLevel.SystemAdjacent).Should().BeTrue();
    }
}
