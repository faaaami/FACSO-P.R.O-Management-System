using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PDFtoImage.Exceptions;
using PROManagementSystem.Infrastructure.Extraction;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 6B.5: unit tests for <see cref="ScannedPdfOcrService"/>. Fake page
/// renderer and OCR extractor drive every policy: text combined in page
/// order, blank pages skipped, the MaximumOcrPages skip policy (checked before
/// any rendering), the declared fail-soft categories (PDF/IOException) and the
/// cancellation contract. The final combined text is a plain return value —
/// nothing is written, extracted or exposed outside the pipeline here.
/// </summary>
public class ScannedPdfOcrServiceTests
{
    // ------------------------------------------------------------------
    // Text outcomes
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A single text page contributes its recognised text")]
    public async Task SingleTextPage_ReturnsRecognisedText()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 1,
            RenderResult = _ => FakeRender(new MemoryStream())
        };
        var ocr = new ExtractionStubs.StubImageOcr
        {
            Result = () => new OcrTextExtractionResult("DOC NUMBER 123", true)
        };

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.True(result.HasUsableText);
        Assert.Equal("DOC NUMBER 123", result.CombinedText);
    }

    [Fact(DisplayName =
        "Multi-page text is combined in page order")]
    public async Task MultiPageText_IsCombinedInPageOrder()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 2,
            RenderResult = _ => FakeRender(new MemoryStream())
        };
        var ocr = new ExtractionStubs.StubImageOcr
        {
            Result = () => new OcrTextExtractionResult("PASS", true)
        };

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.True(result.HasUsableText);
        Assert.Contains("PASS", result.CombinedText);
        Assert.Equal(2, ocr.Calls);
        Assert.Equal(new[] { 1, 2 }, renderer.RenderedPages);
    }

    [Fact(DisplayName =
        "Blank pages are skipped and do not contribute text")]
    public async Task BlankPages_AreSkipped()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 3,
            RenderResult = _ => FakeRender(new MemoryStream())
        };
        var ocr = new ExtractionStubs.StubImageOcr
        {
            Result = () => new OcrTextExtractionResult(" ", false)
        };

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.CombinedText);
        Assert.Equal(3, ocr.Calls);
    }

    [Fact(DisplayName =
        "A null or unreadable stream resolves to an empty result")]
    public async Task InvalidStream_ReturnsEmpty()
    {
        var renderer = new ExtractionStubs.StubPageRenderer();
        var service = CreateService(renderer, new ExtractionStubs.StubImageOcr());

        var nullResult = await service.OcrAsync(null!);
        var unreadableResult = await service.OcrAsync(
            new ExtractionStubs.UnreadableStream());

        var empty = new ScannedPdfOcrResult(string.Empty, false);

        Assert.Equal(empty, nullResult);
        Assert.Equal(empty, unreadableResult);
        Assert.Equal(0, renderer.CountCalls);
    }

    // ------------------------------------------------------------------
    // MaximumOcrPages skip policy
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A scan above MaximumOcrPages is skipped before any page renders")]
    public async Task OverPageLimit_IsSkippedWithoutRendering()
    {
        var renderer = new ExtractionStubs.StubPageRenderer { PageCount = 21 };
        var ocr = new ExtractionStubs.StubImageOcr();

        var service = CreateService(
            renderer,
            ocr,
            new OcrOptions { MaximumOcrPages = 20 });

        var result = await service.OcrAsync(new MemoryStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.CombinedText);
        Assert.Equal(1, renderer.CountCalls);
        Assert.Equal(0, renderer.RenderCalls);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact(DisplayName =
        "A scan at or below MaximumOcrPages is OCR'd normally")]
    public async Task WithinPageLimit_IsOcrNormal()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 20,
            RenderResult = _ => FakeRender(new MemoryStream())
        };
        var ocr = new ExtractionStubs.StubImageOcr
        {
            Result = () => new OcrTextExtractionResult("PASS", true)
        };

        var service = CreateService(
            renderer,
            ocr,
            new OcrOptions { MaximumOcrPages = 20 });

        var result = await service.OcrAsync(new MemoryStream());

        Assert.True(result.HasUsableText);
        Assert.Equal(20, renderer.RenderCalls);
        Assert.Equal(20, ocr.Calls);
    }

    [Fact(DisplayName =
        "MaximumOcrPages of zero disables the skip policy")]
    public async Task ZeroPageLimit_DisablesTheSkipPolicy()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 30,
            RenderResult = _ => FakeRender(new MemoryStream())
        };
        var ocr = new ExtractionStubs.StubImageOcr
        {
            Result = () => new OcrTextExtractionResult("PASS", true)
        };

        var service = CreateService(
            renderer,
            ocr,
            new OcrOptions { MaximumOcrPages = 0 });

        var result = await service.OcrAsync(new MemoryStream());

        Assert.True(result.HasUsableText);
        Assert.Equal(30, renderer.RenderCalls);
    }

    // ------------------------------------------------------------------
    // Fail-soft policy
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A PDF that cannot be opened resolves to an empty result")]
    public async Task UnopenablePdf_ReturnsEmpty()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            CountException = new PdfInvalidFormatException()
        };
        var ocr = new ExtractionStubs.StubImageOcr();

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact(DisplayName =
        "A stream that fails while being read resolves to an empty result")]
    public async Task UnreadableStream_ReturnsEmpty()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            CountException = new IOException("simulated")
        };
        var ocr = new ExtractionStubs.StubImageOcr();

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact(DisplayName =
        "A page that fails to render is skipped but the rest continue")]
    public async Task FailedRenderPage_IsSkippedAndRemainingPagesContinue()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 2,
            RenderException = new PdfPageNotFoundException()
        };

        var service = CreateService(renderer, new ExtractionStubs.StubImageOcr());

        var result = await service.OcrAsync(new MemoryStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(0, renderer.RenderCalls);
        Assert.Empty(renderer.RenderedPages);
    }

    [Fact(DisplayName =
        "A page OCR failure is skipped but remaining pages continue")]
    public async Task FailedOcrPage_IsSkippedAndRemainingPagesContinue()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 2,
            RenderResult = _ => FakeRender(new MemoryStream())
        };
        var ocr = new ExtractionStubs.StubImageOcr
        {
            ExceptionToThrow = new IOException("simulated")
        };

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(2, renderer.RenderCalls);
        Assert.Equal(2, ocr.Calls);
    }

    [Fact(DisplayName =
        "Unexpected renderer failures are not swallowed")]
    public async Task UnexpectedFailure_IsNotSwallowed()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 1,
            RenderException = new InvalidOperationException("simulated")
        };
        var service = CreateService(renderer, new ExtractionStubs.StubImageOcr());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.OcrAsync(new MemoryStream()));
    }

    [Fact(DisplayName =
        "An encrypted PDF resolves to an empty result (declared PdfException category)")]
    public async Task EncryptedPdf_ReturnsEmpty()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            CountException = new PdfPasswordProtectedException()
        };
        var ocr = new ExtractionStubs.StubImageOcr();

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.False(result.HasUsableText);
        Assert.Equal(string.Empty, result.CombinedText);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact(DisplayName =
        "A page that fails to render bails only that page; later successes survive")]
    public async Task PartialRenderFailure_PreservesSuccessfulPages()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 2,
            AsyncRender = (page, ct) =>
                page == 1
                    ? throw new PdfPageNotFoundException()
                    : Task.FromResult(FakeRender(new MemoryStream()))
        };
        var ocr = new ExtractionStubs.StubImageOcr
        {
            Result = () => new OcrTextExtractionResult("SURVIVOR", true)
        };

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.True(result.HasUsableText);
        Assert.Contains("SURVIVOR", result.CombinedText);
        Assert.Equal(new[] { 1, 2 }, renderer.RenderedPages);
        Assert.Equal(1, ocr.Calls);
    }

    [Fact(DisplayName =
        "A page whose OCR fails bails only that page; later successes survive")]
    public async Task PartialOcrFailure_PreservesSuccessfulPages()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 2,
            RenderResult = _ => FakeRender(new MemoryStream())
        };
        var ocr = new ExtractionStubs.StubImageOcr();
        ocr.Result = () => ocr.Calls == 1
            ? throw new IOException("simulated")
            : new OcrTextExtractionResult("SURVIVOR", true);

        var service = CreateService(renderer, ocr);

        var result = await service.OcrAsync(new MemoryStream());

        Assert.True(result.HasUsableText);
        Assert.Contains("SURVIVOR", result.CombinedText);
        Assert.Equal(2, renderer.RenderCalls);
        Assert.Equal(2, ocr.Calls);
    }

    [Fact(DisplayName =
        "Unexpected OCR failures are not swallowed")]
    public async Task UnexpectedOcrFailure_IsNotSwallowed()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 1,
            RenderResult = _ => FakeRender(new MemoryStream())
        };
        var ocr = new ExtractionStubs.StubImageOcr
        {
            ExceptionToThrow = new InvalidOperationException("simulated")
        };

        var service = CreateService(renderer, ocr);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.OcrAsync(new MemoryStream()));
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Cancellation propagates as OperationCanceledException")]
    public async Task CancelledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = CreateService(
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.OcrAsync(new MemoryStream(), cts.Token));
    }

    [Fact(DisplayName =
        "Cancellation between pages stops the loop")]
    public async Task CancellationBetweenPages_ThrowsOperationCanceled()
    {
        var firstPageRendered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var renderGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 3,
            AsyncRender = async (page, ct) =>
            {
                if (page == 1)
                {
                    firstPageRendered.TrySetResult();
                    await renderGate.Task;
                }
                return FakeRender(new MemoryStream());
            }
        };
        var service = CreateService(renderer, new ExtractionStubs.StubImageOcr());

        using var cts = new CancellationTokenSource();

        var run = service.OcrAsync(new MemoryStream(), cts.Token);

        await firstPageRendered.Task;
        cts.Cancel();
        renderGate.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PdfPageRenderResult FakeRender(Stream imageStream)
        => new(imageStream, 2550, 3300);

    private static ScannedPdfOcrService CreateService(
        IPdfPageRenderer renderer,
        IImageOcrExtractor imageOcr,
        OcrOptions? options = null)
        => new(
            renderer,
            imageOcr,
            Options.Create(options ?? new OcrOptions()),
            NullLogger<ScannedPdfOcrService>.Instance);
}