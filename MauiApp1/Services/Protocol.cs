using System.Globalization;
using MauiApp1.Models;

namespace MauiApp1.Services;

/// <summary>
/// Формирование и разбор команд протокола (раздел 6 ТЗ).
/// Формат: ASCII-символ команды + числовое значение, оканчивается '\n'.
/// Числа форматируются с инвариантной культурой ('.' как разделитель),
/// т.к. Arduino parseFloat читает именно такой формат.
/// </summary>
public static class Protocol
{
    /// <summary>T + float — установить время удержания (WIN_TIME), минуты. Пример: "T1.5\n".</summary>
    public static string SetWinTime(double minutes) =>
        $"T{minutes.ToString(CultureInfo.InvariantCulture)}\n";

    /// <summary>D + float — задержка перехвата (switchDelay), секунды. Пример: "D45\n".</summary>
    public static string SetSwitchDelay(double seconds) =>
        $"D{seconds.ToString(CultureInfo.InvariantCulture)}\n";

    /// <summary>I + int — интервал воскрешения аптечки, минуты. Пример: "I10\n".</summary>
    public static string SetMedkitInterval(int minutes) =>
        $"I{minutes.ToString(CultureInfo.InvariantCulture)}\n";

    /// <summary>R — сброс. Пример: "R\n".</summary>
    public const string Reset = "R\n";

    /// <summary>V — запрос версии/типа прошивки (автоопределение устройства, п. 4.4.3).</summary>
    public const string QueryVersion = "V\n";

    /// <summary>Ожидание ответа от устройства, сек (раздел 7 ТЗ: таймаут 3 секунды).</summary>
    public const int ResponseTimeoutSeconds = 3;

    /// <summary>
    /// Пытается определить тип устройства по строке приветствия/ответа на 'V'.
    /// Возвращает DeviceType.Unknown, если распознать не удалось.
    /// </summary>
    public static DeviceType DetectDeviceType(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return DeviceType.Unknown;

        var s = response.ToLowerInvariant();

        if (s.Contains("medkit") || s.Contains("apt") /* «аптечка» в транслите/распознавке */)
            return DeviceType.Medkit;
        if (s.Contains("point") || s.Contains("capture") || s.Contains("win"))
            return DeviceType.CapturePoint;

        // Косвенные признаки по формату подтверждений протокола.
        if (s.Contains("interval"))
            return DeviceType.Medkit;
        if (s.Contains("delay") || s.Contains("time:"))
            return DeviceType.CapturePoint;

        return DeviceType.Unknown;
    }

    /// <summary>Убирает символы конца строки из полученного сообщения.</summary>
    public static string Clean(string raw) =>
        raw.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
}
