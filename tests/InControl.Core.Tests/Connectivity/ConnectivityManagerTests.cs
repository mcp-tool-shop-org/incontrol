using FluentAssertions;
using InControl.Core.Connectivity;
using Xunit;

namespace InControl.Core.Tests.Connectivity;

public class ConnectivityManagerTests : IDisposable
{
    private readonly string _tempPath;
    private readonly FakeNetworkGateway _gateway;
    private readonly ConnectivityManager _manager;

    public ConnectivityManagerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"connectivity-test-{Guid.NewGuid()}.json");
        _gateway = new FakeNetworkGateway();
        _manager = new ConnectivityManager(_gateway, _tempPath);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }
    }

    [Fact]
    public void DefaultMode_IsOfflineOnly()
    {
        _manager.Mode.Should().Be(ConnectivityMode.OfflineOnly);
    }

    [Fact]
    public void DefaultStatus_IsOffline()
    {
        _manager.Status.Should().Be(ConnectivityStatus.Offline);
    }

    [Fact]
    public void IsOnline_IsFalse_WhenOfflineOnly()
    {
        _manager.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void SetMode_ChangesMode()
    {
        _manager.SetMode(ConnectivityMode.Connected);

        _manager.Mode.Should().Be(ConnectivityMode.Connected);
    }

    [Fact]
    public void SetMode_PersistsAcrossInstances()
    {
        _manager.SetMode(ConnectivityMode.Assisted);

        var newManager = new ConnectivityManager(_gateway, _tempPath);

        newManager.Mode.Should().Be(ConnectivityMode.Assisted);
    }

    [Fact]
    public void SetMode_RaisesModeChangedEvent()
    {
        ConnectivityModeChangedEventArgs? capturedArgs = null;
        _manager.ModeChanged += (_, args) => capturedArgs = args;

        _manager.SetMode(ConnectivityMode.Connected);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.OldMode.Should().Be(ConnectivityMode.OfflineOnly);
        capturedArgs.NewMode.Should().Be(ConnectivityMode.Connected);
    }

    [Fact]
    public void GoOfflineNow_SetsOfflineMode()
    {
        _manager.SetMode(ConnectivityMode.Connected);

        _manager.GoOfflineNow();

        _manager.Mode.Should().Be(ConnectivityMode.OfflineOnly);
        _manager.Status.Should().Be(ConnectivityStatus.Offline);
    }

    [Fact]
    public async Task RequestAsync_ReturnsNull_WhenOfflineOnly()
    {
        var request = CreateTestRequest("https://api.example.com/data");

        var response = await _manager.RequestAsync(request);

        response.Should().BeNull();
    }

    [Fact]
    public async Task RequestAsync_RaisesRequestBlocked_WhenOfflineOnly()
    {
        NetworkRequestBlockedEventArgs? capturedArgs = null;
        _manager.RequestBlocked += (_, args) => capturedArgs = args;

        var request = CreateTestRequest("https://api.example.com/data");
        await _manager.RequestAsync(request);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Reason.Should().Contain("Offline mode");
    }

    [Fact]
    public async Task RequestAsync_Succeeds_WhenConnected()
    {
        _manager.SetMode(ConnectivityMode.Connected);
        var request = CreateTestRequest("https://api.example.com/data");

        var response = await _manager.RequestAsync(request);

        response.Should().NotBeNull();
        response!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RequestAsync_RecordsInHistory()
    {
        _manager.SetMode(ConnectivityMode.Connected);
        var request = CreateTestRequest("https://api.example.com/data");

        await _manager.RequestAsync(request);

        var history = _manager.GetRequestHistory();
        history.Should().HaveCount(1);
        history[0].Request.Endpoint.Should().Be("https://api.example.com/data");
    }

    [Fact]
    public async Task RequestAsync_RaisesRequestMadeEvent()
    {
        _manager.SetMode(ConnectivityMode.Connected);
        NetworkRequestEventArgs? capturedArgs = null;
        _manager.RequestMade += (_, args) => capturedArgs = args;

        var request = CreateTestRequest("https://api.example.com/data");
        await _manager.RequestAsync(request);

        capturedArgs.Should().NotBeNull();
        capturedArgs!.Entry.Request.Endpoint.Should().Be("https://api.example.com/data");
    }

    [Fact]
    public async Task RequestAsync_UpdatesStatus_DuringRequest()
    {
        _manager.SetMode(ConnectivityMode.Connected);
        _gateway.Delay = TimeSpan.FromMilliseconds(50);

        var statuses = new List<ConnectivityStatus>();
        _manager.StatusChanged += (_, args) => statuses.Add(args.NewStatus);

        var request = CreateTestRequest("https://api.example.com/data");
        var task = _manager.RequestAsync(request);
        await Task.Delay(10);

        statuses.Should().Contain(ConnectivityStatus.Active);

        await task;
    }

    [Fact]
    public async Task RequestAsync_RequiresIntent()
    {
        _manager.SetMode(ConnectivityMode.Connected);
        var request = new NetworkRequest(
            Endpoint: "https://api.example.com/data",
            Method: "GET",
            Intent: "",  // Empty intent
            DataSent: null,
            RequestedAt: DateTimeOffset.UtcNow
        );

        var response = await _manager.RequestAsync(request);

        response.Should().BeNull();
    }

    [Fact]
    public async Task RequestAsync_InAssistedMode_RequiresAllowedEndpoint()
    {
        _manager.SetMode(ConnectivityMode.Assisted);
        var request = CreateTestRequest("https://notallowed.example.com/data");

        var response = await _manager.RequestAsync(request);

        response.Should().BeNull();
    }

    [Fact]
    public async Task RequestAsync_InAssistedMode_SucceedsForAllowedEndpoint()
    {
        _manager.SetMode(ConnectivityMode.Assisted);
        _manager.AllowEndpoint("https://api.example.com");

        var request = CreateTestRequest("https://api.example.com/data");
        var response = await _manager.RequestAsync(request);

        response.Should().NotBeNull();
        response!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AllowEndpoint_AddsToAllowlist()
    {
        _manager.AllowEndpoint("https://api.example.com");

        var allowed = _manager.GetAllowedEndpoints();
        allowed.Should().Contain("https://api.example.com");
    }

    [Fact]
    public void AllowEndpoint_DoesNotDuplicate()
    {
        _manager.AllowEndpoint("https://api.example.com");
        _manager.AllowEndpoint("https://api.example.com");

        var allowed = _manager.GetAllowedEndpoints();
        allowed.Count(e => e == "https://api.example.com").Should().Be(1);
    }

    [Fact]
    public void DenyEndpoint_RemovesFromAllowlist()
    {
        _manager.AllowEndpoint("https://api.example.com");
        _manager.DenyEndpoint("https://api.example.com");

        var allowed = _manager.GetAllowedEndpoints();
        allowed.Should().NotContain("https://api.example.com");
    }

    [Fact]
    public void CheckRequestAllowed_ReturnsNotAllowed_WhenOffline()
    {
        var request = CreateTestRequest("https://api.example.com/data");

        var result = _manager.CheckRequestAllowed(request);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Contain("Offline");
    }

    [Fact]
    public void CheckRequestAllowed_ReturnsAllowed_WhenConnected()
    {
        _manager.SetMode(ConnectivityMode.Connected);
        var request = CreateTestRequest("https://api.example.com/data");

        var result = _manager.CheckRequestAllowed(request);

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task GetRecentActivity_ReturnsLastNRequests()
    {
        _manager.SetMode(ConnectivityMode.Connected);

        for (int i = 0; i < 15; i++)
        {
            await _manager.RequestAsync(CreateTestRequest($"https://api.example.com/{i}"));
        }

        var recent = _manager.GetRecentActivity(5);

        recent.Should().HaveCount(5);
    }

    [Fact]
    public async Task ClearHistory_RemovesAllRequests()
    {
        _manager.SetMode(ConnectivityMode.Connected);
        await _manager.RequestAsync(CreateTestRequest("https://api.example.com/data"));

        _manager.ClearHistory();

        _manager.GetRequestHistory().Should().BeEmpty();
    }

    [Fact]
    public void IsOnline_IsTrue_WhenConnectedAndNotOffline()
    {
        _manager.SetMode(ConnectivityMode.Connected);

        _manager.IsOnline.Should().BeTrue();
    }

    [Fact]
    public void IsOnline_IsTrue_WhenAssistedMode()
    {
        _manager.SetMode(ConnectivityMode.Assisted);

        _manager.IsOnline.Should().BeTrue();
    }

    private static NetworkRequest CreateTestRequest(string endpoint) => new(
        Endpoint: endpoint,
        Method: "GET",
        Intent: "Test request for unit testing",
        DataSent: null,
        RequestedAt: DateTimeOffset.UtcNow
    );

    private sealed class FakeNetworkGateway : INetworkGateway
    {
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;
        public bool ShouldFail { get; set; }

        public async Task<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken ct = default)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, ct);
            }

            if (ShouldFail)
            {
                return new NetworkResponse(
                    IsSuccess: false,
                    StatusCode: 500,
                    Data: null,
                    Error: "Simulated failure",
                    Duration: Delay
                );
            }

            return new NetworkResponse(
                IsSuccess: true,
                StatusCode: 200,
                Data: "{\"result\": \"success\"}",
                Error: null,
                Duration: Delay
            );
        }
    }
}

public class ConnectivityModeTests
{
    [Theory]
    [InlineData(ConnectivityMode.OfflineOnly)]
    [InlineData(ConnectivityMode.Assisted)]
    [InlineData(ConnectivityMode.Connected)]
    public void ConnectivityMode_AllValuesAreDefined(ConnectivityMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }
}

public class ConnectivityStatusTests
{
    [Theory]
    [InlineData(ConnectivityStatus.Offline)]
    [InlineData(ConnectivityStatus.Idle)]
    [InlineData(ConnectivityStatus.Active)]
    public void ConnectivityStatus_AllValuesAreDefined(ConnectivityStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }
}

public class NetworkRequestTests
{
    [Fact]
    public void NetworkRequest_RecordsAllProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new NetworkRequest(
            Endpoint: "https://api.example.com",
            Method: "POST",
            Intent: "Send data",
            DataSent: "{\"key\": \"value\"}",
            RequestedAt: now
        );

        request.Endpoint.Should().Be("https://api.example.com");
        request.Method.Should().Be("POST");
        request.Intent.Should().Be("Send data");
        request.DataSent.Should().Be("{\"key\": \"value\"}");
        request.RequestedAt.Should().Be(now);
    }
}

public class NetworkResponseTests
{
    [Fact]
    public void NetworkResponse_RecordsSuccess()
    {
        var response = new NetworkResponse(
            IsSuccess: true,
            StatusCode: 200,
            Data: "response data",
            Error: null,
            Duration: TimeSpan.FromMilliseconds(100)
        );

        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(200);
        response.Data.Should().Be("response data");
        response.Error.Should().BeNull();
    }

    [Fact]
    public void NetworkResponse_RecordsFailure()
    {
        var response = new NetworkResponse(
            IsSuccess: false,
            StatusCode: 500,
            Data: null,
            Error: "Internal server error",
            Duration: TimeSpan.FromMilliseconds(50)
        );

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Be("Internal server error");
    }
}
