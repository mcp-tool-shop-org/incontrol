using FluentAssertions;
using InControl.Core.Plugins;
using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Policy-governed plugin host: pre-approval, re-checking policy after load,
/// enabling, and how inner failures are reported.
/// </summary>
public class PluginPolicyFlowTests
{
    private static PluginManifest Manifest(string id = "com.test.governed", string version = "1.0.0") => new()
    {
        Id = id,
        Version = version,
        Name = "Governed",
        Author = "Tests",
        Description = "A governed plugin",
        Capabilities = [new PluginCapability { ToolId = "t", Name = "T", Description = "Does a thing" }]
    };

    private static (PluginHost Host, PolicyGovernedPluginHost Governed, PolicyEngine Engine) Create(PolicyDocument? user = null)
    {
        var engine = new PolicyEngine();
        if (user is not null)
        {
            engine.SetPolicy(PolicySource.User, user);
        }

        var host = new PluginHost(new Sandbox(), new InMemoryPluginAuditLog());
        return (host, host.WithPolicyEnforcement(engine), engine);
    }

    private static PolicyDocument Allowing(string pattern = "com.test.*") => new()
    {
        Version = "1.0",
        Plugins = new PluginPolicyRules { Allow = [pattern] }
    };

    private static readonly IReadOnlyDictionary<string, object?> NoParameters = new Dictionary<string, object?>();

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        var host = new PluginHost(new Sandbox(), new InMemoryPluginAuditLog());

        var noHost = () => new PolicyGovernedPluginHost(null!, new PolicyEngine());
        var noEngine = () => new PolicyGovernedPluginHost(host, null!);

        noHost.Should().Throw<ArgumentNullException>();
        noEngine.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CheckPluginPolicy_ConstraintsAreNotAPluginDecision_SoItFailsClosed()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Locked = true,
            Plugins = new PluginPolicyRules { Default = PolicyDecision.AllowWithConstraints }
        });
        var governed = new PluginHost(new Sandbox(), new InMemoryPluginAuditLog()).WithPolicyEnforcement(engine);

        var check = governed.CheckPluginPolicy("com.test.p");

        check.CanLoad.Should().BeFalse();
        check.Decision.Should().Be(PolicyDecision.Deny);
        check.Reason.Should().Be("Unknown policy decision");
        check.RequiresApproval.Should().BeFalse();
        check.Source.Should().Be(PolicySource.Organization);
    }

    [Fact]
    public void CheckPluginPolicy_Deny_CarriesTheRuleId()
    {
        var (_, governed, _) = Create(new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { Deny = ["com.bad.*"] }
        });

        var check = governed.CheckPluginPolicy("com.bad.one");

        check.CanLoad.Should().BeFalse();
        check.RuleId.Should().Be("plugins.deny.com.bad.*");
    }

    [Fact]
    public void Approval_IsRecordedWithWhoAndWhen_AndOnlyUpgradesAnApprovalRequirement()
    {
        var (_, governed, _) = Create();
        governed.ApprovePlugin("com.test.a", "alice");

        var check = governed.CheckPluginPolicy("com.test.a");

        check.CanLoad.Should().BeTrue();
        check.Decision.Should().Be(PolicyDecision.Allow);
        check.Source.Should().Be(PolicySource.Session);
        check.Reason.Should().Contain("Pre-approved").And.Contain("alice");
        governed.GetApprovedPlugins().Should().ContainSingle().Which.ApprovedBy.Should().Be("alice");
        governed.CheckPluginPolicy("com.test.other").RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task Load_InnerHostRejectsTheManifest_ReportedAsAFailureNotABlock()
    {
        var (_, governed, _) = Create(Allowing());

        var result = await governed.LoadPluginAsync(Manifest(version: "not-a-version"), new Instance());

        result.Success.Should().BeFalse();
        result.WasBlocked.Should().BeFalse();
        result.RequiredApproval.Should().BeFalse();
        result.Error.Should().Contain("Invalid manifest");
    }

    [Fact]
    public async Task Execute_PolicyTightenedAfterLoad_IsBlockedAndRaisesTheEvent()
    {
        var (_, governed, engine) = Create(Allowing());
        await governed.LoadPluginAsync(Manifest(), new Instance());
        PluginBlockedEventArgs? raised = null;
        governed.PluginBlocked += (_, e) => raised = e;
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { Deny = ["com.test.*"] }
        });

        var result = await governed.ExecuteAsync("com.test.governed", "run", NoParameters);

        result.Success.Should().BeFalse();
        result.WasBlocked.Should().BeTrue();
        result.BlockSource.Should().Be(PolicySource.Organization);
        result.Error.Should().Contain("blocked");
        raised!.PluginId.Should().Be("com.test.governed");
        raised.PluginName.Should().Be("Governed");
    }

    [Fact]
    public async Task Execute_PassesThePluginsOutputAndErrorThrough()
    {
        var (_, governed, _) = Create(Allowing());
        var instance = new Instance { Result = PluginActionResult.Failed("it said no") };
        await governed.LoadPluginAsync(Manifest(), instance);

        var failed = await governed.ExecuteAsync("com.test.governed", "run", NoParameters);
        instance.Result = PluginActionResult.Succeeded(42);
        var ok = await governed.ExecuteAsync("com.test.governed", "run", NoParameters);

        failed.Success.Should().BeFalse();
        failed.Error.Should().Be("it said no");
        failed.WasBlocked.Should().BeFalse();
        ok.Success.Should().BeTrue();
        ok.Output.Should().Be(42);
        ok.PluginId.Should().Be("com.test.governed");
        ok.ActionId.Should().Be("run");
    }

    [Fact]
    public async Task Enable_UnknownPlugin_IsFalse_AndDisableIsAPassthrough()
    {
        var (host, governed, _) = Create(Allowing());

        governed.EnablePlugin("com.test.none").Should().BeFalse();
        governed.DisablePlugin("com.test.none").Should().BeFalse();

        await governed.LoadPluginAsync(Manifest(), new Instance());
        governed.DisablePlugin("com.test.governed").Should().BeTrue();
        host.GetPlugin("com.test.governed")!.State.Should().Be(PluginState.Disabled);
        governed.EnablePlugin("com.test.governed").Should().BeTrue();
        host.GetPlugin("com.test.governed")!.State.Should().Be(PluginState.Enabled);
    }

    [Fact]
    public async Task Enable_WhenPolicyNoLongerAllowsIt_IsRefusedAndRaisesTheEvent()
    {
        var (host, governed, engine) = Create(Allowing());
        await governed.LoadPluginAsync(Manifest(), new Instance());
        governed.DisablePlugin("com.test.governed");
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { Enabled = false }
        });
        PluginBlockedEventArgs? raised = null;
        governed.PluginBlocked += (_, e) => raised = e;

        governed.EnablePlugin("com.test.governed").Should().BeFalse();

        host.GetPlugin("com.test.governed")!.State.Should().Be(PluginState.Disabled);
        raised!.Source.Should().Be(PolicySource.Organization);
        raised.Reason.Should().Contain("disabled");
    }

    [Fact]
    public async Task Unload_IsAPassthrough()
    {
        var (host, governed, _) = Create(Allowing());
        await governed.LoadPluginAsync(Manifest(), new Instance());

        (await governed.UnloadPluginAsync("com.test.governed")).Should().BeTrue();
        (await governed.UnloadPluginAsync("com.test.governed")).Should().BeFalse();

        host.LoadedPlugins.Should().BeEmpty();
        governed.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    public void LoadablePlugins_KeepApprovalRequiredOnes_AndDropDeniedOnes()
    {
        var (_, governed, _) = Create(new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules { Allow = ["com.ok.*"], Deny = ["com.bad.*"] }
        });
        var manifests = new[] { Manifest("com.ok.one"), Manifest("com.bad.one"), Manifest("com.maybe.one") };

        var loadable = governed.GetLoadablePlugins(manifests);
        var all = governed.GetAllPluginsWithPolicy(manifests);

        loadable.Select(p => p.Manifest.Id).Should().Equal("com.ok.one", "com.maybe.one");
        all.Should().HaveCount(3);
        all.Single(p => p.Manifest.Id == "com.bad.one").PolicyStatus.CanLoad.Should().BeFalse();
    }

    [Fact]
    public void ResultFactories_SetTheFieldsTheyName()
    {
        var blocked = PolicyGovernedPluginLoadResult.Blocked("p", "no", PolicySource.Team);
        blocked.WasBlocked.Should().BeTrue();
        blocked.BlockSource.Should().Be(PolicySource.Team);

        var approval = PolicyGovernedPluginLoadResult.RequiresApproval("p", "ask");
        approval.RequiredApproval.Should().BeTrue();
        approval.BlockReason.Should().Be("ask");

        var failed = PolicyGovernedPluginLoadResult.Failed("p", "bad");
        failed.Error.Should().Be("bad");
        failed.Success.Should().BeFalse();

        PolicyGovernedPluginLoadResult.Loaded("p").Success.Should().BeTrue();

        var notLoaded = PolicyGovernedPluginExecutionResult.Failed("p", "a", "not loaded");
        notLoaded.Success.Should().BeFalse();
        notLoaded.WasBlocked.Should().BeFalse();
        notLoaded.Duration.Should().Be(TimeSpan.Zero);

        var execBlocked = PolicyGovernedPluginExecutionResult.Blocked("p", "a", "why", PolicySource.User);
        execBlocked.WasBlocked.Should().BeTrue();
        execBlocked.BlockSource.Should().Be(PolicySource.User);
    }

    private sealed class Sandbox : IPluginSandbox
    {
        public IPluginContext CreateContext(PluginManifest manifest) => PluginTestHelpers.CreateTestContext(manifest);
    }

    private sealed class Instance : IPluginInstance
    {
        public PluginActionResult Result { get; set; } = PluginActionResult.Succeeded("ok");

        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PluginActionResult> ExecuteAsync(
            string actionId,
            IReadOnlyDictionary<string, object?> parameters,
            IPluginContext context,
            CancellationToken ct = default) => Task.FromResult(Result);

        public IReadOnlyList<PluginCapability> GetCapabilities() => [];
    }
}
