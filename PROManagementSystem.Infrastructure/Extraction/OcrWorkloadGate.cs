using Microsoft.Extensions.Options;

namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>
/// Process-wide gate that caps the number of simultaneous heavy OCR
/// operations (image Tesseract recognition and scanned-PDF rasterisation +
/// per-page OCR) for the Local extraction provider. The gate is acquired once
/// around each complete heavy operation in
/// <see cref="PROManagementSystem.Infrastructure.AI.LocalDocumentExtractionService"/>
/// and is intentionally NOT used inside the individual extractor, renderer or
/// scanned-PDF service, so a single operation can never re-enter the gate and
/// self-deadlock. <see cref="OcrOptions.MaxConcurrentOcr"/> of 0 (or negative)
/// disables the gate entirely: <see cref="WaitAsync"/> completes immediately
/// and no semaphore is allocated.
/// </summary>
public sealed class OcrWorkloadGate : IDisposable
{
    private readonly SemaphoreSlim? _semaphore;

    public OcrWorkloadGate(IOptions<OcrOptions> options)
    {
        var maxConcurrentOcr = options.Value.MaxConcurrentOcr;

        if (maxConcurrentOcr > 0)
        {
            _semaphore = new SemaphoreSlim(
                maxConcurrentOcr,
                maxConcurrentOcr);
        }
    }

    /// <summary>
    /// Waits until a heavy-OCR slot is available, then marks it busy. When the
    /// gate is disabled this completes immediately without allocating a slot.
    /// </summary>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_semaphore is null)
        {
            return Task.CompletedTask;
        }

        return _semaphore.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Releases a heavy-OCR slot acquired by <see cref="WaitAsync"/>. Calling
    /// this on a disabled gate is a no-op.
    /// </summary>
    public void Release()
    {
        _semaphore?.Release();
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}