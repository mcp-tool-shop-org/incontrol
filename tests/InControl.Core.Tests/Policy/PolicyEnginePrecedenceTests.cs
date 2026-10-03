using FluentAssertions;
using InControl.Core.Plugins;
using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Precedence and edge cases of the policy engine: organization over team over user over
/// session, locked organization defaults, rule conditions, pattern matching and the merged
/// memory, connectivity and update settings.
/// </summary>
public class PolicyEnginePrecedenceTests
{
    private static PolicyDocument Doc(
        ToolPolicyRules? tools = null,
        PluginPolicyRules? plugins = null,
        MemoryPolicyRules? memory = null,
        ConnectivityPolicyRules? connectivity = null,
        UpdatePolicyRules? updates = null,
        bool locked = false) => new()
        {
            Version = "1.0",
            Locked = locked,
            Tools = tools,
            Plugins = plugins,
            Memory = memory,
            Connectivity = connectivity,
            Updates = updates
        };

    private static PolicyEngine Engine(
        PolicyDocument? org = null,
        PolicyDocument? team = null,
        PolicyDocument? user = null,
        PolicyDocument? session = null)
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, org);
        engine.SetPolicy(PolicySource.Team, team);
        engine.SetPolicy(PolicySource.User, user);
        engine.SetPolicy(PolicySource.Session, session);
        return engine;
    }

    #region Engine state

    [Fact]
    public void GetEffectivePolicy_ReportsLockedOrgAndTheFilesItReadsFrom()
    {
        var engine = Engine(org: Doc(locked: true), team: Doc(), user: Doc());

        var info = engine.GetEffectivePolicy();

        info.IsLocked.Should().BeTrue();
        info.Summary.Should().Be("Managed by Organization (locked)");
        info.OrgPolicyPath.Should().Be(PolicyPaths.GetOrgPolicyPath());
        info.TeamPolicyPath.Should().Be(PolicyPaths.GetTeamPolicyPath());
        info.UserPolicyPath.Should().Be(PolicyPaths.GetUserPolicyPath());
        info.LoadedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GetEffectivePolicy_WithNoPolicies_HasNoPaths()
    {
        var info = new PolicyEngine().GetEffectivePolicy();

        info.OrgPolicyPath.Should().BeNull();
        info.TeamPolicyPath.Should().BeNull();
        info.UserPolicyPath.Should().BeNull();
        info.IsLocked.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false, "Organization policy active")]
    [InlineData(false, true, false, "Team policy active")]
    [InlineData(false, false, true, "User policy active")]
    [InlineData(false, false, false, "Default policy")]
    public void Summary_NamesTheHighestPolicyPresent(bool org, bool team, bool user, string expected)
    {
        var engine = Engine(
            org: org ? Doc() : null,
            team: team ? Doc() : null,
            user: user ? Doc() : null);

        engine.GetEffectivePolicy().Summary.Should().Be(expected);
    }

    [Fact]
    public void ClearPolicies_RemovesSessionPoliciesToo()
    {
        var engine = Engine(session: Doc(tools: new ToolPolicyRules { Deny = ["shell"] }));
        engine.EvaluateTool("shell").Decision.Should().Be(PolicyDecision.Deny);

        engine.ClearPolicies();

        engine.EvaluateTool("shell").Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void LoadSummary_CountsPoliciesAndErrors()
    {
        var clean = new PolicyLoadSummary([(PolicySource.User, "u.json")], []);
        var broken = new PolicyLoadSummary([], ["User policy: bad"]);

        clean.PolicyCount.Should().Be(1);
        clean.HasErrors.Should().BeFalse();
        broken.PolicyCount.Should().Be(0);
        broken.HasErrors.Should().BeTrue();
    }

    #endregion

    #region Tool precedence

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EvaluateTool_RequiresAToolId(string? toolId)
    {
        var act = () => new PolicyEngine().EvaluateTool(toolId!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EvaluateTool_OrgDeny_BeatsTeamAndUserAllow()
    {
        var engine = Engine(
            org: Doc(tools: new ToolPolicyRules { Deny = ["shell*"] }),
            team: Doc(tools: new ToolPolicyRules { Allow = ["shell"] }),
            user: Doc(tools: new ToolPolicyRules { Allow = ["shell"] }));

        var result = engine.EvaluateTool("shell");

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Source.Should().Be(PolicySource.Organization);
        result.RuleId.Should().Be("tools.deny.shell*");
        result.SourcePath.Should().Be(PolicyPaths.GetOrgPolicyPath());
    }

    [Fact]
    public void EvaluateTool_AnOrgThatSaysNothingAboutATool_LeavesItToTheTeamThenUserThenSession()
    {
        var org = Doc(tools: new ToolPolicyRules { Deny = ["other"] });

        Engine(org, team: Doc(tools: new ToolPolicyRules { Deny = ["shell"] }))
            .EvaluateTool("shell").Source.Should().Be(PolicySource.Team);
        Engine(org, user: Doc(tools: new ToolPolicyRules { Deny = ["shell"] }))
            .EvaluateTool("shell").Source.Should().Be(PolicySource.User);
        var session = Engine(org, session: Doc(tools: new ToolPolicyRules { Deny = ["shell"] }))
            .EvaluateTool("shell");
        session.Source.Should().Be(PolicySource.Session);
        session.SourcePath.Should().BeNull();
    }

    [Fact]
    public void EvaluateTool_DocumentWithoutAToolsSection_IsSkipped()
    {
        var engine = Engine(
            org: Doc(memory: new MemoryPolicyRules()),
            user: Doc(tools: new ToolPolicyRules { RequireApproval = ["shell"] }));

        engine.EvaluateTool("shell").Decision.Should().Be(PolicyDecision.AllowWithApproval);
    }

    [Fact]
    public void EvaluateTool_InsideOneDocument_DenyBeatsRulesBeatApprovalBeatsAllow()
    {
        var tools = new ToolPolicyRules
        {
            Deny = ["a"],
            Allow = ["a", "b", "c", "d"],
            RequireApproval = ["b", "c", "d"],
            Rules =
            [
                new ToolRule { Id = "rule-allow-c", Tool = "c", Decision = PolicyDecision.Allow },
                new ToolRule { Id = "rule-allow-a", Tool = "a", Decision = PolicyDecision.Allow }
            ]
        };
        var engine = Engine(user: Doc(tools: tools));

        engine.EvaluateTool("a").Decision.Should().Be(PolicyDecision.Deny);
        engine.EvaluateTool("c").Reason.Should().Contain("rule-allow-c");
        engine.EvaluateTool("b").Decision.Should().Be(PolicyDecision.AllowWithApproval);
        engine.EvaluateTool("d").Decision.Should().Be(PolicyDecision.AllowWithApproval);
        engine.EvaluateTool("e").Source.Should().Be(PolicySource.Default);
    }

    [Fact]
    public void EvaluateTool_AllowList_ReportsThePatternItMatched()
    {
        var engine = Engine(team: Doc(tools: new ToolPolicyRules { Allow = ["fs.*"] }));

        var result = engine.EvaluateTool("fs.read");

        result.Decision.Should().Be(PolicyDecision.Allow);
        result.Source.Should().Be(PolicySource.Team);
        result.Reason.Should().Contain("fs.*");
    }

    [Theory]
    [InlineData(PolicyDecision.Allow, PolicyDecision.Allow)]
    [InlineData(PolicyDecision.Deny, PolicyDecision.Deny)]
    [InlineData(PolicyDecision.AllowWithApproval, PolicyDecision.AllowWithApproval)]
    public void EvaluateTool_LockedOrgDefault_DecidesForAnUnlistedTool_AndStopsTheWalk(
        PolicyDecision orgDefault, PolicyDecision expected)
    {
        var engine = Engine(
            org: Doc(tools: new ToolPolicyRules { Default = orgDefault, Allow = ["listed"] }, locked: true),
            team: Doc(tools: new ToolPolicyRules { Allow = ["unlisted"] }));

        var result = engine.EvaluateTool("unlisted");

        result.Decision.Should().Be(expected);
        result.Source.Should().Be(PolicySource.Organization);
        result.Reason.Should().Contain("organization default policy");
    }

    [Fact]
    public void EvaluateTool_LockedOrgDefaultWithConstraints_HasNoToolMeaning_SoLowerPoliciesDecide()
    {
        var engine = Engine(
            org: Doc(tools: new ToolPolicyRules { Default = PolicyDecision.AllowWithConstraints }, locked: true),
            team: Doc(tools: new ToolPolicyRules { Deny = ["x"] }));

        engine.EvaluateTool("x").Source.Should().Be(PolicySource.Team);
    }

    [Fact]
    public void EvaluateTool_ToolsDefaultOnAnUnlockedDocument_IsNotApplied()
    {
        // Current behavior, pinned: unlike plugins, a tools default only counts on a locked
        // organization document. A user or team "default: deny" falls through to the built-in allow.
        var engine = Engine(user: Doc(tools: new ToolPolicyRules { Default = PolicyDecision.Deny }));

        var result = engine.EvaluateTool("anything");

        result.Decision.Should().Be(PolicyDecision.Allow);
        result.Source.Should().Be(PolicySource.Default);
    }

    [Fact]
    public void EvaluateTool_LockedFlagOnATeamDocument_DoesNotApplyItsDefault()
    {
        var engine = Engine(team: Doc(tools: new ToolPolicyRules { Default = PolicyDecision.Deny }, locked: true));

        engine.EvaluateTool("anything").Source.Should().Be(PolicySource.Default);
    }

    [Theory]
    [InlineData("*", "anything", true)]
    [InlineData("shell", "SHELL", true)]
    [InlineData("shell", "shell2", false)]
    [InlineData("fs.*", "fs.read", true)]
    [InlineData("fs.*", "FS.WRITE", true)]
    [InlineData("fs.*", "fsXread", false)]
    [InlineData("*.read", "fs.read", true)]
    [InlineData("*.read", "fs.read.more", false)]
    [InlineData("a*c", "abbbc", true)]
    [InlineData("a*c", "ac", true)]
    [InlineData("a*c", "abd", false)]
    [InlineData("a.b*", "axb", false)]
    [InlineData("a.b*", "a.bz", true)]
    [InlineData("(x)*", "(x)y", true)]
    [InlineData("[ab]*", "ax", false)]
    [InlineData("[ab]*", "[ab]x", true)]
    public void PatternMatching_IsCaseInsensitiveGlobWithLiteralOtherCharacters(string pattern, string tool, bool expected)
    {
        var engine = Engine(user: Doc(tools: new ToolPolicyRules { Deny = [pattern] }));

        var denied = engine.EvaluateTool(tool).Decision == PolicyDecision.Deny;

        denied.Should().Be(expected);
    }

    #endregion

    #region Tool rule decisions and conditions

    private static PolicyEvaluationResult EvaluateRule(ToolRule rule) =>
        Engine(user: Doc(tools: new ToolPolicyRules { Rules = [rule] })).EvaluateTool("t");

    [Fact]
    public void Rule_Allow_UsesTheReasonOrADefaultThatNamesTheRule()
    {
        var withReason = EvaluateRule(new ToolRule { Id = "r1", Tool = "t", Decision = PolicyDecision.Allow, Reason = "Because." });
        var bare = EvaluateRule(new ToolRule { Id = "r1", Tool = "t", Decision = PolicyDecision.Allow });

        withReason.Reason.Should().Be("Because.");
        bare.Reason.Should().Contain("r1");
        bare.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void Rule_Deny_CarriesTheRuleId()
    {
        var result = EvaluateRule(new ToolRule { Id = "no-t", Tool = "t", Decision = PolicyDecision.Deny });

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.RuleId.Should().Be("no-t");
        result.Reason.Should().Contain("no-t");
    }

    [Fact]
    public void Rule_Approval_UsesTheReasonOrADefault()
    {
        EvaluateRule(new ToolRule { Id = "ask", Tool = "t", Decision = PolicyDecision.AllowWithApproval, Reason = "Ask first." })
            .Reason.Should().Be("Ask first.");
        EvaluateRule(new ToolRule { Id = "ask", Tool = "t", Decision = PolicyDecision.AllowWithApproval })
            .Reason.Should().Contain("ask");
    }

    [Fact]
    public void Rule_WithConstraints_ReturnsThem()
    {
        var result = EvaluateRule(new ToolRule
        {
            Id = "limit",
            Tool = "t",
            Decision = PolicyDecision.AllowWithConstraints,
            Constraints = new Dictionary<string, object> { ["maxBytes"] = 1024 }
        });

        result.Decision.Should().Be(PolicyDecision.AllowWithConstraints);
        result.Constraints.Should().ContainKey("maxBytes").WhoseValue.Should().Be(1024);
        result.Reason.Should().Contain("limit");
    }

    [Fact]
    public void Rule_WithConstraintsDecisionButNoConstraints_FallsBackToAllow()
    {
        // The validator rejects this combination when a file is loaded; the engine itself stays total.
        var result = EvaluateRule(new ToolRule { Id = "x", Tool = "t", Decision = PolicyDecision.AllowWithConstraints });

        result.Decision.Should().Be(PolicyDecision.Allow);
        result.Constraints.Should().BeNull();
    }

    [Fact]
    public void Rule_ThatDoesNotMatchTheTool_IsSkipped()
    {
        var engine = Engine(user: Doc(tools: new ToolPolicyRules
        {
            Rules = [new ToolRule { Id = "other", Tool = "other", Decision = PolicyDecision.Deny }]
        }));

        engine.EvaluateTool("t").Decision.Should().Be(PolicyDecision.Allow);
    }

    private static string Hhmm(DateTime t) => t.ToString("HH:mm");

    [Fact]
    public void Condition_TimeRangeAroundNow_Applies()
    {
        var now = DateTime.Now;
        var range = $"{Hhmm(now.AddHours(-1))}-{Hhmm(now.AddHours(1))}";

        EvaluateRule(new ToolRule
        {
            Id = "window",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions { TimeRange = range }
        }).Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void Condition_TimeRangeThatExcludesNow_DoesNotApply()
    {
        var now = DateTime.Now;
        var later = $"{Hhmm(now.AddHours(2))}-{Hhmm(now.AddHours(3))}";

        EvaluateRule(new ToolRule
        {
            Id = "window",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions { TimeRange = later }
        }).Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void Condition_WrappingRange_ExcludesTheGapAroundNow_AndCoversTheRestOfTheDay()
    {
        var now = DateTime.Now;
        // Starts an hour ahead and ends an hour ago: the gap (now-1h, now+1h) is outside the range.
        var excludesNow = $"{Hhmm(now.AddHours(1))}-{Hhmm(now.AddHours(-1))}";
        // Starts an hour ago and ends two hours ago: everything except (now-2h, now-1h) is inside.
        var includesNow = $"{Hhmm(now.AddHours(-1))}-{Hhmm(now.AddHours(-2))}";

        EvaluateRule(new ToolRule
        {
            Id = "gap",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions { TimeRange = excludesNow }
        }).Decision.Should().Be(PolicyDecision.Allow);

        EvaluateRule(new ToolRule
        {
            Id = "wrap",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions { TimeRange = includesNow }
        }).Decision.Should().Be(PolicyDecision.Deny);
    }

    [Theory]
    [InlineData("not a range")]
    [InlineData("09:00")]
    [InlineData("09:00-10:00-11:00")]
    [InlineData("xx-yy")]
    [InlineData("")]
    public void Condition_UnparseableTimeRange_IsIgnoredSoTheRuleStillApplies(string range)
    {
        // Pinned: a malformed range does not switch the rule off. The validator does not check
        // time ranges, so a typo in an allow rule would widen access; reported, not changed here.
        EvaluateRule(new ToolRule
        {
            Id = "typo",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions { TimeRange = range }
        }).Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void Condition_DaysOfWeek_MatchesToday_AndSkipsOtherDays()
    {
        var today = (int)DateTime.Now.DayOfWeek;
        var tomorrow = (today + 1) % 7;

        EvaluateRule(new ToolRule
        {
            Id = "today",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions { DaysOfWeek = [today] }
        }).Decision.Should().Be(PolicyDecision.Deny);

        EvaluateRule(new ToolRule
        {
            Id = "tomorrow",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions { DaysOfWeek = [tomorrow] }
        }).Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void Condition_EmptyDaysOrNoConditions_AlwaysApply()
    {
        EvaluateRule(new ToolRule
        {
            Id = "empty",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions { DaysOfWeek = [] }
        }).Decision.Should().Be(PolicyDecision.Deny);

        EvaluateRule(new ToolRule
        {
            Id = "plain",
            Tool = "t",
            Decision = PolicyDecision.Deny,
            Conditions = new RuleConditions()
        }).Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void Condition_ThatDoesNotHold_LetsTheNextRuleDecide()
    {
        var other = ((int)DateTime.Now.DayOfWeek + 1) % 7;
        var engine = Engine(user: Doc(tools: new ToolPolicyRules
        {
            Rules =
            [
                new ToolRule { Id = "weekday", Tool = "t", Decision = PolicyDecision.Deny, Conditions = new RuleConditions { DaysOfWeek = [other] } },
                new ToolRule { Id = "fallback", Tool = "t", Decision = PolicyDecision.AllowWithApproval }
            ]
        }));

        engine.EvaluateTool("t").Reason.Should().Contain("fallback");
    }

    #endregion

    #region Plugin evaluation

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void EvaluatePlugin_RequiresAnId(string? id)
    {
        var act = () => new PolicyEngine().EvaluatePlugin(id!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EvaluatePlugin_NoPolicy_RequiresApproval()
    {
        var result = new PolicyEngine().EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(PolicyDecision.AllowWithApproval);
        result.Source.Should().Be(PolicySource.Default);
    }

    [Theory]
    [InlineData(PolicySource.Team)]
    [InlineData(PolicySource.User)]
    public void EvaluatePlugin_DisabledAtAnyLevel_DeniesEverything(PolicySource source)
    {
        var disabled = Doc(plugins: new PluginPolicyRules { Enabled = false });
        var engine = source == PolicySource.Team ? Engine(team: disabled) : Engine(user: disabled);

        var result = engine.EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Source.Should().Be(source);
        result.RuleId.Should().Be("plugins.enabled.false");
    }

    [Fact]
    public void EvaluatePlugin_OrgDisable_BeatsAnExplicitUserAllow()
    {
        var engine = Engine(
            org: Doc(plugins: new PluginPolicyRules { Enabled = false }),
            user: Doc(plugins: new PluginPolicyRules { Allow = ["com.test.p"] }));

        var result = engine.EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Source.Should().Be(PolicySource.Organization);
    }

    [Theory]
    [InlineData(PluginRiskLevelPolicy.ReadOnly, PluginRiskLevel.ReadOnly, true)]
    [InlineData(PluginRiskLevelPolicy.ReadOnly, PluginRiskLevel.LocalMutation, false)]
    [InlineData(PluginRiskLevelPolicy.ReadOnly, PluginRiskLevel.Network, false)]
    [InlineData(PluginRiskLevelPolicy.LocalMutation, PluginRiskLevel.LocalMutation, true)]
    [InlineData(PluginRiskLevelPolicy.LocalMutation, PluginRiskLevel.Network, false)]
    [InlineData(PluginRiskLevelPolicy.Network, PluginRiskLevel.Network, true)]
    public void EvaluatePlugin_RiskLevelAboveTheMaximum_IsDenied(
        PluginRiskLevelPolicy max, PluginRiskLevel risk, bool withinLimit)
    {
        var engine = Engine(team: Doc(plugins: new PluginPolicyRules
        {
            MaxRiskLevel = max,
            Default = PolicyDecision.Allow
        }));

        var result = engine.EvaluatePlugin("com.test.p", riskLevel: risk);

        if (withinLimit)
        {
            result.Decision.Should().Be(PolicyDecision.Allow);
        }
        else
        {
            result.Decision.Should().Be(PolicyDecision.Deny);
            result.Source.Should().Be(PolicySource.Team);
            result.RuleId.Should().StartWith("plugins.maxRiskLevel.");
        }
    }

    [Fact]
    public void EvaluatePlugin_RiskLimitFromAnyOfOrgTeamUser_AndAnUnknownLimitMeansNetwork()
    {
        var limit = new PluginPolicyRules { MaxRiskLevel = PluginRiskLevelPolicy.ReadOnly };

        Engine(org: Doc(plugins: limit)).EvaluatePlugin("p", riskLevel: PluginRiskLevel.Network)
            .Source.Should().Be(PolicySource.Organization);
        Engine(user: Doc(plugins: limit)).EvaluatePlugin("p", riskLevel: PluginRiskLevel.Network)
            .Source.Should().Be(PolicySource.User);

        var unknown = new PluginPolicyRules { MaxRiskLevel = (PluginRiskLevelPolicy)99, Default = PolicyDecision.Allow };
        Engine(team: Doc(plugins: unknown)).EvaluatePlugin("p", riskLevel: PluginRiskLevel.Network)
            .Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluatePlugin_WithoutARiskLevel_SkipsTheRiskCheck()
    {
        var engine = Engine(team: Doc(plugins: new PluginPolicyRules
        {
            MaxRiskLevel = PluginRiskLevelPolicy.ReadOnly,
            Default = PolicyDecision.Allow
        }));

        engine.EvaluatePlugin("p").Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluatePlugin_DenyListWinsOverRulesAuthorsAndAllow()
    {
        var engine = Engine(user: Doc(plugins: new PluginPolicyRules
        {
            Deny = ["com.bad.*"],
            Allow = ["com.bad.one"],
            TrustedAuthors = ["Friend"],
            Rules = [new PluginRule { Id = "ok", Plugin = "com.bad.one", Decision = PolicyDecision.Allow }]
        }));

        var result = engine.EvaluatePlugin("com.bad.one", author: "Friend");

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.RuleId.Should().Be("plugins.deny.com.bad.*");
    }

    [Theory]
    [InlineData(PolicyDecision.Allow, PolicyDecision.Allow, null)]
    [InlineData(PolicyDecision.Deny, PolicyDecision.Deny, "deny-rule")]
    [InlineData(PolicyDecision.AllowWithApproval, PolicyDecision.AllowWithApproval, null)]
    public void EvaluatePlugin_Rules_ProduceTheirDecision(PolicyDecision decision, PolicyDecision expected, string? ruleId)
    {
        var engine = Engine(user: Doc(plugins: new PluginPolicyRules
        {
            Rules = [new PluginRule { Id = "deny-rule", Plugin = "com.test.*", Decision = decision }]
        }));

        var result = engine.EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(expected);
        result.RuleId.Should().Be(ruleId);
        result.Reason.Should().Contain("deny-rule");
    }

    [Fact]
    public void EvaluatePlugin_RuleReason_ReplacesTheDefaultText()
    {
        var engine = Engine(user: Doc(plugins: new PluginPolicyRules
        {
            Rules = [new PluginRule { Id = "r", Plugin = "*", Decision = PolicyDecision.Allow, Reason = "Reviewed." }]
        }));

        engine.EvaluatePlugin("any").Reason.Should().Be("Reviewed.");
    }

    [Fact]
    public void EvaluatePlugin_RuleWithConstraintsDecision_IsNotAPluginDecision_SoTheDefaultApplies()
    {
        var engine = Engine(user: Doc(plugins: new PluginPolicyRules
        {
            Default = PolicyDecision.Deny,
            Rules = [new PluginRule { Id = "c", Plugin = "*", Decision = PolicyDecision.AllowWithConstraints }]
        }));

        var result = engine.EvaluatePlugin("any");

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.RuleId.Should().Be("plugins.default");
    }

    [Fact]
    public void EvaluatePlugin_TrustedAuthor_IsCaseInsensitive_AndNeedsAnAuthor()
    {
        var engine = Engine(user: Doc(plugins: new PluginPolicyRules
        {
            Default = PolicyDecision.Deny,
            TrustedAuthors = ["Acme Corp"]
        }));

        engine.EvaluatePlugin("p", author: "acme corp").Decision.Should().Be(PolicyDecision.Allow);
        engine.EvaluatePlugin("p", author: "Other").Decision.Should().Be(PolicyDecision.Deny);
        engine.EvaluatePlugin("p", author: null).Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void EvaluatePlugin_AllowList_MatchesPatterns()
    {
        var engine = Engine(team: Doc(plugins: new PluginPolicyRules
        {
            Default = PolicyDecision.Deny,
            Allow = ["com.acme.*"]
        }));

        engine.EvaluatePlugin("com.acme.tool").Decision.Should().Be(PolicyDecision.Allow);
        engine.EvaluatePlugin("com.other.tool").Decision.Should().Be(PolicyDecision.Deny);
    }

    [Theory]
    [InlineData(PolicyDecision.Allow)]
    [InlineData(PolicyDecision.Deny)]
    [InlineData(PolicyDecision.AllowWithApproval)]
    [InlineData(PolicyDecision.AllowWithConstraints)]
    public void EvaluatePlugin_LockedOrgDefault_StopsTheWalkForAnUnlistedPlugin(PolicyDecision orgDefault)
    {
        var engine = Engine(
            org: Doc(plugins: new PluginPolicyRules { Default = orgDefault }, locked: true),
            user: Doc(plugins: new PluginPolicyRules { Allow = ["com.test.p"] }));

        var result = engine.EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(orgDefault);
        result.Source.Should().Be(PolicySource.Organization);
        if (orgDefault == PolicyDecision.AllowWithConstraints)
        {
            result.Constraints.Should().NotBeNull().And.BeEmpty();
        }
    }

    [Fact]
    public void EvaluatePlugin_LockedOrgThatListsThePlugin_StillUsesTheListedDecision()
    {
        var engine = Engine(org: Doc(
            plugins: new PluginPolicyRules { Default = PolicyDecision.Deny, Allow = ["com.test.p"] },
            locked: true));

        engine.EvaluatePlugin("com.test.p").Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void EvaluatePlugin_UnlockedDocuments_ContributeTheirDefault_HighestFirst()
    {
        var engine = Engine(
            org: Doc(plugins: new PluginPolicyRules { Default = PolicyDecision.AllowWithApproval }),
            team: Doc(plugins: new PluginPolicyRules { Default = PolicyDecision.Deny }));

        var result = engine.EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(PolicyDecision.AllowWithApproval);
        result.Source.Should().Be(PolicySource.Organization);
    }

    [Fact]
    public void EvaluatePlugin_ALowerDocumentsExplicitEntry_BeatsAnUnlockedHigherDefault()
    {
        var engine = Engine(
            org: Doc(plugins: new PluginPolicyRules { Default = PolicyDecision.Deny }),
            user: Doc(plugins: new PluginPolicyRules { Allow = ["com.test.p"] }));

        var result = engine.EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(PolicyDecision.Allow);
        result.Source.Should().Be(PolicySource.User);
    }

    [Fact]
    public void EvaluatePlugin_SessionPolicy_IsConsultedLast()
    {
        var engine = Engine(session: Doc(plugins: new PluginPolicyRules { Deny = ["com.test.p"] }));

        var result = engine.EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Source.Should().Be(PolicySource.Session);
        result.SourcePath.Should().BeNull();
    }

    [Fact]
    public void EvaluatePlugin_UnknownDefaultDecision_DeniesRatherThanAllows()
    {
        var engine = Engine(user: Doc(plugins: new PluginPolicyRules { Default = (PolicyDecision)99 }));

        var result = engine.EvaluatePlugin("com.test.p");

        result.Decision.Should().Be(PolicyDecision.Deny);
        result.RuleId.Should().Be("plugins.default");
    }

    [Fact]
    public void EvaluatePlugin_DocumentsWithoutAPluginsSection_AreSkipped()
    {
        var engine = Engine(
            org: Doc(tools: new ToolPolicyRules()),
            team: Doc(plugins: new PluginPolicyRules { Default = PolicyDecision.Allow }));

        var result = engine.EvaluatePlugin("com.test.p");

        result.Source.Should().Be(PolicySource.Team);
    }

    #endregion

    #region Memory evaluation

    [Fact]
    public void EvaluateMemory_FieldsComeFromTheHighestDocumentThatSetsThem()
    {
        var engine = Engine(
            org: Doc(memory: new MemoryPolicyRules { Enabled = true, MaxRetentionDays = 30, MaxMemories = 0 }),
            team: Doc(memory: new MemoryPolicyRules { MaxRetentionDays = 90, MaxMemories = 500, AutoFormation = false }),
            user: Doc(memory: new MemoryPolicyRules { MaxMemories = 50, AllowExport = false, AllowImport = false, EncryptAtRest = false }));

        var result = engine.EvaluateMemoryPolicy();

        result.MaxRetentionDays.Should().Be(30);
        result.MaxMemories.Should().Be(500);
        result.AutoFormation.Should().BeTrue("the org document sets every flag, even to its default");
        result.AllowExport.Should().BeTrue();
        result.AllowImport.Should().BeTrue();
        result.EncryptAtRest.Should().BeTrue();
    }

    [Fact]
    public void EvaluateMemory_ZeroLimitsInLowerDocuments_AreUnsetNotUnlimitedOverrides()
    {
        var engine = Engine(
            team: Doc(memory: new MemoryPolicyRules { MaxRetentionDays = 0, MaxMemories = 0 }),
            user: Doc(memory: new MemoryPolicyRules { MaxRetentionDays = 7, MaxMemories = 12 }));

        var result = engine.EvaluateMemoryPolicy();

        result.MaxRetentionDays.Should().Be(7);
        result.MaxMemories.Should().Be(12);
    }

    [Fact]
    public void EvaluateMemory_OrgDisable_BeatsUserEnable()
    {
        var engine = Engine(
            org: Doc(memory: new MemoryPolicyRules { Enabled = false }),
            user: Doc(memory: new MemoryPolicyRules { Enabled = true }));

        engine.EvaluateMemoryPolicy().Enabled.Should().BeFalse();
    }

    [Fact]
    public void EvaluateMemory_ExcludeCategoriesAreTheCaseInsensitiveUnionOfAllLevels()
    {
        var engine = Engine(
            org: Doc(memory: new MemoryPolicyRules { ExcludeCategories = ["Health"] }),
            team: Doc(memory: new MemoryPolicyRules { ExcludeCategories = ["health", "finance"] }),
            user: Doc(memory: new MemoryPolicyRules { ExcludeCategories = ["Passwords"] }));

        engine.EvaluateMemoryPolicy().ExcludeCategories.Should().BeEquivalentTo("Health", "finance", "Passwords");
    }

    #endregion

    #region Connectivity evaluation

    [Fact]
    public void EvaluateConnectivity_FieldsFollowOrgTeamUser()
    {
        var engine = Engine(
            org: Doc(connectivity: new ConnectivityPolicyRules { AllowedModes = ["offline"], AllowTelemetry = false }),
            team: Doc(connectivity: new ConnectivityPolicyRules { AllowedModes = ["online"], DefaultMode = "local", AllowModeChange = false }),
            user: Doc(connectivity: new ConnectivityPolicyRules { DefaultMode = "online" }));

        var result = engine.EvaluateConnectivityPolicy();

        result.AllowedModes.Should().Equal("offline");
        result.DefaultMode.Should().Be("local", "the org sets no default, so the team's applies before the user's");
        result.AllowTelemetry.Should().BeFalse();
        // Current behavior, pinned: a flag is a plain bool, so a document that has a connectivity
        // section and says nothing about it still "sets" it to the default. The org's silent
        // allowModeChange (true) beats the team's explicit false.
        result.AllowModeChange.Should().BeTrue();
    }

    [Fact]
    public void EvaluateConnectivity_DefaultModeAndFlags_ComeFromTheHighestSetter()
    {
        var engine = Engine(
            org: Doc(connectivity: new ConnectivityPolicyRules { DefaultMode = "offline", AllowModeChange = false, AllowTelemetry = false }),
            team: Doc(connectivity: new ConnectivityPolicyRules { DefaultMode = "online" }));

        var result = engine.EvaluateConnectivityPolicy();

        result.DefaultMode.Should().Be("offline");
        result.AllowModeChange.Should().BeFalse();
        result.AllowTelemetry.Should().BeFalse();
    }

    [Fact]
    public void EvaluateConnectivity_TeamAndUserSettings_ApplyWhenOrgHasNone()
    {
        var engine = Engine(
            team: Doc(connectivity: new ConnectivityPolicyRules { AllowedModes = ["local"] }),
            user: Doc(connectivity: new ConnectivityPolicyRules { DefaultMode = "local", AllowTelemetry = false }));

        var result = engine.EvaluateConnectivityPolicy();

        result.AllowedModes.Should().Equal("local");
        result.DefaultMode.Should().Be("local");
        result.AllowTelemetry.Should().BeTrue("the team document has a connectivity section, so its default true is the setting");
    }

    [Fact]
    public void EvaluateConnectivity_AUserRestriction_AppliesWhenNoHigherDocumentHasAConnectivitySection()
    {
        var engine = Engine(
            org: Doc(tools: new ToolPolicyRules()),
            user: Doc(connectivity: new ConnectivityPolicyRules { AllowTelemetry = false, AllowModeChange = false }));

        var result = engine.EvaluateConnectivityPolicy();

        result.AllowTelemetry.Should().BeFalse();
        result.AllowModeChange.Should().BeFalse();
    }

    [Fact]
    public void EvaluateConnectivity_AllowedDomainsComeFromOneLevelOnly_ButBlockedDomainsAreUnioned()
    {
        var engine = Engine(
            org: Doc(connectivity: new ConnectivityPolicyRules { AllowedDomains = ["a.example"], BlockedDomains = ["x.example"] }),
            team: Doc(connectivity: new ConnectivityPolicyRules { AllowedDomains = ["b.example"], BlockedDomains = ["y.example"] }),
            user: Doc(connectivity: new ConnectivityPolicyRules { BlockedDomains = ["X.EXAMPLE", "z.example"] }));

        var result = engine.EvaluateConnectivityPolicy();

        result.AllowedDomains.Should().Equal("a.example");
        result.BlockedDomains.Should().BeEquivalentTo("x.example", "y.example", "z.example");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvaluateConnectivity_AllowedDomainsFallBackToTeamThenUser(bool fromTeam)
    {
        var rules = new ConnectivityPolicyRules { AllowedDomains = ["only.example"] };
        var engine = fromTeam ? Engine(team: Doc(connectivity: rules)) : Engine(user: Doc(connectivity: rules));

        engine.EvaluateConnectivityPolicy().AllowedDomains.Should().Equal("only.example");
    }

    [Fact]
    public void EvaluateConnectivity_AnEmptyAllowedDomainsList_MeansNoRestriction()
    {
        var engine = Engine(org: Doc(connectivity: new ConnectivityPolicyRules { AllowedDomains = [] }));

        engine.EvaluateConnectivityPolicy().AllowedDomains.Should().BeNull();
        engine.EvaluateDomain("anything.example").Decision.Should().Be(PolicyDecision.Allow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EvaluateDomain_RequiresADomain(string? domain)
    {
        var act = () => new PolicyEngine().EvaluateDomain(domain!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EvaluateDomain_BlockWinsOverAllow_AndLookalikesAreNotMatched()
    {
        var engine = Engine(org: Doc(connectivity: new ConnectivityPolicyRules
        {
            AllowedDomains = ["example.com"],
            BlockedDomains = ["ads.example.com"]
        }));

        engine.EvaluateDomain("ads.example.com").Decision.Should().Be(PolicyDecision.Deny);
        engine.EvaluateDomain("x.ads.example.com").Decision.Should().Be(PolicyDecision.Deny);
        engine.EvaluateDomain("example.com").Decision.Should().Be(PolicyDecision.Allow);
        engine.EvaluateDomain("api.example.com").Decision.Should().Be(PolicyDecision.Allow);
        engine.EvaluateDomain("notexample.com").Decision.Should().Be(PolicyDecision.Deny);
        engine.EvaluateDomain("example.com.evil.net").Decision.Should().Be(PolicyDecision.Deny);
    }

    #endregion

    #region Update evaluation

    [Fact]
    public void EvaluateUpdates_FieldsFollowOrgTeamUser()
    {
        var engine = Engine(
            org: Doc(updates: new UpdatePolicyRules { AutoUpdate = false, RequiredChannel = "stable", DeferDays = 14 }),
            team: Doc(updates: new UpdatePolicyRules { RequiredChannel = "beta", DeferDays = 3, MinimumVersion = "1.2.0", AllowedChannels = ["stable", "beta"] }),
            user: Doc(updates: new UpdatePolicyRules { CheckOnStartup = false, MinimumVersion = "9.9.9", AllowedChannels = ["dev"] }));

        var result = engine.EvaluateUpdatePolicy();

        result.AutoUpdate.Should().BeFalse();
        result.RequiredChannel.Should().Be("stable");
        result.DeferDays.Should().Be(14);
        result.MinimumVersion.Should().Be("1.2.0");
        result.AllowedChannels.Should().Equal("stable", "beta");
        result.CheckOnStartup.Should().BeTrue("the org document sets it, even to the default");
    }

    [Fact]
    public void EvaluateUpdates_UserSettingsApplyWhenNoHigherLevelSetsThem()
    {
        var engine = Engine(user: Doc(updates: new UpdatePolicyRules
        {
            AutoUpdate = false,
            CheckOnStartup = false,
            DeferDays = 5,
            RequiredChannel = "beta",
            MinimumVersion = "2.0.0",
            AllowedChannels = ["beta"]
        }));

        var result = engine.EvaluateUpdatePolicy();

        result.AutoUpdate.Should().BeFalse();
        result.CheckOnStartup.Should().BeFalse();
        result.DeferDays.Should().Be(5);
        result.RequiredChannel.Should().Be("beta");
        result.MinimumVersion.Should().Be("2.0.0");
        result.AllowedChannels.Should().Equal("beta");
    }

    #endregion

    #region Audit log

    [Fact]
    public void AuditLog_ListsNewestFirst_AndHonoursTheLimit()
    {
        var engine = new PolicyEngine();
        engine.EvaluateTool("first");
        engine.EvaluateTool("second");
        engine.EvaluateTool("third");

        engine.GetAuditLog(2).Select(e => e.Subject).Should().Equal("third", "second");
        engine.GetAuditLog().Should().HaveCount(3);
    }

    [Fact]
    public void AuditLog_RecordsCategoryActionAndRule()
    {
        var engine = Engine(user: Doc(plugins: new PluginPolicyRules { Deny = ["p"] }));

        engine.EvaluatePlugin("p");
        engine.EvaluateDomain("example.com");
        engine.EvaluateTool("t", action: "invoke");

        var log = engine.GetAuditLog();
        log.Should().Contain(e => e.Category == PolicyCategory.Plugins && e.Action == "load" && e.RuleId == "plugins.deny.p");
        log.Should().Contain(e => e.Category == PolicyCategory.Connectivity && e.Action == "access");
        log.Should().Contain(e => e.Category == PolicyCategory.Tools && e.Action == "invoke");
    }

    [Fact]
    public void AuditLog_IsBounded_DroppingTheOldestThousandOnceOverTenThousand()
    {
        var engine = new PolicyEngine();

        for (var i = 0; i < 10_001; i++)
        {
            engine.EvaluateTool("t" + i);
        }

        var log = engine.GetAuditLog(limit: 20_000);
        log.Should().HaveCount(9_001);
        log[0].Subject.Should().Be("t10000");
        log[^1].Subject.Should().Be("t1000");
    }

    #endregion
}
