using System.Runtime.Versioning;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using MauiApp1.Models;
using AndroidBluetooth = Android.Bluetooth;

namespace MauiApp1.Services;

/// <summary>
/// Набор BroadcastReceiver для работы с Bluetooth Classic (раздел 8.2 ТЗ):
/// ACTION_STATE_CHANGED, ACTION_FOUND, ACTION_BOND_STATE_CHANGED,
/// ACTION_PAIRING_REQUEST, ACTION_REQUEST_ENABLE.
/// Приёмники регистрируются динамически из MainActivity (чтобы не теряться при перезапуске).
/// </summary>
[SupportedOSPlatform("android")]
internal static class BtReceivers
{
    public static event Action<bool>? StateChanged;
    public static event Action<string, string, int?>? DeviceFound;           // name, address, rssi
    public static event Action<string, BondState>? BondStateChanged;          // address, state
    public static event Action<string>? PairingRequest;                       // address
    public static event Action<bool>? AdapterEnableResult;                    // OK?

    private static IRegisteredReceiver? _registrationToken;

    /// <summary>Регистрация всех приёмников (вызывается из MainActivity).</summary>
    public static void Register(Activity activity)
    {
        if (_registrationToken is not null) return;

        var receiver = new InternalReceiver();
        var filter = new IntentFilter();
        filter.AddAction(BluetoothAdapter.ActionStateChanged);
        filter.AddAction(BluetoothDevice.ActionFound);
        filter.AddAction(BluetoothDevice.ActionNameChanged);
        filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);
        filter.AddAction(BluetoothDevice.ActionBondStateChanged);
        filter.AddAction(BluetoothDevice.ActionPairingRequest);

        try
        {
            // Начиная с Android 13 (API 33) broadcast-приёмники обязаны указывать флаг экспорта.
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                activity.RegisterReceiver(receiver, filter, (int)global::Android.Content.ReceiveFlags.NotExported);
            else
                activity.RegisterReceiver(receiver, filter);
            _registrationToken = receiver;
        }
        catch
        {
            _registrationToken = null;
        }
    }

    internal static void OnStateChanged(bool enabled) => StateChanged?.Invoke(enabled);
    internal static void OnDeviceFound(string name, string address, int? rssi) => DeviceFound?.Invoke(name, address, rssi);
    internal static void OnBondStateChanged(string address, BondState state) => BondStateChanged?.Invoke(address, state);
    internal static void OnPairingRequest(string address) => PairingRequest?.Invoke(address);
    internal static void OnEnableResult(bool ok) => AdapterEnableResult?.Invoke(ok);

    /// <summary>Внутренний агрегирующий BroadcastReceiver.</summary>
    private sealed class InternalReceiver : BroadcastReceiver, IRegisteredReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action is null) return;

            switch (intent.Action)
            {
                case BluetoothAdapter.ActionStateChanged:
                {
                    var state = (BluetoothState)intent.GetIntExtra(BluetoothAdapter.ExtraState, -1)!;
                    OnStateChanged(state == BluetoothState.On);
                    break;
                }

                case BluetoothDevice.ActionFound:
                case BluetoothDevice.ActionNameChanged:
                {
                    if (intent.GetParcelableExtra(BluetoothDevice.ExtraDevice) is not BluetoothDevice device)
                        return;
                    var name = device.Name ?? intent.GetStringExtra(BluetoothDevice.ExtraName) ?? "";
                    int? rssi = intent.HasExtra(BluetoothDevice.ExtraRssi)
                        ? intent.GetIntExtra(BluetoothDevice.ExtraRssi, -127)
                        : null;
                    OnDeviceFound(name, device.Address ?? "", rssi);
                    break;
                }

                case BluetoothDevice.ActionBondStateChanged:
                {
                    if (intent.GetParcelableExtra(BluetoothDevice.ExtraDevice) is not BluetoothDevice device)
                        return;
                    var bondState = (AndroidBluetooth.Bond)intent.GetIntExtra(
                        BluetoothDevice.ExtraBondState, (int)AndroidBluetooth.Bond.None)!;
                    var state = bondState switch
                    {
                        AndroidBluetooth.Bond.Bonded => BondState.Bonded,
                        AndroidBluetooth.Bond.Bonding => BondState.Bonding,
                        _ => BondState.None
                    };
                    OnBondStateChanged(device.Address ?? "", state);
                    break;
                }

                case BluetoothDevice.ActionPairingRequest:
                {
                    var address = intent.GetStringExtra(BluetoothDevice.ExtraDevice) ?? "";
                    if (!string.IsNullOrEmpty(address))
                        OnPairingRequest(address);
                    break;
                }
            }
        }
    }

    /// <summary>Интерфейс «токена» регистрации (для хранения ссылки на приёмник).</summary>
    internal interface IRegisteredReceiver;
}
