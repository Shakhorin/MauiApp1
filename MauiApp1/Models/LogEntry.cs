namespace MauiApp1.Models;

/// <summary>Направление записи журнала обмена.</summary>
public enum LogDirection
{
    Tx,
    Rx,
    Info,
    Error
}

/// <summary>Запись журнала обмена командами (раздел 5.5 ТЗ).</summary>
public record LogEntry(DateTime Time, LogDirection Direction, string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss");

    public string Prefix => Direction switch
    {
        LogDirection.Tx => "→ ",
        LogDirection.Rx => "← ",
        LogDirection.Error => "! ",
        _ => "* "
    };

    public string FullText => $"[{TimeText}] {Prefix}{Message}";
}
