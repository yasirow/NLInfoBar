using System;
using System.Drawing;
using System.Runtime.InteropServices;
using static NLInfoBar.NativeMethods;

namespace NLInfoBar
{
    internal enum TaskbarEdge
    {
        Left = 0,
        Top = 1,
        Right = 2,
        Bottom = 3,
    }

    internal readonly struct TaskbarInfo
    {
        public TaskbarInfo(Rectangle bounds, TaskbarEdge edge, uint dpi)
        {
            Bounds = bounds;
            Edge = edge;
            Dpi = dpi;
        }

        /// <summary>Physical pixels.</summary>
        public Rectangle Bounds { get; }
        public TaskbarEdge Edge { get; }
        public uint Dpi { get; }
        public bool IsVertical => Edge == TaskbarEdge.Left || Edge == TaskbarEdge.Right;
    }

    /// <summary>Read-only queries about the primary taskbar (docs/DESIGN.md).</summary>
    internal static class Taskbar
    {
        public static bool TryGet(IntPtr hwnd, out TaskbarInfo info)
        {
            info = default;
            RECT rc;
            TaskbarEdge edge;

            var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)), hWnd = hwnd };
            if (SHAppBarMessage(ABM_GETTASKBARPOS, ref abd) != UIntPtr.Zero && IsUsable(abd.rc) && abd.uEdge <= 3)
            {
                rc = abd.rc;
                edge = (TaskbarEdge)abd.uEdge;
            }
            else
            {
                // Fallback: the taskbar window's own rectangle, edge inferred from its monitor.
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray == IntPtr.Zero || !GetWindowRect(tray, out rc) || !IsUsable(rc))
                    return false;
                edge = InferEdge(rc);
            }

            IntPtr monitor = MonitorFromRect(ref rc, MONITOR_DEFAULTTONEAREST);
            uint dpi = 96;
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                dpi = dpiX;

            info = new TaskbarInfo(Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom), edge, dpi);
            return true;
        }

        /// <summary>The notification area's window, or zero. Read-only; used for the default position.</summary>
        public static IntPtr FindNotificationArea()
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            return tray == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        }

        public static bool TryGetWindowBounds(IntPtr hwnd, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT rc) || !IsUsable(rc))
                return false;
            bounds = Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom);
            return true;
        }

        private static bool IsUsable(RECT rc) => rc.Right > rc.Left && rc.Bottom > rc.Top;

        private static TaskbarEdge InferEdge(RECT rc)
        {
            IntPtr monitor = MonitorFromRect(ref rc, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref mi))
                return TaskbarEdge.Bottom;

            RECT m = mi.rcMonitor;
            if (rc.Right - rc.Left >= rc.Bottom - rc.Top)
                return rc.Top - m.Top <= m.Bottom - rc.Bottom ? TaskbarEdge.Top : TaskbarEdge.Bottom;
            return rc.Left - m.Left <= m.Right - rc.Right ? TaskbarEdge.Left : TaskbarEdge.Right;
        }
    }
}
