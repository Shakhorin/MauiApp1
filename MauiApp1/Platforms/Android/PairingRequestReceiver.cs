using Android.App;
using Android.Bluetooth;
using Android.Content;

namespace MauiApp1.Services;

/// <summary>
/// Приёмник ACTION_PAIRING_REQUEST: даёт приложению возможность показать
/// собственный диалог ввода PIN (п. 4.1.3 ТЗ) и подтвердить сопряжение программно,
/// без перехода в системные настройки Android.
/// </summary>
[SupportedOSPlatform("android")]
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([BluetoothDevice.ActionPairingRequest])]
public sealed class PairingRequestReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != BluetoothDevice.ActionPairingRequest) return;

        // Сохраняем интент запроса — через него приложение может ответить системе
        // (confirmPairing), когда пользователь введёт PIN в нашем диалоге.
        PendingPairingIntent = intent;

        var address = intent.GetStringExtra(BluetoothDevice.ExtraDevice);
        if (string.IsNullOrEmpty(address)) return;

        // Уведомляем ViewModel главного экрана — она покажет диалог ввода PIN.
        BtReceivers.OnPairingRequest(address);
    }

    /// <summary>Последний полученный интент ACTION_PAIRING_REQUEST.</summary>
    public static Intent? PendingPairingIntent { get; private set; }
}
