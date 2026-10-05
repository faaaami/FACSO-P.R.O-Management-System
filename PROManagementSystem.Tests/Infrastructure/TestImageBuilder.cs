using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Task 6B.4: raster-image fixtures for the local Tesseract OCR path.
/// Images are rendered in memory with a system font so Tesseract can be
/// exercised against the same shapes a user's scan would produce.
/// </summary>
public static class TestImageBuilder
{
    /// <summary>Rendered bitmap width; also the PDF XObject width used when a
    /// JPEG is embedded into a scanned-PDF fixture.</summary>
    public const int ScanWidth = 1024;

    /// <summary>Rendered bitmap height; also the PDF XObject height used when
    /// a JPEG is embedded into a scanned-PDF fixture.</summary>
    public const int ScanHeight = 320;

    /// <summary>Renders the phrase as large black-on-white PNG bytes.</summary>
    public static byte[] CreateTextPng(string text)
        => RenderAsByteArray(Format.Png, text);

    /// <summary>Renders the phrase as large black-on-white JPEG bytes.</summary>
    public static byte[] CreateTextJpeg(string text)
        => RenderAsByteArray(Format.Jpeg, text);

    /// <summary>An all-white PNG with no text for the empty-result path.</summary>
    public static byte[] CreateBlankPng()
        => RenderAsByteArray(Format.Png, null);

    /// <summary>An all-white JPEG with no text for the empty-result path.</summary>
    public static byte[] CreateBlankJpeg()
        => RenderAsByteArray(Format.Jpeg, null);

    /// <summary>
    /// A PNG whose encoded payload is cut short mid-stream, like a corrupt or
    /// interrupted download. Exercises the corrupt-image fail-soft policy.
    /// </summary>
    public static byte[] CreateTruncatedPng()
        => Truncate(CreateTextPng("TRUNC"));

    /// <summary>
    /// A JPEG whose encoded payload is cut short mid-stream, like a corrupt or
    /// interrupted download. Exercises the corrupt-image fail-soft policy.
    /// </summary>
    public static byte[] CreateTruncatedJpeg()
        => Truncate(CreateTextJpeg("TRUNC"));

    /// <summary>
    /// Multi-line document fixture JPEG rendered at a smaller font on a wider
    /// canvas so longer classification-indicator phrases fit within the raster
    /// width. Used for end-to-end OCR classification integration tests.
    /// </summary>
    public static byte[] CreateDocumentTextJpeg(string text)
        => RenderDocumentLines(text, Format.Jpeg);

    /// <summary>
    /// Multi-line document fixture PNG rendered at a smaller font on a wider
    /// canvas so longer classification-indicator phrases fit within the raster
    /// width. Used for end-to-end OCR classification integration tests.
    /// </summary>
    public static byte[] CreateDocumentTextPng(string text)
        => RenderDocumentLines(text, Format.Png);

    private static byte[] Truncate(byte[] bytes)
        => bytes[..(bytes.Length / 2)];

    private static byte[] RenderAsByteArray(Format format, string? text)
    {
        using var image = new Image<Rgba32>(ScanWidth, ScanHeight);
        image.Mutate(ctx => ctx.BackgroundColor(Color.White));

        if (!string.IsNullOrWhiteSpace(text))
        {
            image.Mutate(ctx => ctx.DrawText(
                new RichTextOptions(GetFont())
                {
                    Origin = new PointF(48, 64),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                },
                text,
                Color.Black));
        }

        using var buffer = new MemoryStream();

        if (format == Format.Png)
        {
            image.SaveAsPng(buffer);
        }
        else
        {
            image.SaveAsJpeg(buffer);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Renders multi-line text at a smaller font on a wider canvas so longer
    /// classification fixture phrases fit within the raster width (Task 6C).
    /// </summary>
    private static byte[] RenderDocumentLines(
        string text,
        Format format)
    {
        const float fontSize = 52f;
        const int canvasWidth = 1600;
        const int canvasHeight = 900;
        const int originX = 48;
        const int startY = 80;
        const int lineGap = 12;

        using var image = new Image<Rgba32>(canvasWidth, canvasHeight);
        image.Mutate(ctx => ctx.BackgroundColor(Color.White));

        if (!string.IsNullOrWhiteSpace(text))
        {
            var font = GetFontForSize(fontSize);
            var lines = text.Split('\n');
            var y = startY;

            image.Mutate(ctx =>
            {
                foreach (var rawLine in lines)
                {
                    var line = rawLine.TrimEnd();

                    if (line.Length > 0)
                    {
                        ctx.DrawText(
                            new RichTextOptions(font)
                            {
                                Origin = new PointF(originX, y),
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top
                            },
                            line,
                            Color.Black);
                    }

                    y += (int)(fontSize + lineGap);
                }
            });
        }

        using var buffer = new MemoryStream();

        if (format == Format.Png)
        {
            image.SaveAsPng(buffer);
        }
        else
        {
            image.SaveAsJpeg(buffer);
        }

        return buffer.ToArray();
    }

    private static Font GetFont()
        => GetFontForSize(88f);

    private static Font GetFontForSize(float size)
    {
        foreach (var family in new[] { "Arial", "Segoe UI", "Verdana", "Tahoma" })
        {
            if (SystemFonts.TryGet(family, out var resolved))
            {
                return resolved.CreateFont(size, FontStyle.Bold);
            }
        }

        var fallback = SystemFonts.Families.First();
        return fallback.CreateFont(size, FontStyle.Bold);
    }

    private enum Format
    {
        Png,
        Jpeg
    }
}