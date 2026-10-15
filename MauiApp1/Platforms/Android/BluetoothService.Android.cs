using System.Runtime.Versioning;
using Java.Lang;
using Java.Lang.Reflect;
using MauiApp1.Models;
using AndroidBluetooth = Android.Bluetooth;

namespace MauiApp1.Services;

/// <summary>
/// Реализация IBluetoothService для Android (раздел 8.2 ТЗ):
/// BluetoothAdapter, BluetoothDevice, BluetoothSocket (RFCOMM/SPP), BroadcastReceiver.
/// </summary>
[SupportedOSPlatform("android")]
public sealed partial class BluetoothService : IBluetoothService, IDisposable
{
    // UUID по умолчанию для SPP-сервера на Arduino (HC-05/06, SerialPortProfile).
    private static readonly Java.Util.UUID SppUuid =
        Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

    private readonly object _socketLock = new();
    private AndroidBluetooth.BluetoothSocket? _socket;
    private System.Threading.Thread? _rxThread;
    private volatile bool _rxRunning;

    public event EventHandler<bool>? AdapterStateChanged;
    public event EventHandler<BluetoothDeviceInfo>? DeviceFound;
    public event EventHandler<(string Address, BondState State)>? BondStateChanged;
    public event EventHandler<string>? PairingRequestPin;
    public event EventHandler<string>? Disconnected;
    public event EventHandler<string>? DataReceived;

    private AndroidBluetooth.BluetoothAdapter? Adapter => AndroidBluetooth.BluetoothAdapter.DefaultAdapter;

    public bool IsAvailable => Adapter is not null;
    public bool IsEnabled => Adapter?.IsEnabled ?? false;
    public bool IsScanning { get; private set; }
    public string? ConnectedAddress { get; private set; }

    public BluetoothService()
    {
        BtReceivers.StateChanged += enabled => AdapterStateChanged?.Invoke(this, enabled);
        BtReceivers.DeviceFound += (name, address, rssi) =>
            DeviceFound?.Invoke(this, new BluetoothDeviceInfo(address, name) { Rssi = rssi, IsBonded = IsBondedLocal(address) });
        BtReceivers.BondStateChanged += (address, state) =>
            BondStateChanged?.Invoke(this, (address, state));
        BtReceivers.PairingRequest += address => PairingRequestPin?.Invoke(this, address);
    }

    private static bool IsBondedLocal(string address)
    {
        var adapter = AndroidBluetooth.BluetoothAdapter.DefaultAdapter;
        if (adapter is null) return false;
        try
        {
            var device = adapter.GetRemoteDevice(address);
            return device is not null && device.BondState == (int)AndroidBluetooth.Bond.Bonded;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EnableAsync()
    {
        if (Adapter is null) return false;
        if (Adapter.IsEnabled) return true;

        var tcs = new TaskCompletionSource<bool>();
        BtReceivers.AdapterEnableResult += OnResult;
        try
        {
            var intent = new Android.Content.Intent(AndroidBluetooth.BluetoothAdapter.ActionRequestEnable);
            var activity = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Context
                           as Android.App.Activity
                           ?? Platform.CurrentActivity;
            if (activity is null)
            {
                // Fallback: системный экран настроек Bluetooth.
                OpenBluetoothSettings();
                return false;
            }
            activity.StartActivity(intent);

            // Если пользователь быстро включил BT через другой путь — проверим состояние.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            cts.Token.Register(() => tcs.TrySetResult(false));
            return await tcs.Task;
        }
        finally
        {
            BtReceivers.AdapterEnableResult -= OnResult;
        }

        void OnResult(bool ok) => tcs.TrySetResult(ok || (Adapter?.IsEnabled ?? false));
    }

    public void StartDiscovery()
    {
        var adapter = Adapter;
        if (adapter is null || !adapter.IsEnabled) return;
        try
        {
            adapter.StartDiscovery();
            IsScanning = true;
        }
        catch (SecurityException)
        {
            // Нет разрешения BLUETOOTH_SCAN — ViewModel запросит его отдельно.
        }
    }

    public void StopDiscovery()
    {
        try { Adapter?.CancelDiscovery(); } catch { /* ignore */ }
        IsScanning = false;
    }

    public Task<IReadOnlyList<BluetoothDeviceInfo>> GetBondedDevicesAsync()
    {
        var adapter = Adapter;
        IReadOnlyList<BluetoothDeviceInfo> list = Array.Empty<BluetoothDeviceInfo>();
        if (adapter?.BondedDevices is { } devices)
        {
            var result = new List<BluetoothDeviceInfo>();
            foreach (var d in devices)
            {
                result.Add(new BluetoothDeviceInfo(d.Address ?? "", d.Name ?? d.Address ?? "")
                {
                    IsBonded = true
                });
            }
            list = result;
        }
        return Task.FromResult(list);
    }

    public Task<bool> PairAsync(string address)
    {
        var device = GetDevice(address);
        if (device is null) return Task.FromResult(false);
        if (device.BondState == (int)AndroidBluetooth.Bond.Bonded) return Task.FromResult(true);
        try
        {
            return Task.FromResult(device.CreateBond());
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> ConfirmPairingAsync(string address, string pin)
    {
        var device = GetDevice(address);
        if (device is null) return Task.FromResult(false);
        try
        {
            // setPin — скрытый API: вводит PIN от имени приложения,
            // позволяя сопрягиваться без системного диалога.
            bool ok = device.SetPin(System.Text.Encoding.ASCII.GetBytes(pin), 0);
            return Task.FromResult(ok);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> ConnectAsync(string address)
    {
        var device = GetDevice(address);
        if (device is null) return Task.FromResult(false);

        return Task.Run(() =>
        {
            try
            {
                DisconnectCore();

                AndroidBluetooth.BluetoothSocket socket;
                try
                {
                    socket = device.CreateRfcommSocketToServiceRecord(SppUuid);
                }
                catch
                {
                    // Fallback для некоторых модулей (HC-05 в режиме slave).
                    socket = CreateInsecureFallbackSocket(device);
                }

                // Сканирование мешает подключению.
                StopDiscovery();

                socket.Connect();

                lock (_socketLock)
                {
                    _socket = socket;
                    ConnectedAddress = address;
                }
                StartRxLoop(socket);
                LastUsedAddress = address;
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    private static AndroidBluetooth.BluetoothSocket CreateInsecureFallbackSocket(AndroidBluetooth.BluetoothDevice device)
    {
        // Рефлексивный вызов createRfcommSocket(int channel) — классический обход
        // для модулей, не поддерживающих безопасный RFCOMM по UUID.
        try
        {
            Method? method = device.ClassMethods.FirstOrDefault(
                m => m.Name?.Equals("createRfcommSocket") == true && m.ParameterTypes.Length == 1);
            if (method is not null)
            {
                var result = method.Invoke(device, Integer.ValueOf(1));
                if (result is AndroidBluetooth.BluetoothSocket s)
                    return s;
            }
        }
        catch { /* ignore */ }

        return device.CreateInsecureRfcommSocketToServiceRecord(SppUuid);
    }

    private void StartRxLoop(AndroidBluetooth.BluetoothSocket socket)
    {
        _rxRunning = true;
        _rxThread = new System.Threading.Thread(() =>
        {
            var buffer = new byte[256];
            var lineBuilder = new System.Text.StringBuilder();
            try
            {
                var stream = socket.InputStream;
                while (_rxRunning)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    for (int i = 0; i < read; i++)
                    {
                        char c = (char)buffer[i];
                        if (c == '\n')
                        {
                            var line = lineBuilder.ToString().Trim();
                            lineBuilder.Clear();
                            if (line.Length > 0)
                                DataReceived?.Invoke(this, line);
                        }
                        else if (c != '\r')
                        {
                            lineBuilder.Append(c);
                        }
                    }
                }
            }
            catch
            {
                // Сокет закрыт или связь потеряна.
            }
            finally
            {
                var addr = ConnectedAddress;
                DisconnectCore();
                if (addr is not null)
                    Disconnected?.Invoke(this, addr);
            }
        })
        {
            IsBackground = true,
            Name = "BtRxLoop"
        };
        _rxThread.Start();
    }

    public Task DisconnectAsync()
    {
        return Task.Run(() =>
        {
            var addr = ConnectedAddress;
            DisconnectCore();
            if (addr is not null)
                Disconnected?.Invoke(this, addr);
        });
    }

    private void DisconnectCore()
    {
        _rxRunning = false;
        lock (_socketLock)
        {
            try { _socket?.Close(); } catch { /* ignore */ }
            _socket = null;
            ConnectedAddress = null;
        }
        try { _rxThread?.Join(500); } catch { /* ignore */ }
        _rxThread = null;
    }

    public async Task SendAsync(string command)
    {
        AndroidBluetooth.BluetoothSocket? socket;
        lock (_socketLock) socket = _socket;
        if (socket is null)
            throw new InvalidOperationException("Устройство не подключено.");

        var data = System.Text.Encoding.ASCII.GetBytes(command);
        var stream = socket.OutputStream;
        await stream.WriteAsync(data);
        await stream.FlushAsync();
    }

    public Task<bool> RemoveBondAsync(string address)
    {
        // removeBond() скрыт из публичного API Android — используем рефлексию.
        // На новых версиях Android может быть запрещён → вернём false,
        // и приложение предложит системный экран (п. 4.1 ТЗ «Исключение»).
        var device = GetDevice(address);
        if (device is null) return Task.FromResult(false);
        try
        {
            Method? method = device.ClassMethods.FirstOrDefault(m => m.Name?.Equals("removeBond") == true);
            if (method is null) return Task.FromResult(false);
            var result = method.Invoke(device) as Boolean;
            return Task.FromResult(result?.BooleanValue() ?? false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public void OpenBluetoothSettings()
    {
        try
        {
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionBluetoothSettings);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            Platform.AppContext.StartActivity(intent);
        }
        catch { /* ignore */ }
    }

    private AndroidBluetooth.BluetoothDevice? GetDevice(string address)
    {
        try { return Adapter?.GetRemoteDevice(address); }
        catch { return null; }
    }

    public string? LastUsedAddress
    {
        get => Preferences.Default.Get(PrefKeys.LastDeviceAddress, (string?)null);
        set => Preferences.Default.Set(PrefKeys.LastDeviceAddress, value);
    }

    public void Dispose() => DisconnectCore();
}
