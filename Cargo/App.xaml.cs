namespace Cargo;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // A desktop Uno app is windowed, so redirected stdout captures nothing:
        // startup failures go to startup.log next to the executable instead of vanishing.
        UnhandledException += (_, e) => Trace("UnhandledException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Trace("AppDomain", e.ExceptionObject as Exception);

        try
        {
            await LaunchAsync(args);
        }
        catch (Exception ex)
        {
            Trace("OnLaunched", ex);
            throw;
        }
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        // The store owns the shift clock (a DispatcherTimer), so it is built here on the UI
        // thread; the router resolves page models on a background thread.
        var state = new PortState();

        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                    logBuilder
                        .SetMinimumLevel(context.HostingEnvironment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning)
                        .CoreLogLevel(LogLevel.Warning),
                    enableUnoLogging: true)
                .UseToolkitNavigation()
                .ConfigureServices((context, services) =>
                {
                    // One store for the whole shift: every page model reads and writes the same port
                    services.AddSingleton(state);

                    // Section models, handed to MainViewModel: each section page is built ahead of its first visit
                    services.AddTransient<OverviewViewModel>();
                    services.AddTransient<BerthsViewModel>();
                    services.AddTransient<CargoViewModel>();
                    services.AddTransient<FleetViewModel>();
                    services.AddTransient<SecurityViewModel>();
                })
                .UseNavigation(RegisterRoutes));

        MainWindow = builder.Window;

#if DEBUG
        // Hot Design blocks window creation when no DevServer is reachable, so headless
        // verification runs opt out with APP_NO_HOTDESIGN=1.
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
        {
            MainWindow.UseStudio();
        }
#endif
        MainWindow.SetWindowIcon();

        // The dashboard is authored for a wide desk; give it room rather than opening
        // at the platform default and clipping the harbour.
        MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1680, Height = 1020 });

        // Open straight on the section the store starts in (Overview, unless a DEBUG hook says otherwise)
        Host = await builder.NavigateAsync<Shell>(initialNavigate: (services, navigator) =>
            navigator.NavigateRouteAsync(this, $"Main/{MainViewModel.RouteFor(state)}"));
    }

    /// <summary>
    /// Main hosts the header, the harbour stage and the section region; each section is a
    /// nested route whose name is its <see cref="PortState.Section"/> id.
    /// </summary>
    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellViewModel)),
            new ViewMap<MainPage, MainViewModel>());

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellViewModel>(),
                Nested:
                [
                    new RouteMap("Main", View: views.FindByViewModel<MainViewModel>(), IsDefault: true,
                        Nested:
                        [
                            // Sections are panes declared in MainPage's Visibility region (built in the
                            // background after startup), so these routes carry no views of their own
                            new RouteMap("overview", IsDefault: true),
                            new RouteMap("berths"),
                            new RouteMap("cargo"),
                            new RouteMap("fleet"),
                            new RouteMap("security",
                                Nested:
                                [
                                    // In-page tabs: panes named in SecurityView's Visibility region, no views of their own
                                    new RouteMap("zones", IsDefault: true),
                                    new RouteMap("access"),
                                    new RouteMap("inspection")
                                ])
                        ])
                ]));
    }

    private static void Trace(string source, Exception? ex)
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {source}: {ex}\n\n");
        }
        catch
        {
            // Tracing must never be the thing that breaks startup.
        }
    }
}
