using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.ViewModels;

/// <summary>ViewModel экрана журнала обмена (раздел 5.5 ТЗ).</summary>
public partial class LogViewModel : ObservableObject
{
    private readonly LogService _log;

    public LogViewModel(LogService log)
    {
        _log = log;
    }

    public System.Collections.ObjectModel.ObservableCollection<LogEntry> Entries => _log.Entries;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool autoScroll = true;

    /// <summary>Очистка лога.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Clear()
    {
        _log.Clear();
        _log.Info("Журнал очищен.");
    }

    /// <summary>Копирование всего лога в буфер обмена.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task CopyAsync()
    {
        await Clipboard.Default.SetTextAsync(_log.ToText());
        _log.Info("Журнал скопирован в буфер обмена.");
    }
}
