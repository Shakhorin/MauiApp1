using Microsoft.Extensions.DependencyInjection;
using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        // ViewModel внедряется через DI (MauiProgram).
        BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<MainViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // П. 4.1.8 ТЗ: предложить подключиться к последнему устройству при запуске.
        if (BindingContext is MainViewModel vm)
            await vm.SuggestLastDeviceAsync();
    }
}
