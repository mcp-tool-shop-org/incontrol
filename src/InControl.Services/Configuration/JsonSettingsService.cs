using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InControl.Core.Configuration;
using InControl.Core.Storage;
using InControl.Services.Interfaces;

namespace InControl.Services.Configuration;

/// <summary>
/// Keeps the person's settings between launches in one JSON file under the app's config folder.
/// The live option objects the app already reads are updated in place, so a change takes
/// effect at once, and saved values are laid over appsettings.json when the app starts.
/// Only settings someone changed are written; everything else keeps following the defaults.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, object> _sections;
    private readonly JsonObject _saved;

    public JsonSettingsService(
        IOptions<AppOptions> app,
        IOptions<ChatOptions> chat,
        IOptions<InferenceOptions> inference,
        IOptions<OllamaOptions> ollama,
        IOptions<VoiceOptions> voice,
        ILogger<JsonSettingsService> logger,
        string? path = null)
    {
        _logger = logger;
        _path = path ?? Path.Combine(DataPaths.Config, "settings.json");
        _sections = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [AppOptions.SectionName] = app.Value,
            [ChatOptions.SectionName] = chat.Value,
            [InferenceOptions.SectionName] = inference.Value,
            [OllamaOptions.SectionName] = ollama.Value,
            [VoiceOptions.SectionName] = voice.Value
        };

        _saved = Read();
        foreach (var (name, target) in _sections)
        {
            if (_saved[name] is JsonObject values)
                Apply(values, target);
        }
    }

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public AppOptions AppOptions => (AppOptions)_sections[AppOptions.SectionName];
    public ChatOptions ChatOptions => (ChatOptions)_sections[ChatOptions.SectionName];
    public InferenceOptions InferenceOptions => (InferenceOptions)_sections[InferenceOptions.SectionName];
    public OllamaOptions OllamaOptions => (OllamaOptions)_sections[OllamaOptions.SectionName];
    public VoiceOptions VoiceOptions => (VoiceOptions)_sections[VoiceOptions.SectionName];

    public Task UpdateAppOptionsAsync(Action<AppOptions> configure, CancellationToken ct = default) =>
        UpdateAsync(AppOptions.SectionName, AppOptions, configure, ct);

    public Task UpdateChatOptionsAsync(Action<ChatOptions> configure, CancellationToken ct = default) =>
        UpdateAsync(ChatOptions.SectionName, ChatOptions, configure, ct);

    public Task UpdateInferenceOptionsAsync(Action<InferenceOptions> configure, CancellationToken ct = default) =>
        UpdateAsync(InferenceOptions.SectionName, InferenceOptions, configure, ct);

    public Task UpdateOllamaOptionsAsync(Action<OllamaOptions> configure, CancellationToken ct = default) =>
        UpdateAsync(OllamaOptions.SectionName, OllamaOptions, configure, ct);

    public Task UpdateVoiceOptionsAsync(Action<VoiceOptions> configure, CancellationToken ct = default) =>
        UpdateAsync(VoiceOptions.SectionName, VoiceOptions, configure, ct);

    public async Task ResetToDefaultsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            foreach (var (name, target) in _sections)
            {
                if (_saved[name] is not JsonObject values)
                    continue;

                // Put back the defaults for exactly the settings that were changed.
                var defaults = Activator.CreateInstance(target.GetType())!;
                foreach (var key in values.Select(p => p.Key).ToList())
                {
                    var property = Property(target.GetType(), key);
                    property?.SetValue(target, property.GetValue(defaults));
                }
            }

            _saved.Clear();
            await WriteAsync(ct);
        }
        finally
        {
            _gate.Release();
        }

        foreach (var name in _sections.Keys)
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs { Section = name });
    }

    public Task<string> ExportAsync(CancellationToken ct = default)
    {
        var all = new JsonObject();
        foreach (var (name, target) in _sections)
            all[name] = JsonSerializer.SerializeToNode(target, target.GetType(), Json);

        return Task.FromResult(all.ToJsonString(Json));
    }

    public async Task ImportAsync(string json, CancellationToken ct = default)
    {
        var incoming = JsonNode.Parse(json) as JsonObject
            ?? throw new ArgumentException("Settings JSON must be an object.", nameof(json));

        await _gate.WaitAsync(ct);
        try
        {
            foreach (var (name, target) in _sections)
            {
                if (incoming[name] is not JsonObject values)
                    continue;

                Apply(values, target);
                var kept = _saved[name] as JsonObject ?? new JsonObject();
                foreach (var (key, value) in values)
                    kept[key] = value?.DeepClone();
                _saved[name] = kept;
            }

            await WriteAsync(ct);
        }
        finally
        {
            _gate.Release();
        }

        foreach (var name in _sections.Keys)
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs { Section = name });
    }

    private async Task UpdateAsync<T>(string section, T target, Action<T> configure, CancellationToken ct)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configure);

        await _gate.WaitAsync(ct);
        try
        {
            var before = JsonSerializer.SerializeToNode(target, Json)!.AsObject();
            configure(target);
            var after = JsonSerializer.SerializeToNode(target, Json)!.AsObject();

            // Record only what this change touched, so untouched settings keep following the defaults.
            var kept = _saved[section] as JsonObject ?? new JsonObject();
            foreach (var (key, value) in after)
            {
                if (!JsonNode.DeepEquals(before[key], value))
                    kept[key] = value?.DeepClone();
            }

            _saved[section] = kept;
            await WriteAsync(ct);
        }
        finally
        {
            _gate.Release();
        }

        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs { Section = section });
    }

    private JsonObject Read()
    {
        try
        {
            if (File.Exists(_path) && JsonNode.Parse(File.ReadAllText(_path)) is JsonObject saved)
                return saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged file must not stop the app. The defaults apply, and the next change rewrites it.
            _logger.LogWarning(ex, "Saved settings could not be read; using defaults");
        }

        return new JsonObject();
    }

    private async Task WriteAsync(CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, _saved.ToJsonString(Json), ct);
        File.Move(temp, _path, overwrite: true);
    }

    private void Apply(JsonObject values, object target)
    {
        foreach (var (key, value) in values)
        {
            var property = Property(target.GetType(), key);
            if (property is null || value is null)
                continue;

            try
            {
                property.SetValue(target, value.Deserialize(property.PropertyType, Json));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
            {
                _logger.LogWarning("Saved setting {Name} could not be applied: {Message}", key, ex.Message);
            }
        }
    }

    private static PropertyInfo? Property(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return property is { CanRead: true, CanWrite: true } ? property : null;
    }
}
