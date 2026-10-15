using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using BandBar.Api;
using static BandBar.NativeMethods;

namespace BandBar
{
    /// <summary>
    /// The bar: a borderless, never-activated, topmost, per-pixel-alpha tool window on the taskbar surface
    /// (docs/DESIGN.md). Registers as an AppBar without reserving space, only to receive the shell's taskbar and
    /// full-screen notifications.
    /// </summary>
    internal sealed class BarForm : Form
    {
        private const int AppBarCallbackMessage = WM_APP + 1;
        private const int TrayGapDip = 4;
        private const int FallbackOffsetDip = 220;
        // After a shell event, check about once per frame (the Windows timer resolution, ~16 ms) for ~600 ms whether
        // the taskbar has raised itself over the bar.
        private const int ZOrderCheckIntervalMs = 16;
        private const int ZOrderCheckCount = 38;
        private static readonly Color InactiveArrowColor = Color.FromArgb(0x8A, 0x8A, 0x8A);
        // Invisible, but keeps every pixel of the bar clickable; fully transparent pixels pass clicks through.
        private static readonly Color HitTestableClear = Color.FromArgb(1, 0, 0, 0);

        private readonly Settings settings;
        private readonly NlMonitor monitor;
        private readonly ToolTip toolTip;
        private readonly ContextMenu menu;
        private readonly Timer repositionTimer;
        private readonly Timer zOrderTimer;
        private readonly uint taskbarCreatedMessage;
        // Referenced for the hook's lifetime: native code calls it, so the GC must not collect it.
        private readonly WinEventProc shellEventProc;

        private BarLayout layout;
        private IntPtr appBarHandle;
        private IntPtr shellEventHook;
        private IntPtr taskbarWindow;
        private int zOrderChecksLeft;
        private IntPtr notificationArea;
        private Rectangle lastNotificationArea;
        private bool started;
        private bool fullscreen;
        private bool sessionLocked;
        private bool taskbarMissingLogged;
        private Reading reading = Reading.Connecting;
        private string upText = "…";
        private string downText = "…";
        private string toolTipText;
        private Color textColor;

        public BarForm(Settings settings)
        {
            this.settings = settings;

            Text = "BandBar";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;

            taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
            toolTip = new ToolTip { ShowAlways = true, InitialDelay = 400, AutoPopDelay = 10000 };
            menu = new ContextMenu(new[] { new MenuItem(Strings.Exit, (s, e) => Close()) });

            // Coalesces bursts of shell notifications into one reposition; not a polling timer.
            repositionTimer = new Timer { Interval = 100 };
            repositionTimer.Tick += (s, e) =>
            {
                repositionTimer.Stop();
                Reposition();
            };

            // Runs only for a short while after a shell event: the taskbar may raise itself a little after the
            // click that triggered the event (docs/DESIGN.md). Stops by itself; never a standing poll.
            zOrderTimer = new Timer { Interval = ZOrderCheckIntervalMs };
            zOrderTimer.Tick += (s, e) =>
            {
                RaiseIfCoveredByTaskbar();
                if (--zOrderChecksLeft <= 0)
                    zOrderTimer.Stop();
            };

            shellEventProc = OnShellEvent;
            textColor = settings.ResolveTextColor();
            monitor = new NlMonitor(settings.ZoneFilterId, PostReading);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // A popup, not WinForms' overlapped window: overlapped windows get Windows' minimum size (39 px tall
                // at 100 %), which would overflow a thin taskbar.
                cp.Style = (cp.Style & ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX)) | WS_POPUP;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterAppBar();
            Reposition();
            UpdateToolTip();

            if (!started)
            {
                started = true;
                SystemEvents.SessionSwitch += OnSessionSwitch;

                // Out of context: Windows posts the events to this thread's message loop; nothing is loaded into
                // any other process (docs/DESIGN.md).
                shellEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_CAPTUREEND, IntPtr.Zero,
                    shellEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                if (shellEventHook == IntPtr.Zero)
                    Log.Warn("Shell event notifications unavailable; the taskbar may cover the bar after a click");

                monitor.Start();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterAppBar();
            base.OnHandleDestroyed(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            if (shellEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(shellEventHook);
                shellEventHook = IntPtr.Zero;
            }
            repositionTimer.Stop();
            zOrderTimer.Stop();
            monitor.Dispose();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                monitor.Dispose();
                repositionTimer.Dispose();
                zOrderTimer.Dispose();
                toolTip.Dispose();
                menu.Dispose();
                layout?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_MOUSEACTIVATE:
                    m.Result = (IntPtr)MA_NOACTIVATE;
                    return;
                case AppBarCallbackMessage:
                    OnAppBarNotification((int)m.WParam.ToInt64(), m.LParam);
                    return;
                case WM_DPICHANGED:
                    // We size ourselves from the taskbar; ignore the suggested rectangle.
                    ScheduleReposition();
                    m.Result = IntPtr.Zero;
                    return;
                case WM_DISPLAYCHANGE:
                    ScheduleReposition();
                    break;
                case WM_SETTINGCHANGE:
                    if (m.WParam.ToInt64() == SPI_SETWORKAREA)
                        ScheduleReposition();
                    else if (m.LParam != IntPtr.Zero && Marshal.PtrToStringUni(m.LParam) == "ImmersiveColorSet")
                        OnThemeChanged(); // light/dark mode switched
                    break;
                case WM_DWMCOLORIZATIONCOLORCHANGED:
                    OnThemeChanged(); // accent colour changed
                    break;
            }

            if (taskbarCreatedMessage != 0 && m.Msg == (int)taskbarCreatedMessage)
            {
                // Explorer restarted: the old AppBar registration and window handles are gone with it.
                Log.Info("Taskbar recreated; registering again");
                appBarHandle = IntPtr.Zero;
                taskbarWindow = IntPtr.Zero;
                notificationArea = IntPtr.Zero;
                RegisterAppBar();
                SetFullscreen(false);
                ScheduleReposition();
            }

            base.WndProc(ref m);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Right)
                return;

            toolTip.Hide(this);
            // The one deliberate activation: without it the menu would not close when the user clicks elsewhere.
            SetForegroundWindow(Handle);
            menu.Show(this, e.Location);
        }

        private void RegisterAppBar()
        {
            var abd = new APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)),
                hWnd = Handle,
                uCallbackMessage = AppBarCallbackMessage,
            };
            // ABM_NEW only; ABM_SETPOS is never sent, so no screen space is reserved and the taskbar is untouched.
            if (SHAppBarMessage(ABM_NEW, ref abd) != UIntPtr.Zero)
                appBarHandle = Handle;
            else
                Log.Warn("AppBar registration failed; full-screen and taskbar notifications are unavailable");
        }

        private void UnregisterAppBar()
        {
            if (appBarHandle == IntPtr.Zero)
                return;
            var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)), hWnd = appBarHandle };
            SHAppBarMessage(ABM_REMOVE, ref abd);
            appBarHandle = IntPtr.Zero;
        }

        private void OnAppBarNotification(int code, IntPtr lParam)
        {
            switch (code)
            {
                case ABN_FULLSCREENAPP:
                    SetFullscreen(lParam != IntPtr.Zero);
                    break;
                case ABN_POSCHANGED:
                case ABN_STATECHANGE:
                    ScheduleReposition();
                    break;
            }
        }

        private void SetFullscreen(bool value)
        {
            if (!settings.HideOnFullscreen)
                value = false;
            if (fullscreen == value)
                return;

            fullscreen = value;
            if (value)
            {
                Log.Info("Full-screen application opened; hiding");
                toolTip.Hide(this);
                zOrderTimer.Stop();
                // Hidden and out of the topmost band: nothing of ours overlaps the game (docs/DESIGN.md).
                SetWindowPos(Handle, HWND_NOTOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_HIDEWINDOW);
            }
            else
            {
                Log.Info("Full-screen application closed; showing");
                Reposition();
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_SHOWWINDOW);
                // The taskbar shows itself again at about the same time and may land on top of us.
                WatchZOrder();
            }
            UpdatePaused();
        }

        // Foreground, menu and mouse-capture events from any process, delivered out of context through our message
        // loop. Pressing a taskbar button raises a capture event at once; the foreground event covers the rest.
        private void OnShellEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild,
                                  uint eventThread, uint eventTime)
        {
            try
            {
                if (fullscreen || IsDisposed)
                    return;
                if (eventType == EVENT_SYSTEM_FOREGROUND)
                {
                    if (CoversItsMonitor(hwnd))
                        return; // a full-screen app is coming forward; ABN_FULLSCREENAPP hides the bar instead
                }
                else if (!BelongsToTaskbar(hwnd))
                {
                    return; // menu and mouse-capture events matter only when they happen on the taskbar
                }
                RaiseIfCoveredByTaskbar();
                WatchZOrder();
            }
            catch (Exception ex)
            {
                // Called from native code: never let an exception escape.
                Log.Debug("Shell event handler failed", ex);
            }
        }

        private void WatchZOrder()
        {
            zOrderChecksLeft = ZOrderCheckCount;
            zOrderTimer.Stop();
            zOrderTimer.Start();
        }

        private void RaiseIfCoveredByTaskbar()
        {
            if (fullscreen || !IsHandleCreated || IsDisposed || !IsCoveredByTaskbar())
                return;
            // Directly above the taskbar rather than at the very top, so Start, jump lists and tooltips stay above us.
            IntPtr above = GetWindow(taskbarWindow, GW_HWNDPREV);
            SetWindowPos(Handle, above == IntPtr.Zero ? HWND_TOPMOST : above, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        }

        private IntPtr GetTaskbarWindow()
        {
            if (taskbarWindow == IntPtr.Zero)
                taskbarWindow = FindWindow("Shell_TrayWnd", null);
            return taskbarWindow;
        }

        private bool BelongsToTaskbar(IntPtr hwnd)
        {
            IntPtr taskbar = GetTaskbarWindow();
            return taskbar != IntPtr.Zero && hwnd != IntPtr.Zero && GetAncestor(hwnd, GA_ROOT) == taskbar;
        }

        // Reads the z-order only: is the taskbar somewhere above the bar?
        private bool IsCoveredByTaskbar()
        {
            if (GetTaskbarWindow() == IntPtr.Zero)
                return false;

            IntPtr window = Handle;
            for (int i = 0; i < 1024; i++)
            {
                window = GetWindow(window, GW_HWNDPREV);
                if (window == IntPtr.Zero)
                    return false;
                if (window == taskbarWindow)
                    return true;
            }
            return false;
        }

        private static bool CoversItsMonitor(IntPtr hwnd)
        {
            if (!GetWindowRect(hwnd, out RECT window))
                return false;
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi))
                return false;
            RECT m = mi.rcMonitor;
            return window.Left <= m.Left && window.Top <= m.Top && window.Right >= m.Right && window.Bottom >= m.Bottom;
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            bool? locked = e.Reason == SessionSwitchReason.SessionLock ? true
                         : e.Reason == SessionSwitchReason.SessionUnlock ? false
                         : (bool?)null;
            if (locked == null)
                return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    sessionLocked = locked.Value;
                    UpdatePaused();
                }));
            }
            catch (InvalidOperationException)
            {
                // Window already gone during shutdown.
            }
        }

        private void UpdatePaused() => monitor.SetPaused(fullscreen || sessionLocked);

        private void ScheduleReposition()
        {
            repositionTimer.Stop();
            repositionTimer.Start();
        }

        private void Reposition()
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (!Taskbar.TryGet(Handle, out TaskbarInfo taskbar))
            {
                if (!taskbarMissingLogged)
                {
                    taskbarMissingLogged = true;
                    Log.Warn("Taskbar position unavailable; using the bottom of the primary screen");
                }
                Rectangle screen = Screen.PrimaryScreen.Bounds;
                taskbar = new TaskbarInfo(new Rectangle(screen.Left, screen.Bottom - 48, screen.Width, 48), TaskbarEdge.Bottom, 96);
            }

            Rectangle tb = taskbar.Bounds;
            int thickness = taskbar.IsVertical ? tb.Width : tb.Height;
            if (layout == null || layout.Dpi != taskbar.Dpi || layout.MaxHeight != thickness)
            {
                layout?.Dispose();
                layout = new BarLayout(taskbar.Dpi, thickness, SpeedFormat.WidestSamples(settings)
                    .Concat(Enum.GetValues(typeof(LinkStatus)).Cast<LinkStatus>().Select(Strings.Short)));
            }

            Size size = layout.Size;
            Rectangle target;
            if (!taskbar.IsVertical)
            {
                int right = FindAnchorEnd(taskbar, layout);
                int x = Math.Max(tb.Left, right - size.Width);
                int y = tb.Top + Math.Max(0, (tb.Height - size.Height) / 2);
                target = new Rectangle(x, y, size.Width, size.Height);
            }
            else
            {
                int bottom = FindAnchorEnd(taskbar, layout);
                int y = Math.Max(tb.Top, bottom - size.Height);
                int x = size.Width <= tb.Width ? tb.Left + (tb.Width - size.Width) / 2
                      : taskbar.Edge == TaskbarEdge.Left ? tb.Left
                      : tb.Right - size.Width;
                target = new Rectangle(x, y, size.Width, size.Height);
            }

            if (Bounds != target)
            {
                Bounds = target;
                Log.Debug("Positioned on the " + taskbar.Edge + " taskbar " + tb + " at dpi " + taskbar.Dpi
                          + ": target " + target + ", actual " + Bounds);
            }
            RaiseIfCoveredByTaskbar();
            Render();
        }

        /// <summary>
        /// Where the bar ends along the taskbar (right edge on a horizontal taskbar, bottom on a vertical one): just
        /// before the notification area by default, or the saved offset from the taskbar's end.
        /// </summary>
        private int FindAnchorEnd(TaskbarInfo taskbar, BarLayout metrics)
        {
            Rectangle tb = taskbar.Bounds;
            if (settings.AnchorOffsetDip.HasValue)
                return (taskbar.IsVertical ? tb.Bottom : tb.Right) - metrics.Scale(settings.AnchorOffsetDip.Value);

            if (notificationArea == IntPtr.Zero)
                notificationArea = Taskbar.FindNotificationArea();
            if (Taskbar.TryGetWindowBounds(notificationArea, out Rectangle tray) && tb.Contains(tray))
            {
                lastNotificationArea = tray;
                return (taskbar.IsVertical ? tray.Top : tray.Left) - metrics.Scale(TrayGapDip);
            }

            notificationArea = IntPtr.Zero;
            lastNotificationArea = Rectangle.Empty;
            return (taskbar.IsVertical ? tb.Bottom : tb.Right) - metrics.Scale(FallbackOffsetDip);
        }

        // The shell sends no notification when the notification area grows (a new tray icon, a wider clock), so in
        // the default position its rectangle is compared on each reading the bar already receives once a second.
        // One GetWindowRect, no extra timer.
        private void FollowNotificationArea()
        {
            if (settings.AnchorOffsetDip.HasValue || notificationArea == IntPtr.Zero || fullscreen)
                return;
            if (!Taskbar.TryGetWindowBounds(notificationArea, out Rectangle tray) || tray != lastNotificationArea)
                ScheduleReposition();
        }

        private void Render()
        {
            if (layout == null || !IsHandleCreated || IsDisposed)
                return;

            bool live = reading.Status == LinkStatus.Connected;
            Size size = layout.Size;
            using (var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(settings.BackgroundColor ?? HitTestableClear);
                    layout.Draw(g, upText, downText,
                        live ? settings.UploadColor : InactiveArrowColor,
                        live ? settings.DownloadColor : InactiveArrowColor,
                        textColor);
                }
                int error = LayeredWindow.Update(Handle, bitmap, Location);
                if (error != 0)
                    Log.Debug("UpdateLayeredWindow failed (" + error + ")");
            }
        }

        private void OnThemeChanged()
        {
            Color color = settings.ResolveTextColor();
            if (color == textColor)
                return;
            textColor = color;
            Log.Info("Taskbar theme changed; text colour updated");
            Render();
        }

        // Called on the monitor thread.
        private void PostReading(Reading r)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke((Action)(() => ApplyReading(r)));
            }
            catch (InvalidOperationException)
            {
                // Handle destroyed during shutdown.
            }
        }

        private void ApplyReading(Reading r)
        {
            string up, down;
            switch (r.Status)
            {
                case LinkStatus.Connected:
                    up = SpeedFormat.Format(r.Up, settings);
                    down = SpeedFormat.Format(r.Down, settings);
                    break;
                case LinkStatus.Connecting:
                case LinkStatus.NoService:
                    up = down = Strings.Short(r.Status);
                    break;
                default:
                    up = Strings.Short(r.Status);
                    down = "";
                    break;
            }

            bool changed = up != upText || down != downText || r.Status != reading.Status;
            reading = r;
            upText = up;
            downText = down;
            if (changed)
                Render();
            UpdateToolTip();
            FollowNotificationArea();
        }

        private void UpdateToolTip()
        {
            var sb = new StringBuilder("BandBar ").Append(Program.Version);
            if (reading.ZoneName != null)
                sb.Append('\n').Append(Strings.Zone).Append(": ").Append(reading.ZoneName);
            sb.Append('\n').Append(Strings.Describe(reading.Status));
            if (reading.ServiceVersion != null)
                sb.Append('\n').Append("NetLimiter ").Append(reading.ServiceVersion);
            if (reading.Status != LinkStatus.Connected && reading.Detail != null)
                sb.Append('\n').Append(reading.Detail);
            if (Program.IsElevated)
                sb.Append('\n').Append(Strings.Elevated);

            string text = sb.ToString();
            if (text != toolTipText)
            {
                toolTipText = text;
                toolTip.SetToolTip(this, text);
            }
        }
    }
}
