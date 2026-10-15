using Microsoft.Extensions.DependencyInjection;
using MauiApp1.ViewModels;

namespace MauiApp1.Views;

public partial class PresetsPage : ContentPage
{
    public PresetsPage()
    {
        InitializeComponent();
        BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<PresetsViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is PresetsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
