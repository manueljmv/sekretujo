using Avalonia;
using System;
using Sekretujo.App.Services;

namespace Sekretujo.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        ProcessHardening.Apply();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // xdg-desktop-portal refuses a caller it cannot inspect, and ProcessHardening makes /proc/<pid>
            // unreadable, so the portal file picker always fails. The GTK dialog runs in-process instead.
            .With(new X11PlatformOptions { UseDBusFilePicker = false })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
