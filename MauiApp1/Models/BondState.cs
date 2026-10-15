namespace MauiApp1.Models;

/// <summary>Состояние Bluetooth-устройства для индикации в списке (п. 4.1.7 ТЗ).</summary>
public enum BondState
{
    /// <summary>«Не сопряжено».</summary>
    None,

    /// <summary>Сопряжение выполняется.</summary>
    Bonding,

    /// <summary>«Сопряжено».</summary>
    Bonded,

    /// <summary>«Подключено» (SPP-сокет открыт).</summary>
    Connected
}

public static class BondStateExtensions
{
    public static string ToRussian(this BondState state) => state switch
    {
        BondState.None => "Не сопряжено",
        BondState.Bonding => "Сопряжение…",
        BondState.Bonded => "Сопряжено",
        BondState.Connected => "Подключено",
        _ => "—"
    };
}
