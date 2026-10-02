using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InControl.Services.Health;
using InControl.Services.Storage;
using Xunit;

namespace InControl.Services.Tests.Health;

public class StorageHealthCheckTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FileStore _fileStore;
    private readonly Mock<ILogger<FileStore>> _loggerMock;

    public StorageHealthCheckTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"InControlHealthTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _loggerMock = new Mock<ILogger<FileStore>>();
        _fileStore = new FileStore(_loggerMock.Object, _testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }

    [Fact]
    public async Task CheckAsync_ReturnsHealthy_WhenStorageIsAccessible()
    {
        var check = new StorageHealthCheck(_fileStore);

        var result = await check.CheckAsync();

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("writable");
    }

    [Fact]
    public async Task CheckAsync_CleansUpProbeFile()
    {
        var check = new StorageHealthCheck(_fileStore);

        await check.CheckAsync();

        var probeFile = Path.Combine(_testRoot, ".health-probe");
        File.Exists(probeFile).Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_IncludesBasePath_InProperties()
    {
        var check = new StorageHealthCheck(_fileStore);

        var result = await check.CheckAsync();

        result.Properties.Should().ContainKey("BasePath");
        result.Properties!["BasePath"].Should().Be(_testRoot);
    }

    [Fact]
    public void Name_IsStorage()
    {
        var check = new StorageHealthCheck(_fileStore);

        check.Name.Should().Be("Storage");
    }

    [Fact]
    public void Category_IsStorage()
    {
        var check = new StorageHealthCheck(_fileStore);

        check.Category.Should().Be("Storage");
    }
}
