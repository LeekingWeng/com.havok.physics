#if UNITY_EDITOR_WIN
using UnityEditor;
using UnityEngine;
using Havok.Physics.Authoring;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Havok.Physics.Editor
{
    // Havok Visual Debugger is currently only available on Windows.

    class VisualDebuggerClientApplication
    {
        public delegate bool EnumWindowProc(IntPtr hWnd, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder lpString, int cch);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Int32 GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd); 
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
        [DllImport("user32.dll")] private static extern int AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);
        [DllImport("user32.dll")] private static extern bool LockSetForegroundWindow(uint uLockCode);
        [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int dwProcessId);
        [DllImport("user32.dll")] private static extern IntPtr FindWindowEx(IntPtr parentWindow, IntPtr previousChildWindow, string windowClass, string windowTitle);

        const int SW_RESTORE = 9;
        const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        const uint LSFW_UNLOCK = 2;
        const int ASFW_ANY = -1;

        public static void ForceWindowIntoForeground(IntPtr window)
        {
            int currentThread = System.Threading.Thread.CurrentThread.ManagedThreadId;

            IntPtr activeWindow = GetForegroundWindow();
            int activeThread = GetWindowThreadProcessId(activeWindow, out int activeProcess);
            int windowThread = GetWindowThreadProcessId(window, out int windowProcess);

            if (currentThread != activeThread)
            {
                AttachThreadInput(currentThread, activeThread, true);
            }
            if (windowThread != currentThread)
            {
                AttachThreadInput(windowThread, currentThread, true);
            }

            uint oldTimeout = 0, newTimeout = 0;
            SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref oldTimeout, 0);
            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref newTimeout, 0);
            LockSetForegroundWindow(LSFW_UNLOCK);
            AllowSetForegroundWindow(ASFW_ANY);

            SetForegroundWindow(window); 
            ShowWindow(window, SW_RESTORE);

            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref oldTimeout, 0);

            if (currentThread != activeThread)
            {
                AttachThreadInput(currentThread, activeThread, false);
            }
            if (windowThread != currentThread)
            {
                AttachThreadInput(windowThread, currentThread, false);
            }
        }

        [MenuItem("Window/Analysis/Havok Visual Debugger", false, 51)]
        private static void VDBMenuOption()
        {
            string vdbExe = System.IO.Path.GetFullPath("Packages/com.havok.physics/Tools/VisualDebugger/HavokVisualDebugger.exe");
            string vdbProcessName = System.IO.Path.GetFileNameWithoutExtension(vdbExe);

            // Find all the instances of the VDB or make a new one
            List<System.Diagnostics.Process> processes = new List<System.Diagnostics.Process>();
            processes.AddRange(System.Diagnostics.Process.GetProcessesByName(vdbProcessName));
            if (processes.Count == 0)
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = vdbExe;
                // TODO: launch instance with specific connection settings
                process.StartInfo.Arguments = "";
                process.Start();
                process.WaitForInputIdle();
                process.Refresh();
                processes.Add(process);
            }

            // Bring the main window of the VDB processes to the foreground.
            IntPtr hWnd = IntPtr.Zero;
            do
            {
                hWnd = FindWindowEx(IntPtr.Zero, hWnd, null, null);
                GetWindowThreadProcessId(hWnd, out int iProcess);
                if (processes.Exists(process => process.Id == iProcess))
                {
                    int textLength = GetWindowTextLength(hWnd);
                    System.Text.StringBuilder winText = new System.Text.StringBuilder(textLength + 1);
                    GetWindowText(hWnd, winText, winText.Capacity);
                    // VDB main window title is "Havok Visual Debugger (<ip address>:<port>)"
                    // TODO: search for specific instance that matches connection settings
                    const string wName = "Havok Visual Debugger";
                    if (winText.ToString().StartsWith(wName))
                    {
                        ForceWindowIntoForeground(hWnd);
                    }
                }
            }
            while (hWnd != IntPtr.Zero);
        }
    }
}
#endif
