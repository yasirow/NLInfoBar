using System;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace NLInfoBar
{
    /// <summary>
    /// Small rotating text log (256 KB + one backup). Messages are scrubbed of profile paths, the user name and the
    /// machine name because users may attach the log to a public issue (docs/DESIGN.md).
    /// </summary>
    internal static class Log
    {
        private enum Level { Debug, Info, Warn, Error }

        private const long MaxBytes = 256 * 1024;
        private static readonly object Gate = new object();
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private static string path;
        private static Level minLevel = Level.Info;
        private static (string Find, string Replace)[] scrubPairs = Array.Empty<(string, string)>();
        private static Regex scrubUserSegment;

        public static void Init(string dataDir)
        {
            path = dataDir == null ? null : Path.Combine(dataDir, AppPaths.LogFileName);
            BuildScrubber();
        }

        public static void SetLevel(string level)
        {
            switch ((level ?? "").Trim().ToLowerInvariant())
            {
                case "debug": minLevel = Level.Debug; break;
                case "warn": minLevel = Level.Warn; break;
                case "error": minLevel = Level.Error; break;
                default: minLevel = Level.Info; break;
            }
        }

        public static void Debug(string message, Exception ex = null) => Write(Level.Debug, message, ex);
        public static void Info(string message, Exception ex = null) => Write(Level.Info, message, ex);
        public static void Warn(string message, Exception ex = null) => Write(Level.Warn, message, ex);
        public static void Error(string message, Exception ex = null) => Write(Level.Error, message, ex);

        private static void Write(Level level, string message, Exception ex)
        {
            if (level < minLevel || path == null)
                return;

            var sb = new StringBuilder(128);
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] ").Append(message);
            if (ex != null)
                sb.Append(" | ").Append(Describe(ex, withStack: level == Level.Error));
            string line = Scrub(sb.ToString()) + Environment.NewLine;

            lock (Gate)
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > MaxBytes)
                    {
                        string backup = path + ".1";
                        File.Delete(backup);
                        File.Move(path, backup);
                    }
                    File.AppendAllText(path, line, Utf8NoBom);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is SecurityException)
                {
                    // Logging must never take the app down.
                }
            }
        }

        private static string Describe(Exception ex, bool withStack)
        {
            var sb = new StringBuilder();
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (sb.Length > 0)
                    sb.Append(" --> ");
                sb.Append(e.GetType().FullName).Append(": ").Append(e.Message);
            }
            if (withStack && ex.StackTrace != null)
                sb.Append(Environment.NewLine).Append(ex.StackTrace);
            return sb.ToString();
        }

        private static void BuildScrubber()
        {
            var pairs = new System.Collections.Generic.List<(string, string)>();
            void Add(Environment.SpecialFolder folder, string placeholder)
            {
                string value = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(value))
                    pairs.Add((value, placeholder));
            }

            // Most specific first.
            Add(Environment.SpecialFolder.LocalApplicationData, "%LOCALAPPDATA%");
            Add(Environment.SpecialFolder.ApplicationData, "%APPDATA%");
            Add(Environment.SpecialFolder.UserProfile, "%USERPROFILE%");
            string temp = Path.GetTempPath().TrimEnd('\\');
            if (temp.Length > 0)
                pairs.Insert(0, (temp, "%TEMP%"));

            string machine = Environment.MachineName;
            if (machine.Length >= 3)
                pairs.Add((machine, "<machine>"));
            scrubPairs = pairs.ToArray();

            // The user name only where it can be one: a path segment or DOMAIN\user. A short name such as "A"
            // must not be replaced inside ordinary words.
            string user = Environment.UserName;
            if (!string.IsNullOrEmpty(user))
                scrubUserSegment = new Regex(@"(?<=[\\/])" + Regex.Escape(user) + @"(?=[\\/]|$|\s)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string Scrub(string text)
        {
            foreach ((string find, string replace) in scrubPairs)
                text = Replace(text, find, replace);
            return scrubUserSegment == null ? text : scrubUserSegment.Replace(text, "<user>");
        }

        private static string Replace(string text, string find, string replace)
        {
            int i = text.IndexOf(find, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
                return text;
            var sb = new StringBuilder(text.Length);
            int start = 0;
            while (i >= 0)
            {
                sb.Append(text, start, i - start).Append(replace);
                start = i + find.Length;
                i = text.IndexOf(find, start, StringComparison.OrdinalIgnoreCase);
            }
            return sb.Append(text, start, text.Length - start).ToString();
        }
    }
}
