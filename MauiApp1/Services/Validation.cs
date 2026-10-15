using System.Text.RegularExpressions;

namespace MauiApp1.Services;

/// <summary>
/// Валидация ввода согласно разделу 4.2/4.3 ТЗ:
/// T &gt; 0.05 мин, D &gt;= 0 сек, I &gt; 0 (целое).
/// Поддерживается и запятая, и точка как десятичный разделитель.
/// </summary>
public static partial class Validation
{
    [GeneratedRegex(@"^\d+([.,]\d+)?$")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex IntRegex();

    public static bool TryParseNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().Replace(',', '.');
        if (!NumberRegex().IsMatch(text)) return false;
        return double.TryParse(text, System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    /// <summary>WIN_TIME: > 0.05 мин (3 секунды).</summary>
    public static bool IsWinTimeValid(string? text, out double minutes) =>
        TryParseNumber(text, out minutes) && minutes > 0.05;

    /// <summary>switchDelay: >= 0 сек (рекомендуется 30–90).</summary>
    public static bool IsSwitchDelayValid(string? text, out double seconds) =>
        TryParseNumber(text, out seconds) && seconds >= 0;

    /// <summary>Интервал аптечки: целое > 0.</summary>
    public static bool IsIntervalValid(string? text, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        if (!IntRegex().IsMatch(text)) return false;
        return int.TryParse(text, out minutes) && minutes > 0;
    }

    /// <summary>Рекомендуемый диапазон switchDelay (30–90 сек) — только подсказка, не ошибка.</summary>
    public static bool IsDelayInRecommendedRange(double seconds) => seconds is >= 30 and <= 90;
}
