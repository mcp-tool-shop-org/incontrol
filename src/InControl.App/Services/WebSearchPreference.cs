using System.Text.Json;
using InControl.Core.Storage;

namespace InControl.App.Services;

/// <summary>
/// Remembers the composer's web search toggle between launches, in its own small file
/// under the app's config folder.
/// </summary>
public static class WebSearchPreference
{
    private static string FilePath => Path.Combine(DataPaths.Config, "web-search.json");

    private sealed record Saved(bool Enabled);

    /// <summary>
    /// The saved choice, or the fallback when nothing was saved or the file cannot be read.
    /// </summary>
    public static bool Load(bool fallback)
    {
        try
        {
            if (!File.Exists(FilePath))
                return fallback;

            return JsonSerializer.Deserialize<Saved>(File.ReadAllText(FilePath))?.Enabled ?? fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return fallback;
        }
    }

    /// <summary>
    /// Saves the choice. False when it could not be written.
    /// </summary>
    public static bool Save(bool enabled)
    {
        try
        {
            Directory.CreateDirectory(DataPaths.Config);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new Saved(enabled)));
            File.Move(temp, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
