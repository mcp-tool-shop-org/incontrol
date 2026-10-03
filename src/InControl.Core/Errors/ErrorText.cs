using System.Text.RegularExpressions;

namespace InControl.Core.Errors;

/// <summary>
/// Turns error text from a backend into a sentence a person can read.
/// </summary>
public static partial class ErrorText
{
    [GeneratedRegex(@"""message""\s*:\s*""((?:[^""\\]|\\.)*)""")]
    private static partial Regex MessageField();

    /// <summary>
    /// Ollama reports errors as JSON. Returns the message inside it, or the text unchanged.
    /// </summary>
    public static string Readable(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var match = MessageField().Match(text);
        if (!match.Success)
            return text;

        try
        {
            return Regex.Unescape(match.Groups[1].Value);
        }
        catch (ArgumentException)
        {
            return match.Groups[1].Value;
        }
    }
}
