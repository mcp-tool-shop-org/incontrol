using FluentAssertions;
using InControl.Core.Connectivity;
using InControl.Core.Plugins;
using Xunit;

namespace InControl.Core.Tests.Plugins;

/// <summary>
/// The sandbox is the only thing between a plugin and the disk, the network and memory.
/// These tests check that each door opens only for the permission that names it.
/// </summary>
public class PluginSandboxBoundaryTests : IDisposable
{
    private readonly string _root;
    private readonly string _storage;
    private readonly string _allowed;
    private readonly string _settingsPath;
    private readonly RecordingGateway _gateway = new();
    private readonly ConnectivityManager _connectivity;
    private readonly InMemoryPluginAuditLog _audit = new();
    private readonly PluginSandbox _sandbox;

    public PluginSandboxBoundaryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sandbox-" + Guid.NewGuid().ToString("N"));
        _storage = Path.Combine(_root, "storage");
        _allowed = Path.Combine(_root, "allowed");
        Directory.CreateDirectory(_allowed);
        _settingsPath = Path.Combine(_root, "connectivity.json");
        _connectivity = new ConnectivityManager(_gateway, _settingsPath);
        _sandbox = new PluginSandbox(_connectivity, _storage, _audit);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PluginPermission Perm(PermissionType type, PermissionAccess access, string? scope = null) =>
        new() { Type = type, Access = access, Scope = scope };

    private static PluginManifest Manifest(string id = "com.test.sandbox", params PluginPermission[] permissions) => new()
    {
        Id = id,
        Version = "1.0.0",
        Name = "Sandbox test",
        Author = "Tests",
        Description = "Sandbox test plugin",
        Permissions = permissions
    };

    private IPluginContext Context(params PluginPermission[] permissions) =>
        _sandbox.CreateContext(Manifest(permissions: permissions));

    private IEnumerable<PluginAuditEntry> Entries(ResourceAccessType type) =>
        _audit.GetRecentEntries().Where(e => e.ResourceType == type);

    #region Sandbox and context

    [Fact]
    public void Sandbox_CreatesItsStorageFolder()
    {
        Directory.Exists(_storage).Should().BeTrue();
    }

    [Fact]
    public void Context_ExposesTheManifestAndMediatedAccess()
    {
        var manifest = Manifest();

        using var context = _sandbox.CreateContext(manifest);

        context.Manifest.Should().BeSameAs(manifest);
        context.Files.Should().NotBeNull();
        context.Network.Should().NotBeNull();
        context.Memory.Should().NotBeNull();
        context.Storage.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispose_IsSafeToCallTwice_AndStorageSurvivesIt()
    {
        var context = _sandbox.CreateContext(Manifest());
        await context.Storage.SetAsync("kept", "v");

        context.Dispose();
        context.Dispose();

        File.Exists(Path.Combine(_storage, "com.test.sandbox", "kept.json")).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_RequiresMatchingTypeAndAccess()
    {
        using var context = Context(Perm(PermissionType.Memory, PermissionAccess.Read));

        context.HasPermission(PermissionType.Memory, PermissionAccess.Read).Should().BeTrue();
        context.HasPermission(PermissionType.Memory, PermissionAccess.Write).Should().BeFalse();
        context.HasPermission(PermissionType.Settings, PermissionAccess.Read).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_UnscopedGrantCoversAnyScope_ButScopedGrantOnlyItsOwn()
    {
        using var unscoped = Context(Perm(PermissionType.File, PermissionAccess.Read));
        using var scoped = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        unscoped.HasPermission(PermissionType.File, PermissionAccess.Read, Path.Combine(_root, "anywhere")).Should().BeTrue();
        scoped.HasPermission(PermissionType.File, PermissionAccess.Read, Path.Combine(_allowed, "a.txt")).Should().BeTrue();
        scoped.HasPermission(PermissionType.File, PermissionAccess.Read, Path.Combine(_root, "other", "a.txt")).Should().BeFalse();
        scoped.HasPermission(PermissionType.File, PermissionAccess.Read, _allowed + "-evil").Should().BeFalse();
        scoped.HasPermission(PermissionType.File, PermissionAccess.Read).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_NetworkScopeIsComparedAsAnEndpoint()
    {
        using var context = Context(Perm(PermissionType.Network, PermissionAccess.Read, "https://api.example.com/v1"));

        context.HasPermission(PermissionType.Network, PermissionAccess.Read, "https://api.example.com/v1/models").Should().BeTrue();
        context.HasPermission(PermissionType.Network, PermissionAccess.Read, "https://api.example.com/v2").Should().BeFalse();
        context.HasPermission(PermissionType.Network, PermissionAccess.Read, "https://api.example.com.evil.net/v1").Should().BeFalse();
        context.HasPermission(PermissionType.Network, PermissionAccess.Read, "https://api.example.com@evil.net/v1").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_EmptyScopeCoversNothingThatAsksForAScope()
    {
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Read, ""));

        context.HasPermission(PermissionType.File, PermissionAccess.Read, _allowed).Should().BeFalse();
    }

    #endregion

    #region Files

    [Fact]
    public async Task Files_ReadInsideTheGrantedFolder_ReturnsContent_AndIsAudited()
    {
        var file = Path.Combine(_allowed, "note.txt");
        await File.WriteAllTextAsync(file, "hello");
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        var result = await context.Files.ReadAsync(file);

        result.Success.Should().BeTrue();
        result.Content.Should().Be("hello");
        result.Error.Should().BeNull();
        Entries(ResourceAccessType.FileRead).Should().ContainSingle(e => e.Resource == file && e.Success == true);
    }

    [Fact]
    public async Task Files_ReadOutsideTheGrant_IsRefusedAndAudited()
    {
        var secret = Path.Combine(_root, "secret.txt");
        await File.WriteAllTextAsync(secret, "do not read");
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        var result = await context.Files.ReadAsync(secret);

        result.Success.Should().BeFalse();
        result.Content.Should().BeNull();
        result.Error.Should().Contain("No read permission");
        Entries(ResourceAccessType.FileRead).Should().ContainSingle(e => e.Success == false);
    }

    [Fact]
    public async Task Files_DotDotTraversalOutOfTheGrant_IsRefused()
    {
        var secret = Path.Combine(_root, "secret.txt");
        await File.WriteAllTextAsync(secret, "do not read");
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        var result = await context.Files.ReadAsync(Path.Combine(_allowed, "..", "secret.txt"));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No read permission");
    }

    [Fact]
    public async Task Files_SiblingFolderWithTheSamePrefix_IsRefused()
    {
        var sibling = _allowed + "-evil";
        Directory.CreateDirectory(sibling);
        await File.WriteAllTextAsync(Path.Combine(sibling, "x.txt"), "x");
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        (await context.Files.ReadAsync(Path.Combine(sibling, "x.txt"))).Success.Should().BeFalse();
        (await context.Files.ListAsync(sibling)).Success.Should().BeFalse();
    }

    [Fact]
    public async Task Files_ReadOfAMissingFileInsideTheGrant_FailsWithTheReason()
    {
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        var result = await context.Files.ReadAsync(Path.Combine(_allowed, "missing.txt"));

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.Error.Should().NotContain("No read permission");
    }

    [Fact]
    public async Task Files_WritePermissionDoesNotGrantReadAndReadDoesNotGrantWrite()
    {
        var file = Path.Combine(_allowed, "w.txt");
        using var writeOnly = Context(Perm(PermissionType.File, PermissionAccess.Write, _allowed));
        using var readOnly = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        (await writeOnly.Files.WriteAsync(file, "data")).Success.Should().BeTrue();
        (await writeOnly.Files.ReadAsync(file)).Success.Should().BeFalse();
        (await writeOnly.Files.ListAsync(_allowed)).Success.Should().BeFalse();

        var denied = await readOnly.Files.WriteAsync(Path.Combine(_allowed, "nope.txt"), "x");
        denied.Success.Should().BeFalse();
        denied.Error.Should().Contain("No write permission");
        File.Exists(Path.Combine(_allowed, "nope.txt")).Should().BeFalse();
        (await readOnly.Files.ReadAsync(file)).Content.Should().Be("data");
    }

    [Fact]
    public async Task Files_WriteFailureInsideTheGrant_IsReportedNotThrown()
    {
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Write, _allowed));

        var result = await context.Files.WriteAsync(Path.Combine(_allowed, "no-such-folder", "x.txt"), "x");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Files_ListReturnsTheFilesInAGrantedFolder()
    {
        await File.WriteAllTextAsync(Path.Combine(_allowed, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(_allowed, "b.txt"), "b");
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        var result = await context.Files.ListAsync(_allowed);

        result.Success.Should().BeTrue();
        result.Files.Select(Path.GetFileName).Should().BeEquivalentTo("a.txt", "b.txt");
        Entries(ResourceAccessType.FileList).Should().ContainSingle();
    }

    [Fact]
    public async Task Files_ListOutsideTheGrant_IsRefused_AndAMissingFolderFails()
    {
        using var context = Context(Perm(PermissionType.File, PermissionAccess.Read, _allowed));

        var denied = await context.Files.ListAsync(_root);
        var missing = await context.Files.ListAsync(Path.Combine(_allowed, "gone"));

        denied.Success.Should().BeFalse();
        denied.Files.Should().BeEmpty();
        denied.Error.Should().Contain("No read permission");
        missing.Success.Should().BeFalse();
        missing.Files.Should().BeEmpty();
    }

    [Fact]
    public void Files_IsPathPermitted_IgnoresOtherPermissionTypesAndEmptyScopes()
    {
        using var context = Context(
            Perm(PermissionType.Memory, PermissionAccess.Read, _allowed),
            Perm(PermissionType.File, PermissionAccess.Read, ""),
            Perm(PermissionType.File, PermissionAccess.Read, null));

        context.Files.IsPathPermitted(Path.Combine(_allowed, "x"), PermissionAccess.Read).Should().BeFalse();
    }

    [Fact]
    public async Task Files_WithoutAnAuditLog_StillEnforce()
    {
        var quiet = new PluginSandbox(_connectivity, _storage);
        using var context = quiet.CreateContext(Manifest(permissions: Perm(PermissionType.File, PermissionAccess.Read, _allowed)));

        (await context.Files.ReadAsync(Path.Combine(_root, "secret.txt"))).Success.Should().BeFalse();
    }

    #endregion

    #region Network

    private const string Endpoint = "https://api.example.com/v1/items";

    private void GoOnline() => _connectivity.SetMode(ConnectivityMode.Connected);

    [Fact]
    public void Network_IsAvailable_FollowsTheConnectivityManager()
    {
        using var context = Context();

        context.Network.IsAvailable.Should().BeFalse();
        GoOnline();
        context.Network.IsAvailable.Should().BeTrue();
        _connectivity.GoOfflineNow();
        context.Network.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Network_IsEndpointPermitted_ChecksTypeAndScope()
    {
        using var context = Context(
            Perm(PermissionType.Network, PermissionAccess.Read, "https://api.example.com/v1"),
            Perm(PermissionType.File, PermissionAccess.Read, "https://other.example.com"),
            Perm(PermissionType.Network, PermissionAccess.Read, ""));

        context.Network.IsEndpointPermitted(Endpoint).Should().BeTrue();
        context.Network.IsEndpointPermitted("https://api.example.com/v2").Should().BeFalse();
        context.Network.IsEndpointPermitted("https://other.example.com").Should().BeFalse();
    }

    [Fact]
    public async Task Network_WithoutAGrant_NeverReachesTheGateway()
    {
        GoOnline();
        using var context = Context();

        var result = await context.Network.RequestAsync(Endpoint, "GET", null, "look it up");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("No network permission");
        _gateway.Requests.Should().BeEmpty();
        Entries(ResourceAccessType.NetworkRequest).Should().ContainSingle(e => e.Success == false);
    }

    [Theory]
    [InlineData("TRACE")]
    [InlineData("CONNECT")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("GET /x HTTP/1.1")]
    public async Task Network_UnknownMethod_IsRefusedEvenWithEveryGrant(string method)
    {
        GoOnline();
        using var context = Context(
            Perm(PermissionType.Network, PermissionAccess.Read, "https://api.example.com"),
            Perm(PermissionType.Network, PermissionAccess.Write, "https://api.example.com"),
            Perm(PermissionType.Network, PermissionAccess.Execute, "https://api.example.com"));

        var result = await context.Network.RequestAsync(Endpoint, method, null, "x");

        result.Success.Should().BeFalse();
        _gateway.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("GET", PermissionAccess.Read, true)]
    [InlineData("get", PermissionAccess.Read, true)]
    [InlineData(" Head ", PermissionAccess.Read, true)]
    [InlineData("OPTIONS", PermissionAccess.Read, true)]
    [InlineData("POST", PermissionAccess.Write, true)]
    [InlineData("PUT", PermissionAccess.Write, true)]
    [InlineData("PATCH", PermissionAccess.Write, true)]
    [InlineData("DELETE", PermissionAccess.Write, true)]
    [InlineData("GET", PermissionAccess.Write, false)]
    [InlineData("POST", PermissionAccess.Read, false)]
    [InlineData("DELETE", PermissionAccess.Read, false)]
    [InlineData("GET", PermissionAccess.Execute, false)]
    public async Task Network_MethodNeedsTheMatchingAccess(string method, PermissionAccess granted, bool expected)
    {
        GoOnline();
        using var context = Context(Perm(PermissionType.Network, granted, "https://api.example.com/v1"));

        var result = await context.Network.RequestAsync(Endpoint, method, "{}", "do it");

        result.Success.Should().Be(expected);
        _gateway.Requests.Count.Should().Be(expected ? 1 : 0);
    }

    [Fact]
    public async Task Network_Success_SendsThroughTheManagerWithThePluginNamedInTheIntent()
    {
        GoOnline();
        using var context = Context(Perm(PermissionType.Network, PermissionAccess.Write, "https://api.example.com"));

        var result = await context.Network.RequestAsync(Endpoint, "POST", "{\"a\":1}", "save item");

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().Be("ok");
        var sent = _gateway.Requests.Should().ContainSingle().Subject;
        sent.Endpoint.Should().Be(Endpoint);
        sent.Method.Should().Be("POST");
        sent.DataSent.Should().Be("{\"a\":1}");
        sent.Intent.Should().Be("[Plugin:com.test.sandbox] save item");
        _connectivity.GetRequestHistory().Should().ContainSingle();
    }

    [Fact]
    public async Task Network_WhenOffline_IsRefusedAfterThePermissionCheck()
    {
        using var context = Context(Perm(PermissionType.Network, PermissionAccess.Read, "https://api.example.com"));

        var result = await context.Network.RequestAsync(Endpoint, "GET", null, "x");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Network is offline");
        _gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Network_ErrorResponse_SurfacesTheGatewayError_OrAGenericOne()
    {
        GoOnline();
        using var context = Context(Perm(PermissionType.Network, PermissionAccess.Read, "https://api.example.com"));

        _gateway.Next = new NetworkResponse(false, 503, null, "upstream down", TimeSpan.Zero);
        (await context.Network.RequestAsync(Endpoint, "GET", null, "x")).Error.Should().Be("upstream down");

        _gateway.Next = new NetworkResponse(false, 500, null, null, TimeSpan.Zero);
        (await context.Network.RequestAsync(Endpoint, "GET", null, "x")).Error.Should().Be("Request failed");
    }

    [Fact]
    public async Task Network_GatewayThrowing_IsReportedNotThrown()
    {
        GoOnline();
        _gateway.Throw = new InvalidOperationException("socket exploded");
        using var context = Context(Perm(PermissionType.Network, PermissionAccess.Read, "https://api.example.com"));

        var result = await context.Network.RequestAsync(Endpoint, "GET", null, "x");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("socket exploded");
    }

    [Fact]
    public async Task Network_AssistedModeWithoutTheEndpointAllowed_IsBlockedByTheManager()
    {
        _connectivity.SetMode(ConnectivityMode.Assisted);
        using var context = Context(Perm(PermissionType.Network, PermissionAccess.Read, "https://api.example.com"));

        var result = await context.Network.RequestAsync(Endpoint, "GET", null, "x");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Request was blocked or failed");
        _gateway.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Network_LookalikeHostsAreNotCoveredByTheGrant()
    {
        GoOnline();
        using var context = Context(Perm(PermissionType.Network, PermissionAccess.Read, "https://api.example.com"));

        foreach (var evil in new[]
        {
            "https://api.example.com.evil.net/",
            "https://api.example.com@evil.net/",
            "http://api.example.com/",
            "https://api.example.com:8443/"
        })
        {
            (await context.Network.RequestAsync(evil, "GET", null, "x")).Success.Should().BeFalse(evil);
        }

        _gateway.Requests.Should().BeEmpty();
    }

    #endregion

    #region Memory

    [Fact]
    public async Task Memory_WithoutPermissions_StoresNothingAndReturnsNothing()
    {
        using var context = Context();

        (await context.Memory.StoreAsync("k", "v")).Should().BeFalse();
        (await context.Memory.RetrieveAsync("k")).Should().BeNull();
        (await context.Memory.SearchAsync("v")).Should().BeEmpty();
        Entries(ResourceAccessType.MemoryWrite).Should().ContainSingle(e => e.Success == false);
        Entries(ResourceAccessType.MemoryRead).Should().ContainSingle(e => e.Success == false);
        Entries(ResourceAccessType.MemorySearch).Should().ContainSingle(e => e.Success == false);
    }

    [Fact]
    public async Task Memory_ReadOnlyCannotWrite_AndWriteOnlyCannotRead()
    {
        using var readOnly = Context(Perm(PermissionType.Memory, PermissionAccess.Read));
        using var writeOnly = Context(Perm(PermissionType.Memory, PermissionAccess.Write));

        (await readOnly.Memory.StoreAsync("k", "v")).Should().BeFalse();
        (await writeOnly.Memory.StoreAsync("k", "v")).Should().BeTrue();
        (await writeOnly.Memory.RetrieveAsync("k")).Should().BeNull();
        (await writeOnly.Memory.SearchAsync("v")).Should().BeEmpty();
    }

    [Fact]
    public async Task Memory_StoreRetrieveAndSearch_WithBothGrants()
    {
        using var context = Context(
            Perm(PermissionType.Memory, PermissionAccess.Read),
            Perm(PermissionType.Memory, PermissionAccess.Write));

        (await context.Memory.StoreAsync("a", "Alpha note")).Should().BeTrue();
        (await context.Memory.StoreAsync("b", "beta NOTE")).Should().BeTrue();
        (await context.Memory.StoreAsync("c", "unrelated")).Should().BeTrue();
        (await context.Memory.StoreAsync("a", "Alpha note, edited")).Should().BeTrue();

        (await context.Memory.RetrieveAsync("a")).Should().Be("Alpha note, edited");
        (await context.Memory.RetrieveAsync("missing")).Should().BeNull();
        (await context.Memory.SearchAsync("note")).Should().BeEquivalentTo("Alpha note, edited", "beta NOTE");
        (await context.Memory.SearchAsync("note", limit: 1)).Should().HaveCount(1);
        (await context.Memory.SearchAsync("zzz")).Should().BeEmpty();
    }

    [Fact]
    public async Task Memory_IsNotSharedBetweenContexts()
    {
        var grants = new[]
        {
            Perm(PermissionType.Memory, PermissionAccess.Read),
            Perm(PermissionType.Memory, PermissionAccess.Write)
        };
        using var one = _sandbox.CreateContext(Manifest("com.test.one", grants));
        using var two = _sandbox.CreateContext(Manifest("com.test.two", grants));

        await one.Memory.StoreAsync("k", "secret of one");

        (await two.Memory.RetrieveAsync("k")).Should().BeNull();
        (await two.Memory.SearchAsync("secret")).Should().BeEmpty();
    }

    #endregion

    #region Storage

    [Fact]
    public async Task Storage_RoundTrips_Lists_Removes_AndClears()
    {
        using var context = Context();

        await context.Storage.SetAsync("one", "1");
        await context.Storage.SetAsync("two", "2");
        await context.Storage.SetAsync("one", "uno");

        (await context.Storage.GetAsync("one")).Should().Be("uno");
        (await context.Storage.GetAsync("absent")).Should().BeNull();
        (await context.Storage.ListKeysAsync()).Should().BeEquivalentTo("one", "two");

        (await context.Storage.RemoveAsync("one")).Should().BeTrue();
        (await context.Storage.RemoveAsync("one")).Should().BeFalse();
        (await context.Storage.ListKeysAsync()).Should().BeEquivalentTo("two");

        await context.Storage.ClearAsync();
        (await context.Storage.ListKeysAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Storage_IsIsolatedPerPlugin()
    {
        using var one = _sandbox.CreateContext(Manifest("com.test.one"));
        using var two = _sandbox.CreateContext(Manifest("com.test.two"));

        await one.Storage.SetAsync("shared-name", "one's data");

        (await two.Storage.GetAsync("shared-name")).Should().BeNull();
        (await two.Storage.ListKeysAsync()).Should().BeEmpty();
        await two.Storage.ClearAsync();
        (await one.Storage.GetAsync("shared-name")).Should().Be("one's data");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("a..b")]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("sub/dir")]
    [InlineData("sub\\dir")]
    [InlineData("C:\\Windows\\win")]
    [InlineData("C:evil")]
    [InlineData("/rooted")]
    [InlineData("\\\\server\\share\\x")]
    [InlineData("bad|name")]
    [InlineData("bad?name")]
    [InlineData("bad*name")]
    [InlineData("bad\0name")]
    [InlineData("con:stream")]
    public async Task Storage_KeysThatAreNotOneFileName_AreRefused_AndAudited(string key)
    {
        using var context = Context();

        var set = async () => await context.Storage.SetAsync(key, "x");
        var get = async () => await context.Storage.GetAsync(key);
        var remove = async () => await context.Storage.RemoveAsync(key);

        await set.Should().ThrowAsync<ArgumentException>();
        await get.Should().ThrowAsync<ArgumentException>();
        await remove.Should().ThrowAsync<ArgumentException>();
        Entries(ResourceAccessType.StorageWrite).Should().ContainSingle(e => e.Success == false);
        Entries(ResourceAccessType.StorageRead).Should().ContainSingle(e => e.Success == false);
        Entries(ResourceAccessType.StorageDelete).Should().ContainSingle(e => e.Success == false);
    }

    [Fact]
    public async Task Storage_TraversalKey_NeverWritesOutsideThePluginFolder()
    {
        using var context = Context();
        var escape = Path.Combine(_storage, "escaped.json");

        var act = async () => await context.Storage.SetAsync("../escaped", "x");

        await act.Should().ThrowAsync<ArgumentException>();
        File.Exists(escape).Should().BeFalse();
        Directory.GetFiles(_root, "escaped*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Storage_ClearOnlyRemovesTheCallingPluginsFiles()
    {
        using var one = _sandbox.CreateContext(Manifest("com.test.one"));
        using var two = _sandbox.CreateContext(Manifest("com.test.two"));
        await one.Storage.SetAsync("k", "1");
        await two.Storage.SetAsync("k", "2");

        await one.Storage.ClearAsync();

        (await one.Storage.GetAsync("k")).Should().BeNull();
        (await two.Storage.GetAsync("k")).Should().Be("2");
    }

    [Fact]
    public async Task Storage_ListAndClearAreAudited()
    {
        using var context = Context();

        await context.Storage.ListKeysAsync();
        await context.Storage.ClearAsync();

        _audit.GetRecentEntries().Should().Contain(e => e.ResourceType == ResourceAccessType.StorageRead && e.Resource == "*");
        _audit.GetRecentEntries().Should().Contain(e => e.ResourceType == ResourceAccessType.StorageDelete && e.Resource == "*");
    }

    #endregion

    private sealed class RecordingGateway : INetworkGateway
    {
        public List<NetworkRequest> Requests { get; } = [];

        public NetworkResponse Next { get; set; } = new(true, 200, "ok", null, TimeSpan.FromMilliseconds(5));

        public Exception? Throw { get; set; }

        public Task<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            if (Throw is not null)
            {
                throw Throw;
            }

            return Task.FromResult(Next);
        }
    }
}
