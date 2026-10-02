using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy document schema and serialization.
/// </summary>
public class PolicyDocumentTests
{
    #region PolicyDocument Schema Tests

    [Fact]
    public void PolicyDocument_DefaultsAreReasonable()
    {
        var doc = new PolicyDocument();

        Assert.Equal("1.0", doc.Version);
        Assert.False(doc.Locked);
        Assert.Null(doc.Tools);
        Assert.Null(doc.Plugins);
        Assert.Null(doc.Memory);
        Assert.Null(doc.Connectivity);
        Assert.Null(doc.Updates);
    }

    [Fact]
    public void PolicyDocument_CanBeFullyPopulated()
    {
        var doc = new PolicyDocument
        {
            Id = "org-policy-2024",
            Name = "Organization Policy",
            Description = "Corporate security policy",
            Created = DateTimeOffset.UtcNow.AddDays(-30),
            Modified = DateTimeOffset.UtcNow,
            Locked = true,
            Tools = new ToolPolicyRules
            {
                Default = PolicyDecision.AllowWithApproval,
                Allow = ["read-file", "write-file"],
                Deny = ["execute-shell"]
            },
            Plugins = new PluginPolicyRules
            {
                Enabled = true,
                MaxRiskLevel = PluginRiskLevelPolicy.LocalMutation
            },
            Memory = new MemoryPolicyRules
            {
                MaxRetentionDays = 90,
                EncryptAtRest = true
            },
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedModes = ["online", "local"],
                DefaultMode = "online"
            },
            Updates = new UpdatePolicyRules
            {
                AutoUpdate = true,
                RequiredChannel = "stable"
            }
        };

        Assert.Equal("org-policy-2024", doc.Id);
        Assert.True(doc.Locked);
        Assert.Equal(PolicyDecision.AllowWithApproval, doc.Tools!.Default);
        Assert.True(doc.Plugins!.Enabled);
        Assert.Equal(90, doc.Memory!.MaxRetentionDays);
        Assert.Contains("online", doc.Connectivity!.AllowedModes!);
        Assert.True(doc.Updates!.AutoUpdate);
    }

    #endregion

    #region Tool Policy Rules Tests

    [Fact]
    public void ToolPolicyRules_DefaultsToAllow()
    {
        var rules = new ToolPolicyRules();

        Assert.Equal(PolicyDecision.Allow, rules.Default);
        Assert.Null(rules.Allow);
        Assert.Null(rules.Deny);
        Assert.Null(rules.RequireApproval);
    }

    [Fact]
    public void ToolRule_CanHaveConstraints()
    {
        var rule = new ToolRule
        {
            Id = "rate-limit-search",
            Tool = "web-search",
            Decision = PolicyDecision.AllowWithConstraints,
            Reason = "Rate limited for cost control",
            Constraints = new Dictionary<string, object>
            {
                ["max_requests_per_hour"] = 100,
                ["allowed_providers"] = new[] { "bing", "duckduckgo" }
            }
        };

        Assert.Equal(PolicyDecision.AllowWithConstraints, rule.Decision);
        Assert.NotNull(rule.Constraints);
        Assert.Equal(100, Convert.ToInt32(rule.Constraints["max_requests_per_hour"]));
    }

    [Fact]
    public void ToolRule_CanHaveConditions()
    {
        var rule = new ToolRule
        {
            Id = "business-hours-only",
            Tool = "send-email",
            Decision = PolicyDecision.Allow,
            Conditions = new RuleConditions
            {
                TimeRange = "09:00-17:00",
                DaysOfWeek = [1, 2, 3, 4, 5] // Mon-Fri
            }
        };

        Assert.NotNull(rule.Conditions);
        Assert.Equal("09:00-17:00", rule.Conditions.TimeRange);
        Assert.Equal(5, rule.Conditions.DaysOfWeek!.Count);
    }

    #endregion

    #region Plugin Policy Rules Tests

    [Fact]
    public void PluginPolicyRules_DefaultsToApproval()
    {
        var rules = new PluginPolicyRules();

        Assert.True(rules.Enabled);
        Assert.Equal(PolicyDecision.AllowWithApproval, rules.Default);
        Assert.Equal(PluginRiskLevelPolicy.Network, rules.MaxRiskLevel);
    }

    [Fact]
    public void PluginRule_CanDenyByPattern()
    {
        var rule = new PluginRule
        {
            Id = "block-unknown",
            Plugin = "com.unknown.*",
            Decision = PolicyDecision.Deny,
            Reason = "Unknown publisher not trusted"
        };

        Assert.Equal("com.unknown.*", rule.Plugin);
        Assert.Equal(PolicyDecision.Deny, rule.Decision);
    }

    [Fact]
    public void PluginPolicyRules_CanTrustAuthors()
    {
        var rules = new PluginPolicyRules
        {
            TrustedAuthors = ["InControl Team", "Verified Publisher"]
        };

        Assert.Contains("InControl Team", rules.TrustedAuthors);
    }

    #endregion

    #region Memory Policy Rules Tests

    [Fact]
    public void MemoryPolicyRules_HasSensibleDefaults()
    {
        var rules = new MemoryPolicyRules();

        Assert.True(rules.Enabled);
        Assert.Equal(0, rules.MaxRetentionDays); // Unlimited
        Assert.Equal(10000, rules.MaxMemories);
        Assert.True(rules.EncryptAtRest);
        Assert.True(rules.AutoFormation);
        Assert.True(rules.AllowExport);
        Assert.True(rules.AllowImport);
    }

    [Fact]
    public void MemoryPolicyRules_CanRestrictCategories()
    {
        var rules = new MemoryPolicyRules
        {
            ExcludeCategories = ["passwords", "api-keys", "personal-health"]
        };

        Assert.Equal(3, rules.ExcludeCategories.Count);
        Assert.Contains("passwords", rules.ExcludeCategories);
    }

    #endregion

    #region Connectivity Policy Rules Tests

    [Fact]
    public void ConnectivityPolicyRules_DefaultsToFlexible()
    {
        var rules = new ConnectivityPolicyRules();

        Assert.True(rules.AllowModeChange);
        Assert.True(rules.AllowTelemetry);
        Assert.Null(rules.AllowedModes);
    }

    [Fact]
    public void ConnectivityPolicyRules_CanRestrictModes()
    {
        var rules = new ConnectivityPolicyRules
        {
            AllowedModes = ["offline"],
            AllowModeChange = false,
            DefaultMode = "offline"
        };

        Assert.Single(rules.AllowedModes);
        Assert.False(rules.AllowModeChange);
    }

    [Fact]
    public void ConnectivityPolicyRules_CanBlockDomains()
    {
        var rules = new ConnectivityPolicyRules
        {
            BlockedDomains = ["suspicious.com", "malware.net"]
        };

        Assert.Equal(2, rules.BlockedDomains.Count);
    }

    #endregion

    #region Update Policy Rules Tests

    [Fact]
    public void UpdatePolicyRules_DefaultsToAuto()
    {
        var rules = new UpdatePolicyRules();

        Assert.True(rules.AutoUpdate);
        Assert.True(rules.CheckOnStartup);
        Assert.Equal(0, rules.DeferDays);
    }

    [Fact]
    public void UpdatePolicyRules_CanLockToChannel()
    {
        var rules = new UpdatePolicyRules
        {
            RequiredChannel = "stable",
            AllowedChannels = ["stable"],
            DeferDays = 7
        };

        Assert.Equal("stable", rules.RequiredChannel);
        Assert.Single(rules.AllowedChannels);
        Assert.Equal(7, rules.DeferDays);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void PolicySerializer_RoundTripsDocument()
    {
        var original = new PolicyDocument
        {
            Id = "test-policy",
            Name = "Test Policy",
            Version = "1.0",
            Locked = true,
            Tools = new ToolPolicyRules
            {
                Default = PolicyDecision.AllowWithApproval,
                Allow = ["safe-tool"],
                Deny = ["dangerous-tool"]
            }
        };

        var json = PolicySerializer.ToJson(original);
        var result = PolicySerializer.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal("test-policy", result.Document.Id);
        Assert.True(result.Document.Locked);
        Assert.Contains("safe-tool", result.Document.Tools!.Allow!);
    }

    [Fact]
    public void PolicySerializer_HandlesInvalidJson()
    {
        var result = PolicySerializer.LoadFromJson("{ invalid json }");

        Assert.False(result.IsSuccess);
        Assert.Contains("JSON parse error", result.Error);
    }

    [Fact]
    public void PolicySerializer_PreservesEnums()
    {
        var original = new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Default = PolicyDecision.AllowWithConstraints,
                Rules =
                [
                    new ToolRule
                    {
                        Id = "test-rule",
                        Tool = "test",
                        Decision = PolicyDecision.Deny
                    }
                ]
            }
        };

        var json = PolicySerializer.ToJson(original);
        var result = PolicySerializer.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.AllowWithConstraints, result.Document!.Tools!.Default);
        Assert.Equal(PolicyDecision.Deny, result.Document.Tools.Rules![0].Decision);
    }

    [Fact]
    public void PolicySerializer_AllowsComments()
    {
        var json = """
        {
            // This is a comment
            "version": "1.0",
            "name": "Test Policy"
        }
        """;

        var result = PolicySerializer.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.Equal("Test Policy", result.Document!.Name);
    }

    [Fact]
    public void PolicySerializer_AllowsTrailingCommas()
    {
        var json = """
        {
            "version": "1.0",
            "tools": {
                "allow": [
                    "tool1",
                    "tool2",
                ],
            },
        }
        """;

        var result = PolicySerializer.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Document!.Tools!.Allow!.Count);
    }

    [Fact]
    public void PolicySerializer_IsCaseInsensitive()
    {
        var json = """
        {
            "VERSION": "1.0",
            "Name": "Test",
            "LOCKED": true
        }
        """;

        var result = PolicySerializer.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.Equal("1.0", result.Document!.Version);
        Assert.Equal("Test", result.Document.Name);
        Assert.True(result.Document.Locked);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void PolicyValidator_AcceptsValidDocument()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Name = "Valid Policy"
        };

        var result = PolicyValidator.Validate(doc);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void PolicyValidator_RequiresVersion()
    {
        var doc = new PolicyDocument { Version = "" };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Version"));
    }

    [Fact]
    public void PolicyValidator_EnforcesVersionFormat()
    {
        var doc = new PolicyDocument { Version = "invalid" };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid version format"));
    }

    [Fact]
    public void PolicyValidator_RequiresToolRuleId()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Rules = [new ToolRule { Id = "", Tool = "test", Decision = PolicyDecision.Allow }]
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("missing ID"));
    }

    [Fact]
    public void PolicyValidator_EnforcesRuleIdFormat()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Rules = [new ToolRule { Id = "invalid id!", Tool = "test", Decision = PolicyDecision.Allow }]
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid tool rule ID format"));
    }

    [Fact]
    public void PolicyValidator_DetectsDuplicateRuleIds()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Rules =
                [
                    new ToolRule { Id = "same-id", Tool = "tool1", Decision = PolicyDecision.Allow },
                    new ToolRule { Id = "same-id", Tool = "tool2", Decision = PolicyDecision.Deny }
                ]
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate tool rule ID"));
    }

    [Fact]
    public void PolicyValidator_RequiresConstraintsForAllowWithConstraints()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Rules = [new ToolRule { Id = "test", Tool = "test", Decision = PolicyDecision.AllowWithConstraints }]
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no constraints defined"));
    }

    [Fact]
    public void PolicyValidator_DetectsToolConflict()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Tools = new ToolPolicyRules
            {
                Allow = ["conflict-tool"],
                Deny = ["conflict-tool"]
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("both allow and deny"));
    }

    [Fact]
    public void PolicyValidator_DetectsPluginConflict()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Plugins = new PluginPolicyRules
            {
                Allow = ["com.conflict.plugin"],
                Deny = ["com.conflict.plugin"]
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Plugin") && e.Contains("both allow and deny"));
    }

    [Fact]
    public void PolicyValidator_DetectsDomainConflict()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedDomains = ["example.com"],
                BlockedDomains = ["example.com"]
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Domain") && e.Contains("both allowed and blocked"));
    }

    [Fact]
    public void PolicyValidator_ValidatesConnectivityModes()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedModes = ["invalid-mode"]
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid connectivity mode"));
    }

    [Fact]
    public void PolicyValidator_ValidatesUpdateChannels()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules
            {
                RequiredChannel = "invalid-channel"
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid required update channel"));
    }

    [Fact]
    public void PolicyValidator_ValidatesDeferDays()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules
            {
                DeferDays = 500 // > 365
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid deferDays"));
    }

    [Fact]
    public void PolicyValidator_ValidatesMinimumVersion()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Updates = new UpdatePolicyRules
            {
                MinimumVersion = "not-a-version"
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid minimumVersion format"));
    }

    [Fact]
    public void PolicyValidator_ValidatesMemoryConstraints()
    {
        var doc = new PolicyDocument
        {
            Version = "1.0",
            Memory = new MemoryPolicyRules
            {
                MaxRetentionDays = -1,
                MaxMemories = -100
            }
        };

        var result = PolicyValidator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("maxRetentionDays cannot be negative"));
        Assert.Contains(result.Errors, e => e.Contains("maxMemories cannot be negative"));
    }

    #endregion

    #region File Operations Tests

    [Fact]
    public async Task PolicySerializer_LoadFromFile_ReturnsNotFoundForMissing()
    {
        var result = await PolicySerializer.LoadFromFileAsync("/nonexistent/path/policy.json");

        Assert.False(result.IsSuccess);
        Assert.True(result.FileNotFound);
    }

    [Fact]
    public async Task PolicySerializer_RoundTripsThroughFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"policy-test-{Guid.NewGuid()}.json");
        try
        {
            var original = new PolicyDocument
            {
                Id = "file-test",
                Version = "1.0",
                Name = "File Test Policy",
                Tools = new ToolPolicyRules { Allow = ["tool1", "tool2"] }
            };

            await PolicySerializer.SaveToFileAsync(original, tempPath);
            var result = await PolicySerializer.LoadFromFileAsync(tempPath);

            Assert.True(result.IsSuccess);
            Assert.Equal("file-test", result.Document!.Id);
            Assert.Equal(tempPath, result.SourcePath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task PolicySerializer_CreatesDirectoryIfNeeded()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"policy-dir-{Guid.NewGuid()}");
        var tempPath = Path.Combine(tempDir, "sub", "policy.json");
        try
        {
            var doc = new PolicyDocument { Version = "1.0" };

            await PolicySerializer.SaveToFileAsync(doc, tempPath);

            Assert.True(File.Exists(tempPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public void PolicyDocument_ComplexOrgPolicy()
    {
        var doc = new PolicyDocument
        {
            Id = "acme-corp-2024",
            Name = "ACME Corporation Security Policy",
            Description = "Enterprise security policy for all InControl deployments",
            Version = "1.0",
            Locked = true,
            Tools = new ToolPolicyRules
            {
                Default = PolicyDecision.AllowWithApproval,
                Allow = ["read-file", "write-file", "list-directory"],
                Deny = ["execute-shell", "delete-file"],
                RequireApproval = ["web-search", "send-email"],
                Rules =
                [
                    new ToolRule
                    {
                        Id = "rate-limit-search",
                        Tool = "web-search",
                        Decision = PolicyDecision.AllowWithConstraints,
                        Reason = "Search is rate-limited for cost control",
                        Constraints = new Dictionary<string, object>
                        {
                            ["max_per_hour"] = 50
                        }
                    }
                ]
            },
            Plugins = new PluginPolicyRules
            {
                Enabled = true,
                Default = PolicyDecision.Deny,
                MaxRiskLevel = PluginRiskLevelPolicy.LocalMutation,
                Allow = ["com.acme.*"],
                TrustedAuthors = ["ACME Internal"]
            },
            Memory = new MemoryPolicyRules
            {
                Enabled = true,
                MaxRetentionDays = 90,
                MaxMemories = 5000,
                EncryptAtRest = true,
                AutoFormation = false, // Manual only
                ExcludeCategories = ["pii", "credentials"],
                AllowExport = false
            },
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedModes = ["online", "local"],
                DefaultMode = "online",
                AllowModeChange = false,
                BlockedDomains = ["competitor.com"],
                AllowTelemetry = false
            },
            Updates = new UpdatePolicyRules
            {
                AutoUpdate = false,
                AllowedChannels = ["stable"],
                RequiredChannel = "stable",
                DeferDays = 14,
                CheckOnStartup = true,
                MinimumVersion = "2.0.0"
            }
        };

        var validation = PolicyValidator.Validate(doc);

        Assert.True(validation.IsValid, string.Join(", ", validation.Errors));

        // Verify serialization
        var json = PolicySerializer.ToJson(doc);
        var reloaded = PolicySerializer.LoadFromJson(json);

        Assert.True(reloaded.IsSuccess);
        Assert.True(reloaded.Document!.Locked);
        Assert.Equal(PolicyDecision.Deny, reloaded.Document.Plugins!.Default);
        Assert.False(reloaded.Document.Memory!.AllowExport);
    }

    #endregion
}
