using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Application.Common.FileValidation;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Extraction.Mrz;

namespace PROManagementSystem.Infrastructure.Extraction;

/// <summary>Extraction:Ai settings.</summary>
public sealed class ExtractionAiOptions
{
    public const string SectionName = "Extraction:Ai";

    /// <summary>
    /// Off by default. When off, nothing is sent to an AI service: passports
    /// and Emirates IDs are read from the MRZ/OCR, other types are entered by
    /// hand.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// The "Local" extraction provider. For the document type staff selected:
/// <list type="number">
/// <item>Recover input: a PDF's text layer, otherwise PDF page 1 or the
/// photo, normalised by <see cref="IImagePreprocessor"/>.</item>
/// <item>Passport / Emirates ID: read the MRZ (check digits verified per
/// field). Emirates ID without an MRZ falls back to a 784 number found in
/// the OCR text.</item>
/// <item>Other types: labelled values found in the text; AI if enabled.</item>
/// <item>Validate every value (formats, dates) into per-field errors and
/// warnings, and explain any failure in words for staff.</item>
/// </list>
/// Heavy work (image preparation, OCR) holds one <see cref="OcrWorkloadGate"/>
/// slot under the <see cref="OcrOptions.OcrTimeoutSeconds"/> deadline; a
/// deadline expiry becomes OCR_TIMEOUT while caller cancellation propagates.
/// Logs carry only the type, duration and outcome code: never text, values
/// or images.
/// </summary>
public sealed class DocumentExtractionPipeline : IDocumentExtractionService
{
    private const string CheckDigitError =
        "Check digit does not match. Check this value against the document.";

    public const string PassportReviewNote =
        "Read from MRZ – compare with the number printed on the passport.";

    private const string CompositeWarning =
        "The MRZ overall check failed. Verify all values against the document.";

    private readonly IPdfTextExtractor _pdfText;
    private readonly IPdfPageRenderer _pdfRenderer;
    private readonly IImageOcrExtractor _ocr;
    private readonly IImagePreprocessor _images;
    private readonly MrzReader _mrzReader;
    private readonly IDocumentDetailsExtractor _labelledValues;
    private readonly IDocumentTypeSchemaProvider _schemas;
    private readonly OcrWorkloadGate _gate;
    private readonly OcrOptions _options;
    private readonly IAiFieldExtractor? _ai;
    private readonly ILogger<DocumentExtractionPipeline> _logger;

    public DocumentExtractionPipeline(
        IPdfTextExtractor pdfText,
        IPdfPageRenderer pdfRenderer,
        IImageOcrExtractor ocr,
        IImagePreprocessor images,
        MrzReader mrzReader,
        IDocumentDetailsExtractor labelledValues,
        IDocumentTypeSchemaProvider schemas,
        OcrWorkloadGate gate,
        IOptions<OcrOptions> options,
        ILogger<DocumentExtractionPipeline> logger,
        IAiFieldExtractor? ai = null)
    {
        _pdfText = pdfText;
        _pdfRenderer = pdfRenderer;
        _ocr = ocr;
        _images = images;
        _mrzReader = mrzReader;
        _labelledValues = labelledValues;
        _schemas = schemas;
        _gate = gate;
        _options = options.Value;
        _ai = ai;
        _logger = logger;
    }

    public async Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var result = await ExtractCoreAsync(request, cancellationToken);

        _logger.LogInformation(
            "Extraction for {DocumentType} finished in {ElapsedMs} ms: {Outcome}, {ReadFields} field(s) read.",
            request.DocumentType,
            stopwatch.ElapsedMilliseconds,
            result.Failure?.Code ?? "OK",
            result.Fields.Count(pair => pair.Value.Value is not null));

        return result;
    }

    private async Task<DocumentExtractionResult> ExtractCoreAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var type = request.DocumentType;
        var isPdf = request.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
        var isImage = request.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                      || request.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
                      || FileUploadPolicy.IsHeic(request.ContentType);

        if (!isPdf && !isImage)
        {
            return DocumentExtractionResult.Empty(type, ExtractionFailures.Unreadable());
        }

        var mrzFormat = MrzFormatFor(type);
        string? text = null;
        MrzResult? mrz = null;

        if (isPdf)
        {
            var layer = await _pdfText.ExtractAsync(request.Content, cancellationToken);
            Rewind(request.Content);

            if (layer.HasUsableText)
            {
                text = layer.Text;

                if (mrzFormat is { } format)
                {
                    mrz = MrzParser.FindAndParse(text, format);
                }
            }
        }

        var needsImage = text is null || (mrzFormat is not null && mrz is null);
        var heavy = HeavyResult.Skipped;

        if (needsImage)
        {
            heavy = await RunHeavyAsync(
                token => ReadImageAsync(request, isPdf, mrzFormat, needsText: text is null, token),
                cancellationToken);
        }

        if (heavy.Image?.Problem == ImagePreparationProblem.Unreadable && text is null)
        {
            return DocumentExtractionResult.Empty(type, ExtractionFailures.Unreadable());
        }

        mrz ??= heavy.Mrz;
        text ??= heavy.Text;

        var fields = new Dictionary<string, ExtractedField>();

        if (mrz is not null)
        {
            AddMrzFields(fields, type, mrz);
        }
        else if (type == DocumentType.EmiratesId && EmiratesIdNumber.FindInText(text) is { } idNumber)
        {
            fields["idNumber"] = ExtractedField.Read(idNumber, ExtractedFieldSource.Ocr);
            fields[DocumentExtractionResult.DocumentNumberKey] =
                ExtractedField.Read(idNumber, ExtractedFieldSource.Ocr);
        }

        if (mrzFormat is null && text is not null)
        {
            await AddLabelledValuesAsync(fields, type, text, cancellationToken);
        }

        var aiOutcome = AiOutcome.NotUsed;

        if (_ai is not null && text is not null && (mrzFormat is null || mrz is null))
        {
            aiOutcome = await AddAiValuesAsync(fields, type, text, cancellationToken);
        }

        Validate(fields, type);

        return new DocumentExtractionResult(
            type,
            fields,
            Failure(type, mrzFormat, mrz, text, heavy, aiOutcome, fields));
    }

    private async Task<HeavyRead> ReadImageAsync(
        DocumentExtractionRequest request,
        bool isPdf,
        MrzFormat? mrzFormat,
        bool needsText,
        CancellationToken token)
    {
        PreparedImage image;

        if (isPdf)
        {
            PdfPageRenderResult page;

            try
            {
                page = await _pdfRenderer.RenderPageAsync(request.Content, 1, token);
            }
            finally
            {
                Rewind(request.Content);
            }

            await using (page.ImageStream)
            {
                image = await _images.PrepareAsync(page.ImageStream, token);
            }
        }
        else
        {
            image = await _images.PrepareAsync(request.Content, token);
            Rewind(request.Content);
        }

        if (image.Problem == ImagePreparationProblem.Unreadable)
        {
            return new HeavyRead(image, null, null);
        }

        MrzResult? mrz = null;

        if (mrzFormat is { } format)
        {
            mrz = await _mrzReader.ReadAsync(image, format, token);
        }

        string? text = null;

        // Full-page text is needed when there is no text layer and either the
        // type has no MRZ or the MRZ could not be read (fallbacks use it).
        if (needsText && (mrzFormat is null || mrz is null))
        {
            await using var png = new MemoryStream(image.Png, writable: false);
            var ocr = await _ocr.ExtractAsync(png, token);

            if (ocr.HasUsableText)
            {
                text = ocr.Text;
                mrz ??= mrzFormat is { } f ? MrzParser.FindAndParse(text, f) : null;
            }
        }

        return new HeavyRead(image, mrz, text);
    }

    private static void AddMrzFields(
        Dictionary<string, ExtractedField> fields,
        DocumentType type,
        MrzResult mrz)
    {
        var compositeWarnings = mrz.CompositeCheckValid
            ? Array.Empty<string>()
            : new[] { CompositeWarning };

        void Set(string key, MrzField field, IReadOnlyList<string>? warnings = null)
            => fields[key] = ExtractedField.Read(
                field.Value,
                ExtractedFieldSource.Mrz,
                field.CheckDigitValid == false ? new[] { CheckDigitError } : null,
                warnings);

        void SetText(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields[key] = ExtractedField.Read(value, ExtractedFieldSource.Mrz);
            }
        }

        if (type == DocumentType.EmiratesId)
        {
            Set("cardNumber", mrz.DocumentNumber);

            var idNumber = EmiratesIdNumber.Normalize(DigitsOnly(mrz.OptionalData1));
            var idField = new MrzField(idNumber, mrz.CompositeCheckValid ? null : false);

            Set("idNumber", idField);
            Set(DocumentExtractionResult.DocumentNumberKey, idField);
        }
        else
        {
            // Check digits cannot detect every OCR look-alike (L vs 1, I vs
            // 8, S vs 8, …), so an MRZ passport number is never presented as
            // fully verified. Known per-country layouts add specific hints.
            var numberWarnings = new List<string> { PassportReviewNote };
            numberWarnings.AddRange(
                PassportNumberFormats.Warnings(mrz.DocumentNumber.Value, mrz.IssuingState));

            Set("passportNumber", mrz.DocumentNumber, numberWarnings);
            Set(
                DocumentExtractionResult.DocumentNumberKey,
                mrz.DocumentNumber,
                numberWarnings.Concat(compositeWarnings).ToList());
        }

        Set("dateOfBirth", mrz.DateOfBirth);
        Set("expiryDate", mrz.ExpiryDate);
        SetText("surname", mrz.Surname);
        SetText("givenNames", mrz.GivenNames);
        SetText("nationality", mrz.Nationality);
        SetText("sex", mrz.Sex == "X" ? string.Empty : mrz.Sex);
    }

    private async Task AddLabelledValuesAsync(
        Dictionary<string, ExtractedField> fields,
        DocumentType type,
        string text,
        CancellationToken cancellationToken)
    {
        var found = await _labelledValues.ExtractAsync(text, type, cancellationToken);

        foreach (var (key, value) in found.Details)
        {
            if (value is not null && !fields.ContainsKey(key))
            {
                fields[key] = ExtractedField.Read(
                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                    ExtractedFieldSource.Ocr);
            }
        }

        MirrorPrimaryNumber(fields, type);
    }

    private async Task<AiOutcome> AddAiValuesAsync(
        Dictionary<string, ExtractedField> fields,
        DocumentType type,
        string text,
        CancellationToken cancellationToken)
    {
        var ai = await _ai!.ExtractAsync(text, type, cancellationToken);

        if (!ai.Available)
        {
            return AiOutcome.Unavailable;
        }

        var allowed = _schemas.Get(type).Select(f => f.Key)
            .Append(DocumentExtractionResult.DocumentNumberKey)
            .Append(DocumentExtractionResult.IssueDateKey)
            .Append(DocumentExtractionResult.ExpiryDateKey)
            .ToHashSet(StringComparer.Ordinal);

        // AI only fills gaps: values read from the MRZ or the text win.
        foreach (var (key, value) in ai.Values)
        {
            if (allowed.Contains(key) && !fields.ContainsKey(key))
            {
                fields[key] = ExtractedField.Read(value, ExtractedFieldSource.Ai);
            }
        }

        MirrorPrimaryNumber(fields, type);
        return AiOutcome.Used;
    }

    /// <summary>
    /// The document number shown on the form is the type's primary
    /// identifier (passport number, visa number, …) when only one was read.
    /// </summary>
    private void MirrorPrimaryNumber(Dictionary<string, ExtractedField> fields, DocumentType type)
    {
        var primary = _schemas.Get(type).FirstOrDefault(f => f.Required)?.Key;

        if (primary is null)
        {
            return;
        }

        if (!fields.ContainsKey(DocumentExtractionResult.DocumentNumberKey) &&
            fields.TryGetValue(primary, out var value))
        {
            fields[DocumentExtractionResult.DocumentNumberKey] = value;
        }
        else if (!fields.ContainsKey(primary) &&
                 fields.TryGetValue(DocumentExtractionResult.DocumentNumberKey, out var number))
        {
            fields[primary] = number;
        }
    }

    private static void Validate(Dictionary<string, ExtractedField> fields, DocumentType type)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly? dateOfBirth = null;

        foreach (var key in fields.Keys.ToList())
        {
            var field = fields[key];
            var errors = field.Errors.ToList();
            var warnings = field.Warnings.ToList();

            if (field.Value is not null &&
                key is "dateOfBirth" or DocumentExtractionResult.IssueDateKey or DocumentExtractionResult.ExpiryDateKey)
            {
                if (!DateOnly.TryParseExact(field.Value, "yyyy-MM-dd", out var date))
                {
                    errors.Add("This is not a valid date.");
                }
                else if (key == DocumentExtractionResult.ExpiryDateKey && date < today)
                {
                    warnings.Add("This document has expired.");
                }
                else if (key != DocumentExtractionResult.ExpiryDateKey && date > today)
                {
                    errors.Add("This date is in the future.");
                }
                else if (key == "dateOfBirth")
                {
                    dateOfBirth = date;
                }
            }

            fields[key] = field with { Errors = errors, Warnings = warnings };
        }

        if (type != DocumentType.EmiratesId)
        {
            return;
        }

        foreach (var key in new[] { "idNumber", DocumentExtractionResult.DocumentNumberKey })
        {
            if (!fields.TryGetValue(key, out var field) || field.Value is null)
            {
                continue;
            }

            if (!EmiratesIdNumber.IsValidFormat(field.Value))
            {
                fields[key] = field with { Errors = field.Errors.Append(EmiratesIdNumber.FormatMessage).ToList() };
            }
            else
            {
                fields[key] = field with
                {
                    Value = EmiratesIdNumber.Normalize(field.Value),
                    Warnings = field.Warnings.Concat(EmiratesIdNumber.Warnings(field.Value, dateOfBirth)).ToList()
                };
            }
        }
    }

    private static ExtractionFailure? Failure(
        DocumentType type,
        MrzFormat? mrzFormat,
        MrzResult? mrz,
        string? text,
        HeavyResult heavy,
        AiOutcome ai,
        Dictionary<string, ExtractedField> fields)
    {
        var anyValue = fields.Values.Any(f => f.Value is not null);

        if (heavy.TimedOut && !anyValue)
        {
            return ExtractionFailures.Timeout();
        }

        if (mrzFormat is not null)
        {
            if (mrz is null)
            {
                return heavy.Image?.Problem == ImagePreparationProblem.TooSmall
                    ? ExtractionFailures.TooSmall()
                    : ExtractionFailures.MrzNotFoundFor(type);
            }

            return mrz.AllChecksValid ? null : ExtractionFailures.MrzCheckFailedFailure();
        }

        if (text is null)
        {
            return heavy.Image?.Problem == ImagePreparationProblem.TooSmall
                ? ExtractionFailures.TooSmall()
                : ExtractionFailures.Nothing();
        }

        if (anyValue)
        {
            return null;
        }

        return ai switch
        {
            AiOutcome.Unavailable => ExtractionFailures.Unavailable(),
            AiOutcome.NotUsed => ExtractionFailures.Disabled(),
            _ => ExtractionFailures.Nothing()
        };
    }

    private static MrzFormat? MrzFormatFor(DocumentType type) => type switch
    {
        DocumentType.Passport => MrzFormat.Td3,
        DocumentType.EmiratesId => MrzFormat.Td1,
        _ => null
    };

    /// <summary>OCR look-alikes in the all-digit Emirates ID number.</summary>
    private static string DigitsOnly(string value)
        => new(value.Select(c => c switch
        {
            'O' or 'Q' or 'D' => '0',
            'I' or 'L' => '1',
            'Z' => '2',
            'S' => '5',
            'G' => '6',
            'B' => '8',
            _ => c
        }).Where(char.IsAsciiDigit).ToArray());

    private static void Rewind(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }

    /// <summary>
    /// Runs the heavy part under one gate slot and the server deadline. A
    /// deadline expiry (while the caller is still waiting) is reported as
    /// TimedOut; caller cancellation propagates.
    /// </summary>
    private async Task<HeavyResult> RunHeavyAsync(
        Func<CancellationToken, Task<HeavyRead>> operation,
        CancellationToken callerToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);

        if (_options.OcrTimeoutSeconds > 0)
        {
            linkedCts.CancelAfter(TimeSpan.FromSeconds(_options.OcrTimeoutSeconds));
        }

        var token = linkedCts.Token;

        try
        {
            await _gate.WaitAsync(token);
        }
        catch (OperationCanceledException)
            when (!callerToken.IsCancellationRequested)
        {
            return HeavyResult.Timeout;
        }

        try
        {
            var read = await operation(token);
            return new HeavyResult(read.Image, read.Mrz, read.Text, TimedOut: false);
        }
        catch (OperationCanceledException)
            when (!callerToken.IsCancellationRequested)
        {
            return HeavyResult.Timeout;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record HeavyRead(PreparedImage? Image, MrzResult? Mrz, string? Text);

    private sealed record HeavyResult(PreparedImage? Image, MrzResult? Mrz, string? Text, bool TimedOut)
    {
        public static readonly HeavyResult Skipped = new(null, null, null, false);
        public static readonly HeavyResult Timeout = new(null, null, null, true);
    }

    private enum AiOutcome
    {
        NotUsed,
        Used,
        Unavailable
    }
}
