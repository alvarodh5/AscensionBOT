using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static Bootstrapper.WinImports;

namespace Bootstrapper
{
    class Program
    {
        // Project Ascension launches its client through its own launcher (it patches the
        // client and sets the realmlist). So instead of creating the process ourselves,
        // we attach to the already-running Ascension.exe and inject into it.
        const string ProcessName = "Ascension";

        static void Main()
        {
            var currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // find the running game client
            var process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            if (process == null)
            {
                Console.WriteLine($"No encuentro el proceso '{ProcessName}.exe'.");
                Console.WriteLine("Abre Ascension con su launcher, llega a la pantalla de login/personajes y vuelve a ejecutar el Bootstrapper (como Administrador).");
                Console.ReadKey();
                return;
            }

            // get a handle to the game process
            var processHandle = process.Handle;

            // resolve the file path to Loader.dll relative to our current working directory
            var loaderPath = Path.Combine(currentFolder, "Loader.dll");

            // allocate enough memory to hold the full file path to Loader.dll within the AscensionBot process
            var loaderPathPtr = VirtualAllocEx(
                processHandle, 
                (IntPtr)0, 
                loaderPath.Length, 
                MemoryAllocationType.MEM_COMMIT, 
                MemoryProtectionType.PAGE_EXECUTE_READWRITE);

            // this seems to help prevent timing issues
            Thread.Sleep(500);

            int error = Marshal.GetLastWin32Error();
            if (error > 0)
                throw new InvalidOperationException($"Failed to allocate memory for Loader.dll, error code: {error}");

            // write the file path to Loader.dll to the EoE process's memory
            var bytes = Encoding.Unicode.GetBytes(loaderPath);
            var bytesWritten = 0; // throw away
            WriteProcessMemory(processHandle, loaderPathPtr, bytes, bytes.Length, ref bytesWritten);

            // this seems to help prevent timing issues
            Thread.Sleep(1000);

            error = Marshal.GetLastWin32Error();
            if (error > 0 || bytesWritten == 0)
                throw new InvalidOperationException($"Failed to write Loader.dll into the WoW.exe process, error code: {error}");

            // search current process's for the memory address of the LoadLibraryW function within the kernel32.dll module
            var loaderDllPointer = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");

            // this seems to help prevent timing issues
            Thread.Sleep(1000);

            error = Marshal.GetLastWin32Error();
            if (error > 0)
                throw new InvalidOperationException($"Failed to get memory address to Loader.dll in the WoW.exe process, error code: {error}");

            // create a new thread with the execution starting at the LoadLibraryW function, 
            // with the path to our Loader.dll passed as a parameter
            CreateRemoteThread(processHandle, (IntPtr)null, (IntPtr)0, loaderDllPointer, loaderPathPtr, 0, (IntPtr)null);

            // this seems to help prevent timing issues
            Thread.Sleep(1000);

            error = Marshal.GetLastWin32Error();
            if (error > 0)
                throw new InvalidOperationException($"Failed to create remote thread to start execution of Loader.dll in the WoW.exe process, error code: {error}");

            // free the memory that was allocated by VirtualAllocEx
            VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);

            // Watchdog: stay alive and alert on Telegram if the game PROCESS dies (crash / closed).
            // The in-game bot can't report this (it dies with the process), so the Bootstrapper —
            // a separate process — watches from outside.
            WatchProcess(process, currentFolder);
        }

        static void WatchProcess(Process process, string currentFolder)
        {
            bool enabled = false;
            string token = null, chatId = null;
            try
            {
                var path = Path.Combine(currentFolder, "botSettings.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    enabled = Regex.IsMatch(json, "\"TelegramEnabled\"\\s*:\\s*true", RegexOptions.IgnoreCase);
                    token = MatchString(json, "TelegramBotToken");
                    chatId = MatchString(json, "TelegramChatId");
                }
            }
            catch { }

            Console.WriteLine("Inyeccion completa. Vigilando el proceso del juego...");
            Console.WriteLine("(Deja esta ventana abierta para recibir el aviso si WoW se cierra/cae.)");

            try { process.WaitForExit(); } catch { }

            Console.WriteLine("El proceso del juego se ha cerrado.");
            if (enabled && !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(chatId))
                SendTelegram(token, chatId, "⚠️ El proceso de WoW (Ascension) se ha cerrado o caido.");

            Console.WriteLine("Pulsa una tecla para salir.");
            try { Console.ReadKey(); } catch { }
        }

        static string MatchString(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        static void SendTelegram(string token, string chatId, string message)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var url = $"https://api.telegram.org/bot{token}/sendMessage" +
                          $"?chat_id={Uri.EscapeDataString(chatId)}&text={Uri.EscapeDataString(message)}";
                using (var client = new WebClient())
                    client.DownloadString(url);
            }
            catch (Exception e) { Console.WriteLine("Telegram error: " + e.Message); }
        }
    }
}
