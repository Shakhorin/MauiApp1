using Microsoft.Extensions.DependencyInjection;
using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class LogPage : ContentPage
{
    public LogPage()
    {
        InitializeComponent();
        BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<LogViewModel>();
    }
}
