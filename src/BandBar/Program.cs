using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BandBar.Api;

namespace BandBar
{
    internal static class Program
    {
        /// <summary>Single-instance mutex; the installer's AppMutex must use the same name.</summary>
        internal const string MutexName = @"Local\BandBar.SingleInstance";

        // More UI-thread errors than this within the window means something is persistently broken: exit then.
        private const int MaxUiErrors = 3;
        private static readonly TimeSpan UiErrorWindow = TimeSpan.FromSeconds(60);
        private static readonly System.Collections.Generic.Queue<DateTime> RecentUiErrors = new System.Collections.Generic.Queue<DateTime>();

        internal static bool IsElevated { get; private set; }

        internal static string Version => typeof(Program).Assembly.GetName().Version.ToString(3);

        [STAThread]
        private static int Main()
        {
            // No Windows error dialogs: one could pop up over a game. A crash ends only this process.
            NativeMethods.SetErrorMode(NativeMethods.SEM_FAILCRITICALERRORS | NativeMethods.SEM_NOGPFAULTERRORBOX);

            using (var mutex = new Mutex(true, MutexName, out bool createdNew))
            {
                if (!createdNew)
                    return 0;

                AppPaths.Init();
                Log.Init(AppPaths.DataDir);

                AppDomain.CurrentDomain.UnhandledException += (s, e) => Fatal(e.ExceptionObject as Exception);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => OnUiThreadException(e.Exception);
                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    Log.Warn("Unobserved task exception", e.Exception);
                    e.SetObserved();
                };

                IsElevated = CheckElevated();
                Settings settings = Settings.Load(AppPaths.DataDir);
                Log.SetLevel(settings.LogLevel);
                Log.Info("Starting BandBar " + Version + (IsElevated ? " (running elevated, which is not needed)" : ""));

                // Must run before anything touches a NetLimiter type.
                NlApiLoader.Install();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new BarForm(settings));

                Log.Info("Exited");
                return 0;
            }
        }

        // A single failure in a UI handler (a reposition, a redraw) must not silently remove the bar; a burst of them
        // means the state is broken, and exiting is then the safe choice.
        private static void OnUiThreadException(Exception ex)
        {
            DateTime now = DateTime.UtcNow;
            RecentUiErrors.Enqueue(now);
            while (RecentUiErrors.Count > 0 && now - RecentUiErrors.Peek() > UiErrorWindow)
                RecentUiErrors.Dequeue();

            if (RecentUiErrors.Count > MaxUiErrors)
                Fatal(ex);
            else
                Log.Error("Error on the UI thread; continuing", ex);
        }

        private static void Fatal(Exception ex)
        {
            try
            {
                Log.Error("Fatal error, exiting", ex);
            }
            finally
            {
                Environment.Exit(1);
            }
        }

        private static bool CheckElevated()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
