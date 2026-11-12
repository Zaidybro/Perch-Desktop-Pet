using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Perch
{
    // A tiny software sprite. Every shape lands on whole pixels before WPF enlarges it.
    internal static class PixelMochi
    {
        private const int Size = 64;
        private const int Edge = unchecked((int)0xFF4B3034);
        private const int Cream = unchecked((int)0xFFFFF1DA);
        private const int White = unchecked((int)0xFFFFFCF1);
        private const int Pink = unchecked((int)0xFFF4A9AC);
        private const int Eye = unchecked((int)0xFF30262D);
        private const int Shadow = unchecked((int)0x554B3034);
        private const int Mint = unchecked((int)0xFF78AD9E);

        private struct Palette
        {
            public int Fur, Shade, Ear, Blush;
            public Palette(int fur, int shade, int ear, int blush)
            { Fur = fur; Shade = shade; Ear = ear; Blush = blush; }
        }

        private static Palette Colors(string theme)
        {
            switch (theme)
            {
                case "Cream": return new Palette(unchecked((int)0xFFE5C893), unchecked((int)0xFFC9A971), unchecked((int)0xFFE8B6B0), Pink);
                case "Cocoa": return new Palette(unchecked((int)0xFFAD725C), unchecked((int)0xFF89523F), unchecked((int)0xFFE7A9A7), Pink);
                case "Strawberry": return new Palette(unchecked((int)0xFFE8A0A3), unchecked((int)0xFFC97C8E), unchecked((int)0xFFFFCBD3), unchecked((int)0xFFF587A4));
                case "Midnight": return new Palette(unchecked((int)0xFF6A7797), unchecked((int)0xFF485875), unchecked((int)0xFFBD9FB5), unchecked((int)0xFFDF98A8));
                case "Rainy day": return new Palette(unchecked((int)0xFF9BA99A), unchecked((int)0xFF738C84), unchecked((int)0xFFDBB5AE), Pink);
                default: return new Palette(unchecked((int)0xFFF2B66E), unchecked((int)0xFFD98A54), unchecked((int)0xFFF5B1AE), Pink);
            }
        }

        public static BitmapSource Frame(Avatar.Pose p, string theme, string accessory, bool focus,
            double gazeX, double gazeY, bool dancing, bool petting, double seconds)
        {
            var c = new Canvas();
            Palette color = Colors(theme);
            int dx = Snap(p.X / 2), dy = Snap(p.Y / 2);
            int squish = Snap((1 - p.Stretch) * 12);
            c.Ellipse(15 + dx / 3, 59, 34, 4, Shadow);

            // Tail and body move behind the face.
            int tailY = Snap(p.Tail / 12);
            c.Ellipse(47 + dx, 38 + dy + tailY, 13, 10, Edge);
            c.Ellipse(49 + dx, 39 + dy + tailY, 9, 7, color.Fur);
            c.Rect(53 + dx, 42 + dy + tailY, 3, 2, Cream);
            c.Ellipse(12 + dx, 36 + dy + squish, 42, 23 - squish, Edge);
            c.Ellipse(14 + dx, 38 + dy + squish, 38, 19 - squish, color.Fur);
            c.Ellipse(19 + dx, 44 + dy, 28, 12, Cream);
            c.Rect(17 + dx, 45 + dy, 4, 5, color.Shade);
            c.Rect(45 + dx, 45 + dy, 4, 5, color.Shade);

            // Short feet deliberately sit at different heights.
            int leftPaw = Snap(p.LeftPaw / 2), rightPaw = Snap(p.RightPaw / 2);
            Paw(c, 17 + dx, 52 + dy + leftPaw, color.Shade);
            Paw(c, 38 + dx, 53 + dy + rightPaw, color.Shade);

            int hx = dx + Snap(p.HeadAngle / 5), hy = dy + Snap(p.HeadY / 2) + squish;
            Ear(c, 16 + hx + Snap(p.LeftEar / 8), 7 + hy, false, color);
            Ear(c, 40 + hx + Snap(p.RightEar / 8), 9 + hy, true, color);

            // A broad face gives readable expressions even at desktop size.
            c.Ellipse(9 + hx, 18 + hy, 46, 36, Edge);
            c.Ellipse(11 + hx, 20 + hy, 42, 32, color.Fur);
            c.Rect(28 + hx, 20 + hy, 8, 16, Cream);
            c.Rect(26 + hx, 22 + hy, 2, 10, Cream);
            c.Rect(36 + hx, 22 + hy, 2, 10, Cream);
            c.Ellipse(13 + hx, 34 + hy, 38, 18, Cream);
            c.Ellipse(19 + hx, 37 + hy, 13, 11, White);
            c.Ellipse(33 + hx, 37 + hy, 13, 11, White);
            c.Rect(15 + hx, 36 + hy, 5, 2, color.Shade);
            c.Rect(44 + hx, 36 + hy, 5, 2, color.Shade);
            c.Ellipse(15 + hx, 38 + hy, 6, 3, color.Blush);
            c.Ellipse(43 + hx, 38 + hy, 6, 3, color.Blush);

            int gx = Snap(gazeX / 3), gy = Snap(gazeY / 2);
            if (p.Sleepy)
            {
                c.Rect(22 + hx, 34 + hy, 7, 2, Eye);
                c.Rect(36 + hx, 34 + hy, 7, 2, Eye);
            }
            else
            {
                EyeAt(c, 23 + hx + gx, 31 + hy + gy);
                if (p.Wink) c.Rect(37 + hx, 35 + hy, 6, 2, Eye);
                else EyeAt(c, 37 + hx + gx, 31 + hy + gy);
            }
            c.Rect(30 + hx, 40 + hy, 5, 2, Edge);
            c.Rect(31 + hx, 42 + hy, 3, 2, Edge);
            c.Rect(27 + hx, 45 + hy, 4, 2, Edge);
            c.Rect(34 + hx, 45 + hy, 4, 2, Edge);
            c.Rect(29 + hx, 46 + hy, 7, 2, Edge);
            if (p.Smile)
            {
                c.Rect(31 + hx, 48 + hy, 4, 3, Pink);
                c.Rect(32 + hx, 51 + hy, 2, 1, Edge);
            }

            if (focus)
            {
                c.Frame(19 + hx, 29 + hy, 13, 12, unchecked((int)0xFF456A70));
                c.Frame(33 + hx, 29 + hy, 13, 12, unchecked((int)0xFF456A70));
                c.Rect(31 + hx, 32 + hy, 3, 2, unchecked((int)0xFF456A70));
            }
            if (accessory == "Bow")
            {
                c.Rect(27 + hx, 19 + hy, 4, 5, Mint);
                c.Rect(35 + hx, 19 + hy, 4, 5, Mint);
                c.Rect(31 + hx, 20 + hy, 4, 3, Edge);
                c.Rect(32 + hx, 20 + hy, 2, 2, unchecked((int)0xFFF7D781));
            }
            else if (accessory == "Tiny hat")
            {
                c.Rect(25 + hx, 12 + hy, 14, 7, Edge);
                c.Rect(27 + hx, 11 + hy, 10, 6, unchecked((int)0xFF6A85A4));
                c.Rect(22 + hx, 18 + hy, 20, 2, Edge);
            }
            else if (accessory == "Bandana")
            {
                c.Rect(25 + dx, 53 + dy, 16, 3, unchecked((int)0xFFD97673));
                c.Rect(30 + dx, 56 + dy, 6, 3, unchecked((int)0xFFD97673));
            }

            if (p.Paper)
            {
                c.Rect(4 + dx, 35 + dy, 15, 17, Edge);
                c.Rect(6 + dx, 37 + dy, 11, 13, White);
                c.Rect(8 + dx, 40 + dy, 7, 1, Mint);
                c.Rect(8 + dx, 44 + dy, 5, 1, Mint);
            }
            if (petting && seconds < .8)
            {
                c.Rect(7, 16, 5, 4, Pink); c.Rect(4, 18, 11, 4, Pink);
                c.Rect(6, 22, 7, 3, Pink); c.Rect(8, 25, 3, 2, Pink);
            }
            if (dancing)
            {
                Spark(c, 4, 30); Spark(c, 56, 23);
            }

            var bitmap = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null,
                c.Pixels, Size * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static void Paw(Canvas c, int x, int y, int shade)
        {
            c.Ellipse(x, y, 12, 8, Edge);
            c.Ellipse(x + 2, y + 1, 8, 5, Cream);
            c.Rect(x + 4, y + 2, 1, 2, shade);
            c.Rect(x + 7, y + 2, 1, 2, shade);
        }

        private static void Ear(Canvas c, int x, int y, bool right, Palette color)
        {
            int s = right ? 1 : -1;
            c.Polygon(new[] { x - s * 4, y + 22, x + s * 3, y + 8,
                x + s * 10, y, x + s * 14, y + 3,
                x + s * 14, y + 14, x + s * 7, y + 25 }, Edge);
            c.Polygon(new[] { x - s * 2, y + 22, x + s * 5, y + 9,
                x + s * 10, y + 3, x + s * 12, y + 5,
                x + s * 11, y + 14, x + s * 5, y + 23 }, color.Fur);
            c.Polygon(new[] { x + s * 3, y + 19, x + s * 8, y + 8,
                x + s * 10, y + 7, x + s * 9, y + 15,
                x + s * 6, y + 21 }, color.Ear);
        }

        private static void EyeAt(Canvas c, int x, int y)
        {
            c.Ellipse(x, y, 5, 7, Eye);
            c.Rect(x + 1, y + 1, 2, 2, White);
        }

        private static void Spark(Canvas c, int x, int y)
        {
            int gold = unchecked((int)0xFFF7D381);
            c.Rect(x + 2, y, 2, 7, gold);
            c.Rect(x, y + 2, 6, 2, gold);
        }

        private static int Snap(double value) { return (int)Math.Round(value); }

        private sealed class Canvas
        {
            public readonly int[] Pixels = new int[Size * Size];

            public void Rect(int x, int y, int width, int height, int color)
            {
                for (int yy = Math.Max(0, y); yy < Math.Min(Size, y + height); yy++)
                    for (int xx = Math.Max(0, x); xx < Math.Min(Size, x + width); xx++)
                        Pixels[yy * Size + xx] = color;
            }

            public void Frame(int x, int y, int width, int height, int color)
            {
                Rect(x, y, width, 2, color); Rect(x, y + height - 2, width, 2, color);
                Rect(x, y, 2, height, color); Rect(x + width - 2, y, 2, height, color);
            }

            public void Ellipse(int x, int y, int width, int height, int color)
            {
                if (width <= 0 || height <= 0) return;
                double rx = width / 2.0, ry = height / 2.0;
                for (int yy = Math.Max(0, y); yy < Math.Min(Size, y + height); yy++)
                    for (int xx = Math.Max(0, x); xx < Math.Min(Size, x + width); xx++)
                    {
                        double a = (xx + .5 - x - rx) / rx;
                        double b = (yy + .5 - y - ry) / ry;
                        if (a * a + b * b <= 1) Pixels[yy * Size + xx] = color;
                    }
            }

            public void Polygon(int[] points, int color)
            {
                int minY = Size, maxY = 0;
                for (int i = 1; i < points.Length; i += 2)
                { minY = Math.Min(minY, points[i]); maxY = Math.Max(maxY, points[i]); }
                for (int y = Math.Max(0, minY); y <= Math.Min(Size - 1, maxY); y++)
                {
                    var xs = new System.Collections.Generic.List<double>();
                    for (int i = 0; i < points.Length; i += 2)
                    {
                        int j = (i + 2) % points.Length;
                        int ya = points[i + 1], yb = points[j + 1];
                        if ((ya <= y && y < yb) || (yb <= y && y < ya))
                            xs.Add(points[i] + (y + .5 - ya) * (points[j] - points[i]) / (double)(yb - ya));
                    }
                    xs.Sort();
                    for (int i = 0; i + 1 < xs.Count; i += 2)
                        Rect((int)Math.Ceiling(xs[i]), y, (int)Math.Floor(xs[i + 1]) - (int)Math.Ceiling(xs[i]) + 1, 1, color);
                }
            }
        }
    }
}
