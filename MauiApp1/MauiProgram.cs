using MauiApp1.Services;
using MauiApp1.ViewModels;
using MauiApp1.Views;
using Microsoft.Extensions.Logging;

namespace MauiApp1;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ─── Сервисы (синглтоны) ───
        builder.Services.AddSingleton<LogService>();
        builder.Services.AddSingleton<PresetService>();
#if ANDROID
        builder.Services.AddSingleton<IBluetoothService, BluetoothService>();
#else
        // Bluetooth Classic (SPP) поддерживается только на Android (раздел 8.2 ТЗ).
        builder.Services.AddSingleton<IBluetoothService, UnsupportedBluetoothService>();
#endif
        builder.Services.AddSingleton<ConnectionService>();

        // ─── ViewModels (MVVM, раздел 8.1 ТЗ) ───
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<DeviceViewModel>();
        builder.Services.AddSingleton<PresetsViewModel>();
        builder.Services.AddSingleton<LogViewModel>();

        // ─── Страницы ───
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<DevicePage>();
        builder.Services.AddSingleton<PresetsPage>();
        builder.Services.AddSingleton<LogPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}