// -------------------------------------------------------------------------
// FILE VERSION: V0.10.23 (MACRO CORE - EXCAVATOR & 'T' TRIGGER - LPARAM FIX)
// -------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimHub.Plugins;

namespace SimRIG
{
    public static class MacroManager
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private delegate bool EnumWindowProc(IntPtr hWnd, IntPtr lParam);

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const int VK_RETURN = 0x0D;
        private const int VK_T = 0x54;

        private static IntPtr GetLParam(int scanCode, bool isKeyUp)
        {
            // bit 0-15: Repeat count = 1
            // bit 16-23: Scan code
            // bit 24: Extended key = 0
            // bit 29: Context code = 0
            // bit 30: Previous key state (1 for keyup, 0 for keydown)
            // bit 31: Transition state (1 for keyup, 0 for keydown)
            uint repeatCount = 1;
            uint sc = (uint)scanCode & 0xFF;
            uint prevKeyState = isKeyUp ? 1U : 0U;
            uint transitionState = isKeyUp ? 1U : 0U;

            uint lParam = repeatCount | (sc << 16) | (prevKeyState << 30) | (transitionState << 31);
            return (IntPtr)unchecked((int)lParam);
        }

        private static IntPtr FindBestChild(IntPtr parentHandle)
        {
            List<IntPtr> children = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(children);
            try
            {
                EnumWindowProc childProc = new EnumWindowProc(EnumWindow);
                EnumChildWindows(parentHandle, childProc, GCHandle.ToIntPtr(listHandle));
            }
            finally { if (listHandle.IsAllocated) listHandle.Free(); }

            if (children.Count == 0) return IntPtr.Zero;

            IntPtr bestCandidate = IntPtr.Zero;
            foreach (var child in children)
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(child, className, className.Capacity);
                string cls = className.ToString();
                if (cls.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0) bestCandidate = child;
            }
            if (bestCandidate == IntPtr.Zero && children.Count > 0) bestCandidate = children[0];
            return bestCandidate;
        }

        private static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            List<IntPtr> list = gch.Target as List<IntPtr>;
            if (list == null) return false;
            list.Add(handle);
            return true;
        }

        private static IntPtr GetTargetWindow()
        {
            Process proc = Process.GetProcessesByName("iRacingSim64DX11").FirstOrDefault();
            if (proc != null) return proc.MainWindowHandle;

            proc = Process.GetProcessesByName("notepad").FirstOrDefault();
            if (proc != null)
            {
                IntPtr mainHandle = proc.MainWindowHandle;
                IntPtr childHandle = FindBestChild(mainHandle);
                return (childHandle != IntPtr.Zero) ? childHandle : mainHandle;
            }
            return IntPtr.Zero;
        }

        public static void SendChatCommand(string message)
        {
            Task.Run(async () =>
            {
                try
                {
                    IntPtr hwnd = GetTargetWindow();
                    if (hwnd == IntPtr.Zero) return;

                    SetForegroundWindow(hwnd);
                    await Task.Delay(100);

                    SimHub.Logging.Current.Info($"[SimRIG] Macro '{message}' -> {hwnd}");

                    // Trigger Chat Open (T: Virtual Key = 0x54, Scan Code = 0x14)
                    PostMessage(hwnd, WM_KEYDOWN, (IntPtr)VK_T, GetLParam(0x14, false));
                    PostMessage(hwnd, WM_KEYUP, (IntPtr)VK_T, GetLParam(0x14, true));
                    await Task.Delay(100);

                    foreach (char c in message)
                    {
                        PostMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                        await Task.Delay(10);
                    }

                    // Send Enter (Enter: Virtual Key = 0x0D, Scan Code = 0x1C)
                    PostMessage(hwnd, WM_KEYDOWN, (IntPtr)VK_RETURN, GetLParam(0x1C, false));
                    PostMessage(hwnd, WM_KEYUP, (IntPtr)VK_RETURN, GetLParam(0x1C, true));
                }
                catch (Exception ex) { SimHub.Logging.Current.Error($"[SimRIG] Macro Ex: {ex.Message}"); }
            });
        }
    }
}