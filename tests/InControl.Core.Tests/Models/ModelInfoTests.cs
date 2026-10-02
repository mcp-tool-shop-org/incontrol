using FluentAssertions;
using InControl.Core.Models;
using Xunit;

namespace InControl.Core.Tests.Models;

public class ModelInfoTests
{
    [Theory]
    [InlineData(null, "Unknown")]
    [InlineData(512L, "512 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1024L * 1024, "1.0 MB")]
    [InlineData(1024L * 1024 * 1024, "1.0 GB")]
    [InlineData(4L * 1024 * 1024 * 1024, "4.0 GB")]
    public void SizeDisplay_FormatsCorrectly(long? sizeBytes, string expected)
    {
        // Arrange
        var model = new ModelInfo
        {
            Id = "test",
            Name = "Test Model",
            SizeBytes = sizeBytes
        };

        // Act & Assert
        model.SizeDisplay.Should().Be(expected);
    }
}
