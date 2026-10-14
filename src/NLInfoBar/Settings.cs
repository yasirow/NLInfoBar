using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security;

namespace NLInfoBar
{
    /// <summary>settings.json (docs/DESIGN.md). Read-only in this phase: the app never writes it yet.</summary>
    [DataContract]
    internal sealed class Settings
    {
        private const long MaxFileBytes = 64 * 1024;

        [DataMember(Name = "schemaVersion")] public int SchemaVersion;
        [DataMember(Name = "zoneFilterId")] public string ZoneFilterId;
        [DataMember(Name = "unitBase")] public int UnitBase;
        [DataMember(Name = "decimals")] public int Decimals;
        [DataMember(Name = "theme")] public string Theme;
        [DataMember(Name = "colors")] public ColorSettings Colors;
        [DataMember(Name = "opacity")] public double Opacity;
        /// <summary>Null: just before the notification area (docs/DESIGN.md).</summary>
        [DataMember(Name = "anchorOffsetDip")] public int? AnchorOffsetDip;
        [DataMember(Name = "hideOnFullscreen")] public bool HideOnFullscreen;
        [DataMember(Name = "followAutoHideTaskbar")] public bool FollowAutoHideTaskbar;
        [DataMember(Name = "fullscreenExeList")] public List<string> FullscreenExeList;
        [DataMember(Name = "hotkey")] public string Hotkey;
        [DataMember(Name = "logLevel")] public string LogLevel;

        public Settings() => SetDefaults();

        public Color DownloadColor { get; private set; }
        public Color UploadColor { get; private set; }
        public Color TextColor { get; private set; }

        /// <summary>Null: transparent, the taskbar shows through (default).</summary>
        public Color? BackgroundColor { get; private set; }

        public static Settings Load(string dataDir)
        {
            Settings settings = null;
            string path = dataDir == null ? null : Path.Combine(dataDir, AppPaths.SettingsFileName);
            try
            {
                if (path != null && File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        if (stream.Length > MaxFileBytes)
                            Log.Warn("settings.json is larger than 64 KB; using defaults");
                        else
                            settings = new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream) as Settings;
                    }
                }
            }
            catch (Exception ex) when (ex is SerializationException || ex is IOException
                                       || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                Log.Warn("settings.json could not be read; using defaults", ex);
                settings = null;
            }

            settings = settings ?? new Settings();
            settings.Normalize();
            return settings;
        }

        // DataContractJsonSerializer skips constructors, so members missing from the file would stay zero/null.
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => SetDefaults();

        private void SetDefaults()
        {
            SchemaVersion = 1;
            ZoneFilterId = null;
            UnitBase = 1024;
            Decimals = 1;
            Theme = "dark";
            Colors = new ColorSettings();
            Opacity = 0.85;
            AnchorOffsetDip = null;
            HideOnFullscreen = true;
            FollowAutoHideTaskbar = false;
            FullscreenExeList = new List<string>();
            Hotkey = null;
            LogLevel = "info";
        }

        private void Normalize()
        {
            if (UnitBase != 1000)
                UnitBase = 1024;
            Decimals = Clamp(Decimals, 0, 3);
            Opacity = double.IsNaN(Opacity) ? 0.85 : Math.Max(0.3, Math.Min(1.0, Opacity));
            if (AnchorOffsetDip.HasValue)
                AnchorOffsetDip = Clamp(AnchorOffsetDip.Value, 0, 10000);
            FullscreenExeList = FullscreenExeList ?? new List<string>();
            Colors = Colors ?? new ColorSettings();

            bool light = string.Equals(Theme, "light", StringComparison.OrdinalIgnoreCase);
            Theme = light ? "light" : "dark";
            DownloadColor = ParseColor(Colors.Download) ?? Color.FromArgb(0x2E, 0xCC, 0x71);
            UploadColor = ParseColor(Colors.Upload) ?? Color.FromArgb(0xE7, 0x4C, 0x3C);
            TextColor = ParseColor(Colors.Text) ?? (light ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White);
            Color? background = ParseColor(Colors.Background);
            BackgroundColor = background.HasValue ? Color.FromArgb((int)Math.Round(Opacity * 255), background.Value) : (Color?)null;
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static Color? ParseColor(string value)
        {
            if (value != null && value.Length == 7 && value[0] == '#'
                && int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return null;
        }

        [DataContract]
        internal sealed class ColorSettings
        {
            [DataMember(Name = "download")] public string Download = "#2ECC71";
            [DataMember(Name = "upload")] public string Upload = "#E74C3C";
            // Null means "from the theme"; only the JSON file sets them.
            [DataMember(Name = "text")] public string Text = null;
            [DataMember(Name = "background")] public string Background = null;
        }
    }
}
