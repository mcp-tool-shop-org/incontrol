using System.Text;
using FluentAssertions;
using InControl.Core.Attachments;
using Xunit;

namespace InControl.Core.Tests.Attachments;

public class AttachmentReaderTests
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    [Fact]
    public void Read_TextFile_KeepsItsContents()
    {
        var result = AttachmentReader.Read("notes.md", Encoding.UTF8.GetBytes("# Plan\nship it\n"));

        result.Succeeded.Should().BeTrue();
        result.Attachment!.Kind.Should().Be(AttachmentKind.Text);
        result.Attachment.Text.Should().Be("# Plan\nship it\n");
        result.Attachment.ImageBase64.Should().BeNull();
    }

    [Fact]
    public void Read_StripsTheUtf8ByteOrderMark()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("hello")).ToArray();

        AttachmentReader.Read("a.txt", bytes).Attachment!.Text.Should().Be("hello");
    }

    [Fact]
    public void Read_ImageWithNulBytes_IsAnImageNotABinaryReject()
    {
        var result = AttachmentReader.Read("shot.PNG", PngHeader);

        result.Succeeded.Should().BeTrue();
        result.Attachment!.Kind.Should().Be(AttachmentKind.Image);
        result.Attachment.ImageBase64.Should().Be(Convert.ToBase64String(PngHeader));
        result.Attachment.Text.Should().BeNull();
    }

    [Theory]
    [InlineData("a.jpg")]
    [InlineData("a.jpeg")]
    [InlineData("a.webp")]
    public void IsImage_AcceptsTheFormatsOllamaTakes(string name)
    {
        AttachmentReader.IsImage(name).Should().BeTrue();
    }

    [Fact]
    public void Read_BinaryFile_IsRefused()
    {
        var result = AttachmentReader.Read("tool.exe", [0x4D, 0x5A, 0x00, 0x00]);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("not a text file");
    }

    [Fact]
    public void Read_InvalidUtf8_IsRefused()
    {
        var result = AttachmentReader.Read("latin1.txt", [0x63, 0x61, 0x66, 0xE9]);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("UTF-8");
    }

    [Fact]
    public void Read_EmptyImage_IsRefused()
    {
        AttachmentReader.Read("blank.png", []).Error.Should().Contain("empty");
    }

    [Fact]
    public void Read_TextOverTheLimit_IsRefused()
    {
        var bytes = Enumerable.Repeat((byte)'a', (int)AttachmentReader.MaxTextBytes + 1).ToArray();

        var result = AttachmentReader.Read("big.log", bytes);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("256 KB");
    }

    [Fact]
    public void ReadFile_ChecksSizeBeforeReadingAndReportsAMissingFile()
    {
        var dir = Directory.CreateTempSubdirectory("incontrol-attach-");
        try
        {
            var big = Path.Combine(dir.FullName, "big.txt");
            using (var fs = File.Create(big))
                fs.SetLength(AttachmentReader.MaxTextBytes + 1);

            AttachmentReader.ReadFile(big).Error.Should().Contain("larger than");
            AttachmentReader.ReadFile(Path.Combine(dir.FullName, "gone.txt")).Error.Should().Contain("not found");

            var ok = Path.Combine(dir.FullName, "ok.cs");
            File.WriteAllText(ok, "class A {}");
            AttachmentReader.ReadFile(ok).Attachment!.Name.Should().Be("ok.cs");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Compose_PutsFilesBeforeThePromptInFencedBlocks()
    {
        var text = new ChatAttachment("main.py", AttachmentKind.Text, "print(1)", null, 8);
        var image = new ChatAttachment("chart.png", AttachmentKind.Image, null, "AAAA", 3);

        var content = AttachmentReader.Compose("What does this do?", [text, image]);

        content.Should().Be("File: main.py\n```\nprint(1)\n```\n\nImage: chart.png\n\nWhat does this do?");
    }

    [Fact]
    public void Compose_WithoutAttachments_IsThePrompt()
    {
        AttachmentReader.Compose("hi", null).Should().Be("hi");
        AttachmentReader.Compose("hi", []).Should().Be("hi");
    }

    [Fact]
    public void Compose_FileOnly_HasNoTrailingBlankLines()
    {
        var text = new ChatAttachment("a.txt", AttachmentKind.Text, "x\n", null, 2);

        AttachmentReader.Compose("", [text]).Should().Be("File: a.txt\n```\nx\n```");
    }

    [Fact]
    public void Compose_UsesAFenceLongerThanAnyInsideTheFile()
    {
        var readme = new ChatAttachment("README.md", AttachmentKind.Text, "```bash\nls\n```", null, 14);

        var content = AttachmentReader.Compose("Summarize", [readme]);

        content.Should().StartWith("File: README.md\n````\n");
        content.Should().Contain("\n````\n\nSummarize");
    }

    [Fact]
    public void ImagesOf_ReturnsOnlyImages_OrNull()
    {
        var text = new ChatAttachment("a.txt", AttachmentKind.Text, "x", null, 1);
        var image = new ChatAttachment("b.png", AttachmentKind.Image, null, "QUJD", 3);

        AttachmentReader.ImagesOf([text, image]).Should().Equal("QUJD");
        AttachmentReader.ImagesOf([text]).Should().BeNull();
        AttachmentReader.ImagesOf(null).Should().BeNull();
    }
}
