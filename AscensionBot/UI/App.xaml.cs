using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace AscensionBot.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
#if DEBUG
            Debugger.Launch();
#endif

            // When injected into the game process, the AppDomain base directory becomes
            // the game folder, so System.Data.SQLite probes <gameDir>\x86\ for its native
            // interop and fails (loading a wrong-arch dll -> BadImageFormatException).
            // Point it explicitly at OUR folder so it finds Bot\x86\SQLite.Interop.dll.
            // Must run before the first SQLiteConnection is created.
            var botDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Environment.SetEnvironmentVariable("PreLoadSQLite_BaseDirectory", botDir);

            // NOTE: Disabled for Project Ascension. Ascension does not use Blizzard's
            // Warden, so these hooks target the wrong addresses (useless) and may crash
            // the client or trip Ascension's own anti-cheat. Re-enable only on a stock
            // Blizzard/emulator client that actually runs Warden.
            // WardenDisabler.Initialize();

            var mainWindow = new MainWindow();
            Current.MainWindow = mainWindow;
            mainWindow.Closed += (sender, args) => { Environment.Exit(0); };
            mainWindow.Show();

            base.OnStartup(e);
        }
    }
}
