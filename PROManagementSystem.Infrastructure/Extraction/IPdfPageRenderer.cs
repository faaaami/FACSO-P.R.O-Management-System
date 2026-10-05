namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// Rasterises the pages of a scanned PDF so the OCR pipeline can recognise
/// their embedded images. The renderer never owns the caller's PDF stream:
/// it buffers and reads from it without disposing or repositioning it.
/// </summary>
public interface IPdfPageRenderer
{
    /// <summary>
    /// Returns the number of pages in the PDF at <paramref name="pdfStream"/>.
    /// </summary>
    Task<int> GetPageCountAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders <paramref name="pageNumber"/> (one-based) of the PDF at
    /// <paramref name="pdfStream"/> into a PNG-encoded
    /// <see cref="PdfPageRenderResult.ImageStream"/> and reports the bitmap
    /// dimensions in <see cref="PdfPageRenderResult.Width"/> /
    /// <see cref="PdfPageRenderResult.Height"/>.
    /// </summary>
    Task<PdfPageRenderResult> RenderPageAsync(
        Stream pdfStream,
        int pageNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A rendered PDF page. <see cref="ImageStream"/> is a PNG-encoded,
/// read-positioned stream that the caller owns and must dispose; the renderer
/// keeps ownership of the input PDF stream instead.
/// </summary>
public sealed record PdfPageRenderResult(
    Stream ImageStream,
    int Width,
    int Height);