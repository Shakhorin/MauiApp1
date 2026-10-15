using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiApp1.Models;

/// <summary>
/// Платформо-независимое описание Bluetooth-устройства, отображаемое в списке на главном экране.
/// </summary>
public partial class BluetoothDeviceInfo : ObservableObject
{
    public BluetoothDeviceInfo(string address, string name)
    {
        Address = address;
        Name = name;
    }

    /// <summary>MAC-адрес устройства.</summary>
    public string Address { get; }

    /// <summary>Отображаемое имя (если не задано — MAC-адрес).</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? Address : $"{Name} ({Address})";

    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>Уровень сигнала RSSI, дБм (null — если недоступен).</summary>
    [ObservableProperty]
    private int? rssi;

    /// <summary>Индикатор уровня сигнала: 0..4 деления.</summary>
    public int SignalBars => Rssi switch
    {
        null => 0,
        >= -50 => 4,
        >= -60 => 3,
        >= -70 => 2,
        >= -80 => 1,
        _ => 0
    };

    partial void OnRssiChanged(int? value) => OnPropertyChanged(nameof(SignalBars));

    /// <summary>Сопряжено ли устройство с телефоном.</summary>
    [ObservableProperty]
    private bool isBonded;

    /// <summary>Тип устройства (точка захвата / аптечка).</summary>
    [ObservableProperty]
    private DeviceType deviceType = DeviceType.Unknown;

    /// <summary>Выполняется ли сопряжение в данный момент.</summary>
    [ObservableProperty]
    private bool isBonding;

    /// <summary>Является ли устройство текущим подключённым (SPP).</summary>
    [ObservableProperty]
    private bool isConnected;

    /// <summary>Индикатор выбранного элемента списка.</summary>
    [ObservableProperty]
    private bool isSelected;

    partial void OnIsBondedChanged(bool value) => OnPropertyChanged(nameof(StateText));
    partial void OnIsBondingChanged(bool value) => OnPropertyChanged(nameof(StateText));
    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(StateText));
    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));

    /// <summary>Текст состояния для списка: «Не сопряжено» / «Сопряжено» / «Подключено».</summary>
    public string StateText => IsConnected
        ? BondState.Connected.ToRussian()
        : IsBonding
            ? BondState.Bonding.ToRussian()
            : IsBonded
                ? BondState.Bonded.ToRussian()
                : BondState.None.ToRussian();

    public override string ToString() => DisplayName;
}
