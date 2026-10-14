using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace NLInfoBar
{
    /// <summary>
    /// Metrics and drawing for one DPI and taskbar thickness (docs/DESIGN.md): upload on the top line with a red up
    /// arrow, download on the bottom line with a green down arrow, text left-aligned after the arrow. The text column
    /// has a fixed width measured from the widest possible value, so the bar never resizes as the numbers change.
    /// </summary>
    internal sealed class BarLayout : IDisposable
    {
        private const float PreferredPoints = 9f;
        private const float MinimumPoints = 7f;

        private readonly Font font;
        private readonly StringFormat format;
        private readonly int padX, arrowWidth, arrowHeight, arrowGap, lineHeight, textWidth;

        /// <param name="maxHeight">Taskbar thickness in pixels; the font shrinks (down to 7 pt) until two lines fit.</param>
        public BarLayout(uint dpi, int maxHeight, IEnumerable<string> widestTexts)
        {
            Dpi = dpi;
            format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;

            float points = PreferredPoints;
            font = CreateFont(points);
            while (LineHeightOf(font) * 2 > maxHeight && points > MinimumPoints)
            {
                font.Dispose();
                points -= 0.5f;
                font = CreateFont(points);
            }

            lineHeight = LineHeightOf(font);
            padX = Scale(3);
            arrowGap = Scale(4);
            arrowWidth = Scale(8);
            arrowHeight = Math.Min(Scale(10), lineHeight - Scale(2));

            using (var probe = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(probe))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                foreach (string text in widestTexts)
                    textWidth = Math.Max(textWidth, (int)Math.Ceiling(g.MeasureString(text, font, PointF.Empty, format).Width));
            }
        }

        public uint Dpi { get; }

        public Size Size => new Size(padX * 2 + arrowWidth + arrowGap + textWidth, lineHeight * 2);

        public int Scale(int dip) => (int)Math.Round(dip * Dpi / 96.0);

        public void Draw(Graphics g, string up, string down, Color upArrow, Color downArrow, Color text)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // ClearType would fringe on a transparent background
            DrawLine(g, 0, up, pointsUp: true, upArrow, text);
            DrawLine(g, lineHeight, down, pointsUp: false, downArrow, text);
        }

        private void DrawLine(Graphics g, int top, string value, bool pointsUp, Color arrowColor, Color textColor)
        {
            int x = padX;
            int y = top + (lineHeight - arrowHeight) / 2;
            using (var brush = new SolidBrush(arrowColor))
                g.FillPolygon(brush, BlockArrow(x, y, arrowWidth, arrowHeight, pointsUp));

            float textTop = top + (lineHeight - font.GetHeight()) / 2f;
            using (var brush = new SolidBrush(textColor))
                g.DrawString(value ?? "", font, brush, new PointF(x + arrowWidth + arrowGap, textTop), format);
        }

        // Triangle head plus a rectangular stem; defined pointing up and mirrored vertically for down.
        private static PointF[] BlockArrow(float x, float y, float width, float height, bool pointsUp)
        {
            float cx = x + width / 2f;
            float head = Math.Min(height, width / 2f + 1f);
            float halfStem = Math.Max(1f, width * 0.2f);
            float Y(float dy) => pointsUp ? y + dy : y + height - dy;

            return new[]
            {
                new PointF(cx, Y(0)),
                new PointF(x + width, Y(head)),
                new PointF(cx + halfStem, Y(head)),
                new PointF(cx + halfStem, Y(height)),
                new PointF(cx - halfStem, Y(height)),
                new PointF(cx - halfStem, Y(head)),
                new PointF(x, Y(head)),
            };
        }

        private Font CreateFont(float points) =>
            new Font("Segoe UI", points * Dpi / 72f, FontStyle.Regular, GraphicsUnit.Pixel);

        private static int LineHeightOf(Font f) => (int)Math.Ceiling(f.GetHeight());

        public void Dispose()
        {
            font.Dispose();
            format.Dispose();
        }
    }
}
