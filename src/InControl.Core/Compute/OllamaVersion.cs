using System.Text.Json;

namespace InControl.Core.Compute;

/// <summary>
/// Reads the <c>version</c> field from Ollama's <c>/api/version</c> body.
/// A value that is not a short version token is ignored.
/// </summary>
public static class OllamaVersion
{
    public static string? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return Clean(version.GetString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? Clean(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var value = version.Trim();
        if (value.Length is < 1 or > 40)
        {
            return null;
        }

        return value.All(static ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '+' or '-')
            ? value
            : null;
    }
}
