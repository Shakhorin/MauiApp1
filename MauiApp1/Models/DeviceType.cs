namespace MauiApp1.Models;

/// <summary>Тип игрового устройства.</summary>
public enum DeviceType
{
    /// <summary>Не определён (пользователь выбирает вручную).</summary>
    Unknown = 0,

    /// <summary>Точка захвата (кнопки команд + RGB-лента).</summary>
    CapturePoint = 1,

    /// <summary>Аптечка (периодический сигнал воскрешения).</summary>
    Medkit = 2
}

public static class DeviceTypeExtensions
{
    public static string ToRussian(this DeviceType type) => type switch
    {
        DeviceType.CapturePoint => "Точка захвата",
        DeviceType.Medkit => "Аптечка",
        _ => "Не определён"
    };
}
