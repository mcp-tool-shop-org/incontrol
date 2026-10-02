using FluentAssertions;
using InControl.Core.Export;
using InControl.Core.Models;
using Xunit;

namespace InControl.Core.Tests.Export;

public class SessionExporterTests : IDisposable
{
    private readonly string _testDir;
    private readonly Conversation _testConversation;

    public SessionExporterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"export-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _testConversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "Test Conversation",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            ModifiedAt = DateTimeOffset.UtcNow,
            Model = "llama3.2",
            SystemPrompt = "You are a helpful assistant.",
            Messages =
            [
                Message.User("Hello, how are you?"),
                Message.Assistant("I'm doing well, thank you for asking!", "llama3.2"),
                Message.User("What's 2 + 2?"),
                Message.Assistant("2 + 2 equals 4.", "llama3.2")
            ]
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void ToMarkdown_IncludesTitle()
    {
        var markdown = SessionExporter.ToMarkdown(_testConversation);

        markdown.Should().Contain("# Test Conversation");
    }

    [Fact]
    public void ToMarkdown_IncludesMetadata()
    {
        var markdown = SessionExporter.ToMarkdown(_testConversation);

        markdown.Should().Contain("**Created:**");
        markdown.Should().Contain("**Modified:**");
        markdown.Should().Contain("**Model:** llama3.2");
    }

    [Fact]
    public void ToMarkdown_IncludesSystemPrompt()
    {
        var markdown = SessionExporter.ToMarkdown(_testConversation);

        markdown.Should().Contain("## System Prompt");
        markdown.Should().Contain("You are a helpful assistant.");
    }

    [Fact]
    public void ToMarkdown_IncludesAllMessages()
    {
        var markdown = SessionExporter.ToMarkdown(_testConversation);

        markdown.Should().Contain("**User**");
        markdown.Should().Contain("**Assistant**");
        markdown.Should().Contain("Hello, how are you?");
        markdown.Should().Contain("I'm doing well, thank you for asking!");
        markdown.Should().Contain("What's 2 + 2?");
        markdown.Should().Contain("2 + 2 equals 4.");
    }

    [Fact]
    public void ToMarkdown_IncludesExportFooter()
    {
        var markdown = SessionExporter.ToMarkdown(_testConversation);

        markdown.Should().Contain("*Exported from InControl");
    }

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var json = SessionExporter.ToJson(_testConversation);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"exportedAt\"");
        json.Should().Contain("\"conversation\"");
    }

    [Fact]
    public void ToJson_IncludesConversationData()
    {
        var json = SessionExporter.ToJson(_testConversation);

        json.Should().Contain("\"title\"");
        json.Should().Contain("Test Conversation");
        json.Should().Contain("\"messages\"");
    }

    [Fact]
    public async Task ExportToFileAsync_CreatesMarkdownFile()
    {
        var outputPath = Path.Combine(_testDir, "export.md");

        var result = await SessionExporter.ExportToFileAsync(
            _testConversation,
            ExportFormat.Markdown,
            outputPath);

        result.IsSuccess.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("# Test Conversation");
    }

    [Fact]
    public async Task ExportToFileAsync_CreatesJsonFile()
    {
        var outputPath = Path.Combine(_testDir, "export.json");

        var result = await SessionExporter.ExportToFileAsync(
            _testConversation,
            ExportFormat.Json,
            outputPath);

        result.IsSuccess.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(outputPath);
        content.Should().Contain("\"conversation\"");
    }

    [Fact]
    public async Task ExportToFileAsync_GeneratesFilename_WhenPathNotProvided()
    {
        var result = await SessionExporter.ExportToFileAsync(
            _testConversation,
            ExportFormat.Markdown);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().EndWith(".md");
        File.Exists(result.Value).Should().BeTrue();

        // Cleanup
        File.Delete(result.Value!);
    }

    [Fact]
    public async Task ExportAllAsync_CreatesArchive()
    {
        var conversations = new[] { _testConversation };
        var outputPath = Path.Combine(_testDir, "export.zip");

        var result = await SessionExporter.ExportAllAsync(conversations, outputPath);

        result.IsSuccess.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task ExportAllAsync_IncludesManifest()
    {
        var conversations = new[] { _testConversation };
        var outputPath = Path.Combine(_testDir, "export-manifest.zip");

        var result = await SessionExporter.ExportAllAsync(conversations, outputPath);

        result.IsSuccess.Should().BeTrue();

        using var archive = System.IO.Compression.ZipFile.OpenRead(outputPath);
        archive.Entries.Should().Contain(e => e.FullName == "manifest.json");
    }

    [Fact]
    public async Task ExportAllAsync_IncludesConversations()
    {
        var conversations = new[] { _testConversation };
        var outputPath = Path.Combine(_testDir, "export-conversations.zip");

        var result = await SessionExporter.ExportAllAsync(conversations, outputPath);

        result.IsSuccess.Should().BeTrue();

        using var archive = System.IO.Compression.ZipFile.OpenRead(outputPath);
        archive.Entries.Should().Contain(e => e.FullName.StartsWith("conversations/"));
    }
}

public class SessionImporterTests : IDisposable
{
    private readonly string _testDir;

    public SessionImporterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"import-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task ImportFromJsonAsync_ReturnsError_WhenFileNotFound()
    {
        var nonExistentPath = Path.Combine(_testDir, "nonexistent.json");

        var result = await SessionImporter.ImportFromJsonAsync(nonExistentPath);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(InControl.Core.Errors.ErrorCode.FileNotFound);
    }

    [Fact]
    public async Task ImportFromJsonAsync_ImportsExportedConversation()
    {
        var original = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "Original Conversation",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
            Messages = [Message.User("Test message")]
        };

        var exportPath = Path.Combine(_testDir, "exported.json");
        await SessionExporter.ExportToFileAsync(original, ExportFormat.Json, exportPath);

        var result = await SessionImporter.ImportFromJsonAsync(exportPath);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Contain("Original Conversation");
        result.Value.Title.Should().Contain("(Imported)");
        result.Value.Id.Should().NotBe(original.Id); // New ID assigned
    }

    [Fact]
    public async Task ImportFromJsonAsync_PreservesMessages()
    {
        var original = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "Conversation with Messages",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
            Messages =
            [
                Message.User("First message"),
                Message.Assistant("Response")
            ]
        };

        var exportPath = Path.Combine(_testDir, "with-messages.json");
        await SessionExporter.ExportToFileAsync(original, ExportFormat.Json, exportPath);

        var result = await SessionImporter.ImportFromJsonAsync(exportPath);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Messages.Should().HaveCount(2);
        result.Value.Messages[0].Content.Should().Be("First message");
        result.Value.Messages[1].Content.Should().Be("Response");
    }

    [Fact]
    public async Task ImportFromArchiveAsync_ReturnsError_WhenFileNotFound()
    {
        var nonExistentPath = Path.Combine(_testDir, "nonexistent.zip");

        var result = await SessionImporter.ImportFromArchiveAsync(nonExistentPath);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(InControl.Core.Errors.ErrorCode.FileNotFound);
    }

    [Fact]
    public async Task ImportFromArchiveAsync_ImportsMultipleConversations()
    {
        var conversations = new[]
        {
            new Conversation
            {
                Id = Guid.NewGuid(),
                Title = "Conversation 1",
                CreatedAt = DateTimeOffset.UtcNow,
                ModifiedAt = DateTimeOffset.UtcNow,
                Messages = []
            },
            new Conversation
            {
                Id = Guid.NewGuid(),
                Title = "Conversation 2",
                CreatedAt = DateTimeOffset.UtcNow,
                ModifiedAt = DateTimeOffset.UtcNow,
                Messages = []
            }
        };

        var archivePath = Path.Combine(_testDir, "multi-export.zip");
        await SessionExporter.ExportAllAsync(conversations, archivePath);

        var result = await SessionImporter.ImportFromArchiveAsync(archivePath);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(c => c.Title.Contains("Conversation 1"));
        result.Value.Should().Contain(c => c.Title.Contains("Conversation 2"));
    }
}

public class ExportFormatTests
{
    [Theory]
    [InlineData(ExportFormat.Markdown)]
    [InlineData(ExportFormat.Json)]
    public void ExportFormat_AllValuesAreDefined(ExportFormat format)
    {
        Enum.IsDefined(format).Should().BeTrue();
    }
}
