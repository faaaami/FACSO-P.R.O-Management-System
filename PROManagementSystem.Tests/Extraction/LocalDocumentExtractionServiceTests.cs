using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI;
using PROManagementSystem.Infrastructure.Extraction;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 6B.5: routing tests for <see cref="LocalDocumentExtractionService"/>.
/// A PDF with a usable text layer never touches the page renderer or OCR; a
/// scanned PDF (no text layer) falls through to the scanned-OCR service and is
/// rasterised; raster images go straight to the image OCR path. Every route
/// still ends in the neutral result so the confirmation flow owns suggestions.
/// Task 6B.7: heavy OCR runs under the workload gate and server deadline —
/// concurrent scans are capped, the gate queues competing operations, and a
/// server-side OCR timeout degrades to a neutral result while client
/// cancellation still propagates.
/// </summary>
public class LocalDocumentExtractionServiceTests
{
    private const string PdfContentType = "application/pdf";
    private const string JpegContentType = "image/jpeg";

    [Fact(DisplayName =
        "A selectable PDF never invokes the scanned-OCR path")]
    public async Task SelectablePdf_SkipsScannedOcr()
    {
        var renderer = new ExtractionStubs.StubPageRenderer();
        var imageOcr = new ExtractionStubs.StubImageOcr();
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult("PASS", true)
            },
            imageOcr,
            renderer,
            new ExtractionStubs.StubImageOcr());

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Equal(0, renderer.CountCalls);
        Assert.Equal(0, renderer.RenderCalls);
        Assert.Equal(0, imageOcr.Calls);
    }

    [Fact(DisplayName =
        "A scanned PDF falls through to the scanned-OCR path")]
    public async Task ScannedPdf_RoutesToScannedOcr()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 1,
            RenderResult = _ => new PdfPageRenderResult(
                new MemoryStream(),
                2550,
                3300)
        };
        var scannedOcr = new ExtractionStubs.StubImageOcr
        {
            Result = () => new OcrTextExtractionResult("PASS", true)
        };
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(string.Empty, false)
            },
            new ExtractionStubs.StubImageOcr(),
            renderer,
            scannedOcr);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Equal(1, renderer.CountCalls);
        Assert.Equal(1, renderer.RenderCalls);
        Assert.Equal(1, scannedOcr.Calls);
    }

    [Fact(DisplayName =
        "A raster image is routed straight to the image OCR path")]
    public async Task Image_RoutesToImageOcr()
    {
        var renderer = new ExtractionStubs.StubPageRenderer();
        var imageOcr = new ExtractionStubs.StubImageOcr();

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            imageOcr,
            renderer,
            new ExtractionStubs.StubImageOcr());

        var result = await service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Equal(1, imageOcr.Calls);
        Assert.Equal(0, renderer.CountCalls);
    }

    [Fact(DisplayName =
        "An unsupported content type invokes nothing and stays neutral")]
    public async Task UnsupportedContentType_InvokesNothing()
    {
        var renderer = new ExtractionStubs.StubPageRenderer();
        var imageOcr = new ExtractionStubs.StubImageOcr();
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            imageOcr,
            renderer,
            new ExtractionStubs.StubImageOcr());

        var result = await service.ExtractAsync(
            new MemoryStream(),
            "text/plain",
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Equal(0, renderer.CountCalls);
        Assert.Equal(0, imageOcr.Calls);
    }

    // ------------------------------------------------------------------
    // Failure behavior (Task 6B.6): only the declared fail-soft categories
    // are absorbed; unexpected failures must propagate untouched.
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "An unexpected PDF text-layer failure is not swallowed")]
    public async Task PdfTextUnexpectedFailure_Propagates()
    {
        var renderer = new ExtractionStubs.StubPageRenderer();
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                ExceptionToThrow = new InvalidOperationException("simulated")
            },
            new ExtractionStubs.StubImageOcr(),
            renderer,
            new ExtractionStubs.StubImageOcr());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractAsync(
                new MemoryStream(),
                PdfContentType,
                CancellationToken.None));

        Assert.Equal(0, renderer.CountCalls);
    }

    [Fact(DisplayName =
        "An unexpected image OCR failure is not swallowed")]
    public async Task ImageOcrUnexpectedFailure_Propagates()
    {
        var imageOcr = new ExtractionStubs.StubImageOcr
        {
            ExceptionToThrow = new InvalidOperationException("simulated")
        };
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            imageOcr,
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractAsync(
                new MemoryStream(),
                JpegContentType,
                CancellationToken.None));

        Assert.Equal(1, imageOcr.Calls);
    }

    [Fact(DisplayName =
        "An unexpected scanned-OCR failure is not swallowed")]
    public async Task ScannedPdfUnexpectedFailure_Propagates()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 1,
            RenderException = new InvalidOperationException("simulated")
        };
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(string.Empty, false)
            },
            new ExtractionStubs.StubImageOcr(),
            renderer,
            new ExtractionStubs.StubImageOcr());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractAsync(
                new MemoryStream(),
                PdfContentType,
                CancellationToken.None));
    }

    [Fact(DisplayName =
        "The caller's stream is never disposed when extraction fails")]
    public async Task CallerStream_IsNotDisposedOnFailure()
    {
        using var stream = new ExtractionStubs.DisposeTrackingStream(
            Array.Empty<byte>());
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                ExceptionToThrow = new InvalidOperationException("simulated")
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractAsync(
                stream,
                PdfContentType,
                CancellationToken.None));

        Assert.False(stream.IsDisposed);
        Assert.True(stream.CanRead);
    }

    // ------------------------------------------------------------------
    // Workload protection (Task 6B.7)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Concurrent OCR is capped by the gate (max active == MaxConcurrentOcr)")]
    public async Task ConcurrentOcr_IsCappedByGate()
    {
        var tracking = new ConcurrencyTrackingImageOcr(capacity: 2);
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            tracking,
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            new OcrOptions { MaxConcurrentOcr = 2 });

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => service.ExtractAsync(
                new MemoryStream(),
                JpegContentType,
                CancellationToken.None))
            .ToArray();

        // Two slots fill, then the remaining requests queue at the gate. Once
        // both slots are busy no further OCR can start (guaranteed by the
        // semaphore), so the extracted call count stays at exactly two
        // without any timing-based assertion.
        await tracking.SlotsFull;
        Assert.Equal(2, tracking.CallCount);

        tracking.ReleaseAll();

        await Task.WhenAll(tasks);

        Assert.Equal(5, tracking.CallCount);
        Assert.Equal(2, tracking.MaxConcurrent);
    }

    [Fact(DisplayName =
        "A second OCR waits for a slot instead of running concurrently")]
    public async Task SecondOcr_WaitsForAvailableSlot()
    {
        var blocking = new BlockingFirstCallImageOcr();
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            blocking,
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            new OcrOptions { MaxConcurrentOcr = 1 });

        var first = service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            CancellationToken.None);

        await blocking.FirstCallEntered;

        var second = service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            CancellationToken.None);

        // The single slot is still held by the first OCR; the second request
        // is queued at the gate so it cannot have reached the extractor yet.
        Assert.Equal(1, blocking.CallCount);

        blocking.ReleaseFirstCall();

        await Task.WhenAll(first, second);

        var log = blocking.Log;
        Assert.Contains("enter:1", log);
        Assert.Contains("enter:2", log);
        Assert.True(
            Array.IndexOf(log, "exit:1") < Array.IndexOf(log, "enter:2"),
            "The first OCR must complete before the second one starts.");
    }

    [Fact(DisplayName =
        "Waits for a gate slot respect caller cancellation")]
    public async Task GateWait_RespectsCancellation()
    {
        using var cancelledWaitToken = new CancellationTokenSource();
        var blocking = new BlockingFirstCallImageOcr();
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            blocking,
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            new OcrOptions { MaxConcurrentOcr = 1 });

        var first = service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            CancellationToken.None);

        await blocking.FirstCallEntered;

        var second = service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            cancelledWaitToken.Token);

        cancelledWaitToken.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => second);

        blocking.ReleaseFirstCall();
        await first;

        Assert.Equal(1, blocking.CallCount);
        AssertNeutral(await first);
    }

    [Fact(DisplayName =
        "A server-side OCR timeout degrades to a neutral result")]
    public async Task OcrTimeout_ReturnsNeutralResult()
    {
        var blocking = new BlockingFirstCallImageOcr();
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            blocking,
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            new OcrOptions { OcrTimeoutSeconds = 1 });

        var result = await service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Equal(1, blocking.CallCount);
    }

    [Fact(DisplayName =
        "Client/request cancellation still propagates mid-OCR")]
    public async Task ClientCancellation_StillPropagates()
    {
        using var clientToken = new CancellationTokenSource();
        var blocking = new BlockingFirstCallImageOcr();
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            blocking,
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            new OcrOptions { OcrTimeoutSeconds = 60 });

        var extract = service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            clientToken.Token);

        await blocking.FirstCallEntered;

        clientToken.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extract);
    }

    [Fact(DisplayName =
        "The OCR deadline does not apply to selectable-PDF text extraction")]
    public async Task Timeout_DoesNotApplyToTextExtractionSuccess()
    {
        var renderer = new ExtractionStubs.StubPageRenderer();
        var imageOcr = new ExtractionStubs.StubImageOcr();
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult("PASS", true)
            },
            imageOcr,
            renderer,
            new ExtractionStubs.StubImageOcr(),
            new OcrOptions { OcrTimeoutSeconds = 1 });

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Equal(0, renderer.CountCalls);
        Assert.Equal(0, imageOcr.Calls);
    }

    [Fact(DisplayName =
        "A scanned PDF completes through a single-slot gate without deadlocking")]
    public async Task GateNesting_DoesNotDeadlockScannedPath()
    {
        var renderer = new ExtractionStubs.StubPageRenderer
        {
            PageCount = 1,
            RenderResult = _ => new PdfPageRenderResult(
                new MemoryStream(),
                2550,
                3300)
        };
        var scannedOcr = new ExtractionStubs.StubImageOcr
        {
            Result = () => new OcrTextExtractionResult("PASS", true)
        };
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(string.Empty, false)
            },
            new ExtractionStubs.StubImageOcr(),
            renderer,
            scannedOcr,
            new OcrOptions { MaxConcurrentOcr = 1 });

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Equal(1, renderer.CountCalls);
        Assert.Equal(1, renderer.RenderCalls);
        Assert.Equal(1, scannedOcr.Calls);
    }

    // ------------------------------------------------------------------
    // Task 6C: classification wiring
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Selectable PDF text reaches the classifier and maps to the result")]
    public async Task SelectablePdf_TextReachesClassifier()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.Passport,
                0.75m)
        };

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(
                    "PASSPORT NATIONALITY",
                    true)
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal(0.75m, result.Confidence);
        Assert.Single(classifier.ReceivedTexts);
        Assert.Equal("PASSPORT NATIONALITY", classifier.ReceivedTexts[0]);
    }

    [Fact(DisplayName =
        "Image OCR text reaches the classifier and maps to the result")]
    public async Task ImageOcr_TextReachesClassifier()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.TradeLicense,
                0.625m)
        };

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            new ExtractionStubs.StubImageOcr
            {
                Result = () => new OcrTextExtractionResult(
                    "TRADE LICENSE",
                    true)
            },
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            CancellationToken.None);

        Assert.Equal(DocumentType.TradeLicense, result.Type);
        Assert.Equal(0.625m, result.Confidence);
        Assert.Single(classifier.ReceivedTexts);
        Assert.Equal("TRADE LICENSE", classifier.ReceivedTexts[0]);
    }

    [Fact(DisplayName =
        "Scanned PDF combined OCR text reaches the classifier")]
    public async Task ScannedPdf_CombinedTextReachesClassifier()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.EmiratesId,
                0.75m)
        };

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(
                    string.Empty,
                    false)
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer
            {
                PageCount = 1,
                RenderResult = _ => new PdfPageRenderResult(
                    new MemoryStream(),
                    2550,
                    3300)
            },
            new ExtractionStubs.StubImageOcr
            {
                Result = () => new OcrTextExtractionResult(
                    "EMIRATES ID",
                    true)
            },
            classifier: classifier);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        Assert.Equal(DocumentType.EmiratesId, result.Type);
        Assert.Equal(0.75m, result.Confidence);
        Assert.Single(classifier.ReceivedTexts);
        Assert.Equal("EMIRATES ID", classifier.ReceivedTexts[0]);
    }

    [Fact(DisplayName =
        "No usable text skips the classifier and stays neutral")]
    public async Task NoUsableText_SkipsClassifier()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.Passport,
                0.95m)
        };

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(
                    string.Empty,
                    false)
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Empty(classifier.ReceivedTexts);
    }

    [Fact(DisplayName =
        "Classifier returning null type stays neutral")]
    public async Task ClassifierNullResult_RemainsNeutral()
    {
        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            new ExtractionStubs.StubImageOcr
            {
                Result = () => new OcrTextExtractionResult(
                    "some text",
                    true)
            },
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr());

        var result = await service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            CancellationToken.None);

        AssertNeutral(result);
    }

    [Fact(DisplayName =
        "Classifier unexpected exception propagates")]
    public async Task ClassifierUnexpectedException_Propagates()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ExceptionToThrow = new InvalidOperationException(
                "classifier crash")
        };

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            new ExtractionStubs.StubImageOcr
            {
                Result = () => new OcrTextExtractionResult(
                    "text",
                    true)
            },
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractAsync(
                new MemoryStream(),
                JpegContentType,
                CancellationToken.None));

        Assert.Equal("classifier crash", ex.Message);
    }

    [Fact(DisplayName =
        "Unsupported content type invokes nothing and classifier is not called")]
    public async Task UnsupportedContentType_ClassifierNotCalled()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.Passport,
                0.95m)
        };

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            "text/plain",
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Empty(classifier.ReceivedTexts);
    }

    // ------------------------------------------------------------------
    // Task 7B.5: schema-driven field extraction wiring
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Details extractor runs on classified selectable-PDF text and its result is returned")]
    public async Task SelectablePdf_Classified_DetailsFlowToResult()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.Passport,
                0.75m)
        };

        var detailsExtractor = new ExtractionStubs.StubDetailsExtractor
        {
            ExtractImpl = (_, _) => new DocumentDetailsExtractionResult(
                new Dictionary<string, object?>
                {
                    ["passportNumber"] = "P-90321"
                })
        };

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(
                    "PASSPORT NUMBER: P-90321",
                    true)
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier,
            detailsExtractor: detailsExtractor);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Single(detailsExtractor.Received);
        Assert.Equal(
            "PASSPORT NUMBER: P-90321",
            detailsExtractor.Received[0].Text);
        Assert.Equal(DocumentType.Passport, detailsExtractor.Received[0].Type);
        Assert.NotNull(result.Details);
        Assert.Equal("P-90321", result.Details!["passportNumber"]);
    }

    [Fact(DisplayName =
        "An empty details extraction result leaves the result Details null")]
    public async Task EmptyDetailsResult_DetailsIsNull()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.Passport,
                0.75m)
        };

        var detailsExtractor = new ExtractionStubs.StubDetailsExtractor();

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(
                    "PASSPORT",
                    true)
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier,
            detailsExtractor: detailsExtractor);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Single(detailsExtractor.Received);
        Assert.Null(result.Details);
    }

    [Fact(DisplayName =
        "Details extractor is not invoked when classification yields no type")]
    public async Task NullClassification_SkipsDetailsExtractor()
    {
        var detailsExtractor = new ExtractionStubs.StubDetailsExtractor();

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor(),
            new ExtractionStubs.StubImageOcr
            {
                Result = () => new OcrTextExtractionResult(
                    "some text",
                    true)
            },
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            detailsExtractor: detailsExtractor);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            JpegContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Empty(detailsExtractor.Received);
    }

    [Fact(DisplayName =
        "Details extractor is not invoked when there is no usable text")]
    public async Task NoUsableText_SkipsDetailsExtractor()
    {
        var detailsExtractor = new ExtractionStubs.StubDetailsExtractor();

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(string.Empty, false)
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            detailsExtractor: detailsExtractor);

        var result = await service.ExtractAsync(
            new MemoryStream(),
            PdfContentType,
            CancellationToken.None);

        AssertNeutral(result);
        Assert.Empty(detailsExtractor.Received);
    }

    [Fact(DisplayName =
        "Details extractor unexpected exception propagates (no blanket catch)")]
    public async Task DetailsExtractorUnexpectedException_Propagates()
    {
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.Passport,
                0.75m)
        };

        var detailsExtractor = new ExtractionStubs.StubDetailsExtractor
        {
            ExceptionToThrow = new InvalidOperationException(
                "extractor crash")
        };

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(
                    "PASSPORT",
                    true)
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier,
            detailsExtractor: detailsExtractor);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractAsync(
                new MemoryStream(),
                PdfContentType,
                CancellationToken.None));

        Assert.Equal("extractor crash", ex.Message);
    }

    [Fact(DisplayName =
        "Details extractor honours caller cancellation")]
    public async Task DetailsExtractor_CancellationPropagates()
    {
        using var token = new CancellationTokenSource();
        var classifier = new ExtractionStubs.StubDocumentClassifier
        {
            ClassifyImpl = (_, _) => new DocumentClassificationResult(
                DocumentType.Passport,
                0.75m)
        };

        var detailsExtractor = new ExtractionStubs.StubDetailsExtractor();

        var service = CreateService(
            new ExtractionStubs.StubPdfTextExtractor
            {
                Result = new PdfTextExtractionResult(
                    "PASSPORT",
                    true)
            },
            new ExtractionStubs.StubImageOcr(),
            new ExtractionStubs.StubPageRenderer(),
            new ExtractionStubs.StubImageOcr(),
            classifier: classifier,
            detailsExtractor: detailsExtractor);

        token.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExtractAsync(
                new MemoryStream(),
                PdfContentType,
                token.Token));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static LocalDocumentExtractionService CreateService(
        IPdfTextExtractor pdfTextExtractor,
        IImageOcrExtractor imageOcr,
        IPdfPageRenderer renderer,
        IImageOcrExtractor scannedImageOcr,
        OcrOptions? options = null,
        IDocumentClassifier? classifier = null,
        IDocumentDetailsExtractor? detailsExtractor = null)
    {
        var ocrOptions = options ?? new OcrOptions();

        return new LocalDocumentExtractionService(
            pdfTextExtractor,
            imageOcr,
            new ScannedPdfOcrService(
                renderer,
                scannedImageOcr,
                Options.Create(ocrOptions),
                NullLogger<ScannedPdfOcrService>.Instance),
            classifier ?? new ExtractionStubs.StubDocumentClassifier(),
            detailsExtractor ?? new ExtractionStubs.StubDetailsExtractor(),
            new OcrWorkloadGate(Options.Create(ocrOptions)),
            Options.Create(ocrOptions));
    }

    private static void AssertNeutral(
        DocumentExtractionResult result)
    {
        Assert.Null(result.Type);
        Assert.Null(result.DocumentNumber);
        Assert.Null(result.IssueDate);
        Assert.Null(result.ExpiryDate);
        Assert.Null(result.OwnerType);
        Assert.Equal(0m, result.Confidence);
        Assert.Null(result.Details);
    }

    /// <summary>
    /// Image-OCR double used by the concurrency tests. Every call tracks the
    /// currently active count and records the highest concurrency reached; all
    /// calls suspend until <see cref="ReleaseAll"/> so the test can drain the
    /// queue deterministically. <see cref="SlotsFull"/> completes when the
    /// configured number of slots are simultaneously inside the extractor,
    /// which for a capped gate also proves the queued requests have not
    /// started.
    /// </summary>
    private sealed class ConcurrencyTrackingImageOcr : IImageOcrExtractor
    {
        private readonly int _capacity;
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _slotsFull =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;
        private int _active;
        private int _maxConcurrent;

        public ConcurrencyTrackingImageOcr(int capacity)
        {
            _capacity = capacity;
        }

        public int CallCount => Volatile.Read(ref _calls);

        public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);

        public Task SlotsFull => _slotsFull.Task;

        public Task<OcrTextExtractionResult> ExtractAsync(
            Stream imageStream,
            CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _active);
            Interlocked.Increment(ref _calls);

            var observed = _maxConcurrent;

            while (active > observed
                   && Interlocked.CompareExchange(
                       ref _maxConcurrent,
                       active,
                       observed) != observed)
            {
                observed = _maxConcurrent;
            }

            if (active >= _capacity)
            {
                _slotsFull.TrySetResult();
            }

            return WaitForReleaseAsync(cancellationToken);
        }

        private async Task<OcrTextExtractionResult> WaitForReleaseAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await _release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }

            return new OcrTextExtractionResult(string.Empty, false);
        }

        public void ReleaseAll()
        {
            _release.TrySetResult();
        }
    }

    /// <summary>
    /// Image-OCR double where the first call suspends until released (or
    /// cancelled) and every call records enter/exit events in order. Used to
    /// prove queued operations wait and that server timeouts / client
    /// cancellations surface through the linked-token pipeline.
    /// </summary>
    private sealed class BlockingFirstCallImageOcr : IImageOcrExtractor
    {
        private readonly TaskCompletionSource _firstEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string> _log = new();
        private int _calls;

        public int CallCount => Volatile.Read(ref _calls);

        public Task FirstCallEntered => _firstEntered.Task;

        public string[] Log => _log.ToArray();

        public Task<OcrTextExtractionResult> ExtractAsync(
            Stream imageStream,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _calls);
            _log.Enqueue($"enter:{call}");

            if (call == 1)
            {
                _firstEntered.TrySetResult();
            }

            return WaitForReleaseAsync(call, cancellationToken);
        }

        private async Task<OcrTextExtractionResult> WaitForReleaseAsync(
            int call,
            CancellationToken cancellationToken)
        {
            try
            {
                if (call == 1)
                {
                    await _release.Task.WaitAsync(cancellationToken);
                }

                return new OcrTextExtractionResult(string.Empty, false);
            }
            finally
            {
                _log.Enqueue($"exit:{call}");
            }
        }

        public void ReleaseFirstCall()
        {
            _release.TrySetResult();
        }
    }
}