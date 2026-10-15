using MauiApp1.Models;

namespace MauiApp1.Services;

/// <summary>
/// Заглушка Bluetooth-сервиса для платформ, где Bluetooth Classic SPP
/// не поддерживается (iOS/macOS/Windows в текущей версии).
/// Бизнес-логика и UI остаются кроссплатформенными (раздел 9 ТЗ).
/// </summary>
public sealed class UnsupportedBluetoothService : IBluetoothService
{
    public event EventHandler<bool>? AdapterStateChanged;
    public event EventHandler<BluetoothDeviceInfo>? DeviceFound;
    public event EventHandler<(string Address, BondState State)>? BondStateChanged;
    public event EventHandler<string>? PairingRequestPin;
    public event EventHandler<string>? Disconnected;
    public event EventHandler<string>? DataReceived;

    public bool IsAvailable => false;
    public bool IsEnabled => false;
    public bool IsScanning => false;
    public string? ConnectedAddress => null;

    public Task<bool> EnableAsync() => Task.FromResult(false);
    public void StartDiscovery() { }
    public void StopDiscovery() { }
    public Task<IReadOnlyList<BluetoothDeviceInfo>> GetBondedDevicesAsync()
        => Task.FromResult<IReadOnlyList<BluetoothDeviceInfo>>(Array.Empty<BluetoothDeviceInfo>());
    public Task<bool> PairAsync(string address) => Task.FromResult(false);
    public Task<bool> ConfirmPairingAsync(string address, string pin) => Task.FromResult(false);
    public Task<bool> ConnectAsync(string address) => Task.FromResult(false);
    public Task DisconnectAsync() => Task.CompletedTask;
    public Task SendAsync(string command) =>
        throw new PlatformNotSupportedException("Bluetooth SPP поддерживается только на Android.");
    public Task<bool> RemoveBondAsync(string address) => Task.FromResult(false);
    public void OpenBluetoothSettings() { }

    public string? LastUsedAddress
    {
        get => Preferences.Default.Get(PrefKeys.LastDeviceAddress, (string?)null);
        set => Preferences.Default.Set(PrefKeys.LastDeviceAddress, value);
    }
}
