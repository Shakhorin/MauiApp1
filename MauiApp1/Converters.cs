using System.Globalization;

namespace MauiApp1;

/// <summary>Конвертеры для XAML.</summary>
public sealed class BoolToGreenGrayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "#2ECC71" : "#9AA0A6";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToRedGrayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "#E74C3C" : "Transparent";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>РSSI → «полоски» сигнала (▂▄▆█).</summary>
public sealed class RssiToBarsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int bars and > 0
            ? string.Concat(Enumerable.Repeat("▂", Math.Min(bars, 1))) +
              string.Concat(Enumerable.Repeat("▄", Math.Clamp(bars - 1, 0, 1))) +
              string.Concat(Enumerable.Repeat("▆", Math.Clamp(bars - 2, 0, 1))) +
              string.Concat(Enumerable.Repeat("█", Math.Clamp(bars - 3, 0, 1)))
            : "·";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DeviceTypeToShortConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Models.DeviceType.CapturePoint ? "Точка"
        : value is Models.DeviceType.Medkit ? "Аптечка"
        : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SelectedBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "#512BD4" : "Transparent";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// bool → visible/collapsed
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Visible" : "Collapsed";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// invert bool
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
