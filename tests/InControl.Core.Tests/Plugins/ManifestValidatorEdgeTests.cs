using FluentAssertions;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// Manifest rules that decide what a plugin may ask for: scopes, wildcards, network intent,
/// risk level and capability consistency.
/// </summary>
public class ManifestValidatorEdgeTests
{
    private static readonly ManifestValidator Validator = new();

    private static PluginCapability Cap(
        string id = "tool",
        string name = "Tool",
        string description = "Does a thing",
        bool network = false,
        bool modifies = false) => new()
        {
            ToolId = id,
            Name = name,
            Description = description,
            RequiresNetwork = network,
            ModifiesState = modifies
        };

    private static PluginManifest Manifest(
        IReadOnlyList<PluginPermission>? permissions = null,
        IReadOnlyList<PluginCapability>? capabilities = null,
        PluginRiskLevel risk = PluginRiskLevel.ReadOnly,
        NetworkIntent? intent = null) => new()
        {
            Id = "com.test.plugin",
            Version = "1.0.0",
            Name = "Test",
            Author = "Tests",
            Description = "A plugin",
            Permissions = permissions ?? [],
            Capabilities = capabilities ?? [Cap()],
            RiskLevel = risk,
            NetworkIntent = intent
        };

    private static PluginPermission Perm(PermissionType type, PermissionAccess access, string? scope = null, string? reason = "because") =>
        new() { Type = type, Access = access, Scope = scope, Reason = reason };

    #region Identity fields

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Id_Blank_IsRequired(string id)
    {
        var result = Validator.Validate(Manifest() with { Id = id });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Plugin ID is required");
    }

    [Theory]
    [InlineData("Com.Test")]
    [InlineData("com test")]
    [InlineData("com_test")]
    [InlineData(".com.test")]
    [InlineData("com.test.")]
    [InlineData("com..test")]
    [InlineData("com/test")]
    [InlineData("com.test;rm")]
    [InlineData("..")]
    public void Id_MustBeLowercaseAlphanumericWithDotsAndHyphens(string id)
    {
        var result = Validator.Validate(Manifest() with { Id = id });

        result.Errors.Should().Contain(e => e.Contains("lowercase alphanumeric"));
    }

    [Theory]
    [InlineData("com.test")]
    [InlineData("com-test.plugin-2")]
    [InlineData("a")]
    public void Id_ValidShapes_AreAccepted(string id)
    {
        Validator.Validate(Manifest() with { Id = id }).Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Version_Blank_IsRequired(string version)
    {
        Validator.Validate(Manifest() with { Version = version }).Errors.Should().Contain("Version is required");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.2.3.4.5")]
    [InlineData("a.b.c")]
    [InlineData("1.x")]
    [InlineData("v1.0")]
    public void Version_MustBeSemanticVersionLike(string version)
    {
        Validator.Validate(Manifest() with { Version = version }).Errors.Should().Contain(e => e.Contains("semantic version"));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0")]
    [InlineData("1.0.0.1")]
    [InlineData("1.0.0-beta")]
    public void Version_ValidShapes_AreAccepted(string version)
    {
        Validator.Validate(Manifest() with { Version = version }).Errors.Should().BeEmpty();
    }

    [Fact]
    public void Name_Author_Description_AreRequired()
    {
        var result = Validator.Validate(Manifest() with { Name = " ", Author = "", Description = "\t" });

        result.Errors.Should().Contain(["Name is required", "Author is required", "Description is required"]);
    }

    #endregion

    #region Permissions

    [Fact]
    public void Permissions_DuplicateEntry_IsAWarningAndIsNotCheckedTwice()
    {
        var read = Perm(PermissionType.File, PermissionAccess.Read, "C:\\data");
        var result = Validator.Validate(Manifest([read, read]));

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.StartsWith("Duplicate permission"));
    }

    [Fact]
    public void Permissions_FileAndNetwork_RequireAScope()
    {
        var result = Validator.Validate(Manifest(
            [Perm(PermissionType.File, PermissionAccess.Read, null), Perm(PermissionType.Network, PermissionAccess.Read, "")],
            risk: PluginRiskLevel.Network));

        result.Errors.Should().Contain("File permissions require a scope (path pattern)");
        result.Errors.Should().Contain("Network permissions require a scope (endpoint pattern)");
    }

    [Fact]
    public void Permissions_WildcardWriteToFiles_IsRefused_ButAWildcardReadIsNotNamedInTheRule()
    {
        var write = Validator.Validate(Manifest(
            [Perm(PermissionType.File, PermissionAccess.Write, "*")], risk: PluginRiskLevel.LocalMutation));
        var read = Validator.Validate(Manifest([Perm(PermissionType.File, PermissionAccess.Read, "*")]));

        write.Errors.Should().Contain("Wildcard write access to files is not permitted");
        read.Errors.Should().NotContain(e => e.Contains("Wildcard"));
    }

    [Theory]
    [InlineData(PermissionType.File, "C:\\out")]
    [InlineData(PermissionType.Network, "https://api.example.com")]
    public void Permissions_WriteWithoutAReason_IsAWarning(PermissionType type, string scope)
    {
        var result = Validator.Validate(Manifest(
            [Perm(type, PermissionAccess.Write, scope, reason: null)],
            risk: type == PermissionType.Network ? PluginRiskLevel.Network : PluginRiskLevel.LocalMutation,
            intent: type == PermissionType.Network
                ? new NetworkIntent { Endpoints = ["https://api.example.com"] }
                : null));

        result.Warnings.Should().Contain(w => w.Contains("should include a reason"));
    }

    #endregion

    #region Capabilities

    [Fact]
    public void Capabilities_BlankToolId_IsAnErrorAndSkipsTheRestOfThatCapability()
    {
        var result = Validator.Validate(Manifest(capabilities: [Cap(id: " ", name: "", description: "")]));

        result.Errors.Should().Contain("Capability tool ID is required");
        result.Errors.Should().NotContain(e => e.Contains("requires a name"));
    }

    [Fact]
    public void Capabilities_DuplicateIdAndMissingNameOrDescription_AreReported()
    {
        var result = Validator.Validate(Manifest(capabilities:
        [
            Cap(id: "dup"),
            Cap(id: "dup", name: "", description: " ")
        ]));

        result.Errors.Should().Contain("Duplicate capability tool ID: dup");
        result.Errors.Should().Contain("Capability 'dup' requires a name");
        result.Warnings.Should().Contain("Capability 'dup' should have a description");
    }

    [Fact]
    public void Capabilities_NetworkTool_NeedsANetworkPermission()
    {
        var result = Validator.Validate(Manifest(capabilities: [Cap(network: true)]));

        result.Errors.Should().Contain(e => e.Contains("requires network but no network permission"));
    }

    [Fact]
    public void Capabilities_StateModifyingTool_NeedsAWriteOrExecutePermission()
    {
        var without = Validator.Validate(Manifest([Perm(PermissionType.File, PermissionAccess.Read, "C:\\d")], [Cap(modifies: true)]));
        var withWrite = Validator.Validate(Manifest(
            [Perm(PermissionType.File, PermissionAccess.Write, "C:\\d")], [Cap(modifies: true)], PluginRiskLevel.LocalMutation));
        var withExecute = Validator.Validate(Manifest(
            [Perm(PermissionType.Settings, PermissionAccess.Execute)], [Cap(modifies: true)], PluginRiskLevel.LocalMutation));

        without.Errors.Should().Contain(e => e.Contains("modifies state but no write permission"));
        withWrite.Errors.Should().BeEmpty();
        withExecute.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Capabilities_NoneDeclared_IsAWarning()
    {
        var result = Validator.Validate(Manifest(capabilities: []));

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain("Plugin declares no capabilities");
    }

    #endregion

    #region Risk level

    [Theory]
    [InlineData(PluginRiskLevel.ReadOnly, PermissionType.File, PermissionAccess.Write, "C:\\d")]
    [InlineData(PluginRiskLevel.ReadOnly, PermissionType.Settings, PermissionAccess.Execute, null)]
    public void Risk_DeclaredBelowWhatPermissionsNeed_IsAnError(
        PluginRiskLevel declared, PermissionType type, PermissionAccess access, string? scope)
    {
        var result = Validator.Validate(Manifest([Perm(type, access, scope)], risk: declared));

        result.Errors.Should().Contain(e => e.Contains("lower than required"));
    }

    [Fact]
    public void Risk_DeclaredAboveWhatPermissionsNeed_IsAWarning()
    {
        var result = Validator.Validate(Manifest(risk: PluginRiskLevel.LocalMutation));

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("higher than necessary"));
    }

    [Fact]
    public void Risk_SystemAdjacent_IsNotAvailable()
    {
        var result = Validator.Validate(Manifest(risk: PluginRiskLevel.SystemAdjacent));

        result.Errors.Should().Contain(e => e.Contains("SystemAdjacent"));
    }

    #endregion

    #region Network intent

    private static IReadOnlyList<PluginPermission> NetworkGrant(string scope = "https://api.example.com") =>
        [Perm(PermissionType.Network, PermissionAccess.Read, scope)];

    [Fact]
    public void NetworkPermission_WithoutIntent_IsAnError()
    {
        var result = Validator.Validate(Manifest(NetworkGrant(), risk: PluginRiskLevel.Network));

        result.Errors.Should().Contain("Network permission requires NetworkIntent declaration");
    }

    [Fact]
    public void NetworkIntent_WithoutAPermission_IsAWarning_AndItsEndpointsAreStillChecked()
    {
        var result = Validator.Validate(Manifest(intent: new NetworkIntent { Endpoints = ["https://api.example.com"] }));

        result.Warnings.Should().Contain("NetworkIntent declared but no network permission requested");
        result.Errors.Should().Contain(e => e.Contains("not covered by network permissions"));
    }

    [Fact]
    public void NetworkIntent_NeedsAtLeastOneEndpoint()
    {
        var result = Validator.Validate(Manifest(NetworkGrant(), risk: PluginRiskLevel.Network, intent: new NetworkIntent { Endpoints = [] }));

        result.Errors.Should().Contain("NetworkIntent must declare at least one endpoint");
    }

    [Fact]
    public void NetworkIntent_EndpointMustBeAnAbsoluteUrl()
    {
        var result = Validator.Validate(Manifest(NetworkGrant(), risk: PluginRiskLevel.Network, intent: new NetworkIntent { Endpoints = ["api.example.com/v1"] }));

        result.Errors.Should().Contain("Invalid endpoint URL: api.example.com/v1");
    }

    [Fact]
    public void NetworkIntent_PlainHttp_IsAWarningNotAnError()
    {
        var result = Validator.Validate(Manifest(
            NetworkGrant("http://api.example.com"),
            risk: PluginRiskLevel.Network,
            intent: new NetworkIntent { Endpoints = ["http://api.example.com/v1"] }));

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("non-HTTPS"));
    }

    [Theory]
    [InlineData("https://api.example.com.evil.net/v1")]
    [InlineData("https://other.example.com/v1")]
    [InlineData("http://api.example.com/v1")]
    [InlineData("https://api.example.com@evil.net/")]
    public void NetworkIntent_EndpointOutsideTheGrantedScope_IsAnError(string endpoint)
    {
        var result = Validator.Validate(Manifest(
            NetworkGrant("https://api.example.com"),
            risk: PluginRiskLevel.Network,
            intent: new NetworkIntent { Endpoints = [endpoint] }));

        result.Errors.Should().Contain(e => e.Contains("not covered by network permissions"));
    }

    [Fact]
    public void NetworkIntent_EndpointInsideTheGrantedScope_IsAccepted()
    {
        var result = Validator.Validate(Manifest(
            NetworkGrant("https://api.example.com/v1"),
            risk: PluginRiskLevel.Network,
            intent: new NetworkIntent { Endpoints = ["https://api.example.com/v1/items"] }));

        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region Result helpers and serializer

    [Fact]
    public void ResultHelpers_BuildTheExpectedShapes()
    {
        ManifestValidationResult.Success().IsValid.Should().BeTrue();

        var failed = ManifestValidationResult.Failed("a", "b");
        failed.IsValid.Should().BeFalse();
        failed.Errors.Should().Equal("a", "b");

        var warned = ManifestValidationResult.SuccessWithWarnings("careful");
        warned.IsValid.Should().BeTrue();
        warned.Warnings.Should().Equal("careful");
    }

    [Fact]
    public void Serializer_RoundTripsAManifest_InSnakeCase()
    {
        var manifest = Manifest(
            [Perm(PermissionType.File, PermissionAccess.Read, "C:\\data")],
            [Cap()]) with { MinAppVersion = "1.0.0", Tags = ["demo"] };

        var json = ManifestSerializer.Serialize(manifest);
        var back = ManifestSerializer.Deserialize(json);

        json.Should().Contain("\"min_app_version\"").And.Contain("\"risk_level\": \"read_only\"");
        back.Should().NotBeNull();
        back!.Id.Should().Be(manifest.Id);
        back.Permissions.Should().ContainSingle().Which.Scope.Should().Be("C:\\data");
        back.Tags.Should().Equal("demo");
    }

    [Fact]
    public void TryDeserialize_ReturnsTheManifest_OrTheParseError()
    {
        var json = ManifestSerializer.Serialize(Manifest());

        ManifestSerializer.TryDeserialize(json, out var manifest, out var error).Should().BeTrue();
        manifest.Should().NotBeNull();
        error.Should().BeNull();

        ManifestSerializer.TryDeserialize("{ not json", out var none, out var parseError).Should().BeFalse();
        none.Should().BeNull();
        parseError.Should().NotBeNullOrWhiteSpace();

        ManifestSerializer.TryDeserialize("null", out var nothing, out var nullError).Should().BeFalse();
        nothing.Should().BeNull();
        nullError.Should().BeNull();
    }

    #endregion
}
