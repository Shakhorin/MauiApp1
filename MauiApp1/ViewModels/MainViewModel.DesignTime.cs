using CommunityToolkit.Mvvm.ComponentModel;
using MauiApp1.Services;

namespace MauiApp1.ViewModels;

public partial class MainViewModel : ObservableObject
{
    /// <summary>Параметризованный конструктор для DI (см. MauiProgram).</summary>
    public MainViewModel(ConnectionService connection, LogService log)
    {
        _connection = connection;
        _bt = connection.Bluetooth;
        _log = log;

        _bt.AdapterStateChanged += (_, enabled) =>
            MainThread.BeginInvokeOnMainThread(() => IsBluetoothEnabled = enabled);
        _bt.DeviceFound += (_, device) => MainThread.BeginInvokeOnMainThread(() => UpsertDevice(device));
        _bt.BondStateChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() => OnBondChanged(e.Address, e.State));
        // Событие PairingRequestPin — EventHandler<string>, поэтому два параметра (sender, address).
        _bt.PairingRequestPin += (_, address) => MainThread.BeginInvokeOnMainThread(() => OnPairingRequested(address));
        _bt.Disconnected += (_, _) => MainThread.BeginInvokeOnMainThread(OnDisconnected);

        _connection.LinkLost += (_, _) => MainThread.BeginInvokeOnMainThread(OnConnectionLost);
    }

    /// <summary>
    /// Конструктор для design-time / DataTemplate в XAML: использует singleton-сервисы,
    /// зарегистрированные в MauiProgram (общий экземпляр с DI-конструктором).
    /// </summary>
    public MainViewModel()
        : this(
            IPlatformApplication.Current!.Services.GetRequiredService<ConnectionService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<LogService>())
    {
    }
}
