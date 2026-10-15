using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiApp1.Models;

/// <summary>
/// Пресет — сохранённый набор параметров для сценария игры (раздел 4.4.1 ТЗ).
/// Хранится в JSON-файле в <see cref="FileSystem.AppDataDirectory"/>.
/// </summary>
public partial class Preset : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string title = "Новый сценарий";

    /// <summary>Время удержания точки, минуты (WIN_TIME).</summary>
    [ObservableProperty]
    private double winTimeMinutes = 1.0;

    /// <summary>Задержка перехвата, секунды (switchDelay).</summary>
    [ObservableProperty]
    private double switchDelaySeconds = 45.0;

    /// <summary>Интервал воскрешения аптечки, минуты.</summary>
    [ObservableProperty]
    private int medkitIntervalMinutes = 10;

    public Preset Clone() => new()
    {
        Title = Title,
        WinTimeMinutes = WinTimeMinutes,
        SwitchDelaySeconds = SwitchDelaySeconds,
        MedkitIntervalMinutes = MedkitIntervalMinutes
    };

    public override string ToString() => Title;
}
