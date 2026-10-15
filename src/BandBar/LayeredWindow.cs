using System;
using System.Drawing;
using static BandBar.NativeMethods;

namespace BandBar
{
    /// <summary>
    /// Pushes a 32-bit ARGB bitmap to our own layered window with per-pixel alpha (docs/DESIGN.md), so the taskbar
    /// shows through the bar.
    /// </summary>
    internal static class LayeredWindow
    {
        public static bool Update(IntPtr hwnd, Bitmap bitmap, Point location)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr previous = IntPtr.Zero;
            try
            {
                // Color.FromArgb(0): keep the alpha channel; GDI+ hands back premultiplied pixels as ULW expects.
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                previous = SelectObject(memoryDc, hBitmap);

                var destination = new POINT { X = location.X, Y = location.Y };
                var size = new SIZE { Cx = bitmap.Width, Cy = bitmap.Height };
                var source = new POINT();
                var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
                return UpdateLayeredWindow(hwnd, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (previous != IntPtr.Zero)
                    SelectObject(memoryDc, previous);
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}
