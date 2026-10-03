using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using InControl.Core.Configuration;
using InControl.Services.Configuration;
using Xunit;

namespace InControl.Services.Tests.Configuration;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("incontrol-settings-").FullName;

    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>
    /// A fresh set of option objects, as a new app launch would bind them from appsettings.json.
    /// </summary>
    private JsonSettingsService Launch(out AppOptions app, out InferenceOptions inference, out VoiceOptions voice, out OllamaOptions ollama)
    {
        app = new AppOptions();
        inference = new InferenceOptions();
        voice = new VoiceOptions();
        ollama = new OllamaOptions();
        return new JsonSettingsService(
            Options.Create(app),
            Options.Create(new ChatOptions()),
            Options.Create(inference),
            Options.Create(ollama),
            Options.Create(voice),
            NullLogger<JsonSettingsService>.Instance,
            FilePath);
    }

    [Fact]
    public async Task AChange_TakesEffectAtOnce_AndSurvivesARestart()
    {
        var first = Launch(out var app, out var inference, out var voice, out _);

        await first.UpdateAppOptionsAsync(o => o.Theme = "Dark");
        await first.UpdateVoiceOptionsAsync(o => { o.DefaultVoice = "am_adam"; o.AutoSpeak = false; o.Speed = 1.25f; });
        await first.UpdateInferenceOptionsAsync(o => { o.WebSearch = true; o.DefaultModel = "gemma4:31b"; });

        app.Theme.Should().Be("Dark");
        voice.AutoSpeak.Should().BeFalse();

        Launch(out var app2, out var inference2, out var voice2, out _);
        app2.Theme.Should().Be("Dark");
        voice2.DefaultVoice.Should().Be("am_adam");
        voice2.AutoSpeak.Should().BeFalse();
        voice2.Speed.Should().Be(1.25f);
        inference2.WebSearch.Should().BeTrue();
        inference2.DefaultModel.Should().Be("gemma4:31b");
    }

    [Fact]
    public async Task OnlyChangedSettingsAreWritten_SoTheRestKeepFollowingTheDefaults()
    {
        var settings = Launch(out _, out _, out _, out _);

        await settings.UpdateVoiceOptionsAsync(o => o.Volume = 0.3f);

        var saved = JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject();
        saved.Select(p => p.Key).Should().Equal(VoiceOptions.SectionName);
        saved[VoiceOptions.SectionName]!.AsObject().Select(p => p.Key).Should().Equal(nameof(VoiceOptions.Volume));
    }

    [Fact]
    public async Task TheOllamaSection_WithAColonInItsName_RoundTrips()
    {
        var settings = Launch(out _, out _, out _, out _);

        await settings.UpdateOllamaOptionsAsync(o => o.BaseUrl = "http://127.0.0.1:11500");

        Launch(out _, out _, out _, out var ollama).OllamaOptions.BaseUrl.Should().Be("http://127.0.0.1:11500");
        ollama.BaseUrl.Should().Be("http://127.0.0.1:11500");
    }

    [Fact]
    public async Task Reset_PutsBackTheDefaults_AndRaisesChanged()
    {
        var settings = Launch(out var app, out _, out var voice, out _);
        await settings.UpdateAppOptionsAsync(o => o.Theme = "Light");
        await settings.UpdateVoiceOptionsAsync(o => o.AutoSpeak = false);
        var sections = new List<string>();
        settings.SettingsChanged += (_, e) => sections.Add(e.Section);

        await settings.ResetToDefaultsAsync();

        app.Theme.Should().Be(new AppOptions().Theme);
        voice.AutoSpeak.Should().Be(new VoiceOptions().AutoSpeak);
        sections.Should().Contain([AppOptions.SectionName, VoiceOptions.SectionName]);
        Launch(out var app2, out _, out _, out _);
        app2.Theme.Should().Be(new AppOptions().Theme);
    }

    [Fact]
    public void ADamagedFile_FallsBackToTheDefaults()
    {
        File.WriteAllText(FilePath, "{ not json");

        Launch(out var app, out _, out _, out _);

        app.Theme.Should().Be(new AppOptions().Theme);
    }

    [Fact]
    public void AValueOfTheWrongType_IsSkipped_AndTheRestStillApply()
    {
        File.WriteAllText(FilePath, """{ "Voice": { "Speed": "fast", "DefaultVoice": "bf_emma" } }""");

        Launch(out _, out _, out var voice, out _);

        voice.Speed.Should().Be(new VoiceOptions().Speed);
        voice.DefaultVoice.Should().Be("bf_emma");
    }

    [Fact]
    public async Task Export_ThenImport_CarriesTheSettingsAcross()
    {
        var source = Launch(out _, out _, out _, out _);
        await source.UpdateAppOptionsAsync(o => o.MinimizeToTray = false);
        var json = await source.ExportAsync();

        File.Delete(FilePath);
        var target = Launch(out var app, out _, out _, out _);
        await target.ImportAsync(json);

        app.MinimizeToTray.Should().BeFalse();
        Launch(out var app2, out _, out _, out _);
        app2.MinimizeToTray.Should().BeFalse();
    }

    [Fact]
    public async Task Import_RejectsJsonThatIsNotAnObject()
    {
        var settings = Launch(out _, out _, out _, out _);

        var act = () => settings.ImportAsync("[1,2]");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
