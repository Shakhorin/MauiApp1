using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;
// Разрешаем неоднозначность с Microsoft.Maui.Devices.DeviceType.
using DeviceType = MauiApp1.Models.DeviceType;

namespace MauiApp1.ViewModels;

/// <summary>
/// ViewModel экрана устройства (раздел 5.3 ТЗ).
/// Отображает поля в зависимости от типа: точка захвата (T/D/R) или аптечка (I/R).
/// </summary>
public partial class DeviceViewModel : ObservableObject, IQueryAttributable
{
    private readonly ConnectionService _connection;
    private readonly LogService _log;

    public DeviceViewModel(ConnectionService connection, LogService log)
    {
        _connection = connection;
        _log = log;
        _connection.LinkLost += (_, _) => MainThread.BeginInvokeOnMainThread(OnLinkLost);
    }

    /// <summary>Текущее устройство (передаётся через параметры навигации).</summary>
    [ObservableProperty]
    private BluetoothDeviceInfo? device;

    [ObservableProperty]
    private string statusMessage = "Готов";

    /// <summary>Ответы устройства (область отображения ответов, п. 5.3 ТЗ).</summary>
    public ObservableCollection<string> Responses { get; } = new();

    // ─── Поля ввода точки захвата ───

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWinTimeValid))]
    [NotifyPropertyChangedFor(nameof(CanSendCapture))]
    private string winTimeText = "1.5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDelayValid))]
    [NotifyPropertyChangedFor(nameof(DelayHint))]
    [NotifyPropertyChangedFor(nameof(CanSendCapture))]
    private string switchDelayText = "45";

    // ─── Поле ввода аптечки ───

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIntervalValid))]
    [NotifyPropertyChangedFor(nameof(CanSendMedkit))]
    private string intervalText = "10";

    public bool IsCapturePoint => Device?.DeviceType == DeviceType.CapturePoint;
    public bool IsMedkit => Device?.DeviceType == DeviceType.Medkit;

    partial void OnDeviceChanged(BluetoothDeviceInfo? value)
    {
        OnPropertyChanged(nameof(IsCapturePoint));
        OnPropertyChanged(nameof(IsMedkit));
        OnPropertyChanged(nameof(DeviceTitle));
        ApplyFromPresetFields();
    }

    public string DeviceTitle =>
        Device is null ? "Устройство" : $"{Device.Name} — {Device.DeviceType.ToRussian()}";

    // ─── Валидация (подсветка ошибок, блокировка отправки — раздел 7 ТЗ) ───

    public bool IsWinTimeValid => Validation.IsWinTimeValid(WinTimeText, out _);
    public bool IsDelayValid => Validation.IsSwitchDelayValid(SwitchDelayText, out _);
    public bool IsIntervalValid => Validation.IsIntervalValid(IntervalText, out _);

    public string DelayHint
    {
        get
        {
            if (!Validation.TryParseNumber(SwitchDelayText, out var s)) return "";
            return Validation.IsDelayInRecommendedRange(s)
                ? ""
                : $"Рекомендуемый диапазон 30–90 сек (сейчас {s:0.##})";
        }
    }

    public bool CanSendCapture => IsWinTimeValid && IsDelayValid && _connection.Bluetooth.ConnectedAddress is not null;
    public bool CanSendMedkit => IsIntervalValid && _connection.Bluetooth.ConnectedAddress is not null;
    public bool CanReset => _connection.Bluetooth.ConnectedAddress is not null;

    partial void OnWinTimeTextChanged(string value) => RefreshSendCommands();
    partial void OnSwitchDelayTextChanged(string value) => RefreshSendCommands();
    partial void OnIntervalTextChanged(string value) => RefreshSendCommands();

    private void RefreshSendCommands()
    {
        ApplyCaptureCommand.NotifyCanExecuteChanged();
        ApplyMedkitCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }

    // ───────────────────── Команды ─────────────────────

    /// <summary>«Применить» для точки захвата: T + D.</summary>
    [RelayCommand(CanExecute = nameof(CanSendCapture))]
    private async Task ApplyCaptureAsync()
    {
        Validation.IsWinTimeValid(WinTimeText, out var minutes);
        Validation.IsSwitchDelayValid(SwitchDelayText, out var seconds);

        StatusMessage = "Отправка WIN_TIME…";
        var r1 = await _connection.SendAndWaitAsync(Protocol.SetWinTime(minutes));
        AddResponse(r1);

        StatusMessage = "Отправка switchDelay…";
        var r2 = await _connection.SendAndWaitAsync(Protocol.SetSwitchDelay(seconds));
        AddResponse(r2);

        if (r1 is not null || r2 is not null)
            StatusMessage = "Параметры точки применены.";
    }

    /// <summary>«Применить» для аптечки: I.</summary>
    [RelayCommand(CanExecute = nameof(CanSendMedkit))]
    private async Task ApplyMedkitAsync()
    {
        Validation.IsIntervalValid(IntervalText, out var minutes);

        StatusMessage = "Отправка интервала…";
        var r = await _connection.SendAndWaitAsync(Protocol.SetMedkitInterval(minutes));
        AddResponse(r);

        if (r is not null)
            StatusMessage = "Интервал воскрешения применён.";
    }

    /// <summary>Кнопка «Сброс»: R.</summary>
    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task ResetAsync()
    {
        StatusMessage = "Сброс…";
        var r = await _connection.SendAndWaitAsync(Protocol.Reset);
        AddResponse(r);
        if (r is not null)
            StatusMessage = "Сброс выполнен.";
    }

    /// <summary>Ручная смена типа устройства (если автоопределение не сработало).</summary>
    [RelayCommand]
    private void SetType(string? typeName)
    {
        if (Device is null) return;
        Device.DeviceType = typeName switch
        {
            "medkit" => DeviceType.Medkit,
            _ => DeviceType.CapturePoint
        };
        OnPropertyChanged(nameof(IsCapturePoint));
        OnPropertyChanged(nameof(IsMedkit));
        OnPropertyChanged(nameof(DeviceTitle));
        _log.Info($"Тип устройства вручную: {Device.DeviceType.ToRussian()}");
    }

    private void AddResponse(string? response)
    {
        if (string.IsNullOrEmpty(response)) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Responses.Add(response);
            while (Responses.Count > 50) Responses.RemoveAt(0);
        });
    }

    private void OnLinkLost()
    {
        // Раздел 7 ТЗ: потеря соединения → уведомление и возврат на главный экран.
        StatusMessage = "Соединение потеряно.";
        Shell.Current.GoToAsync("..");
        _ = Application.Current!.MainWindow!.Page!.DisplayAlert(
            "Соединение потеряно",
            "Связь с устройством прервана. Вернитесь на главный экран и подключитесь заново.",
            "Понятно");
    }

    // ─── Применение значений из пресета (п. 4.4.1 ТЗ) ───

    private double? _presetWin;
    private double? _presetDelay;
    private int? _presetInterval;

    /// <summary>Вызывается PresetsViewModel при «применить к подключённому устройству».</summary>
    public void LoadFromPreset(Preset preset)
    {
        _presetWin = preset.WinTimeMinutes;
        _presetDelay = preset.SwitchDelaySeconds;
        _presetInterval = preset.MedkitIntervalMinutes;
        ApplyFromPresetFields();
    }

    private void ApplyFromPresetFields()
    {
        if (_presetWin is double w) WinTimeText = w.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_presetDelay is double d) SwitchDelayText = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_presetInterval is int i) IntervalText = i.ToString();
    }

    /// <summary>Приём параметров навигации (?device=...).</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("device", out var value) && value is BluetoothDeviceInfo info)
        {
            Device = info;
            _log.Info($"Экран устройства: {info.DisplayName}, тип: {info.DeviceType.ToRussian()}");
        }
    }
}
