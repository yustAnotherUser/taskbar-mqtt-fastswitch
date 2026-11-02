using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace TaskbarMqtt.Platform
{
    public static class ImageUtils
    {
        public static SKBitmap Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                using (var stream = File.OpenRead(path))
                using (var decoded = SKBitmap.Decode(stream))
                {
                    if (decoded == null) return null;
                    return EnsureAlpha(decoded);
                }
            }
            catch
            {
                return null;
            }
        }

        public static SKBitmap LoadResource(string nameEnding)
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (var resName in asm.GetManifestResourceNames())
                {
                    if (!resName.EndsWith("." + nameEnding, StringComparison.OrdinalIgnoreCase)) continue;
                    using (var s = asm.GetManifestResourceStream(resName))
                    using (var decoded = SKBitmap.Decode(s))
                    {
                        if (decoded == null) return null;
                        return EnsureAlpha(decoded);
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public static SKBitmap EnsureAlpha(SKBitmap src)
        {
            if (src.ColorType == SKColorType.Bgra8888) return src.Copy();
            var target = new SKBitmap(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(target))
            {
                canvas.DrawBitmap(src, 0, 0);
            }
            return target;
        }

        public static SKBitmap ApplyTransparency(SKBitmap bmp, bool makeWhite, bool makeBlack)
        {
            if (!makeWhite && !makeBlack) return bmp;
            var span = bmp.Pixels;
            for (int i = 0; i < span.Length; i++)
            {
                var c = span[i];
                if ((makeWhite && c.Red > 240 && c.Green > 240 && c.Blue > 240) ||
                    (makeBlack && c.Red < 15 && c.Green < 15 && c.Blue < 15))
                {
                    span[i] = new SKColor(c.Red, c.Green, c.Blue, 0);
                }
            }
            bmp.Pixels = span;
            return bmp;
        }

        public static SKBitmap FitSquare(SKBitmap src, int size, bool stretch)
        {
            if (src == null) return null;
            var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
            var target = new SKBitmap(info);
            using (var surface = new SKCanvas(target))
            {
                surface.Clear(SKColors.Transparent);
                if (stretch)
                {
                    surface.DrawBitmap(src, new SKRect(0, 0, size, size));
                }
                else
                {
                    float ratio = Math.Min((float)size / src.Width, (float)size / src.Height);
                    int w = Math.Max(1, (int)(src.Width * ratio));
                    int h = Math.Max(1, (int)(src.Height * ratio));
                    int x = (size - w) / 2;
                    int y = (size - h) / 2;
                    using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High })
                    {
                        surface.DrawBitmap(src, new SKRect(x, y, x + w, y + h), paint);
                    }
                }
                surface.Flush();
            }
            return target;
        }

        public static SKBitmap RoundCorners(SKBitmap src, int radius)
        {
            if (src == null || radius <= 0) return src;
            var target = new SKBitmap(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var surface = new SKCanvas(target))
            using (var path = new SKPath())
            {
                surface.Clear(SKColors.Transparent);
                path.AddRoundRect(new SKRect(0, 0, src.Width, src.Height), radius, radius, SKPathDirection.Clockwise);
                surface.ClipPath(path, antialias: true);
                surface.DrawBitmap(src, 0, 0);
                surface.Flush();
            }
            return target;
        }

        public static Bitmap ToAvalonia(SKBitmap bmp)
        {
            if (bmp == null) return null;
            using (var image = SKImage.FromBitmap(bmp))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                var ms = new MemoryStream();
                data.SaveTo(ms);
                ms.Position = 0;
                return new Bitmap(ms);
            }
        }

        public static WindowIcon ToIcon(SKBitmap bmp)
        {
            var avaloniaBitmap = ToAvalonia(bmp);
            return avaloniaBitmap == null ? null : new WindowIcon(avaloniaBitmap);
        }
    }
}
