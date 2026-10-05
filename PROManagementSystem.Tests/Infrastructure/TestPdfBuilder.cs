using System.Text;

namespace PROManagementSystem.Tests.Infrastructure;

/// <summary>
/// Deterministic, hand-built minimal PDF fixtures. Every fixture carries a
/// valid "xref/startxref/trailer" so PdfPig and PDFium parse it without
/// reconstruction, and all offsets are computed byte-exactly. Tasks 6B.3/6B.5:
/// selectable text pages, scanned-style pages (graphics only), scanned pages
/// that embed JPEG image XObjects like a real scanner output, and over-sized
/// scans for the page-limit policy.
/// </summary>
public static class TestPdfBuilder
{
    private const int MediaBoxWidth = 612;
    private const int MediaBoxHeight = 792;

    /// <summary>
    /// A PDF with one page per <paramref name="pageTexts"/>. Empty page text
    /// produces a page whose content stream shows nothing.
    /// </summary>
    public static byte[] CreateTextPdf(params string[] pageTexts)
        => CreatePdfWithContents(
            pageTexts.Select(text =>
                $"BT /F1 12 Tf 72 720 Td ({Escape(text)}) Tj ET").ToArray());

    /// <summary>
    /// A single-page "scanned-style" PDF whose content stream carries only
    /// graphics operators and therefore has no text layer.
    /// </summary>
    public static byte[] CreateScannedStylePdf()
        => CreatePdfWithContents("q Q");

    /// <summary>
    /// A single-page PDF whose only drawn text is whitespace.
    /// </summary>
    public static byte[] CreateWhitespaceTextPdf()
        => CreatePdfWithContents("BT /F1 12 Tf 72 720 Td (   ) Tj ET");

    /// <summary>
    /// A valid PDF whose page tree has zero pages.
    /// </summary>
    public static byte[] CreateEmptyPageTreePdf()
        => CreatePdfWithContents();

    /// <summary>
    /// Bytes that pass the "%PDF-" magic-byte check but are not a parseable
    /// PDF (used to prove malformed input never escapes the extractor).
    /// </summary>
    public static byte[] CreateMalformedPdf()
        => Encoding.ASCII.GetBytes(
            "%PDF-1.4\n"
            + "this file has no pdf structure at all, "
            + "only junk bytes after the header\n");

    /// <summary>
    /// A scanned PDF with one page per <paramref name="pageJpegs"/>. Each
    /// JPEG is embedded as an image XObject and drawn full-size, matching the
    /// output of a real scanner (no text layer, only a DCT-decoded raster).
    /// </summary>
    public static byte[] CreateScannedPdf(params byte[][] pageJpegs)
        => Build(CreateScannedObjects(pageJpegs));

    /// <summary>
    /// A one-page scanned PDF whose single image is blank white.
    /// </summary>
    public static byte[] CreateBlankScannedPdf()
        => CreateScannedPdf(TestImageBuilder.CreateBlankJpeg());

    /// <summary>
    /// A scanned PDF with <paramref name="pageCount"/> blank pages — used to
    /// prove the MaximumOcrPages policy skips an oversized scan before any
    /// page is rendered or OCR'd.
    /// </summary>
    public static byte[] CreateOverPageLimitScannedPdf(int pageCount)
        => CreateScannedPdf(
            Enumerable.Repeat(TestImageBuilder.CreateBlankJpeg(), pageCount)
                .ToArray());

    // ------------------------------------------------------------------
    // PDF construction
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds a 1-page-per-content-stream PDF. Object numbering:
    /// 1 Catalog, 2 Pages, 3..N page/content pairs, N+1 shared Type1 font.
    /// </summary>
    private static byte[] CreatePdfWithContents(params string[] streams)
    {
        var count = streams.Length;
        var fontObject = 2 * count + 3;

        var kids = string.Join(
            " ",
            Enumerable.Range(0, count).Select(k => $"{2 * k + 3} 0 R"));

        var objects = new List<byte[]>
        {
            Obj("<< /Type /Catalog /Pages 2 0 R >>"),
            Obj($"<< /Type /Pages /Kids [{kids}] /Count {count} >>")
        };

        for (var k = 0; k < count; k++)
        {
            objects.Add(Obj(
                $"<< /Type /Page /Parent 2 0 R /MediaBox " +
                $"[0 0 {MediaBoxWidth} {MediaBoxHeight}] " +
                $"/Contents {2 * k + 4} 0 R " +
                $"/Resources << /Font << /F1 {fontObject} 0 R >> >> >>"));
            objects.Add(Obj(
                $"<< /Length {Encoding.ASCII.GetByteCount(streams[k])} >>\n" +
                $"stream\n{streams[k]}\nendstream"));
        }

        objects.Add(
            Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        return Build(objects);
    }

    private static List<byte[]> CreateScannedObjects(byte[][] pageJpegs)
    {
        var kids = string.Join(
            " ",
            Enumerable.Range(0, pageJpegs.Length)
                .Select(k => $"{4 * k + 3} 0 R"));

        var objects = new List<byte[]>
        {
            Obj("<< /Type /Catalog /Pages 2 0 R >>"),
            Obj(
                $"<< /Type /Pages /Kids [{kids}] " +
                $"/Count {pageJpegs.Length} >>")
        };

        for (var k = 0; k < pageJpegs.Length; k++)
        {
            objects.Add(Obj(
                $"<< /Type /Page /Parent 2 0 R /MediaBox " +
                $"[0 0 {MediaBoxWidth} {MediaBoxHeight}] " +
                $"/Contents {4 * k + 4} 0 R " +
                $"/Resources << /XObject << /Im0 {4 * k + 5} 0 R >> >> >>"));

            var content =
                $"q {TestImageBuilder.ScanWidth} 0 0 {TestImageBuilder.ScanHeight} " +
                $"0 0 cm /Im0 Do Q";

            objects.Add(Obj(
                $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"));

            objects.Add(CreateImageObject(pageJpegs[k]));
        }

        return objects;
    }

    private static byte[] CreateImageObject(byte[] jpeg)
    {
        var header = Encoding.ASCII.GetBytes(
            "<< /Type /XObject /Subtype /Image /Width " +
            $"{TestImageBuilder.ScanWidth} /Height {TestImageBuilder.ScanHeight} " +
            "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode " +
            $"/Length {jpeg.Length} >>\nstream\n");

        using var image = new MemoryStream();

        image.Write(header, 0, header.Length);
        image.Write(jpeg, 0, jpeg.Length);

        var footer = Encoding.ASCII.GetBytes("\nendstream");
        image.Write(footer, 0, footer.Length);

        return image.ToArray();
    }

    private static byte[] Build(IReadOnlyList<byte[]> objects)
    {
        using var body = new MemoryStream();

        WriteAscii(body, "%PDF-1.4\n");

        var offsets = new long[objects.Count + 1];

        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = body.Length;

            WriteAscii(body, $"{i + 1} 0 obj\n");
            body.Write(objects[i], 0, objects[i].Length);
            WriteAscii(body, "\nendobj\n");
        }

        var startxref = body.Length;

        var xref = new StringBuilder();
        xref.Append($"xref\n0 {objects.Count + 1}\n");
        xref.Append("0000000000 65535 f \n");

        for (var i = 1; i <= objects.Count; i++)
        {
            xref.Append($"{offsets[i]:D10} {0:00000} n \n");
        }

        xref.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        xref.Append($"startxref\n{startxref}\n%%EOF\n");

        WriteAscii(body, xref.ToString());

        return body.ToArray();
    }

    private static byte[] Obj(string content)
        => Encoding.ASCII.GetBytes(content);

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string Escape(string text)
    {
        var escaped = text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");

        return escaped;
    }
}