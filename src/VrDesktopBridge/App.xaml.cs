using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace VrDesktopBridge;

/// <summary>
/// Interaction logic for App.xaml. Routes unhandled UI-thread exceptions
/// to stderr (captured by tools/run_app.py into .tmp/run_*.log) and shows
/// a dialog instead of a silent crash.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
    }

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfoW(uint a, uint b, IntPtr c, uint d);

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Make absolutely sure a crash never leaves the OS cursor hidden.
        SystemParametersInfoW(0x0057 /*SPI_SETCURSORS*/, 0, IntPtr.Zero, 0);
        Console.Error.WriteLine("[FATAL] " + e.Exception);
        MessageBox.Show(e.Exception.ToString(), "VrDesktopBridge — unhandled",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }
}
