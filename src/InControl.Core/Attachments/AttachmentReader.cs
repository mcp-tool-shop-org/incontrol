using System.Text;

namespace InControl.Core.Attachments;

/// <summary>
/// What kind of file was attached to a message.
/// </summary>
public enum AttachmentKind
{
    /// <summary>A text or code file. Its contents go into the message.</summary>
    Text,

    /// <summary>An image. It is sent to the model beside the message, for vision models.</summary>
    Image
}

/// <summary>
/// A file the user attached to the next message.
/// </summary>
/// <param name="Name">File name shown on the chip and in the message.</param>
/// <param name="Kind">Text or image.</param>
/// <param name="Text">File contents, for a text file.</param>
/// <param name="ImageBase64">Base64 bytes, for an image.</param>
/// <param name="Size">Size in bytes.</param>
public sealed record ChatAttachment(string Name, AttachmentKind Kind, string? Text, string? ImageBase64, long Size);

/// <summary>
/// The result of reading a file for attachment. Exactly one of the two is set.
/// </summary>
public sealed record AttachmentReadResult(ChatAttachment? Attachment, string? Error)
{
    public bool Succeeded => Attachment is not null;

    public static AttachmentReadResult Ok(ChatAttachment attachment) => new(attachment, null);

    public static AttachmentReadResult Fail(string error) => new(null, error);
}

/// <summary>
/// Turns files into message attachments, and attachments into what is sent to the model.
/// </summary>
public static class AttachmentReader
{
    /// <summary>Largest text file that is put into a message.</summary>
    public const long MaxTextBytes = 256 * 1024;

    /// <summary>Largest image that is sent to a vision model.</summary>
    public const long MaxImageBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    /// <summary>
    /// True when the name looks like an image Ollama can take.
    /// </summary>
    public static bool IsImage(string name) => ImageExtensions.Contains(Path.GetExtension(name));

    /// <summary>
    /// Reads a file from disk. The size is checked before the bytes are read.
    /// </summary>
    public static AttachmentReadResult ReadFile(string path)
    {
        var name = Path.GetFileName(path);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return AttachmentReadResult.Fail($"{name} was not found.");

            var limit = IsImage(name) ? MaxImageBytes : MaxTextBytes;
            if (info.Length > limit)
                return AttachmentReadResult.Fail(TooLarge(name, limit));

            return Read(name, File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AttachmentReadResult.Fail($"{name} could not be read: {ex.Message}");
        }
    }

    /// <summary>
    /// Classifies and checks a file's bytes.
    /// </summary>
    public static AttachmentReadResult Read(string name, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (string.IsNullOrWhiteSpace(name))
            name = "file";

        if (IsImage(name))
        {
            if (bytes.Length > MaxImageBytes)
                return AttachmentReadResult.Fail(TooLarge(name, MaxImageBytes));
            if (bytes.Length == 0)
                return AttachmentReadResult.Fail($"{name} is empty.");

            return AttachmentReadResult.Ok(new ChatAttachment(name, AttachmentKind.Image, null, Convert.ToBase64String(bytes), bytes.Length));
        }

        if (bytes.Length > MaxTextBytes)
            return AttachmentReadResult.Fail(TooLarge(name, MaxTextBytes));

        // A NUL byte means a binary file. Those would arrive at the model as noise.
        if (Array.IndexOf(bytes, (byte)0) >= 0)
            return AttachmentReadResult.Fail($"{name} is not a text file. Attach text, code, or a PNG, JPEG or WebP image.");

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return AttachmentReadResult.Fail($"{name} is not UTF-8 text.");
        }

        if (text.Length > 0 && text[0] == '﻿')
            text = text[1..];

        return AttachmentReadResult.Ok(new ChatAttachment(name, AttachmentKind.Text, text, null, bytes.Length));
    }

    /// <summary>
    /// The message text the model receives: each text file in a fenced block, a line per image, then the prompt.
    /// </summary>
    public static string Compose(string prompt, IReadOnlyList<ChatAttachment>? attachments)
    {
        prompt ??= string.Empty;
        if (attachments is null || attachments.Count == 0)
            return prompt;

        var sb = new StringBuilder();
        foreach (var attachment in attachments)
        {
            if (attachment.Kind == AttachmentKind.Text)
            {
                var text = attachment.Text ?? string.Empty;
                var fence = FenceFor(text);
                sb.Append("File: ").Append(attachment.Name).Append('\n');
                sb.Append(fence).Append('\n');
                sb.Append(text);
                if (!text.EndsWith('\n'))
                    sb.Append('\n');
                sb.Append(fence).Append("\n\n");
            }
            else
            {
                sb.Append("Image: ").Append(attachment.Name).Append("\n\n");
            }
        }

        sb.Append(prompt);
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// The base64 images to send beside the message, or null when there are none.
    /// </summary>
    public static IReadOnlyList<string>? ImagesOf(IReadOnlyList<ChatAttachment>? attachments)
    {
        if (attachments is null)
            return null;

        var images = attachments
            .Where(a => a.Kind == AttachmentKind.Image && !string.IsNullOrEmpty(a.ImageBase64))
            .Select(a => a.ImageBase64!)
            .ToList();
        return images.Count == 0 ? null : images;
    }

    /// <summary>
    /// A backtick fence longer than any run of backticks inside the text, so the file cannot close it early.
    /// </summary>
    internal static string FenceFor(string text)
    {
        var longest = 0;
        var run = 0;
        foreach (var c in text)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return new string('`', Math.Max(3, longest + 1));
    }

    private static string TooLarge(string name, long limit) =>
        $"{name} is larger than {(limit >= 1024 * 1024 ? $"{limit / (1024 * 1024)} MB" : $"{limit / 1024} KB")}, the most InControl attaches.";
}
