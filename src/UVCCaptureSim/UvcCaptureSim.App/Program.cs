using System.Text;
using Avalonia;
using Avalonia.Threading;

namespace UvcCaptureSim.App;

sealed class Program
{
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UVCCaptureSim",
        "crash.log");

    [STAThread]
    public static void Main(string[] args)
    {
        InstallCrashLogging();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash("Fatal", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static void InstallCrashLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
            LogCrash("Dispatcher.UnhandledException", e.Exception);
    }

    private static void LogCrash(string source, Exception? exception)
    {
        var text = new StringBuilder()
            .AppendLine()
            .Append("===== ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" - ").Append(source).AppendLine(" =====")
            .AppendLine(exception?.ToString())
            .ToString();
        Console.Error.WriteLine(text);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, text);
        }
        catch
        {
            // stderr above is the fallback.
        }
    }
}
