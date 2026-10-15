namespace MauiApp1;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Маршруты для навигации через Shell.GoToAsync (Views/RouteConstants.cs).
        RoutingRouteRegisterer.RegisterRoutes(this);
    }
}

/// <summary>Регистрация маршрутов Shell (п. 5.2–5.5 ТЗ: экраны устройства, пресетов, журнала).</summary>
internal static class RoutingRouteRegisterer
{
    public static void RegisterRoutes(Shell shell)
    {
        Routing.RegisterRoute(Views.RouteConstants.DevicePage, typeof(Views.DevicePage));
        Routing.RegisterRoute(Views.RouteConstants.PresetsPage, typeof(Views.PresetsPage));
        Routing.RegisterRoute(Views.RouteConstants.LogPage, typeof(Views.LogPage));
    }
}