using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using MauiApp1.Models;

namespace MauiApp1.Services;

/// <summary>Сообщение о новом элементе журнала (для подписки ViewModel).</summary>
public sealed class LogAddedMessage(LogEntry entry)
    : CommunityToolkit.Mvvm.Messaging.ValueChangedMessage<LogEntry>(entry);

/// <summary>
/// Централизованный журнал обмена командами (раздел 5.5 ТЗ).
/// Все ошибки дублируются сюда (раздел 7 ТЗ).
/// </summary>
public sealed class LogService
{
    private const int MaxEntries = 500;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public event EventHandler<LogEntry>? EntryAdded;

    private void Add(LogDirection direction, string message)
    {
        var entry = new LogEntry(DateTime.Now, direction, message);

        // Событие может прийти из фонового потока Bluetooth-сервиса — маршалим в UI-поток.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Entries.Add(entry);
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
            EntryAdded?.Invoke(this, entry);
        });

        Messenger.Default.Send(new LogAddedMessage(entry));
    }

    public void Tx(string command) => Add(LogDirection.Tx, Protocol.Clean(command));
    public void Rx(string data) => Add(LogDirection.Rx, data);
    public void Info(string message) => Add(LogDirection.Info, message);
    public void Error(string message) => Add(LogDirection.Error, message);

    public void Clear() => Entries.Clear();

    public string ToText() => string.Join(Environment.NewLine, Entries.Select(e => e.FullText));
}
