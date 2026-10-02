using Microsoft.Extensions.Logging;
using InControl.Core.Models;
using InControl.Core.State;
using InControl.Services.Interfaces;

namespace InControl.Services.Storage;

/// <summary>
/// Stores projects in a single JSON file under the app data root.
/// </summary>
public sealed class JsonProjectLibrary : IProjectLibrary, IDisposable
{
    private const string FileName = "projects.json";
    private readonly IFileStore _files;
    private readonly ILogger<JsonProjectLibrary> _logger;
    private readonly List<ChatProject> _projects = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _loaded;

    public JsonProjectLibrary(IFileStore files, ILogger<JsonProjectLibrary> logger)
    {
        _files = files;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_loaded)
                return;

            await LoadUnlockedAsync(ct);
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChatProject>> AllAsync(CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            return _projects.ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatProject?> FindAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            return _projects.FirstOrDefault(project => project.Id == id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatProject> CreateAsync(string name, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var project = new ChatProject
            {
                Id = Guid.NewGuid(),
                Name = CleanName(name),
                CreatedAt = DateTimeOffset.UtcNow
            };
            _projects.Add(project);
            await SaveUnlockedAsync(ct);
            return project;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatProject> RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var index = _projects.FindIndex(project => project.Id == id);
            if (index < 0)
                throw new KeyNotFoundException($"Project {id} not found");

            var updated = _projects[index].WithName(CleanName(name));
            _projects[index] = updated;
            await SaveUnlockedAsync(ct);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatProject> UpdateInstructionsAsync(Guid id, string? instructions, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            var index = _projects.FindIndex(project => project.Id == id);
            if (index < 0)
                throw new KeyNotFoundException($"Project {id} not found");

            var updated = _projects[index].WithInstructions(CleanInstructions(instructions));
            _projects[index] = updated;
            await SaveUnlockedAsync(ct);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task LoadUnlockedAsync(CancellationToken ct)
    {
        if (!await _files.ExistsAsync(FileName, ct))
        {
            _projects.Clear();
            _projects.Add(ChatProject.General());
            await SaveUnlockedAsync(ct);
            return;
        }

        var read = await _files.ReadTextAsync(FileName, ct);
        if (read.IsFailure)
        {
            _logger.LogWarning("Could not read projects: {Error}", read.Error.Message);
            ResetToGeneral();
            await SaveUnlockedAsync(ct);
            return;
        }

        var parsed = StateSerializer.Deserialize<List<ChatProject>>(read.Value);
        var list = parsed.Value;
        if (parsed.IsFailure || list is null)
        {
            _logger.LogWarning("Could not read projects: {Error}", parsed.IsFailure ? parsed.Error.Message : "empty");
            ResetToGeneral();
            await SaveUnlockedAsync(ct);
            return;
        }

        _projects.Clear();
        _projects.AddRange(list.Where(project => !string.IsNullOrWhiteSpace(project.Name)));
        if (_projects.All(project => project.Id != ChatProject.GeneralId))
            _projects.Insert(0, ChatProject.General());
    }

    private void ResetToGeneral()
    {
        _projects.Clear();
        _projects.Add(ChatProject.General());
    }

    private async Task SaveUnlockedAsync(CancellationToken ct)
    {
        var json = StateSerializer.Serialize(_projects.ToList());
        var write = await _files.WriteTextAsync(FileName, json, ct);
        if (write.IsFailure)
            throw new InvalidOperationException($"Failed to save projects: {write.Error.Message}");
    }

    private static string CleanName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("A project needs a name.");

        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    private static string? CleanInstructions(string? instructions)
    {
        if (string.IsNullOrWhiteSpace(instructions))
            return null;

        var trimmed = instructions.Trim();
        return trimmed.Length <= 2000 ? trimmed : trimmed[..2000];
    }
}
