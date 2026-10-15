using System;
using System.Collections.Generic;
using System.Globalization;

namespace BandBar
{
    /// <summary>
    /// Dynamic B/s → KB/s → MB/s → GB/s with one decimal (docs/DESIGN.md).
    /// Values are truncated, not rounded, so the bar never shows more than was measured and never flips to "1024,0".
    /// </summary>
    internal static class SpeedFormat
    {
        private static readonly string[] Units = { "B/s", "KB/s", "MB/s", "GB/s" };

        public static string Format(uint bytesPerSecond, Settings settings)
        {
            double unit = settings.UnitBase;
            double value = bytesPerSecond;
            int index = 0;
            while (value >= unit && index < Units.Length - 1)
            {
                value /= unit;
                index++;
            }
            return Truncate(value, settings.Decimals) + " " + Units[index];
        }

        /// <summary>Strings at least as wide as anything <see cref="Format"/> can return; used to fix the bar width.</summary>
        public static IEnumerable<string> WidestSamples(Settings settings)
        {
            string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string number = "8888" + (settings.Decimals > 0 ? separator + new string('8', settings.Decimals) : "");
            foreach (string unit in Units)
                yield return number + " " + unit;
        }

        private static string Truncate(double value, int decimals)
        {
            double scale = Math.Pow(10, decimals);
            double truncated = Math.Floor(value * scale + 1e-9) / scale;
            return truncated.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);
        }
    }
}
