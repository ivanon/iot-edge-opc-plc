using Avalonia;
using System;
using System.Linq;

namespace OpcPlc.Gui;

class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--cli"))
        {
            OpcPlc.Program.Main(args);
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
