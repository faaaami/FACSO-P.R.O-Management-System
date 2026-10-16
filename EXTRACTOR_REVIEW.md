# Document Extraction: Review and Redesign Proposal

**Scope:** `LocalDocumentExtractionService`, the OpenRouter provider, OCR (Tesseract / PDFium / PdfPig), the rule-based classifier and field extractor, the extract/confirm commands and controllers, the frontend upload/review screens and the extraction tests.
**Status:** review only; no code changed.
**Date:** 2026-10-16

---

## 1. How it works today

### 1.1 Flow from upload to saved data

```
Browser (DocumentUploadCard)            accepts .pdf .jpg .jpeg .png, ≤ 20 MB, 180 s client timeout
  │  POST /api/v1/admin/documents/extract   (or /agent/documents/extract)
  ▼
FileUploadPolicy / FileSignatureValidator   content-type ↔ extension ↔ magic bytes
  ▼
ExtractDocumentCommandHandler
  1. Saves the original file to local disk (IFileStorageService)
  2. Calls IDocumentExtractionService.ExtractAsync(stream, contentType)
       ├─ Mock        → fixed neutral result (default; also the test default)
       ├─ Local       → text recovery → classifier → rule-based field extractor
       └─ OpenRouter  → text recovery → classifier → prompt → LLM (text only) → parser
  3. Writes a DocumentExtraction row: Suggested{Type, DocumentNumber, IssueDate,
     ExpiryDate, OwnerType, Details}, Confidence, Status = "Pending",
     ExpiresAt = now + 24 h, plus an audit row
  ▼
Browser (DocumentExtractionReviewDrawer)   pre-fills a form from the suggestion;
                                            staff choose the owner and edit/confirm
  │  POST .../documents/extractions/{id}/confirm
  ▼
ConfirmDocumentCommandHandler
  Row-locks the extraction, checks Pending and not expired, validates owner and
  type-specific Details, checks for duplicates, creates the Document from the
  values the reviewer submitted, sets extraction Status = "Confirmed".
  ▼
ExtractionExpiryCleanupJob (daily)          expires unconfirmed drafts, deletes their files
```

**Good already:** extraction never writes a `Document` by itself. A human must confirm, and the confirmed values (not the suggestion) are saved. Uploads are signature-checked, OCR is time-boxed and concurrency-capped, and recovered text is never logged or returned.

### 1.2 Text recovery (shared by Local and OpenRouter)

| Input | Path |
|---|---|
| PDF with a text layer | PdfPig reads the text. |
| Scanned PDF | PDFium renders each page at 300 DPI (pixel-capped at 40 MP, max 20 pages), then Tesseract OCRs it. |
| JPEG / PNG | Tesseract OCR directly. |
| Anything else | Rejected at upload (HEIC, WEBP, TIFF are not accepted). |

Tesseract runs with **`eng` only**, LSTM engine, `PageSegMode.Auto`, no orientation detection, no EXIF rotation, no deskew/denoise/contrast step. It has a 60 s deadline and at most 2 concurrent OCR jobs; a timeout returns an empty ("neutral") result.

### 1.3 Classification (both providers)

`RuleBasedDocumentClassifier` lower-cases the OCR text and looks for fixed English phrases. Each type has 2 "strong" phrases (weight 3) and 2 "supporting" phrases (weight 1), out of a maximum of 8. A type is suggested only if it scores **≥ 0.50**, meaning one strong plus one supporting phrase, and it doesn't tie with another type.

| Type | Strong phrases | Supporting phrases |
|---|---|---|
| Passport | `passport`, `passport number` | `nationality`, `date of birth` |
| Visa | `visa`, `residence visa` | `visa number`, `entry permit` |
| EmiratesId | `emirates id`, `emirates identity card` | `id number`, `identity card number` |
| LaborCard | `labor card`, `labour card` | `labor card number`, `labour card number` |
| TradeLicense | `trade license`, `trade licence` | `license number`, `licence number` |
| EstablishmentCard | `establishment card`, `establishment number` | `economic department`, `company name` |

The returned **"confidence" is this keyword score**. It isn't a measure of whether any extracted field is right.

### 1.4 Field extraction

**Local provider** (`RuleBasedDocumentDetailsExtractor`):
- Only matches lines shaped exactly `LABEL: value`, with the label equal to a fixed English synonym such as `PASSPORT NUMBER` or `ID NUMBER`.
- Dates are accepted only as `yyyy-MM-dd`.
- The top-level `DocumentNumber`, `IssueDate`, `ExpiryDate` and `OwnerType` are **always null**: the service hard-codes them. Only the type and `Details` can be filled.

**OpenRouter provider:**
- **Model:** whatever `Extraction:OpenRouter:Model` says. Locally it is `openrouter/auto`, which lets OpenRouter pick a model per request.
- **Input:** the OCR text from 1.2, trimmed to 20,000 characters. **No image is sent.**
- **System prompt** (`OpenRouterPromptBuilder`):
  - treats the text as untrusted data and forbids inventing values;
  - requires `yyyy-MM-dd` dates and JSON only;
  - lists the schema fields for the classifier's detected type.
- **Output shape:**
  ```
  { "documentType", "documentNumber", "issueDate", "expiryDate", "ownerType",
    "details": { <schema keys> }, "confidence": 0..1 }
  ```
- **Request settings:** no `response_format` / JSON-schema enforcement, no `temperature`, `max_tokens` 2048.
- **Parsing** (`OpenRouterResponseParser`):
  - the model's `documentType` wins over the classifier;
  - fields are validated against the schema, and invalid values are dropped silently;
  - the model's self-reported confidence is used if it's between 0 and 1.
- **Resilience:** 60 s HTTP timeout. Up to 2 retries on network errors, 429 and 5xx, with exponential backoff. Any failure returns a neutral result, so the user just sees an empty form.

### 1.5 Fields defined per document type (`DocumentTypeSchemaProvider`)

| Type | Fields (★ = required on confirm) |
|---|---|
| Passport | passportNumber★, nationality, dateOfBirth, placeOfBirth, sex, issueDate, expiryDate, issuingAuthority |
| Visa | visaNumber★, visaType, issueDate, expiryDate, entryPermitNumber, uidNumber, sponsorName |
| EmiratesId | idNumber★, cardNumber, nationality, dateOfBirth, issueDate, expiryDate |
| LaborCard | laborCardNumber★, employeeNumber, occupation, employerName, issueDate, expiryDate |
| TradeLicense | licenseNumber★, legalName, tradeName, issuingAuthority, licenseType, issueDate, expiryDate |
| EstablishmentCard | establishmentNumber★, companyName, economicDepartment, issueDate, expiryDate |
| Other | none |

No schema has the **holder's name** (or surname/given names). A person's name is the main thing to cross-check against the employee record, and the main thing a PRO officer reads.

---

## 2. Problems

### 2.1 Wrong or missing fields

1. **The Local provider never fills the main form fields.** Document number, issue date and expiry date are hard-coded to null, so with `Extraction:Provider=Local` the reviewer always types them by hand.
2. **The rule-based extractor practically never matches real documents.** Real documents say `Passport No.`, `No.`, `ID Number` with no colon, or put the value on the next line. Dates are printed as `12 JAN 2030`, `12/01/2030`, or in MRZ `YYMMDD` form; only `yyyy-MM-dd` is accepted. Its tests pass because the fixtures are written in exactly that format (`PASSPORT NUMBER: P-90321`).
3. **No MRZ parsing anywhere.** The passport's machine-readable zone is the most reliable data on the page and has built-in check digits, but it's ignored. Tesseract output for `<<<` lines usually contains errors that nobody detects.
4. **Arabic is invisible.** OCR is English-only. Labour, trade-licence and establishment documents are bilingual or Arabic-first, so half the content is lost or turns into garbage text that misleads the LLM.
5. **No holder name** in any schema (see 1.5).
6. **Document number vs. `details.<x>Number`** are two separate fields that can disagree. Nothing reconciles them.
7. **Confidence is misleading.** For Local it's the keyword score; for OpenRouter it's the model's self-assessment. Neither reflects field accuracy, yet the UI shows it as a coloured pill.
8. **Invalid model output is dropped without explanation.** The reviewer sees an empty field and doesn't know whether it was missing or rejected.
9. **The `OwnerType` suggestion is unused.** The reviewer picks the owner manually anyway.

### 2.2 Error handling, timeouts, retries

- Every failure (OCR timeout, HTTP error, bad JSON, unsupported layout) turns into the same empty draft. **The user gets no message explaining why**, so "the AI is broken" and "your photo is sideways" look identical.
- Worst case for one request: OCR 60 s plus LLM 60 s (with retries inside that window), running synchronously in the HTTP request. The browser waits up to 180 s. Under load, the 2-slot OCR gate queues uploads, and a third concurrent upload can time out while waiting.
- The retry policy is correct (no retry on 4xx), but HTTP 200 with malformed JSON isn't retried or repaired.

### 2.3 Cost

- `openrouter/auto` means the model, and therefore price and behaviour, can change from one request to the next. That makes cost unpredictable and accuracy impossible to compare.
- Every upload makes a fresh LLM call; there's no caching by file hash, so re-uploading the same file pays again.
- Sending up to 20,000 characters of noisy OCR text, mostly garbage for photos, wastes input tokens.
- Nothing records tokens or cost per extraction.

### 2.4 Unsupported or poorly handled inputs

| Input | Today |
|---|---|
| **HEIC** (iPhone default) | Rejected at upload; staff must convert first. |
| **Rotated photo** (EXIF orientation, or a card shot sideways) | No EXIF auto-rotate and no orientation detection, so OCR returns nonsense. |
| **Low light / glare / skew / small text** | No preprocessing (grayscale, contrast, deskew, upscaling), so Tesseract quality drops sharply. |
| **Emirates ID front + back** | One file only; two photos can't be combined into one document. |
| **Multi-page PDF with mixed text and scans** | The "has usable text" check is document-wide, so a PDF whose first page has text but the ID is a scanned image on page 2 is never OCR'd. |
| **Large photos** | Above 40 MP the image is silently skipped instead of downscaled. |
| **WEBP / TIFF** | Rejected. |

### 2.5 Security and privacy

- **Personal data leaves the server.** With OpenRouter, the full OCR text of passports and IDs (name, date of birth, nationality, document numbers) is sent to OpenRouter and then to whichever provider `auto` routes to. There's no provider pinning, no `data_collection: "deny"` routing preference, and no zero-retention guarantee. This needs a decision under UAE PDPL and your client contracts.
- The **OpenRouter API key** sits in plain text in `appsettings.Local.json` (gitignored, but should be rotated and moved to a secret store; see step 2).
- **Logs:** good. Recovered text and raw model output are never logged, only status codes and timeout durations.
- **The audit log stores the uploaded file name** (e.g. `Ahmed_Khan_passport.jpg`), which can contain personal data.
- Original files are stored unencrypted on local disk. Unconfirmed drafts are deleted after 24 h; confirmed files are kept indefinitely.
- **Prompt injection** is handled sensibly: the text is wrapped and the model is told to treat it as data. The parser also only accepts schema keys and validated formats, which limits damage.

### 2.6 Test coverage

- **What's covered:** about 345 extraction tests across 22 files. These cover configuration, provider selection, HTTP retry/timeout behaviour, parser hardening, prompt construction, PDF/OCR plumbing, file download and listing, and concurrency of confirmation.
- **The gap:**
  - every fixture is synthetic and written in the exact `LABEL: value` format the rules expect;
  - there are no real-layout samples, not even anonymised specimen documents;
  - there are no rotated, low-quality or Arabic images, and no MRZ test vectors;
  - nothing measures accuracy;
  - the suite proves the mechanics work, not that extraction works on real UAE documents.

### 2.7 Which document types fail most today, and why

Ranked from worst to best:

1. **Emirates ID: almost always fails.**
   - Cards say "Resident Identity Card" / "United Arab Emirates" plus Arabic, not "Emirates ID" or "Emirates Identity Card". So the classifier only finds the supporting phrase "id number" (score 1/8) and suggests no type.
   - With no type, no fields are requested from the model.
   - The ID number `784-YYYY-NNNNNNN-N` is printed without a `LABEL:` colon, and the useful MRZ is on the back, a separate photo.
2. **Labour card / work permit: almost always fails.**
   - MOHRE now issues "Work Permit" / "Electronic Work Permit", and the documents are bilingual. The strong phrases "labor card" / "labour card" rarely appear, and the Arabic half isn't OCR'd.
3. **Trade licence and establishment card: usually fail classification.**
   - Each needs a strong *and* a supporting phrase. Real licences say "Trade License" (strong, 3) but "License No." rather than "License Number", which scores 3/8 = 0.375, below the 0.50 threshold.
   - Establishment cards abbreviate to "Est. No." and the same thing happens.
   - Text-layer PDFs (DED, ICP portals) at least get clean text, so with OpenRouter and a forced type they could work. Classification is what blocks them.
4. **Visa / entry permit: unreliable.**
   - "visa" is common text (passport visa pages, "visa no."). Passports with a visa stamp tie or misclassify.
   - UAE residence stickers were replaced by Emirates ID data in 2022; e-visa / entry-permit PDFs classify as Visa only if the literal word "visa" appears alongside "entry permit".
5. **Passport: classified correctly most often, but fields are mostly empty.**
   - "passport" plus "nationality" or "date of birth" usually reaches 0.50.
   - Field extraction fails on photos: there's no MRZ parsing, dates in `DD MMM YYYY` are rejected, and the label is "Passport No." rather than "PASSPORT NUMBER".

**In short:** with the **Local** provider almost no real document gets any field pre-filled. With **OpenRouter**, results depend on whether the English keyword classifier recognises the type and on how good the Tesseract text is. The model never sees the image, so photos of cards do poorly.

---

## 3. Redesign proposal

### 3.1 Principles

- **Deterministic first, AI second.** Where the document has a machine-readable part (passport MRZ, EID back MRZ), read and validate it with code. Use AI for the rest of the page and as a fallback.
- **The model sees the image, not OCR text.** Use vision models with a strict JSON schema per document type.
- **Per-field results**: value, source (`mrz` / `ai` / `text`), confidence, validation errors.
- **Staff confirm before anything is saved.** That stays as it is today.
- **Measure:** store suggested vs. confirmed values and report accuracy per type and per provider.

### 3.2 Pipeline

```
upload ─► normalise ─► classify ─► extract (per type) ─► validate ─► cross-check ─► draft
          (images)     (rules +     (MRZ / vision AI /    (formats,    (vs employee
                        AI fallback) PDF text)             check digits) record)
```

1. **Normalise (new `IImagePreprocessor`)**
   - Accept HEIC/HEIF, WEBP and TIFF in addition to PDF/JPEG/PNG; convert everything to PNG.
   - Apply EXIF orientation. Detect 90/180/270° rotation by running OCR in the four orientations, or by finding the MRZ at the bottom.
   - Downscale to a maximum long edge of about 2000 px for AI and keep a higher-resolution crop for MRZ OCR. Never silently skip a large image.
   - Render PDFs page by page (already done) and OCR any page that has no text layer.
   - Library: **Magick.NET** (Apache-2.0) covers HEIC, EXIF auto-orient, deskew, contrast and resize in one dependency. ImageSharp is lighter but has no HEIC support, and its licence needs checking for commercial use.
   - Optional **front + back** upload: one extraction with two images, for Emirates ID and labour cards.
2. **Classify**
   - Keep the rule-based classifier, but widen the vocabulary to cover real wording, abbreviations and Arabic keywords. Treat MRZ presence as a signal: a `P<` line means passport, `ID` plus a `784` line means Emirates ID.
   - If the rules aren't confident, use the vision model to classify, or let the reviewer pick the type up front (one dropdown before upload removes most classification errors).
3. **Extract**
   - **Passport:**
     - Find the two 44-character TD3 MRZ lines in OCR text from the bottom third of the page. OCR that crop with a whitelist of `A–Z 0–9 <`.
     - Parse and **validate every check digit** (number, date of birth, expiry, composite) using the ICAO 9303 7-3-1 weights.
     - Map the MRZ to fields: surname, given names, passport number, nationality (ISO 3166 alpha-3), date of birth, sex, expiry date. Issue date and place of birth aren't in the MRZ and come from the AI.
     - **Library:** the .NET MRZ libraries are small and unmaintained (e.g. `Mrz.Net`), so I recommend **writing our own parser** (about 200 lines plus tests; the format is fully specified and stable). We control the OCR-error tolerance this way, e.g. `O→0` in numeric fields, `<` misread as `K`.
     - AI is used only when the MRZ is unreadable or the check digits fail; those fields get flagged.
   - **Emirates ID:**
     - **Back side:** TD1 MRZ (3 lines × 30). Parse it with the same parser, which gives the ID number, date of birth, expiry, nationality and name, all check-digit validated.
     - **Front side:** vision AI for name (English and Arabic), nationality and card number, plus the issue date if printed.
     - **Validate** the ID as `^784-\d{4}-\d{7}-\d$`. The 4-digit block should equal the holder's birth year, so cross-check it against date of birth. The last digit is a Luhn check digit over the 15 digits (widely used and documented, but confirm against a few real cards before enforcing; until then it's a warning, not an error).
   - **Visa / entry permit, work permit (labour card), trade licence, establishment card, others:**
     - Vision model with a **strict JSON schema per type** (`response_format: json_schema`, `strict: true`, `temperature: 0`).
     - The schema includes each field's value, a confidence, and the evidence text the model read.
     - For text-layer PDFs (DED, ICP and MOHRE portal printouts), send the PDF text as well; it's clean and cheap.
4. **Validate** (shared, per type and per field)
   - Formats:
     - Emirates ID: `784-YYYY-NNNNNNN-N`
     - UID: digits only
     - Visa / entry-permit file number: `NNN/YYYY/N/NNNNNNN`
     - MOHRE work-permit and person codes: digits
     - Dates: real dates
   - Logic checks: expiry after issue, date of birth in the past, and expiry not unreasonably far ahead (10 years for passports).
   - Schema additions: holder name fields (`fullName`, `surname`, `givenNames`, `fullNameArabic`) for personal documents, and `workPermitNumber` / `personCode` for MOHRE.
5. **Cross-check** (new)
   - When an employee is chosen (before confirm, or pre-selected because upload started from the employee page), compare:
     - the extracted name with `Employee.FullName` (normalised, token-set fuzzy match);
     - the passport number with `Employee.PassportNumber`;
     - date of birth and nationality with the employee's values.
   - Return `warnings[]`, e.g. "Passport number differs from employee record (P1234567)". The UI shows these prominently. Confirm still works, but the reviewer must tick "I checked this".
   - Also flag "this document number already exists for another person" (the current duplicate guard only blocks exact duplicates on the same owner).
6. **Draft returned to the UI**
   ```json
   {
     "type": "Passport", "typeSource": "mrz",
     "fields": {
       "passportNumber": { "value": "P1234567", "source": "mrz", "confidence": 0.99, "errors": [] },
       "expiryDate":     { "value": "2031-04-12", "source": "mrz", "confidence": 0.99, "errors": [] },
       "placeOfBirth":   { "value": "KARACHI", "source": "ai", "confidence": 0.71, "errors": [] }
     },
     "warnings": ["Name differs from employee record: 'MOHAMMED ALI KHAN' vs 'Mohammad Ali'"],
     "failureReason": null
   }
   ```
   The review form colours each field by confidence and source and shows errors inline. When nothing could be read, `failureReason` gives a clear message, e.g. "The image looks rotated or blurred; please retake it in good light".

### 3.3 Accuracy measurement

- New table `extraction_field_outcomes`: `extraction_id`, `document_type`, `provider`, `model`, `field_key`, `suggested_value_hash`, `was_suggested`, `was_changed`, `source`, `confidence`, `created_at`.
- **Store hashes or booleans, not raw values**, so the accuracy data holds no personal data.
- Filled on confirm by comparing the suggestion with what the reviewer submitted.
- Admin report: per type, field and provider, show fill rate (suggested), accuracy (suggested and unchanged), plus cost and latency per extraction. This is how providers get compared fairly.

### 3.4 Providers behind one interface

```csharp
public interface IDocumentFieldExtractor   // one implementation per provider
{
    string Name { get; }                    // "openrouter:gpt-4.1-mini", "azure-di:idDocument", ...
    Task<ProviderExtraction> ExtractAsync(
        ExtractionInput input,              // normalised images + PDF text + requested type + schema
        CancellationToken ct);
}
```

- **Implementations:** OpenRouter (pinned model, not `auto`); Azure AI Document Intelligence (prebuilt *ID document* model covers passports and many ID cards, with region choice e.g. UAE North); Anthropic Claude (vision with tool or JSON output); a "local only" implementation for clients that refuse cloud processing (MRZ plus Tesseract with Arabic).
- **Config:** a provider and model per document type, e.g. passports use MRZ plus a cheap vision model, trade licences use a stronger model.
- **Comparison mode (admin setting):** run a second "shadow" provider on a sample percentage and record both into the outcomes table. Only the primary result reaches the UI.
- **Cost:** record input and output tokens and price per call, and cache results by SHA-256 of the normalised file.

### 3.5 Privacy, logging, reliability

- **Never log** images, OCR text, model responses or extracted values. Log only extraction id, type, provider, latency, status and error category.
- **Remove the file name** from audit descriptions; store it on the extraction row only.
- **Data residency:**
  - Pin providers that offer zero data retention and no training on inputs.
  - With OpenRouter, set provider routing preferences: `data_collection: "deny"`, an explicit provider list, no fallback to unknown providers.
  - Or use Azure in UAE North.
  - Make "cloud AI allowed" a **per-client-company setting**; companies that say no get MRZ plus local OCR only.
- **Timeouts:** an overall budget of about 45 s per extraction, split between stages. Retry once on 429/5xx, and once on invalid JSON with a repair prompt.
- **Asynchronous processing (recommended):**
  - The upload returns `202` with the extraction id straight away; a Hangfire job extracts.
  - The UI polls, or gets a SignalR push, which is already in place.
  - This removes the 180 s synchronous request and the OCR-gate queueing problem.
- **Clear errors for users:** "unsupported file", "image too blurry or rotated", "AI service unavailable, please fill manually". The upload is never lost; the reviewer can always type values in.

---

## 4. Implementation plan

Effort is in developer-days for one developer who knows the codebase. Each step ships on its own behind the existing provider switch.

| # | Step | Details | Effort |
|---|---|---|---|
| 0 | **Decide the privacy policy** | Which providers and regions are acceptable, whether passports may go to cloud AI at all, and a per-client opt-out. Needs a business decision before step 6. | 0.5 (meeting) |
| 1 | **Test corpus and accuracy harness** | Collect about 10 anonymised or specimen samples per type (ICAO specimen passports, public sample EIDs, your own redacted documents): photos, scans, rotated, low light. Build a test project that runs any provider against them and prints per-field accuracy. Keep it out of CI if the samples are sensitive. | 3 |
| 2 | **Image normalisation** | Magick.NET: HEIC/WEBP/TIFF in, EXIF auto-orient, rotation detection, resize; downscale instead of skipping large images; per-page OCR fallback for mixed PDFs; add Arabic (`ara`) traineddata. Update upload policy and frontend `accept`. | 3 |
| 3 | **MRZ parser** (TD1, TD2, TD3) | Our own parser with check digits and OCR-error tolerance, MRZ-region OCR with a character whitelist, plus unit tests with ICAO examples. | 3 |
| 4 | **Passport and EID extraction on MRZ** | Passport from the MRZ alone; EID back MRZ plus `784-` validation and birth-year cross-check; optional front + back upload for EID. | 3 |
| 5 | **New result contract** | Per-field `{value, source, confidence, errors}`, `warnings[]` and `failureReason` through the API to the review drawer; colour fields by confidence; show reasons. Add holder-name and MOHRE fields to the schemas. | 3 |
| 6 | **Vision provider interface and first implementation** | `IDocumentFieldExtractor`; OpenRouter vision with a pinned model, `json_schema` strict, `temperature: 0`, routing preferences; per-type JSON schemas generated from `DocumentTypeSchemaProvider`; token and cost capture; cache by file hash. | 4 |
| 7 | **Cross-check against employee records** | Name, passport number, date of birth and nationality comparison; "same number on another person" check; reviewer acknowledgement in the UI. | 2 |
| 8 | **Accuracy table and report** | `extraction_field_outcomes` (new migration, hashed values), filled on confirm; Admin report page per type, field and provider. | 3 |
| 9 | **Asynchronous extraction** | Hangfire job, `202` + polling/SignalR, clearer error states, overall time budget. | 3 |
| 10 | **Second provider and comparison** | Azure Document Intelligence (ID documents) or Claude vision; shadow mode on a sample; compare using the step 8 report. | 3 |
| 11 | **Classifier vocabulary and type-first UX** | Real-world phrases (including Arabic), MRZ signals, and an optional "document type" dropdown before upload. | 1.5 |
| 12 | **Privacy hardening** | File name out of audit, a test that nothing personal is logged, per-client "cloud AI allowed" flag, docs. | 1.5 |

**Total ≈ 33 days.**

**Recommended order:** 0 → 1 → 2 → 3 → 4 → 5. That's about 16 days, gets passports and Emirates IDs (the bulk of PRO volume) reliable with no AI dependency, and gives a way to measure. Then 6–9 for the remaining types, and 10–12 once real accuracy data exists.

---

## 5. Decisions needed from you

1. May passport/EID images or text be sent to a cloud AI provider at all? If yes, which providers and regions?
2. Should staff pick the document type before upload? Cheap, and removes most classification errors.
3. Front + back upload for Emirates ID and work permits: yes or no?
4. Can we get about 10 redacted real samples per document type for the test corpus?
