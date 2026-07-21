using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AscensionBot
{
    static public class ThreadSynchronizer
    {
        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        static extern int CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern int SendMessage(
            int hWnd,
            uint Msg,
            int wParam,
            int lParam
        );

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        delegate int WindowProc(IntPtr hWnd, int Msg, int wParam, int lParam);

        static readonly Queue<Action> actionQueue = new Queue<Action>();
        static readonly Queue<Delegate> delegateQueue = new Queue<Delegate>();
        static readonly Queue<object> returnValueQueue = new Queue<object>();

        const int GWL_WNDPROC = -4;
        const int WM_USER = 0x0400;
        static IntPtr oldCallback;
        static WindowProc newCallback;
        static int windowHandle;

        static string matchedTitle = "";

        static ThreadSynchronizer()
        {
            EnumWindows(FindWindowProc, IntPtr.Zero);

            // Fallback: if we didn't match the game window by class, dump every process
            // window (so we can see the real class name) and use the process main window.
            if (windowHandle == 0)
            {
                Console.WriteLine("[ThreadSync] No GxWindow matched. Dumping all visible process windows:");
                EnumWindows(LogWindowProc, IntPtr.Zero);
                windowHandle = (int)Process.GetCurrentProcess().MainWindowHandle;
                matchedTitle = "(fallback MainWindowHandle)";
            }

            var winThreadId = GetWindowThreadProcessId((IntPtr)windowHandle, out int _pid);
            Console.WriteLine($"[ThreadSync] window=0x{windowHandle:X} title='{matchedTitle}' winThread={winThreadId} Threads[0]={Process.GetCurrentProcess().Threads[0].Id}");

            newCallback = WndProc;
            oldCallback = SetWindowLong((IntPtr)windowHandle, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(newCallback));
            Console.WriteLine($"[ThreadSync] SetWindowLong old=0x{oldCallback.ToInt64():X} lastError={Marshal.GetLastWin32Error()}");
        }

        static public void RunOnMainThread(Action action)
        {
            if (GetCurrentThreadId() == Process.GetCurrentProcess().Threads[0].Id)
            {
                action();
                return;
            }
            actionQueue.Enqueue(action);
            SendUserMessage();
        }

        static public T RunOnMainThread<T>(Func<T> function)
        {
            if (GetCurrentThreadId() == Process.GetCurrentProcess().Threads[0].Id)
                return function();

            delegateQueue.Enqueue(function);
            SendUserMessage();
            return (T)returnValueQueue.Dequeue();
        }

        static int WndProc(IntPtr hWnd, int msg, int wParam, int lParam)
        {
            try
            {
                if (msg != WM_USER) return CallWindowProc(oldCallback, hWnd, msg, wParam, lParam);

                while (actionQueue.Count > 0)
                    actionQueue.Dequeue()?.Invoke();
                while (delegateQueue.Count > 0)
                {
                    var invokeTarget = delegateQueue.Dequeue();
                    returnValueQueue.Enqueue(invokeTarget?.DynamicInvoke());
                }
                return 0;
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
            
            return CallWindowProc(oldCallback, hWnd, msg, wParam, lParam);
        }

        static bool FindWindowProc(IntPtr hWnd, IntPtr lParam)
        {
            GetWindowThreadProcessId(hWnd, out int procId);
            if (procId != Process.GetCurrentProcess().Id) return true;
            if (!IsWindowVisible(hWnd)) return true;

            // Match the game's 3D window by WINDOW CLASS, not title. WoW (and Ascension,
            // same engine) uses class "GxWindowClass". Matching by title is unreliable:
            // the AllocConsole window's title is the exe path and can contain "Ascension".
            var classBuilder = new StringBuilder(256);
            GetClassName(hWnd, classBuilder, classBuilder.Capacity);
            var className = classBuilder.ToString();

            var l = GetWindowTextLength(hWnd);
            var titleBuilder = new StringBuilder(l + 1);
            if (l > 0) GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            var title = titleBuilder.ToString();

            if (className.IndexOf("GxWindow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                windowHandle = (int)hWnd;
                matchedTitle = $"{title} [class={className}]";
            }
            return true;
        }

        static bool LogWindowProc(IntPtr hWnd, IntPtr lParam)
        {
            GetWindowThreadProcessId(hWnd, out int procId);
            if (procId != Process.GetCurrentProcess().Id) return true;
            if (!IsWindowVisible(hWnd)) return true;

            var classBuilder = new StringBuilder(256);
            GetClassName(hWnd, classBuilder, classBuilder.Capacity);

            var l = GetWindowTextLength(hWnd);
            var titleBuilder = new StringBuilder(l + 1);
            if (l > 0) GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);

            Console.WriteLine($"    hwnd=0x{(int)hWnd:X} class='{classBuilder}' title='{titleBuilder}'");
            return true;
        }

        static void SendUserMessage() => SendMessage(windowHandle, WM_USER, 0, 0);
    }
}
