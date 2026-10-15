using Microsoft.Extensions.DependencyInjection;
using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class DevicePage : ContentPage
{
    public DevicePage()
    {
        InitializeComponent();
        BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<DeviceViewModel>();
    }
}
