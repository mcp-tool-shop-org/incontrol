using InControl.Core.Connectivity;
using InControl.Core.Policy;
using Xunit;

namespace InControl.Core.Tests.Policy;

/// <summary>
/// Tests for policy-governed connectivity operations.
/// </summary>
public class ConnectivityPolicyEnforcementTests
{
    private static PolicyGovernedConnectivityManager CreateGovernedConnectivity(PolicyEngine? engine = null)
    {
        var gateway = new TestNetworkGateway();
        var tempPath = Path.Combine(Path.GetTempPath(), $"connectivity-test-{Guid.NewGuid()}.json");
        var manager = new ConnectivityManager(gateway, tempPath);
        var policyEngine = engine ?? new PolicyEngine();
        return manager.WithPolicyEnforcement(policyEngine);
    }

    #region Basic Policy Tests

    [Fact]
    public void CheckConnectivityPolicy_AllowsChangesByDefault()
    {
        var governed = CreateGovernedConnectivity();

        var status = governed.CheckConnectivityPolicy();

        Assert.True(status.CanChangeMode);
        Assert.True(status.TelemetryAllowed);
    }

    [Fact]
    public void CheckConnectivityPolicy_RespectsChangeDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules { AllowModeChange = false }
        });
        var governed = CreateGovernedConnectivity(engine);

        var status = governed.CheckConnectivityPolicy();

        Assert.False(status.CanChangeMode);
        Assert.Contains("disabled", status.Reason);
    }

    [Fact]
    public void CheckConnectivityPolicy_ReportsTelemetrySetting()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules { AllowTelemetry = false }
        });
        var governed = CreateGovernedConnectivity(engine);

        var status = governed.CheckConnectivityPolicy();

        Assert.False(status.TelemetryAllowed);
    }

    #endregion

    #region Mode Tests

    [Fact]
    public void IsModeAllowed_TrueByDefault()
    {
        var governed = CreateGovernedConnectivity();

        Assert.True(governed.IsModeAllowed(ConnectivityMode.Connected));
        Assert.True(governed.IsModeAllowed(ConnectivityMode.Assisted));
        Assert.True(governed.IsModeAllowed(ConnectivityMode.OfflineOnly));
    }

    [Fact]
    public void IsModeAllowed_RespectsAllowedModes()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedModes = ["offline", "local"]
            }
        });
        var governed = CreateGovernedConnectivity(engine);

        Assert.True(governed.IsModeAllowed(ConnectivityMode.OfflineOnly)); // "offline"
        Assert.True(governed.IsModeAllowed(ConnectivityMode.Assisted)); // "local"
        Assert.False(governed.IsModeAllowed(ConnectivityMode.Connected)); // "online" not listed
    }

    [Fact]
    public void SetMode_SucceedsWhenAllowed()
    {
        var governed = CreateGovernedConnectivity();

        var result = governed.SetMode(ConnectivityMode.Connected);

        Assert.True(result.IsSuccess);
        Assert.Equal(ConnectivityMode.Connected, result.NewMode);
        Assert.Equal(ConnectivityMode.Connected, governed.Mode);
    }

    [Fact]
    public void SetMode_BlockedWhenChangesDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules { AllowModeChange = false }
        });
        var governed = CreateGovernedConnectivity(engine);

        var result = governed.SetMode(ConnectivityMode.Connected);

        Assert.False(result.IsSuccess);
        Assert.True(result.WasBlocked);
        Assert.Contains("disabled", result.BlockReason);
    }

    [Fact]
    public void SetMode_BlockedWhenModeNotAllowed()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedModes = ["offline"]
            }
        });
        var governed = CreateGovernedConnectivity(engine);

        var result = governed.SetMode(ConnectivityMode.Connected);

        Assert.False(result.IsSuccess);
        Assert.True(result.WasBlocked);
        Assert.Contains("not allowed", result.BlockReason);
    }

    [Fact]
    public void GoOfflineNow_AlwaysAllowed()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules { AllowModeChange = false }
        });
        var governed = CreateGovernedConnectivity(engine);

        // This should work even when mode changes are disabled
        governed.GoOfflineNow();

        Assert.Equal(ConnectivityMode.OfflineOnly, governed.Mode);
    }

    #endregion

    #region Domain Tests

    [Fact]
    public void CheckDomain_AllowsByDefault()
    {
        var governed = CreateGovernedConnectivity();

        var result = governed.CheckDomain("example.com");

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckDomain_RespectsBlockedDomains()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked.com", "malware.net"]
            }
        });
        var governed = CreateGovernedConnectivity(engine);

        var blockedResult = governed.CheckDomain("blocked.com");
        var allowedResult = governed.CheckDomain("allowed.com");

        Assert.False(blockedResult.IsAllowed);
        Assert.True(allowedResult.IsAllowed);
    }

    [Fact]
    public void CheckDomain_BlocksSubdomains()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked.com"]
            }
        });
        var governed = CreateGovernedConnectivity(engine);

        var result = governed.CheckDomain("sub.blocked.com");

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void CheckDomain_RespectsAllowedDomains()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                AllowedDomains = ["internal.corp.com", "api.trusted.com"]
            }
        });
        var governed = CreateGovernedConnectivity(engine);

        var internalResult = governed.CheckDomain("internal.corp.com");
        var externalResult = governed.CheckDomain("external.com");

        Assert.True(internalResult.IsAllowed);
        Assert.False(externalResult.IsAllowed);
    }

    #endregion

    #region Request Tests

    [Fact]
    public void CheckRequestAllowed_RespectsPolicy()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked.com"]
            }
        });
        var governed = CreateGovernedConnectivity(engine);
        governed.SetMode(ConnectivityMode.Connected); // Must be online to make requests

        var blockedRequest = new NetworkRequest(
            "https://blocked.com/api",
            "GET",
            "Test request",
            null,
            DateTimeOffset.UtcNow);

        var allowedRequest = new NetworkRequest(
            "https://allowed.com/api",
            "GET",
            "Test request",
            null,
            DateTimeOffset.UtcNow);

        Assert.False(governed.CheckRequestAllowed(blockedRequest).Allowed);
        Assert.True(governed.CheckRequestAllowed(allowedRequest).Allowed);
    }

    [Fact]
    public void CheckRequestAllowed_BlocksTelemetryWhenDisabled()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules { AllowTelemetry = false }
        });
        var governed = CreateGovernedConnectivity(engine);
        governed.SetMode(ConnectivityMode.Connected); // Must be online to make requests

        var telemetryRequest = new NetworkRequest(
            "https://example.com/telemetry",
            "POST",
            "Send telemetry data",
            null,
            DateTimeOffset.UtcNow);

        var normalRequest = new NetworkRequest(
            "https://example.com/api",
            "GET",
            "Fetch data",
            null,
            DateTimeOffset.UtcNow);

        Assert.False(governed.CheckRequestAllowed(telemetryRequest).Allowed);
        Assert.True(governed.CheckRequestAllowed(normalRequest).Allowed);
    }

    [Fact]
    public async Task RequestAsync_BlocksDomain()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked.com"]
            }
        });
        var governed = CreateGovernedConnectivity(engine);
        governed.SetMode(ConnectivityMode.Connected);

        var request = new NetworkRequest(
            "https://blocked.com/api",
            "GET",
            "Test request",
            null,
            DateTimeOffset.UtcNow);

        var result = await governed.RequestAsync(request);

        Assert.False(result.IsSuccess);
        Assert.True(result.WasBlocked);
        Assert.Contains("blocked", result.BlockReason);
    }

    [Fact]
    public async Task RequestAsync_SucceedsForAllowedDomain()
    {
        var governed = CreateGovernedConnectivity();
        governed.SetMode(ConnectivityMode.Connected);

        var request = new NetworkRequest(
            "https://allowed.com/api",
            "GET",
            "Test request",
            null,
            DateTimeOffset.UtcNow);

        var result = await governed.RequestAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void SetMode_RaisesModeChangeBlockedEvent()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules { AllowModeChange = false }
        });
        var governed = CreateGovernedConnectivity(engine);

        ModeChangeBlockedEventArgs? capturedArgs = null;
        governed.ModeChangeBlocked += (_, args) => capturedArgs = args;

        governed.SetMode(ConnectivityMode.Connected);

        Assert.NotNull(capturedArgs);
        Assert.Equal(ConnectivityMode.Connected, capturedArgs.RequestedMode);
    }

    [Fact]
    public async Task RequestAsync_RaisesDomainBlockedEvent()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.Organization, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked.com"]
            }
        });
        var governed = CreateGovernedConnectivity(engine);
        governed.SetMode(ConnectivityMode.Connected);

        DomainBlockedEventArgs? capturedArgs = null;
        governed.DomainBlocked += (_, args) => capturedArgs = args;

        var request = new NetworkRequest(
            "https://blocked.com/api",
            "GET",
            "Test request",
            null,
            DateTimeOffset.UtcNow);

        await governed.RequestAsync(request);

        Assert.NotNull(capturedArgs);
        Assert.Equal("blocked.com", capturedArgs.Domain);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_ReturnsComprehensiveInfo()
    {
        var engine = new PolicyEngine();
        engine.SetPolicy(PolicySource.User, new PolicyDocument
        {
            Version = "1.0",
            Connectivity = new ConnectivityPolicyRules
            {
                BlockedDomains = ["blocked1.com", "blocked2.com"],
                AllowTelemetry = false
            }
        });
        var governed = CreateGovernedConnectivity(engine);

        var stats = governed.GetStatistics();

        Assert.Equal(2, stats.BlockedDomainCount);
        Assert.False(stats.TelemetryAllowed);
        Assert.True(stats.CanChangeMode);
    }

    #endregion

    #region Extension Tests

    [Fact]
    public void WithPolicyEnforcement_CreatesWrapper()
    {
        var gateway = new TestNetworkGateway();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json");
        var manager = new ConnectivityManager(gateway, tempPath);
        var engine = new PolicyEngine();

        var governed = manager.WithPolicyEnforcement(engine);

        Assert.NotNull(governed);
        Assert.Same(engine, governed.PolicyEngine);
        Assert.Same(manager, governed.InnerManager);
    }

    #endregion

    #region Test Implementations

    private sealed class TestNetworkGateway : INetworkGateway
    {
        public Task<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new NetworkResponse(
                IsSuccess: true,
                StatusCode: 200,
                Data: "{}",
                Error: null,
                Duration: TimeSpan.FromMilliseconds(100)));
        }
    }

    #endregion
}
