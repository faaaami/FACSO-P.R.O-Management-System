using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Synthetic identity-document images: a few printed header lines and, at
/// the bottom, MRZ lines in a monospace font, as on a passport photo page or
/// the back of an ID card. All content is fictional (ICAO specimens or
/// made-up values).
/// </summary>
public static class TestMrzImageBuilder
{
    public const int Width = 1800;
    public const int Height = 1150;

    public static byte[] Png(string[] headerLines, string[] mrzLines, int rotateDegrees = 0)
        => Render(headerLines, mrzLines, rotateDegrees, jpeg: false);

    public static byte[] Jpeg(string[] headerLines, string[] mrzLines, int rotateDegrees = 0)
        => Render(headerLines, mrzLines, rotateDegrees, jpeg: true);

    private static byte[] Render(string[] headerLines, string[] mrzLines, int rotateDegrees, bool jpeg)
    {
        using var image = new Image<Rgba32>(Width, Height);
        image.Mutate(ctx => ctx.BackgroundColor(Color.White));

        var textFont = Font(new[] { "Arial", "Segoe UI", "DejaVu Sans", "Liberation Sans" }, 46f, FontStyle.Bold);
        var mrzFont = Font(new[] { "Courier New", "Consolas", "DejaVu Sans Mono", "Liberation Mono" }, 50f, FontStyle.Bold);

        image.Mutate(ctx =>
        {
            var y = 70f;
            foreach (var line in headerLines)
            {
                ctx.DrawText(line, textFont, Color.Black, new PointF(70, y));
                y += 70;
            }

            var mrzTop = Height - 60 - (mrzLines.Length * 78f);
            foreach (var line in mrzLines)
            {
                ctx.DrawText(line, mrzFont, Color.Black, new PointF(40, mrzTop));
                mrzTop += 78;
            }

            if (rotateDegrees != 0)
            {
                ctx.Rotate(rotateDegrees);
            }
        });

        using var buffer = new MemoryStream();

        if (jpeg)
        {
            image.SaveAsJpeg(buffer);
        }
        else
        {
            image.SaveAsPng(buffer);
        }

        return buffer.ToArray();
    }

    private static Font Font(string[] families, float size, FontStyle style)
    {
        foreach (var family in families)
        {
            if (SystemFonts.TryGet(family, out var resolved))
            {
                return resolved.CreateFont(size, style);
            }
        }

        return SystemFonts.Families.First().CreateFont(size, style);
    }
}
