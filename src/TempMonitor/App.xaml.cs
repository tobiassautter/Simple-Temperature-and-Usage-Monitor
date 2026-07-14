using System.Windows;
using Application = System.Windows.Application;

namespace TempMonitor;

public partial class App : Application
{
    private static Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(true, @"Local\TempMonitorOverlay", out bool isNew);
        if (!isNew)
        {
            // Already running - don't stack a second overlay on top.
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
