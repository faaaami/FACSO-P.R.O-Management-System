using ImageMagick;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;
using PROManagementSystem.Infrastructure.Extraction;
using PROManagementSystem.Infrastructure.Extraction.Mrz;
using Xunit;
using static PROManagementSystem.Tests.Extraction.ExtractionStubs;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// The Local extraction pipeline with stubbed OCR, PDF and AI: routing by
/// the selected type, the AI switch, failure messages, the OCR gate and
/// deadline, cancellation, and that nothing personal reaches the logs.
/// </summary>
public sealed class DocumentExtractionPipelineTests
{
    private static readonly string PassportMrzText =
        $"PASSPORT\n{MrzParserTests.Td3Line1}\n{MrzParserTests.Td3Line2}";

    private sealed class StubAi : IAiFieldExtractor
    {
        public AiFieldExtraction Result { get; set; } = AiFieldExtraction.Unavailable;
        public int Calls { get; private set; }

        public Task<AiFieldExtraction> ExtractAsync(string documentText, DocumentType documentType, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    /// <summary>OCR that waits for its token or a release, tracking concurrency.</summary>
    private sealed class GatedOcr : IImageOcrExtractor
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;

        public int MaxActive;

        public void Release() => _release.TrySetResult();

        public async Task<OcrTextExtractionResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _active);
            InterlockedMax(ref MaxActive, now);

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return new OcrTextExtractionResult(string.Empty, false);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            while ((current = target) < value &&
                   Interlocked.CompareExchange(ref target, value, current) != current)
            {
            }
        }
    }

    private sealed record Harness(
        DocumentExtractionPipeline Pipeline,
        StubPdfTextExtractor Pdf,
        StubPageRenderer Renderer,
        StubDetailsExtractor Labels,
        CapturingLogger<DocumentExtractionPipeline> Logger);

    private static Harness Create(
        IImageOcrExtractor ocr,
        StubAi? ai = null,
        int timeoutSeconds = 60,
        int maxConcurrentOcr = 2)
    {
        var options = Options.Create(new OcrOptions
        {
            OcrTimeoutSeconds = timeoutSeconds,
            MaxConcurrentOcr = maxConcurrentOcr
        });

        var images = new ImagePreprocessor(options);
        var pdf = new StubPdfTextExtractor();
        var renderer = new StubPageRenderer
        {
            RenderResult = _ => new PdfPageRenderResult(new MemoryStream(Photo()), 1200, 800)
        };
        var labels = new StubDetailsExtractor();
        var logger = new CapturingLogger<DocumentExtractionPipeline>();

        var pipeline = new DocumentExtractionPipeline(
            pdf,
            renderer,
            ocr,
            images,
            new MrzReader(ocr, images),
            labels,
            new DocumentTypeSchemaProvider(),
            new OcrWorkloadGate(options),
            options,
            logger,
            ai);

        return new Harness(pipeline, pdf, renderer, labels, logger);
    }

    private static byte[] Photo()
    {
        using var image = new MagickImage(MagickColors.White, 1200, 800);
        image.Format = MagickFormat.Jpeg;
        return image.ToByteArray();
    }

    private static Task<DocumentExtractionResult> Extract(
        Harness h, DocumentType type, string contentType = "image/jpeg", Stream? content = null, CancellationToken ct = default)
        => h.Pipeline.ExtractAsync(
            new DocumentExtractionRequest(content ?? new MemoryStream(Photo()), contentType, type), ct);

    // ------------------------------------------------------------------
    // Routing
    // ------------------------------------------------------------------

    [Fact(DisplayName = "An unsupported content type is reported unreadable without OCR")]
    public async Task UnsupportedType_Unreadable()
    {
        var ocr = new StubImageOcr();
        var h = Create(ocr);

        var result = await Extract(h, DocumentType.Passport, "text/plain");

        Assert.Equal(ExtractionFailures.ImageUnreadable, result.Failure?.Code);
        Assert.Equal(0, ocr.Calls);
        Assert.Equal(0, h.Pdf.Calls);
    }

    [Fact(DisplayName = "A passport PDF with the MRZ in its text layer needs no rendering or OCR")]
    public async Task PassportPdfTextLayer_NoOcr()
    {
        var ocr = new StubImageOcr();
        var h = Create(ocr);
        h.Pdf.Result = new PdfTextExtractionResult(PassportMrzText, true);

        var result = await Extract(h, DocumentType.Passport, "application/pdf", new MemoryStream(new byte[] { 1 }));

        Assert.Equal("L898902C3", result.Fields["documentNumber"].Value);
        Assert.Equal(ExtractedFieldSource.Mrz, result.Fields["documentNumber"].Source);
        Assert.Null(result.Failure);
        Assert.Equal(0, h.Renderer.RenderCalls);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact(DisplayName = "A scanned PDF is read from page 1 only")]
    public async Task ScannedPdf_Page1Only()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult(PassportMrzText, true) };
        var h = Create(ocr);

        var result = await Extract(h, DocumentType.Passport, "application/pdf", new MemoryStream(new byte[] { 1 }));

        Assert.Equal(new[] { 1 }, h.Renderer.RenderedPages);
        Assert.Equal("L898902C3", result.Fields["documentNumber"].Value);
    }

    [Fact(DisplayName = "Once the MRZ is read, no further OCR runs")]
    public async Task MrzRead_StopsOcr()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult(PassportMrzText, true) };
        var h = Create(ocr);

        await Extract(h, DocumentType.Passport);

        Assert.Equal(1, ocr.Calls);
    }

    [Fact(DisplayName = "MRZ passport numbers carry the reviewer note")]
    public async Task PassportNumber_HasReviewNote()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult(PassportMrzText, true) };

        var result = await Extract(Create(ocr), DocumentType.Passport);

        Assert.Contains(DocumentExtractionPipeline.PassportReviewNote, result.Fields["passportNumber"].Warnings);
        Assert.Contains(DocumentExtractionPipeline.PassportReviewNote, result.Fields["documentNumber"].Warnings);
    }

    [Fact(DisplayName = "An Indian passport number with a digit in the letter position is highlighted")]
    public async Task IndianPassport_LetterPositionHighlighted()
    {
        // Synthetic Indian passport whose leading letter was read as "1".
        // The check digits are computed on the misread value, as OCR would
        // produce them for an undetectable L/1 swap.
        const string number = "18369854<";
        const string birth = "900101";
        const string expiry = "310101";
        var line2 =
            number + MrzCheckDigit.Compute(number) +
            "IND" +
            birth + MrzCheckDigit.Compute(birth) +
            "M" +
            expiry + MrzCheckDigit.Compute(expiry) +
            new string('<', 14) + "0";
        line2 += MrzCheckDigit.Compute(line2[..10] + line2[13..20] + line2[21..43]);

        var line1 = "P<INDKUMAR<<RAVI".PadRight(44, '<');
        var text = $"{line1}\n{line2}";

        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult(text, true) };

        var result = await Extract(Create(ocr), DocumentType.Passport);

        var warnings = result.Fields["passportNumber"].Warnings;
        Assert.Contains(warnings, w => w.StartsWith("Character 1 should be a letter but is '1'"));
        Assert.Empty(result.Fields["passportNumber"].Errors);
    }

    // ------------------------------------------------------------------
    // Failures and AI switch
    // ------------------------------------------------------------------

    [Fact(DisplayName = "No MRZ on a passport: 'MRZ not found' with the photo-page hint")]
    public async Task NoMrz_Passport()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult("REPUBLIC OF UTOPIA", true) };

        var result = await Extract(Create(ocr), DocumentType.Passport);

        Assert.Equal(ExtractionFailures.MrzNotFound, result.Failure?.Code);
        Assert.Contains("passport photo page", result.Failure!.Message);
    }

    [Fact(DisplayName = "AI off: other types say automatic reading is not enabled")]
    public async Task AiOff_SaysDisabled()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult("Commercial licence", true) };
        var h = Create(ocr);

        var result = await Extract(h, DocumentType.TradeLicense);

        Assert.Equal(ExtractionFailures.AiDisabled, result.Failure?.Code);
        Assert.Single(h.Labels.Received);
    }

    [Fact(DisplayName = "AI on: values fill the form with source Ai and the primary number is mirrored")]
    public async Task AiOn_FillsValues()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult("Residence visa text", true) };
        var ai = new StubAi
        {
            Result = new AiFieldExtraction(true, new Dictionary<string, string>
            {
                ["visaNumber"] = "201/2026/7/123456",
                ["expiryDate"] = "2028-05-01",
                ["notInSchema"] = "dropped"
            })
        };

        var result = await Extract(Create(ocr, ai), DocumentType.Visa);

        Assert.Null(result.Failure);
        Assert.Equal(ExtractedFieldSource.Ai, result.Fields["visaNumber"].Source);
        Assert.Equal("201/2026/7/123456", result.Fields["documentNumber"].Value);
        Assert.False(result.Fields.ContainsKey("notInSchema"));
    }

    [Fact(DisplayName = "AI unavailable: 'reading service unavailable' message")]
    public async Task AiUnavailable_SaysUnavailable()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult("Residence visa text", true) };

        var result = await Extract(Create(ocr, new StubAi()), DocumentType.Visa);

        Assert.Equal(ExtractionFailures.AiUnavailable, result.Failure?.Code);
    }

    [Fact(DisplayName = "AI is not called when the MRZ was read")]
    public async Task AiNotCalled_WhenMrzRead()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult(PassportMrzText, true) };
        var ai = new StubAi();

        await Extract(Create(ocr, ai), DocumentType.Passport);

        Assert.Equal(0, ai.Calls);
    }

    // ------------------------------------------------------------------
    // Gate, deadline, cancellation, streams
    // ------------------------------------------------------------------

    [Fact(DisplayName = "The OCR deadline gives OCR_TIMEOUT instead of an error")]
    public async Task Deadline_GivesTimeout()
    {
        var ocr = new GatedOcr();

        var result = await Extract(Create(ocr, timeoutSeconds: 1), DocumentType.Visa);

        Assert.Equal(ExtractionFailures.OcrTimeout, result.Failure?.Code);
    }

    [Fact(DisplayName = "Caller cancellation propagates")]
    public async Task CallerCancellation_Propagates()
    {
        var ocr = new GatedOcr();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Extract(Create(ocr), DocumentType.Visa, ct: cts.Token));
    }

    [Fact(DisplayName = "Concurrent OCR is capped by the gate")]
    public async Task Gate_CapsConcurrency()
    {
        var ocr = new GatedOcr();
        var h = Create(ocr, maxConcurrentOcr: 1);

        var first = Extract(h, DocumentType.Visa);
        var second = Extract(h, DocumentType.Visa);

        await Task.Delay(500);
        Assert.Equal(1, ocr.MaxActive);

        ocr.Release();
        await Task.WhenAll(first, second);
        Assert.Equal(1, ocr.MaxActive);
    }

    [Fact(DisplayName = "An unexpected OCR failure is not swallowed")]
    public async Task UnexpectedOcrFailure_Propagates()
    {
        var ocr = new StubImageOcr { ExceptionToThrow = new InvalidOperationException("boom") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Extract(Create(ocr), DocumentType.Visa));
    }

    [Fact(DisplayName = "The caller's stream is never disposed")]
    public async Task CallerStream_NotDisposed()
    {
        var ocr = new StubImageOcr();
        var stream = new DisposeTrackingStream(Photo());

        await Extract(Create(ocr), DocumentType.Visa, content: stream);

        Assert.False(stream.IsDisposed);
    }

    // ------------------------------------------------------------------
    // Privacy (A7)
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Logs never contain recognised text, MRZ lines or extracted values")]
    public async Task Logs_ContainNoPersonalData()
    {
        var ocr = new StubImageOcr { Result = () => new OcrTextExtractionResult(PassportMrzText, true) };
        var h = Create(ocr);

        var result = await Extract(h, DocumentType.Passport);
        Assert.Equal("L898902C3", result.Fields["documentNumber"].Value);

        Assert.NotEmpty(h.Logger.Entries);
        foreach (var entry in h.Logger.Entries)
        {
            foreach (var secret in new[] { "L898902C3", "ERIKSSON", "ANNA", "1974", "740812", "P<UTO", "<<" })
            {
                Assert.DoesNotContain(secret, entry, StringComparison.Ordinal);
            }
        }
    }
}
