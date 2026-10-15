using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;
// В .NET MAUI Application.Current — это Microsoft.Maui.Controls.Application,
// а не WinForms/WPF Application (у которого есть MainWindow).
using Application = Microsoft.Maui.Controls.Application;

namespace MauiApp1.ViewModels;

/// <summary>ViewModel экрана пресетов (раздел 5.4 ТЗ).</summary>
public partial class PresetsViewModel : ObservableObject
{
    private readonly PresetService _presets;
    private readonly ConnectionService _connection;
    private readonly LogService _log;

    public PresetsViewModel(PresetService presets, ConnectionService connection, LogService log)
    {
        _presets = presets;
        _connection = connection;
        _log = log;
    }

    public ObservableCollection<Preset> Items { get; } = new();

    [ObservableProperty]
    private Preset? selected;

    /// <summary>Режим редактирования: карточка с полями.</summary>
    [ObservableProperty]
    private bool isEditing;

    // Поля редактора
    [ObservableProperty] private string editTitle = "";
    [ObservableProperty] private string editWinTime = "1.5";
    [ObservableProperty] private string editDelay = "45";
    [ObservableProperty] private string editInterval = "10";

    private Guid? _editingId;

    public bool IsConnected => _connection.Bluetooth.ConnectedAddress is not null;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await _presets.LoadAsync();
        Items.Clear();
        foreach (var p in _presets.Presets)
            Items.Add(p);
    }

    [RelayCommand]
    private void CreateNew()
    {
        _editingId = null;
        EditTitle = $"Сценарий {Items.Count + 1}";
        EditWinTime = "1.5";
        EditDelay = "45";
        EditInterval = "10";
        IsEditing = true;
    }

    [RelayCommand]
    private void Edit(Preset? preset)
    {
        if (preset is null) return;
        _editingId = preset.Id;
        EditTitle = preset.Title;
        EditWinTime = preset.WinTimeMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        EditDelay = preset.SwitchDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        EditInterval = preset.MedkitIntervalMinutes.ToString();
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var winOk = Validation.IsWinTimeValid(EditWinTime, out var win);
        var delayOk = Validation.IsSwitchDelayValid(EditDelay, out var delay);
        var intOk = Validation.IsIntervalValid(EditInterval, out var interval);

        if (!winOk || !delayOk || !intOk || string.IsNullOrWhiteSpace(EditTitle))
        {
            _log.Error("Пресет не сохранён: некорректные значения параметров.");
            await Application.Current!.MainWindow!.Page!.DisplayAlert(
                "Ошибка ввода",
                "Проверьте значения: WIN_TIME > 0.05 мин, задержка ≥ 0 сек, интервал — целое > 0.",
                "Понятно");
            return;
        }

        var preset = _editingId is Guid id
            ? Items.FirstOrDefault(p => p.Id == id) ?? new Preset()
            : new Preset();

        preset.Title = EditTitle.Trim();
        preset.WinTimeMinutes = win;
        preset.SwitchDelaySeconds = delay;
        preset.MedkitIntervalMinutes = interval;

        if (_editingId is Guid existingId && Items.Any(p => p.Id == existingId))
            await _presets.UpdateAsync(preset);
        else
            await _presets.AddAsync(preset);

        await LoadAsync();
        IsEditing = false;
        _log.Info($"Пресет «{preset.Title}» сохранён.");
    }

    [RelayCommand]
    private async Task DeleteAsync(Preset? preset)
    {
        preset ??= Selected;
        if (preset is null) return;

        bool confirm = await Application.Current!.MainWindow!.Page!.DisplayAlert(
            "Удаление пресета", $"Удалить «{preset.Title}»?", "Удалить", "Отмена");
        if (!confirm) return;

        await _presets.RemoveAsync(preset.Id);
        Items.Remove(preset);
        _log.Info($"Пресет «{preset.Title}» удалён.");
    }

    /// <summary>Быстрое применение пресета к подключённому устройству (п. 4.4.1 ТЗ).</summary>
    [RelayCommand]
    private async Task ApplyAsync(Preset? preset)
    {
        preset ??= Selected;
        if (preset is null) return;

        if (!IsConnected)
        {
            _log.Error("Применение пресета невозможно: нет подключённого устройства.");
            await Application.Current!.MainWindow!.Page!.DisplayAlert(
                "Нет подключения",
                "Сначала подключитесь к устройству на главном экране.",
                "Понятно");
            return;
        }

        _log.Info($"Применение пресета «{preset.Title}»…");

        var addr = _connection.Bluetooth.ConnectedAddress!;
        // Определяем тип подключённого устройства по списку ViewModel главного экрана невозможно —
        // отправляем команды обоих типов, прошивка игнорирует неподдерживаемые.
        // Но корректнее: спрашиваем тип у DeviceType последнего устройства.
        var responses = new List<string>();

        var rT = await _connection.SendAndWaitAsync(Protocol.SetWinTime(preset.WinTimeMinutes));
        if (rT is not null) responses.Add(rT);
        var rD = await _connection.SendAndWaitAsync(Protocol.SetSwitchDelay(preset.SwitchDelaySeconds));
        if (rD is not null) responses.Add(rD);
        var rI = await _connection.SendAndWaitAsync(Protocol.SetMedkitInterval(preset.MedkitIntervalMinutes));
        if (rI is not null) responses.Add(rI);

        _log.Info(responses.Count > 0
            ? $"Пресет «{preset.Title}» применён. Ответы: {string.Join(" | ", responses)}"
            : $"Пресет «{preset.Title}»: ответов от устройства не получено (таймаут).");
    }
}
