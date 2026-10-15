using System.Collections.Concurrent;
using MauiApp1.Models;
// Разрешаем неоднозначность с Microsoft.Maui.Devices.DeviceType.
using DeviceType = MauiApp1.Models.DeviceType;

namespace MauiApp1.Services;

/// <summary>
/// Высокоуровневый сервис соединения поверх IBluetoothService:
/// отправка команд с ожиданием подтверждения (таймаут 3 с — раздел 7 ТЗ),
/// автоопределение типа устройства, автопереподключение.
/// </summary>
public sealed class ConnectionService : IDisposable
{
    private readonly IBluetoothService _bt;
    private readonly LogService _log;

    private readonly ConcurrentQueue<string> _responses = new();
    private readonly SemaphoreSlim _responseSignal = new(0);

    public ConnectionService(IBluetoothService bt, LogService log)
    {
        _bt = bt;
        _log = log;
        _bt.DataReceived += OnDataReceived;
        _bt.Disconnected += OnDisconnected;
    }

    public IBluetoothService Bluetooth => _bt;
    public LogService Log => _log;

    /// <summary>Последнее полученное сообщение от устройства (для отображения на экране).</summary>
    public string? LastResponse { get; private set; }

    /// <summary>Определяется ли тип устройства автоматически при подключении (п. 4.4.3).</summary>
    public bool AutoDetectType { get; set; } = true;

    /// <summary>Автопереподключение при потере связи (п. 4.1.4, опционально).</summary>
    public bool AutoReconnect { get; set; } =
        Preferences.Default.Get(PrefKeys.AutoReconnect, false);

    public event EventHandler<string>? ResponseReceived;
    public event EventHandler<string>? LinkLost;

    private void OnDataReceived(object? sender, string line)
    {
        _log.Rx(line);
        LastResponse = line;

        // Если это ответ на «V» — пробуем определить тип устройства.
        var type = Protocol.DetectDeviceType(line);
        if (type != DeviceType.Unknown)
            DetectedType = type;

        _responses.Enqueue(line);
        _responseSignal.Release();
    }

    /// <summary>Тип устройства, определённый из ответов прошивки.</summary>
    public DeviceType DetectedType { get; private set; } = DeviceType.Unknown;

    private void OnDisconnected(object? sender, string address)
    {
        _log.Info($"Соединение с {address} закрыто.");
        LinkLost?.Invoke(this, address);

        if (AutoReconnect && address == _bt.LastUsedAddress)
        {
            _ = TryReconnectAsync(address);
        }
    }

    private int _reconnectAttempts;

    private async Task TryReconnectAsync(string address)
    {
        while (_reconnectAttempts < 3 && _bt.ConnectedAddress is null)
        {
            _reconnectAttempts++;
            await Task.Delay(TimeSpan.FromSeconds(2));
            if (_bt.ConnectedAddress is not null) break;

            _log.Info($"Попытка переподключения {_reconnectAttempts}/3…");
            if (await _bt.ConnectAsync(address))
            {
                _log.Info("Переподключение выполнено.");
                _reconnectAttempts = 0;
                return;
            }
        }
        _reconnectAttempts = 0;
    }

    /// <summary>
    /// Отправляет команду и ждёт ответ устройства с таймаутом 3 секунды (раздел 7 ТЗ).
    /// Возвращает текст ответа или null по таймауту.
    /// </summary>
    public async Task<string?> SendAndWaitAsync(string command, CancellationToken ct = default)
    {
        await _bt.SendAsync(command);
        _log.Tx(command);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Protocol.ResponseTimeoutSeconds));

        try
        {
            await _responseSignal.WaitAsync(timeoutCts.Token);
            if (_responses.TryDequeue(out var response))
                return response;
            return LastResponse;
        }
        catch (OperationCanceledException)
        {
            _log.Error($"Нет ответа от устройства (таймаут {Protocol.ResponseTimeoutSeconds} с): {Protocol.Clean(command)}");
            return null;
        }
    }

    /// <summary>Подключение + автоопределение типа устройства (запрос 'V').</summary>
    public async Task<DeviceType> ConnectAndDetectAsync(string address)
    {
        var ok = await _bt.ConnectAsync(address);
        if (!ok) return DeviceType.Unknown;

        if (AutoDetectType)
        {
            try
            {
                var answer = await SendAndWaitAsync(Protocol.QueryVersion);
                var detected = Protocol.DetectDeviceType(answer);
                if (detected != DeviceType.Unknown)
                {
                    _log.Info($"Тип устройства определён автоматически: {detected.ToRussian()}");
                    return detected;
                }
            }
            catch { /* прошивка может не поддерживать 'V' */ }
        }
        return DetectedType;
    }

    public void Dispose()
    {
        _bt.DataReceived -= OnDataReceived;
        _bt.Disconnected -= OnDisconnected;
        _responseSignal.Dispose();
    }
}
