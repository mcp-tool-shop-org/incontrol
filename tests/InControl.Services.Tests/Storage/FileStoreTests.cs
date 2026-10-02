using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InControl.Core.Errors;
using InControl.Services.Storage;
using Xunit;

namespace InControl.Services.Tests.Storage;

public class FileStoreTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FileStore _store;
    private readonly Mock<ILogger<FileStore>> _loggerMock;

    public FileStoreTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"InControlTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);

        _loggerMock = new Mock<ILogger<FileStore>>();
        _store = new FileStore(_loggerMock.Object, _testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public void AppDataPath_ReturnsConfiguredRoot()
    {
        _store.AppDataPath.Should().Be(_testRoot);
    }

    [Fact]
    public void IsPathAllowed_AllowsPathsWithinRoot()
    {
        var path = Path.Combine(_testRoot, "subdir", "file.txt");

        _store.IsPathAllowed(path).Should().BeTrue();
    }

    [Fact]
    public void IsPathAllowed_RejectsPathsOutsideRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "outside.txt");

        _store.IsPathAllowed(path).Should().BeFalse();
    }

    [Fact]
    public void IsPathAllowed_RejectsTraversalAttempts()
    {
        var path = Path.Combine(_testRoot, "..", "escaped.txt");

        _store.IsPathAllowed(path).Should().BeFalse();
    }

    [Fact]
    public async Task WriteTextAsync_CreatesFile()
    {
        var result = await _store.WriteTextAsync("test.txt", "Hello, World!");

        result.IsSuccess.Should().BeTrue();
        var fullPath = _store.GetFullPath("test.txt");
        File.Exists(fullPath).Should().BeTrue();
        File.ReadAllText(fullPath).Should().Be("Hello, World!");
    }

    [Fact]
    public async Task WriteTextAsync_CreatesDirectories()
    {
        var result = await _store.WriteTextAsync("subdir/nested/test.txt", "Content");

        result.IsSuccess.Should().BeTrue();
        var fullPath = _store.GetFullPath("subdir/nested/test.txt");
        File.Exists(fullPath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteTextAsync_RejectsPathTraversal()
    {
        var result = await _store.WriteTextAsync("../escape.txt", "Malicious");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.PathNotAllowed);
    }

    [Fact]
    public async Task WriteTextAsync_RejectsAbsolutePaths()
    {
        var result = await _store.WriteTextAsync("/etc/passwd", "Malicious");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.PathNotAllowed);
    }

    [Fact]
    public async Task ReadTextAsync_ReturnsContent()
    {
        await _store.WriteTextAsync("read-test.txt", "Expected content");

        var result = await _store.ReadTextAsync("read-test.txt");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Expected content");
    }

    [Fact]
    public async Task ReadTextAsync_ReturnsErrorForMissingFile()
    {
        var result = await _store.ReadTextAsync("nonexistent.txt");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileNotFound);
    }

    [Fact]
    public async Task ReadTextAsync_RejectsPathTraversal()
    {
        var result = await _store.ReadTextAsync("../../etc/passwd");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.PathNotAllowed);
    }

    [Fact]
    public async Task WriteBytesAsync_WritesData()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };

        var result = await _store.WriteBytesAsync("binary.dat", data);

        result.IsSuccess.Should().BeTrue();
        var read = await File.ReadAllBytesAsync(_store.GetFullPath("binary.dat"));
        read.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task ReadBytesAsync_ReturnsData()
    {
        var data = new byte[] { 10, 20, 30 };
        await _store.WriteBytesAsync("binary-read.dat", data);

        var result = await _store.ReadBytesAsync("binary-read.dat");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        await _store.WriteTextAsync("to-delete.txt", "Temporary");

        var result = await _store.DeleteAsync("to-delete.txt");

        result.IsSuccess.Should().BeTrue();
        File.Exists(_store.GetFullPath("to-delete.txt")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_SucceedsForNonexistentFile()
    {
        var result = await _store.DeleteAsync("never-existed.txt");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_RejectsPathTraversal()
    {
        var result = await _store.DeleteAsync("../important.txt");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.PathNotAllowed);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForExistingFile()
    {
        await _store.WriteTextAsync("exists.txt", "I exist");

        var exists = await _store.ExistsAsync("exists.txt");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForMissingFile()
    {
        var exists = await _store.ExistsAsync("missing.txt");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForTraversalPath()
    {
        var exists = await _store.ExistsAsync("../escape.txt");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsMatchingFiles()
    {
        await _store.WriteTextAsync("list/file1.txt", "A");
        await _store.WriteTextAsync("list/file2.txt", "B");
        await _store.WriteTextAsync("list/file3.json", "C");

        var result = await _store.ListFilesAsync("list", "*.txt");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(f => f.Contains("file1.txt"));
        result.Value.Should().Contain(f => f.Contains("file2.txt"));
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsEmptyForNonexistentDirectory()
    {
        var result = await _store.ListFilesAsync("nonexistent");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ListFilesAsync_RejectsPathTraversal()
    {
        var result = await _store.ListFilesAsync("../");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.PathNotAllowed);
    }

    [Fact]
    public void GetFullPath_ReturnsResolvedPath()
    {
        var fullPath = _store.GetFullPath("subdir/file.txt");

        fullPath.Should().StartWith(_testRoot);
        fullPath.Should().EndWith("file.txt");
    }

    [Fact]
    public async Task WriteTextAsync_HandlesEmptyPath()
    {
        var result = await _store.WriteTextAsync("", "Content");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.InvalidArgument);
    }

    [Fact]
    public async Task WriteTextAsync_HandlesWhitespacePath()
    {
        var result = await _store.WriteTextAsync("   ", "Content");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.InvalidArgument);
    }
}
