using MauiApp1.Models;

namespace MauiApp1.Services;

/// <summary>
/// Абстракция Bluetooth-сервиса (раздел 8.2 ТЗ).
/// Общая часть проекта не знает о платформе; реализация — в Platforms/Android,
/// для остальных платформ зарегистрирован заглушка-сервис.
/// </summary>
public partial interface IBluetoothService
{
    /// <summary>Доступен ли Bluetooth-адаптер на устройстве.</summary>
    bool IsAvailable { get; }

    /// <summary>Включён ли адаптер.</summary>
    bool IsEnabled { get; }

    /// <summary>Выполняется ли сейчас сканирование.</summary>
    bool IsScanning { get; }

    /// <summary>Изменение состояния адаптера (включён/выключен).</summary>
    event EventHandler<bool>? AdapterStateChanged;

    /// <summary>Найдено новое устройство / обновлены данные известного.</summary>
    event EventHandler<BluetoothDeviceInfo>? DeviceFound;

    /// <summary>Изменение состояния сопряжения (bond state changed).</summary>
    event EventHandler<(string Address, BondState State)>? BondStateChanged;

    /// <summary>Запрос PIN-кода со стороны удалённого устройства (п. 4.1.3 ТЗ).</summary>
    event EventHandler<string>? PairingRequestPin;

    /// <summary>Устройство отключилось / сокет закрыт (потеря связи).</summary>
    event EventHandler<string>? Disconnected;

    /// <summary>Получена строка данных от подключённого устройства.</summary>
    event EventHandler<string>? DataReceived;

    /// <summary>Запросить включение Bluetooth у пользователя (системный диалог).</summary>
    Task<bool> EnableAsync();

    /// <summary>Начать сканирование (все доступные устройства, п. 4.1.2 ТЗ).</summary>
    void StartDiscovery();

    /// <summary>Остановить сканирование.</summary>
    void StopDiscovery();

    /// <summary>Список ранее сопряжённых (bonded) устройств.</summary>
    Task<IReadOnlyList<BluetoothDeviceInfo>> GetBondedDevicesAsync();

    /// <summary>Инициировать сопряжение из приложения (п. 4.1.3 ТЗ).</summary>
    Task<bool> PairAsync(string address);

    /// <summary>Подтвердить/отклонить запрос сопряжения с введённым вручную PIN.</summary>
    Task<bool> ConfirmPairingAsync(string address, string pin);

    /// <summary>Открыть SPP-сокет (RFCOMM) к сопряжённому устройству (п. 4.1.4 ТЗ).</summary>
    Task<bool> ConnectAsync(string address);

    /// <summary>Отключиться: корректно закрыть сокет и освободить ресурсы (п. 4.1.5 ТЗ).</summary>
    Task DisconnectAsync();

    /// <summary>MAC-адрес текущего подключённого устройства (null — нет соединения).</summary>
    string? ConnectedAddress { get; }

    /// <summary>Отправить ASCII-команду (с завершающим '\n').</summary>
    Task SendAsync(string command);

    /// <summary>
    /// Попытка программного удаления сопряжения (п. 4.1.6 ТЗ).
    /// true — сопряжение удалено; false — API недоступен, нужен системный экран.
    /// </summary>
    Task<bool> RemoveBondAsync(string address);

    /// <summary>Открыть системный экран настроек Bluetooth (для «Забыть», если API запрещён).</summary>
    void OpenBluetoothSettings();

    /// <summary>Сохранённый MAC последнего использованного устройства (п. 4.1.8 ТЗ).</summary>
    string? LastUsedAddress { get; set; }
}
