using Uno.Resizetizer;

namespace Cargo;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // A desktop Uno app is windowed, so redirected stdout captures nothing:
        // startup failures go to startup.log next to the executable instead of vanishing.
        UnhandledException += (_, e) => Trace("UnhandledException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Trace("AppDomain", e.ExceptionObject as Exception);

        try
        {
            Launch(args);
        }
        catch (Exception ex)
        {
            Trace("OnLaunched", ex);
            throw;
        }
    }

    private void Launch(LaunchActivatedEventArgs args)
    {
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
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<PortState>();
                }));

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

        Host = builder.Build();

        var state = Host.Services.GetRequiredService<PortState>();

        MainWindow.Content = new ShellPage(state);
        MainWindow.Activate();
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
