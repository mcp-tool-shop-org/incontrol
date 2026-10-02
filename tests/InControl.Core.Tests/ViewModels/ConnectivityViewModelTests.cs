using FluentAssertions;
using InControl.Core.Assistant;
using InControl.Core.Connectivity;
using InControl.ViewModels.Connectivity;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InControl.Core.Tests.ViewModels;

public class ConnectivityViewModelTests : IDisposable
{
    private readonly string _connectivityPath;
    private readonly FakeNetworkGateway _gateway;
    private readonly ConnectivityManager _connectivity;
    private readonly ConnectivityViewModel _viewModel;

    public ConnectivityViewModelTests()
    {
        _connectivityPath = Path.Combine(Path.GetTempPath(), $"conn-vm-test-{Guid.NewGuid()}.json");
        _gateway = new FakeNetworkGateway();
        _connectivity = new ConnectivityManager(_gateway, _connectivityPath);
        _viewModel = new ConnectivityViewModel(
            _connectivity,
            NullLogger<ConnectivityViewModel>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_connectivityPath))
        {
            File.Delete(_connectivityPath);
        }
    }

    [Fact]
    public void InitialState_IsOffline()
    {
        _viewModel.Mode.Should().Be(ConnectivityMode.OfflineOnly);
        _viewModel.Status.Should().Be(ConnectivityStatus.Offline);
        _viewModel.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void StatusDescription_CorrectForOffline()
    {
        _viewModel.StatusDescription.Should().Contain("Offline");
    }

    [Fact]
    public void StatusColor_IsGrayForOffline()
    {
        _viewModel.StatusColor.Should().Be("Gray");
    }

    [Fact]
    public void SetMode_UpdatesViewModel()
    {
        _viewModel.SetModeCommand.Execute(ConnectivityMode.Connected);

        _viewModel.Mode.Should().Be(ConnectivityMode.Connected);
        _viewModel.IsOnline.Should().BeTrue();
    }

    [Fact]
    public void GoOffline_SetsOfflineMode()
    {
        _viewModel.SetModeCommand.Execute(ConnectivityMode.Connected);
        _viewModel.GoOfflineCommand.Execute(null);

        _viewModel.Mode.Should().Be(ConnectivityMode.OfflineOnly);
        _viewModel.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void StatusDescription_UpdatesOnModeChange()
    {
        _viewModel.SetModeCommand.Execute(ConnectivityMode.Connected);

        _viewModel.StatusDescription.Should().Contain("Online");
    }

    [Fact]
    public void StatusColor_UpdatesOnModeChange()
    {
        _viewModel.SetModeCommand.Execute(ConnectivityMode.Connected);

        _viewModel.StatusColor.Should().Be("Green");
    }

    [Fact]
    public void RecentActivity_EmptyInitially()
    {
        _viewModel.RecentActivity.Should().BeEmpty();
        _viewModel.HasActivity.Should().BeFalse();
    }

    [Fact]
    public async Task RequestMade_AddsToRecentActivity()
    {
        _viewModel.SetModeCommand.Execute(ConnectivityMode.Connected);

        var request = new NetworkRequest(
            Endpoint: "https://api.example.com",
            Method: "GET",
            Intent: "Test request",
            DataSent: null,
            RequestedAt: DateTimeOffset.UtcNow);

        await _connectivity.RequestAsync(request);

        _viewModel.RecentActivity.Should().HaveCount(1);
        _viewModel.HasActivity.Should().BeTrue();
        _viewModel.TotalRequests.Should().Be(1);
    }

    [Fact]
    public void ClearAuditLog_ClearsActivity()
    {
        _viewModel.SetModeCommand.Execute(ConnectivityMode.Connected);
        _viewModel.ClearAuditLogCommand.Execute(null);

        _viewModel.RecentActivity.Should().BeEmpty();
        _viewModel.TotalRequests.Should().Be(0);
    }

    [Fact]
    public void AvailableModes_ContainsAllModes()
    {
        ConnectivityViewModel.AvailableModes.Should().HaveCount(3);
        ConnectivityViewModel.AvailableModes.Select(m => m.Mode).Should()
            .Contain(ConnectivityMode.OfflineOnly)
            .And.Contain(ConnectivityMode.Assisted)
            .And.Contain(ConnectivityMode.Connected);
    }

    private sealed class FakeNetworkGateway : INetworkGateway
    {
        public Task<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new NetworkResponse(
                IsSuccess: true,
                StatusCode: 200,
                Data: "{}",
                Error: null,
                Duration: TimeSpan.FromMilliseconds(10)));
        }
    }
}

public class ConnectivityPermissionsViewModelTests
{
    private readonly InternetToolPermissions _permissions;
    private ConnectivityPermissionsViewModel _viewModel;

    public ConnectivityPermissionsViewModelTests()
    {
        _permissions = new InternetToolPermissions();
        _viewModel = new ConnectivityPermissionsViewModel(
            _permissions,
            NullLogger<ConnectivityPermissionsViewModel>.Instance);
    }

    [Fact]
    public void InitialState_HasNoRules()
    {
        _viewModel.Rules.Should().BeEmpty();
    }

    [Fact]
    public void InitialState_HasNoPendingRequests()
    {
        _viewModel.PendingRequests.Should().BeEmpty();
        _viewModel.HasPendingRequests.Should().BeFalse();
    }

    [Fact]
    public void AddRule_AddsToRulesList()
    {
        _viewModel.NewEndpointPattern = "https://api.example.com";
        _viewModel.NewPermission = ToolPermission.AlwaysAllow;
        _viewModel.NewNotes = "Test API";

        _viewModel.AddRuleCommand.Execute(null);

        _viewModel.Rules.Should().HaveCount(1);
        _viewModel.Rules[0].EndpointPattern.Should().Be("https://api.example.com");
        _viewModel.Rules[0].Permission.Should().Be(ToolPermission.AlwaysAllow);
    }

    [Fact]
    public void AddRule_WithEmptyPattern_SetsError()
    {
        _viewModel.NewEndpointPattern = "";

        _viewModel.AddRuleCommand.Execute(null);

        _viewModel.HasError.Should().BeTrue();
        _viewModel.Rules.Should().BeEmpty();
    }

    [Fact]
    public void AddRule_ClearsForm()
    {
        _viewModel.NewEndpointPattern = "https://api.example.com";
        _viewModel.NewNotes = "Test API";

        _viewModel.AddRuleCommand.Execute(null);

        _viewModel.NewEndpointPattern.Should().BeEmpty();
        _viewModel.NewNotes.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_RemovesFromList()
    {
        _permissions.SetRule("https://api.example.com", ToolPermission.AlwaysAllow);
        _viewModel = new ConnectivityPermissionsViewModel(
            _permissions,
            NullLogger<ConnectivityPermissionsViewModel>.Instance);

        _viewModel.RemoveRuleCommand.Execute("https://api.example.com");

        _viewModel.Rules.Should().BeEmpty();
    }

    [Fact]
    public void ClearAllRules_RemovesAllRules()
    {
        _viewModel.NewEndpointPattern = "https://api1.example.com";
        _viewModel.AddRuleCommand.Execute(null);
        _viewModel.NewEndpointPattern = "https://api2.example.com";
        _viewModel.AddRuleCommand.Execute(null);

        _viewModel.ClearAllRulesCommand.Execute(null);

        _viewModel.Rules.Should().BeEmpty();
    }

    [Fact]
    public void AddCommonPatterns_AddsPresetRules()
    {
        _viewModel.AddCommonPatternsCommand.Execute(null);

        _viewModel.Rules.Should().NotBeEmpty();
        _viewModel.Rules.Should().Contain(r => r.EndpointPattern.Contains("github"));
    }

    [Fact]
    public void AvailablePermissions_HasAllOptions()
    {
        ConnectivityPermissionsViewModel.AvailablePermissions.Should().HaveCount(3);
    }

    [Fact]
    public void PermissionRuleViewModel_HasCorrectIcon()
    {
        var allowRule = new PermissionRuleViewModel("https://api.example.com", ToolPermission.AlwaysAllow, null);
        var denyRule = new PermissionRuleViewModel("https://blocked.example.com", ToolPermission.Disabled, null);

        allowRule.PermissionIcon.Should().Be("CheckCircle");
        allowRule.PermissionColor.Should().Be("Green");
        denyRule.PermissionIcon.Should().Be("XCircle");
        denyRule.PermissionColor.Should().Be("Red");
    }
}

public class NetworkActivityEntryTests
{
    [Fact]
    public void FromNetworkAuditEntry_MapsCorrectly()
    {
        var request = new NetworkRequest(
            Endpoint: "https://api.example.com/users",
            Method: "GET",
            Intent: "Fetch user list",
            DataSent: null,
            RequestedAt: DateTimeOffset.UtcNow);

        var response = new NetworkResponse(
            IsSuccess: true,
            StatusCode: 200,
            Data: "[]",
            Error: null,
            Duration: TimeSpan.FromMilliseconds(150));

        var logEntry = new NetworkAuditEntry(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            Request: request,
            Status: NetworkRequestStatus.Completed,
            Response: response,
            Error: null);

        var entry = new NetworkActivityEntry(logEntry);

        entry.Endpoint.Should().Be("https://api.example.com/users");
        entry.Method.Should().Be("GET");
        entry.Intent.Should().Be("Fetch user list");
        entry.IsSuccess.Should().BeTrue();
        entry.StatusCode.Should().Be(200);
        entry.StatusIcon.Should().Be("CheckCircle");
    }

    [Fact]
    public void FromNetworkAuditEntry_HandlesFailure()
    {
        var request = new NetworkRequest(
            Endpoint: "https://api.example.com/users",
            Method: "GET",
            Intent: "Fetch user list",
            DataSent: null,
            RequestedAt: DateTimeOffset.UtcNow);

        var response = new NetworkResponse(
            IsSuccess: false,
            StatusCode: 500,
            Data: null,
            Error: "Server error",
            Duration: TimeSpan.FromMilliseconds(50));

        var logEntry = new NetworkAuditEntry(
            Id: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            Request: request,
            Status: NetworkRequestStatus.Failed,
            Response: response,
            Error: "Server error");

        var entry = new NetworkActivityEntry(logEntry);

        entry.IsSuccess.Should().BeFalse();
        entry.StatusCode.Should().Be(500);
        entry.StatusIcon.Should().Be("XCircle");
    }
}
