using System.Text.Json;
using MauiApp1.Models;

namespace MauiApp1.Services;

/// <summary>
/// Хранилище пресетов: JSON-файл в FileSystem.AppDataDirectory (раздел 8.1 ТЗ).
/// </summary>
public sealed class PresetService
{
    private static readonly string FilePath =
        Path.Combine(FileSystem.AppDataDirectory, "presets.json");

    private readonly List<Preset> _presets = new();
    private bool _loaded;

    public IReadOnlyList<Preset> Presets => _presets;

    public async Task LoadAsync()
    {
        _presets.Clear();
        _loaded = true;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = await File.ReadAllTextAsync(FilePath);
                var list = JsonSerializer.Deserialize<List<Preset>>(json);
                if (list is not null)
                    _presets.AddRange(list);
            }
        }
        catch
        {
            // Повреждённый файл — начинаем с пустого списка.
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_presets,
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(FilePath, json);
    }

    public async Task AddAsync(Preset preset)
    {
        if (!_loaded) await LoadAsync();
        _presets.Add(preset);
        await SaveAsync();
    }

    public async Task UpdateAsync(Preset preset)
    {
        if (!_loaded) await LoadAsync();
        var index = _presets.FindIndex(p => p.Id == preset.Id);
        if (index >= 0)
        {
            _presets[index] = preset;
            await SaveAsync();
        }
    }

    public async Task RemoveAsync(Guid id)
    {
        if (!_loaded) await LoadAsync();
        _presets.RemoveAll(p => p.Id == id);
        await SaveAsync();
    }
}
