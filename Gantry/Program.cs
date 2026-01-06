using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Serilog;
using System;
using System.IO;

namespace Gantry;

static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet, and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        string logFilePath;
        if (OperatingSystem.IsLinux() && Directory.Exists("/var/log/gantry"))
        {
            logFilePath = "/var/log/gantry/log.txt";
        }
        else
        {
            DirectoryInfo logsDir = Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));
            logFilePath = Path.Combine(logsDir.FullName, "log.txt");
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Async(a =>
                a.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31))
            .CreateLogger();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }

        Log.CloseAndFlush();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

static class AppExtensions
{
    public static Window? GetMainWindow(this Application application)
    {
        return (application.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}