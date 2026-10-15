import { useCallback, useEffect, useRef } from 'react'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { AlertCircle, FileText, Loader2, ScanLine, ShieldCheck } from 'lucide-react'
import Drawer from '../client/documents/Drawer'
import LoadingState from '../client/LoadingState'
import ErrorState from '../client/ErrorState'
import FormField, { inputClass, inputErrorClass } from '../client/settings/FormField'
import StatusPill from '../client/StatusPill'
import { DOCUMENT_TYPES } from '../client/enumLabels'
import DocumentDetailsFields from './DocumentDetailsFields'
import {
  DOCUMENT_NUMBER_LIMIT,
  OWNER_KIND,
  buildConfirmPayload,
  buildConfirmSchema,
  formatConfidence,
  prefillFromExtraction,
  prefillSuggestedDetails,
} from './documentExtractionForm'
import { formatFileSize } from '../agent/documents/documentDisplay'
import { extractApiErrorMessage } from '../../utils/apiError'

/** Neutral below 60%, warning below 85%, success at or above it. */
function confidenceTone(confidence) {
  const numeric = typeof confidence === 'string' ? Number(confidence) : confidence
  if (typeof numeric !== 'number' || Number.isNaN(numeric)) return 'neutral'
  if (numeric >= 0.85) return 'success'
  if (numeric >= 0.6) return 'warning'
  return 'danger'
}

/**
 * Flattens a Zod error into React Hook Form's error shape, keyed by field path.
 *
 * The schema is rebuilt per document type here rather than passed to
 * `zodResolver` as a fixed schema, so this mapping stands in for what the
 * resolver would do. A pathless issue — a `superRefine` rule with no field to
 * attach to — lands on `root` rather than being dropped, so a cross-field
 * failure is never silently invisible.
 */
function toFormErrors(error) {
  const errors = {}
  for (const issue of error?.issues ?? []) {
    if (!issue.path?.length) {
      errors.root = { type: issue.code ?? 'custom', message: issue.message }
      continue
    }
    let cursor = errors
    for (let i = 0; i < issue.path.length - 1; i += 1) {
      const key = String(issue.path[i])
      cursor[key] = cursor[key] ?? {}
      cursor = cursor[key]
    }
    const last = String(issue.path[issue.path.length - 1])
    cursor[last] = { type: issue.code ?? 'custom', message: issue.message }
  }
  return errors
}

/**
 * SHARED REVIEW / CONFIRM DRAWER for a document extraction draft.
 *
 * Used unchanged by the Agent and the Admin review queues. Everything that is
 * easy to get wrong lives here rather than being written twice: the
 * schema-driven field set, the fact that a failed schema load BLOCKS
 * confirmation, the prefill being a suggestion rather than an answer, and the
 * absence of any file field in the payload.
 *
 * WHAT IS TRUSTED, AND WHAT IS NOT
 * --------------------------------
 * Extraction runs inside the upload request and returns suggestions. Every one
 * of them is a PREFILL: the reviewer can change all of it, and what the server
 * records is what was confirmed here. The file is never sent at all — the server
 * keeps the authoritative stored reference and copies it onto the Document.
 *
 * WHY A FAILED SCHEMA BLOCKS CONFIRMATION
 * ----------------------------------------
 * A type's detail fields are defined only by its schema. If that schema cannot
 * be loaded, an empty field list renders as "this type has no extra fields" and
 * the draft would confirm with details silently missing — indistinguishable from
 * a correct confirmation. So a schema error is a blocked panel with a retry, not
 * an empty form. The server enforces the same rule (DocumentDetailsValidator
 * with validateRequired: true), so this explains a refusal rather than
 * pre-empting one.
 *
 * THE TYPE/SCHEMA ORDERING
 * ------------------------
 * The schema follows the selected type, and the selected type starts as the
 * extractor's suggestion, so the schema cannot be known before the draft is.
 * Seeding therefore happens in two steps: the draft seeds the top-level fields,
 * then the schema's arrival seeds `details`. The resolver reads the live schema
 * through a ref, so validation always matches the fields on screen even though
 * the form was created before the schema existed.
 *
 * PROPS
 *   extractionId  the draft under review
 *   draft         the loaded extraction payload
 *   draftState    'loading' | 'error' | 'ready'
 *   refreshDraft  refetch the draft
 *   schemaHook    useAgentDocumentTypeSchema | useAdminDocumentTypeSchema.
 *                 Passed in rather than imported so one component serves both
 *                 portals; the two hooks share a signature and a
 *                 { fields, isLoading, isError, refresh } shape.
 *   ownerPicker   ({ ownerKind, register, errors, setValue, disabled }) => JSX.
 *                 The owner list is portal-specific (the Agent's choices are
 *                 task-scoped, the Admin's are company-scoped), so it is
 *                 supplied from outside rather than faked here.
 *   confirm       the portal's confirm mutation
 *   onConfirmed   (documentId) => void
 *   onClose
 *   filePanel     optional node: the portal's file preview / download controls
 */
function DocumentExtractionReviewDrawer({
  extractionId,
  draft,
  draftState,
  refreshDraft,
  schemaHook,
  ownerPicker,
  confirm,
  onConfirmed,
  onClose,
  filePanel,
}) {
  // The resolver must validate against the CURRENT schema, but useForm has to be
  // called before that schema is known — the selected type comes from the draft,
  // and the schema follows the type. A ref closes the gap: it is written as the
  // schema arrives and read only at submit, which is always after render.
  const fieldsRef = useRef([])

  const resolver = useCallback(async (values) => {
    const parsed = await buildConfirmSchema(fieldsRef.current).safeParseAsync(values)
    if (parsed.success) {
      return { values: parsed.data, errors: {} }
    }
    return { values: {}, errors: toFormErrors(parsed.error) }
  }, [])

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver,
    defaultValues: {
      type: '',
      documentNumber: '',
      issueDate: '',
      expiryDate: '',
      ownerKind: OWNER_KIND.employee,
      employeeId: '',
      clientEntityId: '',
      details: {},
    },
  })

  const selectedType = watch('type')
  const ownerKind = watch('ownerKind')

  const schema = schemaHook(selectedType)
  const fields = schema.fields
  const schemaLoading = Boolean(selectedType) && schema.isLoading
  const schemaBlocked = Boolean(selectedType) && schema.isError

  useEffect(() => {
    fieldsRef.current = fields
  }, [fields])

  // Step 1 — the draft seeds the top-level fields. `details` is left empty here
  // because the schema that gives those fields their shape has not loaded yet.
  const seededDraftId = useRef(null)
  useEffect(() => {
    const id = draft?.extractionId
    if (!id || seededDraftId.current === id) return
    seededDraftId.current = id
    const seed = prefillFromExtraction(draft, [])
    reset({ ...seed, details: {} })
  }, [draft, reset])

  // Step 2 — once the schema is available, the type-specific suggestions are
  // pre-filled. Keyed by draft AND type so a type change is a fresh seed rather
  // than a repeat of the previous one.
  const seededDetails = useRef(null)
  useEffect(() => {
    const id = draft?.extractionId
    if (!id || !selectedType || schemaLoading || schemaBlocked) return
    const key = `${id}:${selectedType}`
    if (seededDetails.current === key) return
    seededDetails.current = key
    setValue('details', prefillSuggestedDetails(draft, fields), { shouldValidate: false })
  }, [draft, fields, selectedType, schemaLoading, schemaBlocked, setValue])

  // Changing the type discards the previous type's detail values. A number
  // pre-filled for a Passport is not a value anyone read against a Visa field,
  // and keeping it would confirm something that was never shown.
  function onTypeChange(event) {
    const nextType = event.target.value
    setValue('type', nextType, { shouldValidate: false })
    setValue('details', {}, { shouldValidate: false })
    if (nextType) {
      // Claim this key so step 2 does not re-seed the new type either.
      seededDetails.current = `${draft?.extractionId}:${nextType}`
    }
  }

  const isPending = confirm.isPending || isSubmitting
  const isReviewable = draftState === 'ready' && draft?.status === 'Pending'

  function onSubmit(values) {
    confirm.mutate(
      { extractionId, payload: buildConfirmPayload(values) },
      {
        onSuccess: (data) => {
          toast.success('Document confirmed and added to the register.')
          onConfirmed?.(data?.documentId)
        },
        onError: () => {
          // Left inline in the banner. Closing would discard the reviewer's
          // corrections, which are usually what the error is telling them to
          // change; a 409 in particular means someone confirmed this draft first
          // and the queue should be refetched rather than the form retried.
          if (confirm.error?.response?.status === 409) {
            refreshDraft?.()
          }
        },
      },
    )
  }

  if (draftState === 'loading') {
    return (
      <Drawer title="Review document" icon={ScanLine} subtitle="Extracted details" onClose={onClose} size="lg">
        <LoadingState label="Loading extraction…" />
      </Drawer>
    )
  }

  if (draftState === 'error') {
    return (
      <Drawer title="Review document" icon={ScanLine} subtitle="Extracted details" onClose={onClose} size="lg">
        <ErrorState
          message="This extraction draft could not be loaded. It may have expired, been confirmed already, or been removed."
          onRetry={() => refreshDraft?.()}
        />
      </Drawer>
    )
  }

  const confidence = formatConfidence(draft?.confidence)
  const status = draft?.status

  return (
    <Drawer
      title="Review document"
      icon={ScanLine}
      subtitle="Extracted details"
      onClose={onClose}
      size="lg"
    >
      <div className="space-y-5">
        <div className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-3.5">
          <div className="flex items-start gap-2.5">
            <FileText
              size={16}
              strokeWidth={1.75}
              className="mt-0.5 shrink-0 text-[#6B7280]"
              aria-hidden="true"
            />
            <div className="min-w-0 flex-1">
              <p className="break-words text-sm font-semibold text-[#16181D]">{draft?.fileName}</p>
              <p className="mt-0.5 text-xs text-[#6B7280]">
                {draft?.contentType}
                {formatFileSize(draft?.fileSize) ? ` · ${formatFileSize(draft.fileSize)}` : ''}
              </p>
            </div>
            {confidence && (
              <StatusPill
                label={`${confidence} confidence`}
                tone={confidenceTone(draft?.confidence)}
              />
            )}
          </div>

          <p className="mt-3 flex items-start gap-1.5 text-xs leading-relaxed text-[#6B7280]">
            <ShieldCheck
              size={12}
              strokeWidth={1.75}
              className="mt-0.5 shrink-0"
              aria-hidden="true"
            />
            <span>
              {isReviewable
                ? 'Extracted values are suggestions. Check each one against the file before confirming — nothing is recorded until you do.'
                : 'This draft is no longer awaiting review.'}
            </span>
          </p>
        </div>

        {filePanel}

        {isReviewable ? (
          <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-5">
            {confirm.isError && (
              <div
                role="alert"
                className="flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-3.5 py-3"
              >
                <AlertCircle
                  size={15}
                  strokeWidth={2}
                  className="mt-0.5 shrink-0 text-[#DC2626]"
                  aria-hidden="true"
                />
                <p className="text-xs font-medium leading-relaxed text-[#DC2626]">
                  {extractApiErrorMessage(confirm.error, 'The document could not be confirmed.')}
                </p>
              </div>
            )}

            <section className="space-y-4">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-[#6B7280]">Document</h3>

              <div className="grid gap-4 sm:grid-cols-2">
                <FormField
                  label="Document type"
                  htmlFor="extractionType"
                  error={errors.type?.message}
                >
                  <select
                    id="extractionType"
                    className={errors.type ? inputErrorClass : inputClass}
                    value={selectedType}
                    disabled={isPending}
                    onChange={onTypeChange}
                  >
                    <option value="">Choose a type…</option>
                    {Object.entries(DOCUMENT_TYPES).map(([value, label]) => (
                      <option key={value} value={value}>
                        {label}
                      </option>
                    ))}
                  </select>
                </FormField>

                <FormField
                  label="Document number"
                  htmlFor="extractionDocumentNumber"
                  error={errors.documentNumber?.message}
                >
                  <input
                    id="extractionDocumentNumber"
                    type="text"
                    className={errors.documentNumber ? inputErrorClass : inputClass}
                    disabled={isPending}
                    {...register('documentNumber')}
                  />
                </FormField>

                <FormField
                  label="Issue date"
                  htmlFor="extractionIssueDate"
                  error={errors.issueDate?.message}
                >
                  <input
                    id="extractionIssueDate"
                    type="date"
                    className={errors.issueDate ? inputErrorClass : inputClass}
                    disabled={isPending}
                    {...register('issueDate', { setValueAs: (v) => (v === '' ? undefined : v) })}
                  />
                </FormField>

                <FormField
                  label="Expiry date"
                  htmlFor="extractionExpiryDate"
                  error={errors.expiryDate?.message}
                >
                  <input
                    id="extractionExpiryDate"
                    type="date"
                    className={errors.expiryDate ? inputErrorClass : inputClass}
                    disabled={isPending}
                    {...register('expiryDate', { setValueAs: (v) => (v === '' ? undefined : v) })}
                  />
                </FormField>
              </div>

              <p className="text-xs leading-relaxed text-[#6B7280]">
                An expiry date is what carries this document into the expiry register and its renewal
                reminders. The number may be up to {DOCUMENT_NUMBER_LIMIT} characters.
              </p>
            </section>

            <section className="space-y-3">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-[#6B7280]">Owner</h3>
              {ownerPicker({ ownerKind, register, errors, setValue, disabled: isPending })}
            </section>

            <section className="space-y-3">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                {selectedType
                  ? `${DOCUMENT_TYPES[selectedType] ?? 'Document'} details`
                  : 'Document details'}
              </h3>

              {!selectedType ? (
                <p className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] px-3.5 py-3 text-xs leading-relaxed text-[#6B7280]">
                  Choose a document type to see the fields this document type records.
                </p>
              ) : schemaLoading ? (
                <p className="text-xs text-[#9CA3AF]">Loading field definitions…</p>
              ) : schemaBlocked ? (
                <div
                  role="alert"
                  className="rounded-[10px] border border-red-200 bg-red-50/60 px-3.5 py-3"
                >
                  <p className="text-xs font-medium leading-relaxed text-[#DC2626]">
                    The field definitions for this document type could not be loaded, so this document
                    cannot be confirmed yet.
                  </p>
                  <p className="mt-1.5 text-xs leading-relaxed text-[#DC2626]">
                    Confirming now would record the document without the fields its own type requires.
                  </p>
                  <button
                    type="button"
                    onClick={() => schema.refresh()}
                    className="mt-2.5 inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-red-200 bg-white px-3 py-1.5 text-xs font-semibold text-[#DC2626] transition duration-150 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-[#DC2626]"
                  >
                    Try again
                  </button>
                </div>
              ) : (
                <DocumentDetailsFields
              fields={fields}
              register={register}
              errors={errors}
              disabled={isPending}
            />
              )}
            </section>

            <div className="flex flex-col-reverse gap-2 border-t border-[#E2E4E9] pt-4 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={onClose}
                disabled={isPending}
                className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isPending || schemaLoading || schemaBlocked || !selectedType}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Confirming…</span>
                  </>
                ) : (
                  <span>Confirm document</span>
                )}
              </button>
            </div>
          </form>
        ) : (
          <div
            role="status"
            className="flex items-start gap-2.5 rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-3"
          >
            <AlertCircle
              size={15}
              strokeWidth={2}
              className="mt-0.5 shrink-0 text-[#92400E]"
              aria-hidden="true"
            />
            <p className="text-xs leading-relaxed text-[#92400E]">
              {status === 'Confirmed'
                ? 'This draft has already been confirmed, so it cannot be reviewed again. The document it created is on the register.'
                : status === 'Expired'
                  ? 'This draft expired before it was confirmed, so it can no longer be registered. Upload the file again to start a new draft.'
                  : 'This draft is not awaiting review.'}
            </p>
          </div>
        )}
      </div>
    </Drawer>
  )
}

export default DocumentExtractionReviewDrawer
