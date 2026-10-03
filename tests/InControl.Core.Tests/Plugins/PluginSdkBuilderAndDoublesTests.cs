using FluentAssertions;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// The plugin SDK surface that authors build on: the base class guard rails, the manifest
/// builder, parameter helpers and the test doubles shipped for plugin tests.
/// </summary>
public class PluginSdkBuilderAndDoublesTests
{
    private static PluginManifestBuilder ValidBuilder() => new PluginManifestBuilder()
        .WithId("com.test.sdk")
        .WithName("SDK")
        .WithVersion("1.2.3")
        .WithAuthor("Tests")
        .WithDescription("SDK test")
        .AddCapability("t", "T", "Does a thing");

    #region PluginBase

    [Fact]
    public async Task Base_BeforeInitialization_RefusesContextAccess_AndFailsExecution()
    {
        var plugin = new ProbePlugin();

        var manifest = () => plugin.Manifest;
        var capabilities = () => plugin.GetCapabilities();
        manifest.Should().Throw<InvalidOperationException>().WithMessage("Plugin not initialized");
        capabilities.Should().Throw<InvalidOperationException>();
        plugin.IsInitialized.Should().BeFalse();

        var context = PluginTestHelpers.CreateTestContext(PluginTestHelpers.CreateTestManifest());
        var result = await plugin.ExecuteAsync("run", new Dictionary<string, object?>(), context);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Plugin not initialized");
    }

    [Fact]
    public async Task Base_AfterInitialization_ExposesTheMediatedApis_AndTheManifestsCapabilities()
    {
        var manifest = ValidBuilder().Build();
        var plugin = new ProbePlugin();
        var context = PluginTestHelpers.CreateTestContext(manifest);

        await plugin.InitializeAsync(context);

        plugin.IsInitialized.Should().BeTrue();
        plugin.GetCapabilities().Should().ContainSingle().Which.ToolId.Should().Be("t");
        plugin.Exposed.Should().Equal(
            context.Files, context.Network, context.Memory, context.Storage);
        plugin.InitializedCalls.Should().Be(1);
    }

    [Fact]
    public async Task Base_ExecutionErrors_AndCancellation_BecomeFailedResults()
    {
        var plugin = new ProbePlugin();
        var context = PluginTestHelpers.CreateTestContext(PluginTestHelpers.CreateTestManifest());
        await plugin.InitializeAsync(context);

        plugin.Behavior = () => throw new InvalidOperationException("kaput");
        var failed = await plugin.ExecuteAsync("run", new Dictionary<string, object?>(), context);

        plugin.Behavior = () => throw new OperationCanceledException();
        var cancelled = await plugin.ExecuteAsync("run", new Dictionary<string, object?>(), context);

        plugin.Behavior = () => PluginActionResult.Succeeded("done");
        var ok = await plugin.ExecuteAsync("run", new Dictionary<string, object?>(), context);

        failed.Error.Should().Be("Execution error: kaput");
        cancelled.Error.Should().Be("Operation cancelled");
        ok.Success.Should().BeTrue();
        ok.Output.Should().Be("done");
    }

    [Fact]
    public async Task Base_AfterDispose_RefusesInitializationAndExecution_AndDisposeRunsOnce()
    {
        var plugin = new ProbePlugin();
        var context = PluginTestHelpers.CreateTestContext(PluginTestHelpers.CreateTestManifest());
        await plugin.InitializeAsync(context);

        await plugin.DisposeAsync();
        await plugin.DisposeAsync();

        plugin.DisposeCalls.Should().Be(1);
        var execute = async () => await plugin.ExecuteAsync("run", new Dictionary<string, object?>(), context);
        var init = async () => await plugin.InitializeAsync(context);
        await execute.Should().ThrowAsync<ObjectDisposedException>();
        await init.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Manifest builder

    [Fact]
    public void Builder_CarriesEveryFieldIntoTheManifest()
    {
        var manifest = ValidBuilder()
            .WithMinAppVersion("1.0.0")
            .WithHomepage("https://example.com/plugin")
            .WithLicense("MIT")
            .WithEntryPoint("Plugin.dll")
            .WithIcon("icon.png")
            .WithRiskLevel(PluginRiskLevel.LocalMutation)
            .AddFilePermission("C:\\data", PermissionAccess.Write, "Saves notes")
            .AddMemoryPermission(PermissionAccess.Read, "Recalls notes")
            .AddCapability("save", "Save", "Saves a note", modifiesState: true,
                parameters: [CapabilityParameter.RequiredString("text", "Note text")])
            .Build();

        manifest.MinAppVersion.Should().Be("1.0.0");
        manifest.Homepage.Should().Be("https://example.com/plugin");
        manifest.License.Should().Be("MIT");
        manifest.EntryPoint.Should().Be("Plugin.dll");
        manifest.IconPath.Should().Be("icon.png");
        manifest.RiskLevel.Should().Be(PluginRiskLevel.LocalMutation);
        manifest.Permissions.Should().HaveCount(2);
        manifest.Permissions[0].Should().Match<PluginPermission>(p =>
            p.Type == PermissionType.File && p.Access == PermissionAccess.Write && p.Scope == "C:\\data" && p.Reason == "Saves notes");
        manifest.Permissions[1].Type.Should().Be(PermissionType.Memory);
        manifest.Capabilities.Should().HaveCount(2);
        manifest.Capabilities[1].ModifiesState.Should().BeTrue();
        manifest.Capabilities[1].Parameters.Should().ContainSingle().Which.Name.Should().Be("text");
    }

    [Fact]
    public void Builder_NetworkPermission_AlsoDeclaresTheIntent()
    {
        var manifest = ValidBuilder()
            .WithRiskLevel(PluginRiskLevel.Network)
            .AddNetworkPermission("https://api.example.com", "Looks things up", ["query text"])
            .Build();

        manifest.NetworkIntent.Should().NotBeNull();
        manifest.NetworkIntent!.Endpoints.Should().Equal("https://api.example.com");
        manifest.NetworkIntent.DataSent.Should().Equal("query text");
        manifest.Permissions.Should().ContainSingle().Which.Scope.Should().Be("https://api.example.com");
    }

    [Fact]
    public void Builder_RefusesAManifestTheValidatorRejects()
    {
        var act = () => new PluginManifestBuilder().WithId("Bad Id").WithVersion("x").Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("Invalid manifest:*");
    }

    [Fact]
    public void TestManifestHelper_BuildsAValidManifest_WithTheGivenIdAndVersion()
    {
        var manifest = PluginTestHelpers.CreateTestManifest("my-plugin", "2.0.1");

        manifest.Id.Should().Be("my-plugin");
        manifest.Version.Should().Be("2.0.1");
        manifest.Name.Should().Be("Test Plugin: my-plugin");
        new ManifestValidator().Validate(manifest).IsValid.Should().BeTrue();
    }

    #endregion

    #region Capability parameters

    [Fact]
    public void ParameterHelpers_SetTypeRequirednessAndDefaults()
    {
        CapabilityParameter.RequiredString("a", "d").Should().Match<PluginCapabilityParameter>(p => p.Type == "string" && p.Required);
        var optionalString = CapabilityParameter.OptionalString("b", "d", "fallback");
        optionalString.Required.Should().BeFalse();
        optionalString.Default.Should().Be("fallback");
        CapabilityParameter.OptionalString("b", "d").Default.Should().BeNull();

        CapabilityParameter.RequiredNumber("n", "d").Should().Match<PluginCapabilityParameter>(p => p.Type == "number" && p.Required);
        var optionalNumber = CapabilityParameter.OptionalNumber("n", "d", 2.5);
        optionalNumber.Type.Should().Be("number");
        optionalNumber.Required.Should().BeFalse();
        optionalNumber.Default.Should().Be(2.5);

        CapabilityParameter.RequiredBool("f", "d").Should().Match<PluginCapabilityParameter>(p => p.Type == "boolean" && p.Required);
        var optionalBool = CapabilityParameter.OptionalBool("f", "d", defaultValue: true);
        optionalBool.Required.Should().BeFalse();
        optionalBool.Default.Should().Be(true);
        CapabilityParameter.OptionalBool("f", "d").Default.Should().Be(false);

        var choice = CapabilityParameter.Enum("mode", "d", ["fast", "slow"], required: false);
        choice.Type.Should().Be("string");
        choice.Required.Should().BeFalse();
        choice.Enum.Should().Equal("fast", "slow");
        CapabilityParameter.Enum("mode", "d", ["x"]).Required.Should().BeTrue();
    }

    #endregion

    #region Test doubles

    [Fact]
    public async Task TestContext_Files_ReadBackWhatWasWritten_AndReportMissingFiles()
    {
        var context = PluginTestHelpers.CreateTestContext(PluginTestHelpers.CreateTestManifest());

        (await context.Files.ReadAsync("a.txt")).Error.Should().Be("File not found");
        (await context.Files.WriteAsync("a.txt", "hello")).Success.Should().BeTrue();
        (await context.Files.ReadAsync("a.txt")).Content.Should().Be("hello");
        (await context.Files.ListAsync("anywhere")).Files.Should().Equal("a.txt");
        context.Files.IsPathPermitted("anything", PermissionAccess.Write).Should().BeTrue();
    }

    [Fact]
    public async Task TestContext_Network_AlwaysAnswers200()
    {
        var context = PluginTestHelpers.CreateTestContext(PluginTestHelpers.CreateTestManifest());

        context.Network.IsAvailable.Should().BeTrue();
        context.Network.IsEndpointPermitted("https://anything.example").Should().BeTrue();
        var result = await context.Network.RequestAsync("https://anything.example", "GET", null, "test");
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().Be("{}");
        context.HasPermission(PermissionType.File, PermissionAccess.Write, "C:\\x").Should().BeTrue();
        context.Dispose();
    }

    [Fact]
    public async Task TestContext_Memory_StoresRetrievesAndSearches()
    {
        var context = PluginTestHelpers.CreateTestContext(PluginTestHelpers.CreateTestManifest());

        (await context.Memory.StoreAsync("a", "alpha note")).Should().BeTrue();
        (await context.Memory.StoreAsync("b", "beta note")).Should().BeTrue();
        (await context.Memory.StoreAsync("c", "other")).Should().BeTrue();

        (await context.Memory.RetrieveAsync("a")).Should().Be("alpha note");
        (await context.Memory.RetrieveAsync("zzz")).Should().BeNull();
        (await context.Memory.SearchAsync("note")).Should().BeEquivalentTo("alpha note", "beta note");
        (await context.Memory.SearchAsync("note", limit: 1)).Should().HaveCount(1);
    }

    [Fact]
    public async Task TestContext_Storage_SetGetRemoveListClear()
    {
        var context = PluginTestHelpers.CreateTestContext(PluginTestHelpers.CreateTestManifest());

        await context.Storage.SetAsync("k1", "v1");
        await context.Storage.SetAsync("k2", "v2");

        (await context.Storage.GetAsync("k1")).Should().Be("v1");
        (await context.Storage.GetAsync("nope")).Should().BeNull();
        (await context.Storage.ListKeysAsync()).Should().BeEquivalentTo("k1", "k2");
        (await context.Storage.RemoveAsync("k1")).Should().BeTrue();
        (await context.Storage.RemoveAsync("k1")).Should().BeFalse();
        await context.Storage.ClearAsync();
        (await context.Storage.ListKeysAsync()).Should().BeEmpty();
    }

    #endregion

    private sealed class ProbePlugin : PluginBase
    {
        public Func<PluginActionResult> Behavior { get; set; } = () => PluginActionResult.Succeeded(null);

        public int InitializedCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public object[] Exposed => [Files, Network, Memory, Storage];

        protected override Task OnInitializeAsync(CancellationToken ct)
        {
            InitializedCalls++;
            return Task.CompletedTask;
        }

        protected override Task<PluginActionResult> OnExecuteAsync(
            string actionId, IReadOnlyDictionary<string, object?> parameters, CancellationToken ct) =>
            Task.FromResult(Behavior());

        protected override ValueTask OnDisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
