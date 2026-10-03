using FluentAssertions;
using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// What the policy loader accepts and rejects. A policy that fails validation is never applied.
/// </summary>
public class PolicyValidationEdgeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "policy-validation-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static PolicyValidationResult Validate(PolicyDocument document) => PolicyValidator.Validate(document);

    private static PolicyDocument Doc() => new() { Version = "1.0" };

    #region Loading

    [Fact]
    public void LoadFromJson_JsonNull_IsAFailureNotAnEmptyPolicy()
    {
        var result = PolicySerializer.LoadFromJson("null", "policy.json");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Deserialized to null");
        result.SourcePath.Should().Be("policy.json");
        result.Document.Should().BeNull();
    }

    [Fact]
    public void LoadFromJson_InvalidDocument_ReturnsEveryValidationError_AndNoDocument()
    {
        const string json = """
            {
              "version": "one",
              "updates": { "allowedChannels": ["nightly"] },
              "memory": { "maxMemories": -1 }
            }
            """;

        var result = PolicySerializer.LoadFromJson(json, "p.json");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Validation failed");
        result.Document.Should().BeNull();
        result.ValidationErrors.Should().HaveCount(3);
        result.ValidationErrors.Should().Contain(e => e.Contains("Invalid version format"));
        result.ValidationErrors.Should().Contain(e => e.Contains("nightly"));
        result.ValidationErrors.Should().Contain(e => e.Contains("maxMemories"));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    [InlineData("")]
    public void LoadFromJson_BrokenJson_IsAParseFailure(string json)
    {
        var result = PolicySerializer.LoadFromJson(json);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("JSON parse error");
    }

    [Fact]
    public void LoadFromJson_AcceptsCommentsTrailingCommasAndAnyCasing()
    {
        const string json = """
            {
              // org policy
              "VERSION": "2.1",
              "tools": { "deny": ["shell",], },
            }
            """;

        var result = PolicySerializer.LoadFromJson(json);

        result.IsSuccess.Should().BeTrue();
        result.Document!.Version.Should().Be("2.1");
        result.Document.Tools!.Deny.Should().Equal("shell");
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsADocument_AndCreatesTheFolder()
    {
        var path = Path.Combine(_dir, "nested", "policy.json");
        var document = new PolicyDocument
        {
            Version = "1.0",
            Locked = true,
            Tools = new ToolPolicyRules
            {
                Default = PolicyDecision.Deny,
                Allow = ["read_file"],
                Rules = [new ToolRule { Id = "no-shell", Tool = "shell*", Decision = PolicyDecision.Deny, Reason = "Policy." }]
            },
            Plugins = new PluginPolicyRules { MaxRiskLevel = PluginRiskLevelPolicy.LocalMutation, Deny = ["bad.*"] }
        };

        await PolicySerializer.SaveToFileAsync(document, path);
        var loaded = await PolicySerializer.LoadFromFileAsync(path);

        loaded.IsSuccess.Should().BeTrue(loaded.Error);
        loaded.SourcePath.Should().Be(path);
        loaded.Document!.Locked.Should().BeTrue();
        loaded.Document.Tools!.Default.Should().Be(PolicyDecision.Deny);
        loaded.Document.Tools.Rules.Should().ContainSingle().Which.Id.Should().Be("no-shell");
        loaded.Document.Plugins!.MaxRiskLevel.Should().Be(PluginRiskLevelPolicy.LocalMutation);
        PolicySerializer.ToJson(document).Should().Contain("\"locked\": true");
    }

    [Fact]
    public async Task LoadFromFile_MissingFile_IsNotFound_NotAnError()
    {
        var result = await PolicySerializer.LoadFromFileAsync(Path.Combine(_dir, "absent.json"));

        result.IsSuccess.Should().BeFalse();
        result.FileNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task LoadFromFile_FileInUse_IsAnIoFailure()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "locked.json");
        await File.WriteAllTextAsync(path, "{}");
        await using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await PolicySerializer.LoadFromFileAsync(path);

        result.IsSuccess.Should().BeFalse();
        result.FileNotFound.Should().BeFalse();
        result.Error.Should().StartWith("IO error");
    }

    [Fact]
    public async Task LoadFromFile_APathThatIsAFolder_IsRefusedWithoutThrowing()
    {
        Directory.CreateDirectory(_dir);

        var result = await PolicySerializer.LoadFromFileAsync(_dir);

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region Validator

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Version_Blank_IsRequired(string version)
    {
        Validate(Doc() with { Version = version }).Errors.Should().Contain("Version is required");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.0.0")]
    [InlineData("v1.0")]
    [InlineData("1.x")]
    public void Version_MustBeMajorDotMinor(string version)
    {
        Validate(Doc() with { Version = version }).Errors.Should().ContainSingle(e => e.StartsWith("Invalid version format"));
    }

    [Fact]
    public void ValidDocument_HasNoErrors()
    {
        var result = Validate(Doc());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        PolicyValidationResult.Valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ToolRules_IdsMustBePresentWellFormedAndUnique_AndNeedAPattern()
    {
        var result = Validate(Doc() with
        {
            Tools = new ToolPolicyRules
            {
                Rules =
                [
                    new ToolRule { Id = "", Tool = "a", Decision = PolicyDecision.Allow },
                    new ToolRule { Id = "-bad", Tool = "a", Decision = PolicyDecision.Allow },
                    new ToolRule { Id = "dup", Tool = "a", Decision = PolicyDecision.Allow },
                    new ToolRule { Id = "DUP", Tool = "a", Decision = PolicyDecision.Allow },
                    new ToolRule { Id = "no-pattern", Tool = " ", Decision = PolicyDecision.Allow },
                    new ToolRule { Id = "needs-limits", Tool = "a", Decision = PolicyDecision.AllowWithConstraints },
                    new ToolRule { Id = "empty-limits", Tool = "a", Decision = PolicyDecision.AllowWithConstraints, Constraints = [] }
                ]
            }
        });

        result.Errors.Should().Contain("Tool rule missing ID");
        result.Errors.Should().Contain("Invalid tool rule ID format: -bad");
        result.Errors.Should().Contain("Duplicate tool rule ID: DUP");
        result.Errors.Should().Contain("Tool rule no-pattern missing tool pattern");
        result.Errors.Should().Contain("Tool rule needs-limits has AllowWithConstraints but no constraints defined");
        result.Errors.Should().Contain("Tool rule empty-limits has AllowWithConstraints but no constraints defined");
    }

    [Fact]
    public void ToolRule_WithConstraints_IsAccepted()
    {
        var result = Validate(Doc() with
        {
            Tools = new ToolPolicyRules
            {
                Rules = [new ToolRule
                {
                    Id = "limited",
                    Tool = "a",
                    Decision = PolicyDecision.AllowWithConstraints,
                    Constraints = new Dictionary<string, object> { ["maxBytes"] = 10 }
                }]
            }
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PluginRules_IdsMustBePresentWellFormedAndUnique_AndNeedAPattern()
    {
        var result = Validate(Doc() with
        {
            Plugins = new PluginPolicyRules
            {
                Rules =
                [
                    new PluginRule { Id = " ", Plugin = "a", Decision = PolicyDecision.Allow },
                    new PluginRule { Id = "bad id", Plugin = "a", Decision = PolicyDecision.Allow },
                    new PluginRule { Id = "dup", Plugin = "a", Decision = PolicyDecision.Allow },
                    new PluginRule { Id = "dup", Plugin = "a", Decision = PolicyDecision.Allow },
                    new PluginRule { Id = "no-pattern", Plugin = "", Decision = PolicyDecision.Allow }
                ]
            }
        });

        result.Errors.Should().Contain("Plugin rule missing ID");
        result.Errors.Should().Contain("Invalid plugin rule ID format: bad id");
        result.Errors.Should().Contain("Duplicate plugin rule ID: dup");
        result.Errors.Should().Contain("Plugin rule no-pattern missing plugin pattern");
    }

    [Theory]
    [InlineData(-1, 10, "maxRetentionDays")]
    [InlineData(0, -5, "maxMemories")]
    public void Memory_LimitsCannotBeNegative(int retention, int memories, string field)
    {
        var result = Validate(Doc() with { Memory = new MemoryPolicyRules { MaxRetentionDays = retention, MaxMemories = memories } });

        result.Errors.Should().ContainSingle(e => e.Contains(field));
    }

    [Theory]
    [InlineData("online")]
    [InlineData("OFFLINE")]
    [InlineData("local")]
    [InlineData("hybrid")]
    public void Connectivity_KnownModes_AreAccepted(string mode)
    {
        Validate(Doc() with
        {
            Connectivity = new ConnectivityPolicyRules { AllowedModes = [mode], DefaultMode = mode }
        }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Connectivity_UnknownModes_AreRefused_InTheListAndAsTheDefault()
    {
        var result = Validate(Doc() with
        {
            Connectivity = new ConnectivityPolicyRules { AllowedModes = ["online", "satellite"], DefaultMode = "warp" }
        });

        result.Errors.Should().Contain("Invalid connectivity mode: satellite");
        result.Errors.Should().Contain("Invalid default connectivity mode: warp");
    }

    [Fact]
    public void Updates_ChannelDeferAndVersionRules()
    {
        var result = Validate(Doc() with
        {
            Updates = new UpdatePolicyRules
            {
                AllowedChannels = ["stable", "Beta", "nightly"],
                RequiredChannel = "preview",
                DeferDays = 400,
                MinimumVersion = "not-a-version"
            }
        });

        result.Errors.Should().Contain("Invalid update channel: nightly");
        result.Errors.Should().Contain("Invalid required update channel: preview");
        result.Errors.Should().Contain(e => e.StartsWith("Invalid deferDays: 400"));
        result.Errors.Should().Contain("Invalid minimumVersion format: not-a-version");
        result.Errors.Should().NotContain(e => e.Contains("Beta"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Updates_DeferDaysOutOfRange_IsRefused(int days)
    {
        Validate(Doc() with { Updates = new UpdatePolicyRules { DeferDays = days } })
            .Errors.Should().ContainSingle(e => e.StartsWith("Invalid deferDays"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(365)]
    public void Updates_DeferDaysBoundaries_AreAccepted(int days)
    {
        Validate(Doc() with { Updates = new UpdatePolicyRules { DeferDays = days, MinimumVersion = "1.2.3" } }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Conflicts_ASubjectInBothAllowAndDeny_IsRefused_CaseInsensitively()
    {
        var result = Validate(Doc() with
        {
            Tools = new ToolPolicyRules { Allow = ["Shell", "ok"], Deny = ["shell"] },
            Plugins = new PluginPolicyRules { Allow = ["com.x"], Deny = ["COM.X"] },
            Connectivity = new ConnectivityPolicyRules { AllowedDomains = ["example.com"], BlockedDomains = ["Example.com"] }
        });

        result.Errors.Should().Contain("Tool 'Shell' appears in both allow and deny lists");
        result.Errors.Should().Contain("Plugin 'com.x' appears in both allow and deny lists");
        result.Errors.Should().Contain("Domain 'example.com' appears in both allowed and blocked lists");
    }

    [Fact]
    public void Conflicts_OnlyOneListPresent_IsFine()
    {
        Validate(Doc() with
        {
            Tools = new ToolPolicyRules { Allow = ["a"] },
            Plugins = new PluginPolicyRules { Deny = ["b"] },
            Connectivity = new ConnectivityPolicyRules { BlockedDomains = ["c.example"] }
        }).IsValid.Should().BeTrue();
    }

    #endregion
}
