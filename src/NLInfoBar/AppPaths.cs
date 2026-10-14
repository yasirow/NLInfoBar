using System;
using System.IO;
using System.Security;

namespace NLInfoBar
{
    /// <summary>
    /// Where the app may write: next to the executable when that folder is writable, otherwise
    /// %LocalAppData%\NLInfoBar. Nowhere else (docs/DESIGN.md).
    /// </summary>
    internal static class AppPaths
    {
        public const string LogFileName = "nlinfobar.log";
        public const string SettingsFileName = "settings.json";

        /// <summary>Null when neither location is writable; the app then runs without a log and with default settings.</summary>
        public static string DataDir { get; private set; }

        public static void Init()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            if (CanWrite(exeDir))
            {
                DataDir = exeDir;
                return;
            }

            try
            {
                string fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NLInfoBar");
                Directory.CreateDirectory(fallback);
                DataDir = CanWrite(fallback) ? fallback : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                DataDir = null;
            }
        }

        // Opening our own log file for append is the first write the app makes anyway, so the probe leaves nothing foreign behind.
        private static bool CanWrite(string dir)
        {
            try
            {
                using (new FileStream(Path.Combine(dir, LogFileName), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                return false;
            }
        }
    }
}
