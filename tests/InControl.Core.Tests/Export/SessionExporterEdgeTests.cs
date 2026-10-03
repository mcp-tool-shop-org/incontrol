using System.IO.Compression;
using FluentAssertions;
using InControl.Core.Errors;
using InControl.Core.Export;
using InControl.Core.Models;
using Xunit;

namespace InControl.Core.Tests.Export;

/// <summary>
/// Export and import edges: file names built from titles, message roles in Markdown,
/// and imports of damaged or foreign files.
/// </summary>
public class SessionExporterEdgeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "export-edge-" + Guid.NewGuid().ToString("N"));

    public SessionExporterEdgeTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static Conversation Conversation_(string title) => Conversation.Create(title, model: "llama3.2");

    #region Markdown

    [Fact]
    public void Markdown_LabelsEveryRole_AndQuotesEveryLineOfTheSystemPrompt()
    {
        var conversation = Conversation.Create("Roles", systemPrompt: "Line one\nLine two")
            .WithMessage(Message.System("system note"))
            .WithMessage(Message.User("question"))
            .WithMessage(Message.Assistant("answer"));

        var markdown = SessionExporter.ToMarkdown(conversation);

        markdown.Should().Contain("### **System**");
        markdown.Should().Contain("### **User**");
        markdown.Should().Contain("### **Assistant**");
        markdown.Should().Contain("> Line one\n> Line two");
    }

    [Fact]
    public void Markdown_WithoutModelOrSystemPrompt_OmitsThoseSections()
    {
        var markdown = SessionExporter.ToMarkdown(Conversation.Create("Plain"));

        markdown.Should().NotContain("**Model:**");
        markdown.Should().NotContain("## System Prompt");
        markdown.Should().Contain("## Conversation");
    }

    [Fact]
    public void Markdown_AnUnknownRole_IsLabelledWithItsName()
    {
        var odd = Message.User("hmm") with { Role = (MessageRole)42 };

        var markdown = SessionExporter.ToMarkdown(Conversation.Create("Odd").WithMessage(odd));

        markdown.Should().Contain("### **42**");
    }

    #endregion

    #region File names

    private async Task<string[]> EntryNamesFor(string title)
    {
        var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".zip");
        var result = await SessionExporter.ExportAllAsync([Conversation_(title)], path);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.Select(e => e.FullName).Where(n => n.StartsWith("conversations/")).ToArray();
    }

    [Theory]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j")]
    [InlineData("../../escape")]
    [InlineData("C:\\Windows\\system32")]
    public async Task ArchiveEntryNames_NeverContainPathSeparatorsFromTheTitle(string title)
    {
        var names = await EntryNamesFor(title);

        var entry = names.Should().ContainSingle().Subject;
        entry.Split('/').Should().HaveCount(2, "only the conversations folder separator is allowed");
        entry.Should().NotContain("\\").And.NotContain(":");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankTitle_GetsAPlaceholderName(string title)
    {
        var names = await EntryNamesFor(title);

        names.Single().Should().EndWith("_conversation.json");
    }

    [Fact]
    public async Task ALongTitle_IsCutAtFiftyCharacters()
    {
        var names = await EntryNamesFor(new string('t', 120));

        var fileName = names.Single().Split('/')[1];
        fileName.Should().EndWith(new string('t', 50) + ".json");
        fileName.Should().NotContain(new string('t', 51));
    }

    [Fact]
    public async Task ExportToFile_WithNoPath_UsesTheExportsFolderAndASanitizedTitle()
    {
        // Only the name is checked here; the file lands in the real exports folder and is removed.
        var result = await SessionExporter.ExportToFileAsync(Conversation_("my: title?"), ExportFormat.Json);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        try
        {
            Path.GetFileName(result.Value!).Should().StartWith("my_ title__").And.EndWith(".json");
        }
        finally
        {
            File.Delete(result.Value!);
        }
    }

    [Fact]
    public async Task ExportToFile_MarkdownAndJsonPickTheirOwnExtensions_AndUnwritablePathsFail()
    {
        var blocker = Path.Combine(_dir, "blocker");
        await File.WriteAllTextAsync(blocker, "x");

        var failed = await SessionExporter.ExportToFileAsync(
            Conversation_("t"), ExportFormat.Markdown, Path.Combine(blocker, "sub", "out.md"));

        failed.IsFailure.Should().BeTrue();
        failed.Error!.Code.Should().Be(ErrorCode.FileOperationFailed);
    }

    #endregion

    #region Import

    [Fact]
    public async Task ImportJson_ABareConversation_IsAccepted_WithANewIdAndTitleSuffix()
    {
        var original = Conversation_("Bare").WithMessage(Message.User("hello"));
        var path = Path.Combine(_dir, "bare.json");
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(original,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));

        var result = await SessionImporter.ImportFromJsonAsync(path);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value!.Title.Should().Be("Bare (Imported)");
        result.Value.Id.Should().NotBe(original.Id);
        result.Value.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task ImportJson_KeepsTheProjectOfAnExportedSession()
    {
        var project = Guid.NewGuid();
        var original = Conversation.Create("Filed", projectId: project);
        var path = Path.Combine(_dir, "filed.json");
        await SessionExporter.ExportToFileAsync(original, ExportFormat.Json, path);

        var result = await SessionImporter.ImportFromJsonAsync(path);

        result.Value!.ProjectId.Should().Be(project);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    public async Task ImportJson_Garbage_IsAFailureNotAnException(string content)
    {
        var path = Path.Combine(_dir, "garbage.json");
        await File.WriteAllTextAsync(path, content);

        var result = await SessionImporter.ImportFromJsonAsync(path);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileOperationFailed);
    }

    [Fact]
    public async Task ImportJson_JsonNull_ReportsADeserializationFailure()
    {
        var path = Path.Combine(_dir, "null.json");
        await File.WriteAllTextAsync(path, "null");

        var result = await SessionImporter.ImportFromJsonAsync(path);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.DeserializationFailed);
    }

    [Fact]
    public async Task ImportJson_Cancelled_ReportsCancellation()
    {
        var path = Path.Combine(_dir, "any.json");
        await File.WriteAllTextAsync(path, "{}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await SessionImporter.ImportFromJsonAsync(path, cts.Token);

        result.Error!.Code.Should().Be(ErrorCode.Cancelled);
    }

    [Fact]
    public async Task ImportArchive_SkipsTheManifestNonJsonEntriesAndDamagedEntries()
    {
        var path = Path.Combine(_dir, "mixed.zip");
        var good = Conversation_("Good");
        await SessionExporter.ExportAllAsync([good], path);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            WriteEntry(archive, "conversations/notes.txt", "not json");
            WriteEntry(archive, "conversations/broken.json", "{ nope");
            WriteEntry(archive, "conversations/empty-export.json", "{\"version\":1,\"exportedAt\":\"2026-01-01T00:00:00+00:00\"}");
        }

        var result = await SessionImporter.ImportFromArchiveAsync(path);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.Should().ContainSingle().Which.Title.Should().Be("Good (Imported)");
    }

    [Fact]
    public async Task ImportArchive_NotAZip_IsAFailure()
    {
        var path = Path.Combine(_dir, "not.zip");
        await File.WriteAllTextAsync(path, "plain text");

        var result = await SessionImporter.ImportFromArchiveAsync(path);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.FileOperationFailed);
    }

    [Fact]
    public async Task ImportArchive_Cancelled_ReportsCancellation()
    {
        var path = Path.Combine(_dir, "cancel.zip");
        await SessionExporter.ExportAllAsync([Conversation_("One")], path);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await SessionImporter.ImportFromArchiveAsync(path, cts.Token);

        result.Error!.Code.Should().Be(ErrorCode.Cancelled);
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    #endregion
}
