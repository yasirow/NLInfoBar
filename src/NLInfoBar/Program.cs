using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NLInfoBar.Api;

namespace NLInfoBar
{
    internal static class Program
    {
        /// <summary>Single-instance mutex; the installer's AppMutex must use the same name.</summary>
        internal const string MutexName = @"Local\NLInfoBar.SingleInstance";

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
                Application.ThreadException += (s, e) => Fatal(e.Exception);
                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    Log.Warn("Unobserved task exception", e.Exception);
                    e.SetObserved();
                };

                IsElevated = CheckElevated();
                Settings settings = Settings.Load(AppPaths.DataDir);
                Log.SetLevel(settings.LogLevel);
                Log.Info("Starting NLInfoBar " + Version + (IsElevated ? " (running elevated, which is not needed)" : ""));

                // Must run before anything touches a NetLimiter type.
                NlApiLoader.Install();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new BarForm(settings));

                Log.Info("Exited");
                return 0;
            }
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
