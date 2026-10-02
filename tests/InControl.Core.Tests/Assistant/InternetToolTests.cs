using FluentAssertions;
using InControl.Core.Assistant;
using InControl.Core.Connectivity;
using Xunit;

namespace InControl.Core.Tests.Assistant;

public class InternetToolTests : IDisposable
{
    private readonly string _connectivityPath;
    private readonly FakeNetworkGateway _gateway;
    private readonly ConnectivityManager _connectivity;
    private readonly InternetToolPermissions _permissions;
    private readonly InternetTool _tool;

    public InternetToolTests()
    {
        _connectivityPath = Path.Combine(Path.GetTempPath(), $"inet-test-{Guid.NewGuid()}.json");
        _gateway = new FakeNetworkGateway();
        _connectivity = new ConnectivityManager(_gateway, _connectivityPath);
        _permissions = new InternetToolPermissions();
        _tool = new InternetTool(_connectivity, _permissions);
    }

    public void Dispose()
    {
        if (File.Exists(_connectivityPath))
        {
            File.Delete(_connectivityPath);
        }
    }

    [Fact]
    public void Tool_HasCorrectId()
    {
        _tool.Id.Should().Be("internet.request");
    }

    [Fact]
    public void Tool_HasHighRiskLevel()
    {
        _tool.RiskLevel.Should().Be(ToolRiskLevel.High);
    }

    [Fact]
    public void Tool_RequiresNetwork()
    {
        _tool.RequiresNetwork.Should().BeTrue();
    }

    [Fact]
    public void Tool_HasRequiredParameters()
    {
        var paramNames = _tool.Parameters.Select(p => p.Name).ToList();

        paramNames.Should().Contain("endpoint");
        paramNames.Should().Contain("method");
        paramNames.Should().Contain("purpose");
        paramNames.Should().Contain("expected_data");
        paramNames.Should().Contain("retention");
    }

    [Fact]
    public async Task ExecuteAsync_FailsInOfflineMode()
    {
        // Default is offline mode
        var context = CreateContext(
            "https://api.example.com/data",
            "GET",
            "Fetch user data",
            "User profile",
            "Session only");

        var result = await _tool.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Message.Should().Contain("offline");
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithoutEndpointPermission()
    {
        _connectivity.SetMode(ConnectivityMode.Connected);
        // No permission rules set

        var context = CreateContext(
            "https://api.example.com/data",
            "GET",
            "Fetch user data",
            "User profile",
            "Session only");

        var result = await _tool.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Message.Should().Contain("approval required");
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsWithPermission()
    {
        _connectivity.SetMode(ConnectivityMode.Connected);
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow);

        var context = CreateContext(
            "https://api.example.com/data",
            "GET",
            "Fetch user data",
            "User profile",
            "Session only");

        var result = await _tool.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithDeniedPermission()
    {
        _connectivity.SetMode(ConnectivityMode.Connected);
        _permissions.SetRule("https://api.example.com", ToolPermission.Disabled, "Not allowed");

        var context = CreateContext(
            "https://api.example.com/data",
            "GET",
            "Fetch user data",
            "User profile",
            "Session only");

        var result = await _tool.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Message.Should().Contain("denied");
    }

    [Fact]
    public async Task ExecuteAsync_RequiresAllParameters()
    {
        _connectivity.SetMode(ConnectivityMode.Connected);
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow);

        // Missing purpose
        var context = new ToolExecutionContext(
            Parameters: new Dictionary<string, object?>
            {
                ["endpoint"] = "https://api.example.com/data",
                ["method"] = "GET"
                // Missing: purpose, expected_data, retention
            },
            InvocationId: Guid.NewGuid(),
            RequestedAt: DateTimeOffset.UtcNow
        );

        var result = await _tool.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Message.Should().Contain("Missing required parameter");
    }

    [Fact]
    public async Task ExecuteAsync_RecordsIntentForAudit()
    {
        _connectivity.SetMode(ConnectivityMode.Connected);
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow);

        NetworkRequest? capturedRequest = null;
        _connectivity.RequestMade += (_, args) => capturedRequest = args.Entry.Request;

        var context = CreateContext(
            "https://api.example.com/data",
            "GET",
            "Fetch user profile for display",
            "User name and avatar",
            "Until session ends");

        await _tool.ExecuteAsync(context, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Intent.Should().Contain("Fetch user profile");
        capturedRequest.Intent.Should().Contain("User name and avatar");
        capturedRequest.Intent.Should().Contain("Until session ends");
    }

    [Fact]
    public async Task ExecuteAsync_HandlesNetworkFailure()
    {
        _connectivity.SetMode(ConnectivityMode.Connected);
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow);
        _gateway.ShouldFail = true;

        var context = CreateContext(
            "https://api.example.com/data",
            "GET",
            "Fetch data",
            "Data",
            "Session");

        var result = await _tool.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Message.Should().Contain("failed");
    }

    private ToolExecutionContext CreateContext(
        string endpoint,
        string method,
        string purpose,
        string expectedData,
        string retention,
        string? dataSent = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["endpoint"] = endpoint,
            ["method"] = method,
            ["purpose"] = purpose,
            ["expected_data"] = expectedData,
            ["retention"] = retention
        };

        if (dataSent != null)
        {
            parameters["data_sent"] = dataSent;
        }

        return new ToolExecutionContext(
            Parameters: parameters,
            InvocationId: Guid.NewGuid(),
            RequestedAt: DateTimeOffset.UtcNow
        );
    }

    private sealed class FakeNetworkGateway : INetworkGateway
    {
        public bool ShouldFail { get; set; }

        public Task<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken ct = default)
        {
            if (ShouldFail)
            {
                return Task.FromResult(new NetworkResponse(
                    IsSuccess: false,
                    StatusCode: 500,
                    Data: null,
                    Error: "Simulated failure",
                    Duration: TimeSpan.FromMilliseconds(10)
                ));
            }

            return Task.FromResult(new NetworkResponse(
                IsSuccess: true,
                StatusCode: 200,
                Data: "{\"result\": \"success\"}",
                Error: null,
                Duration: TimeSpan.FromMilliseconds(10)
            ));
        }
    }
}

public class InternetToolPermissionsTests
{
    private readonly InternetToolPermissions _permissions;

    public InternetToolPermissionsTests()
    {
        _permissions = new InternetToolPermissions();
    }

    [Fact]
    public async Task CheckPermissionAsync_ReturnsDenied_WithNoRules()
    {
        var result = await _permissions.CheckPermissionAsync(
            "https://api.example.com",
            "Fetch data");

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("approval required");
    }

    [Fact]
    public async Task CheckPermissionAsync_ReturnsAllowed_WhenRuleMatches()
    {
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow);

        var result = await _permissions.CheckPermissionAsync(
            "https://api.example.com/users",
            "Fetch users");

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckPermissionAsync_ReturnsDenied_WhenRuleDisabled()
    {
        _permissions.SetRule("https://api.example.com", ToolPermission.Disabled);

        var result = await _permissions.CheckPermissionAsync(
            "https://api.example.com/users",
            "Fetch users");

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("denied by rule");
    }

    [Fact]
    public void SetRule_AddsRule()
    {
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow, "Trusted API");

        var rules = _permissions.GetRules();

        rules.Should().ContainKey("https://api.example.com");
        rules["https://api.example.com"].Permission.Should().Be(ToolPermission.AlwaysAllow);
        rules["https://api.example.com"].Notes.Should().Be("Trusted API");
    }

    [Fact]
    public void SetRule_OverwritesExisting()
    {
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow);
        _permissions.SetRule("https://api.example.com", ToolPermission.Disabled);

        var rules = _permissions.GetRules();

        rules["https://api.example.com"].Permission.Should().Be(ToolPermission.Disabled);
    }

    [Fact]
    public void RemoveRule_RemovesRule()
    {
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow);
        _permissions.RemoveRule("https://api.example.com");

        var rules = _permissions.GetRules();

        rules.Should().NotContainKey("https://api.example.com");
    }

    [Fact]
    public void ClearRules_RemovesAllRules()
    {
        _permissions.SetRule("https://api1.example.com", ToolPermission.AlwaysAllow);
        _permissions.SetRule("https://api2.example.com", ToolPermission.AlwaysAllow);

        _permissions.ClearRules();

        _permissions.GetRules().Should().BeEmpty();
    }

    [Fact]
    public async Task CheckPermissionAsync_RaisesPermissionRequested_ForUnknownEndpoint()
    {
        InternetPermissionRequestEventArgs? capturedArgs = null;
        _permissions.PermissionRequested += (_, args) => capturedArgs = args;

        await _permissions.CheckPermissionAsync(
            "https://unknown.example.com",
            "Unknown purpose");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Endpoint.Should().Be("https://unknown.example.com");
        capturedArgs.Purpose.Should().Be("Unknown purpose");
    }
}

public class InternetToolDeclarationTests
{
    [Fact]
    public void ToDisplayString_FormatsDeclaration()
    {
        var declaration = new InternetToolDeclaration(
            ToolId: "weather.fetch",
            Endpoints: new[] { "https://api.weather.com/current" },
            DataSent: new[] { "Location (city name)" },
            DataReceived: new[] { "Temperature", "Conditions" },
            RetentionPolicy: "Cached for 1 hour"
        );

        var display = declaration.ToDisplayString();

        display.Should().Contain("weather.fetch");
        display.Should().Contain("https://api.weather.com/current");
        display.Should().Contain("Location (city name)");
        display.Should().Contain("Temperature");
        display.Should().Contain("Cached for 1 hour");
    }
}
