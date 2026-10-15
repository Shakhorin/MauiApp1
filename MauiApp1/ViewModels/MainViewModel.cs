using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Views;
// Разрешаем неоднозначность с Microsoft.Maui.Devices.DeviceType.
using DeviceType = MauiApp1.Models.DeviceType;
// В .NET MAUI Application.Current — это Microsoft.Maui.Controls.Application,
// а не WinForms/WPF Application (у которого есть MainWindow).
using Application = Microsoft.Maui.Controls.Application;

namespace MauiApp1.ViewModels;

/// <summary>
/// ViewModel главного экрана: управление адаптером, сканирование, сопряжение,
/// подключение/отключение, удаление сопряжения (раздел 5.2 ТЗ).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ConnectionService _connection;
    private readonly IBluetoothService _bt;
    private readonly LogService _log;

    // Конструкторы — в файле MainViewModel.DesignTime.cs (DI + design-time).

    /// <summary>Синхронизация состояния адаптера с реальным (на случай, если приёмник не успел).</summary>
    public void RefreshAdapterState() => IsBluetoothEnabled = _bt.IsEnabled;

    public ObservableCollection<BluetoothDeviceInfo> Devices { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelected))]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyPropertyChangedFor(nameof(CanDisconnect))]
    [NotifyPropertyChangedFor(nameof(CanForget))]
    [NotifyCanExecuteChangedFor(nameof(PairCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ForgetCommand))]
    private BluetoothDeviceInfo? selectedDevice;

    public bool IsSelected => SelectedDevice is not null;

    [ObservableProperty]
    private bool isBluetoothEnabled;

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private string statusMessage = "Готов";

    /// <summary>Индикатор текущего подключения на главном экране.</summary>
    public bool IsConnected => _bt.ConnectedAddress is not null;

    public string ConnectionInfo =>
        _bt.ConnectedAddress is { } addr
            ? $"Подключено: {NameFor(addr)} ({addr})"
            : "Не подключено";

    partial void OnIsBluetoothEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanScan));
        ScanCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsScanningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanScan));
        ScanCommand.NotifyCanExecuteChanged();
    }

    public bool CanScan => IsBluetoothEnabled && !IsScanning;

    // ─── Условия доступности кнопок выбранного устройства (п. 5.2 ТЗ) ────

    public bool CanPair => SelectedDevice is { IsBonded: false, IsBonding: false };
    public bool CanConnect => SelectedDevice is { IsBonded: true } d && !d.IsConnected;
    public bool CanDisconnect => SelectedDevice?.IsConnected == true || IsConnected;
    public bool CanForget => SelectedDevice?.IsBonded == true;

    private string NameFor(string address) =>
        Devices.FirstOrDefault(d => d.Address == address)?.Name ?? address;

    // ───────────────────── Команды ─────────────────────

    /// <summary>Кнопка включения Bluetooth из приложения (п. 4.1.1 ТЗ).</summary>
    [RelayCommand]
    private async Task EnableBluetoothAsync()
    {
        if (!_bt.IsAvailable)
        {
            StatusMessage = "Bluetooth-адаптер не обнаружен.";
            _log.Error("Bluetooth-адаптер не обнаружен на устройстве.");
            return;
        }

        StatusMessage = "Запрос включения Bluetooth…";
        var ok = await _bt.EnableAsync();
        IsBluetoothEnabled = ok || _bt.IsEnabled;
        StatusMessage = IsBluetoothEnabled ? "Bluetooth включён" : "Bluetooth не включён";
        _log.Info(IsBluetoothEnabled ? "Bluetooth включён." : "Bluetooth остался выключен.");
    }

    /// <summary>«Обновить» / «Сканировать» — поиск всех доступных устройств (п. 4.1.2 ТЗ).</summary>
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (!await EnsurePermissionsAsync()) return;

        StopScanning();

        // Сначала добавляем уже сопряжённые, затем запускаем discovery.
        var bonded = await _bt.GetBondedDevicesAsync();
        foreach (var d in bonded)
            UpsertDevice(d);

        _bt.StartDiscovery();
        IsScanning = true;
        StatusMessage = "Сканирование…";
        _log.Info("Начато сканирование Bluetooth-устройств.");

        // Автоостановка через 12 секунд (цикл discovery в Android ~12 с).
        await Task.Delay(12000);
        if (IsScanning)
            StopScanning();
    }

    private void StopScanning()
    {
        _bt.StopDiscovery();
        IsScanning = false;
        if (StatusMessage == "Сканирование…")
            StatusMessage = Devices.Count == 0
                ? "Устройства не найдены. Убедитесь, что Bluetooth на Arduino включён."
                : "Сканирование завершено";
    }

    /// <summary>Сопряжение из приложения (п. 4.1.3 ТЗ).</summary>
    [RelayCommand(CanExecute = nameof(CanPair))]
    private async Task PairAsync()
    {
        var device = SelectedDevice;
        if (device is null) return;

        device.IsBonding = true;
        StatusMessage = $"Сопряжение с {device.Name}…";
        _log.Info($"Запрошено сопряжение с {device.DisplayName}.");

        var started = await _bt.PairAsync(device.Address);
        if (!started)
        {
            device.IsBonding = false;
            StatusMessage = "Не удалось начать сопряжение. Попробуйте ещё раз.";
            _log.Error($"Ошибка запуска сопряжения: {device.Address}");
        }
        // Результат придёт через BondStateChanged / диалог PIN.
    }

    /// <summary>Подключение по SPP (п. 4.1.4 ТЗ).</summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        var device = SelectedDevice;
        if (device is null) return;

        StatusMessage = $"Подключение к {device.Name}…";
        _log.Info($"Подключение по SPP к {device.DisplayName}…");

        // Подключение — не более 10 секунд (раздел 9 ТЗ).
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connectTask = _connection.ConnectAndDetectAsync(device.Address);
        var finished = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token));
        if (finished != connectTask)
        {
            StatusMessage = "Таймаут подключения (10 с).";
            _log.Error($"Таймаут подключения к {device.Address}.");
            return;
        }

        var type = await connectTask;
        if (_bt.ConnectedAddress == device.Address)
        {
            device.IsConnected = true;
            device.IsBonded = true;
            if (type != DeviceType.Unknown)
                device.DeviceType = type;

            StatusMessage = $"Подключено: {device.Name}";
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ConnectionInfo));
            RefreshCommands();

            // Переход на экран управления устройством.
            await Shell.Current.GoToAsync($"{RouteConstants.DevicePage}",
                new Dictionary<string, object> { ["device"] = device });
        }
        else
        {
            StatusMessage = "Не удалось подключиться. Убедитесь, что устройство включено и сопряжено.";
            _log.Error($"Не удалось подключиться к {device.Address}.");
        }
    }

    /// <summary>Отключение (п. 4.1.5 ТЗ).</summary>
    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        await _bt.DisconnectAsync();
        OnDisconnected();
        StatusMessage = "Отключено.";
    }

    /// <summary>«Забыть» — удалить сопряжение (п. 4.1.6 ТЗ).</summary>
    [RelayCommand(CanExecute = nameof(CanForget))]
    private async Task ForgetAsync()
    {
        var device = SelectedDevice;
        if (device is null) return;

        var removed = await _bt.RemoveBondAsync(device.Address);
        if (removed)
        {
            device.IsBonded = false;
            device.IsConnected = false;
            StatusMessage = $"Сопряжение с {device.Name} удалено.";
            _log.Info($"Удалено сопряжение с {device.Address}.");
            RefreshCommands();
        }
        else
        {
            // Публичный API Android запрещает программное удаление — поясняем и ведём в настройки.
            _log.Info("Программное удаление сопряжения недоступно — открыт системный экран Bluetooth.");
            bool goSettings = await Application.Current!.MainWindow!.Page!.DisplayAlert(
                "Удаление сопряжения",
                "Ограничения Android не позволяют удалить сопряжение из приложения. " +
                "Открыть системные настройки Bluetooth, где можно «забыть» это устройство?",
                "Открыть настройки", "Отмена");

            if (goSettings)
                _bt.OpenBluetoothSettings();
        }
    }

    /// <summary>Открыть журнал обмена.</summary>
    [RelayCommand]
    private async Task OpenLogAsync() =>
        await Shell.Current.GoToAsync($"{RouteConstants.LogPage}");

    /// <summary>Открыть пресеты.</summary>
    [RelayCommand]
    private async Task OpenPresetsAsync() =>
        await Shell.Current.GoToAsync($"{RouteConstants.PresetsPage}");

    // ───────────────────── Вспомогательные ─────────────────────

    private void UpsertDevice(BluetoothDeviceInfo device)
    {
        var existing = Devices.FirstOrDefault(d => d.Address == device.Address);
        if (existing is null)
        {
            Devices.Add(device);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(device.Name))
                existing.Name = device.Name;
            if (device.Rssi is int rssi)
                existing.Rssi = rssi;
            existing.IsBonded |= device.IsBonded;
        }
    }

    private void OnBondChanged(string address, BondState state)
    {
        var device = Devices.FirstOrDefault(d => d.Address == address);
        if (device is null) return;

        switch (state)
        {
            case BondState.Bonded:
                device.IsBonding = false;
                device.IsBonded = true;
                StatusMessage = $"Сопряжение с {device.Name} выполнено.";
                _log.Info($"Устройство {address} сопряжено.");
                break;
            case BondState.Bonding:
                device.IsBonding = true;
                StatusMessage = $"Сопряжение с {device.Name}…";
                break;
            case BondState.None:
                device.IsBonding = false;
                device.IsBonded = false;
                _log.Info($"Сопряжение с {address} сброшено (None).");
                break;
        }
        RefreshCommands();
    }

    /// <summary>Диалог ввода PIN при ACTION_PAIRING_REQUEST (п. 4.1.3 ТЗ).</summary>
    private async void OnPairingRequested(string address)
    {
        var device = Devices.FirstOrDefault(d => d.Address == address);
        var name = device?.Name ?? address;

        var pin = await Application.Current!.MainWindow!.Page!.DisplayPromptAsync(
            "Сопряжение",
            $"Введите PIN-код для {name}.\nОбычно для HC-05/06: 1234 или 0000.",
            "Сопряжить", "Отмена", keyboard: Keyboard.Numeric,
            initialValue: "1234", maxLength: 8);

        if (string.IsNullOrWhiteSpace(pin))
        {
            _log.Info($"Сопряжение с {address} отменено пользователем.");
            return;
        }

        var ok = await _bt.ConfirmPairingAsync(address, pin);
        _log.Info(ok
            ? $"PIN-код подтверждён для {address}."
            : $"Не удалось передать PIN для {address} — используйте системный диалог Android.");
    }

    private void OnDisconnected()
    {
        foreach (var d in Devices)
            d.IsConnected = false;
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(ConnectionInfo));
        RefreshCommands();
    }

    private void OnConnectionLost()
    {
        OnDisconnected();
        StatusMessage = "Соединение потеряно.";
        _log.Error("Потеря соединения с устройством.");
    }

    private void RefreshCommands()
    {
        PairCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        ForgetCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDeviceChanged(BluetoothDeviceInfo? value)
    {
        foreach (var d in Devices)
            d.IsSelected = ReferenceEquals(d, value);
        RefreshCommands();
    }

    /// <summary>Предложение подключиться к последнему устройству при запуске (п. 4.1.8 ТЗ).</summary>
    public async Task SuggestLastDeviceAsync()
    {
        var last = _bt.LastUsedAddress;
        if (string.IsNullOrEmpty(last)) return;

        var bonded = await _bt.GetBondedDevicesAsync();
        var device = bonded.FirstOrDefault(d => d.Address == last);
        if (device is null) return;

        UpsertDevice(device);
        SelectedDevice = device;

        bool connect = await Application.Current!.MainWindow!.Page!.DisplayAlert(
            "Последнее устройство",
            $"Подключиться к «{device.Name}» ({device.Address})?",
            "Подключить", "Отмена");

        if (connect)
            await ConnectAsync();
    }

    /// <summary>Запрос разрешений Bluetooth/Location (раздел 8.3 ТЗ).</summary>
    private async Task<bool> EnsurePermissionsAsync()
    {
        try
        {
#if ANDROID
            // Android 12+ (API 31): BLUETOOTH_SCAN / BLUETOOTH_CONNECT.
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var scan = await Permissions.RequestAsync<BluetoothScan>();
                var connect = await Permissions.RequestAsync<BluetoothConnect>();
                if (scan != PermissionStatus.Granted || connect != PermissionStatus.Granted)
                {
                    StatusMessage = "Нет разрешений Bluetooth. Разрешите доступ в настройках приложения.";
                    _log.Error("Пользователь не предоставил разрешения BLUETOOTH_SCAN/BLUETOOTH_CONNECT.");
                    return false;
                }
            }
            else
            {
                // Android 6–11: нужен ACCESS_FINE_LOCATION для сканирования.
                var loc = await Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Location>();
                if (loc != PermissionStatus.Granted)
                {
                    StatusMessage = "Для сканирования нужен доступ к геолокации (требование Android).";
                    _log.Error("Не предоставлен ACCESS_FINE_LOCATION.");
                    return false;
                }
            }
#endif
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Ошибка запроса разрешений: {ex.Message}");
            return true; // Пробуем работать дальше — сервис сам сообщит об ошибке.
        }
    }
}

#if ANDROID
/// <summary>Permission-обёртка для BLUETOOTH_SCAN (API 31+, раздел 8.3 ТЗ).</summary>
internal sealed class BluetoothScan : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [("android.permission.BLUETOOTH_SCAN", true)];
}

/// <summary>Permission-обёртка для BLUETOOTH_CONNECT (API 31+, раздел 8.3 ТЗ).</summary>
internal sealed class BluetoothConnect : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [("android.permission.BLUETOOTH_CONNECT", true)];
}
#endif
