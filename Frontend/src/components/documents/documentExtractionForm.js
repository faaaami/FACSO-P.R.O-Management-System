import { z } from 'zod'
import { DOCUMENT_TYPES } from '../client/enumLabels'

/**
 * Shared form logic for the D1 document review / confirmation flow.
 *
 * Used by both the Agent and the Admin review drawer. Kept out of the component
 * so the contract work — date normalisation, schema-driven validation, the
 * exact payload the server accepts — lives in one place and cannot drift
 * between the two portals.
 */

// ---------------------------------------------------------------------
// CONTRACT
// ---------------------------------------------------------------------

/** The server's cap on DocumentNumber (ConfirmDocumentCommandValidator). */
export const DOCUMENT_NUMBER_LIMIT = 100

/**
 * Owner type as the FORM models it. Deliberately lowercase: it is internal
 * form state that decides which id field is sent, and it is never compared
 * against a backend enum.
 */
export const OWNER_KIND = { employee: 'employee', entity: 'entity' }

/**
 * Converts a server date to the `yyyy-MM-dd` an <input type="date"> needs.
 *
 * Slicing the raw string instead of going through `new Date()` is deliberate.
 * The server serialises DateTime as UTC; `new Date('2026-03-01T00:00:00Z')` in
 * a timezone behind UTC is 28 Feb, so the date input would silently show — and
 * then submit — the wrong day. The same string is what goes back on submit, so
 * a value never drifts when the drawer is reopened.
 */
export function toDateInputValue(value) {
  if (typeof value !== 'string') return ''
  const match = value.match(/^(\d{4}-\d{2}-\d{2})/)
  return match ? match[1] : ''
}

/**
 * Interprets the extractor's free-text `suggestedOwnerType` as a form default.
 *
 * This is a SUGGESTION, and the extractor cannot know an owner id, so this only
 * ever decides which picker is shown first. It never supplies an owner id: the
 * reviewer must choose a real one from a real list. An unrecognised or absent
 * value falls back to the employee picker, which is the common case.
 */
export function normaliseSuggestedOwnerType(suggested) {
  const text = typeof suggested === 'string' ? suggested.trim().toLowerCase() : ''
  if (text === 'entity' || text === 'cliententity' || text === 'client entity') {
    return OWNER_KIND.entity
  }
  return OWNER_KIND.employee
}

/** Renders a decimal confidence as a whole percentage, or null. */
export function formatConfidence(value) {
  const numeric = typeof value === 'string' ? Number(value) : value
  if (typeof numeric !== 'number' || Number.isNaN(numeric)) return null
  return `${Math.round(numeric * 100)}%`
}

// ---------------------------------------------------------------------
// VALIDATION
// ---------------------------------------------------------------------

/**
 * Builds the Zod schema for one document type's dynamic field set.
 *
 * The shape is generated from the SERVER's schema response, so a field added
 * server-side becomes required here automatically — nothing in this file names
 * a document field. zod objects strip unknown keys, so an extracted suggestion
 * for a field this type does not declare is dropped instead of being smuggled
 * into `details`.
 *
 * `valueKind` IS CONSULTED BEFORE `required`. The two are independent axes: a
 * field can be a required Number as easily as a required String, and the
 * controls in DocumentDetailsFields coerce by valueKind first. Validating a
 * required Number as a string would reject the number the control actually
 * submits and report it as "is required", which is both wrong and unfixable
 * from the form.
 *
 * Note the `error` option rather than `required_error`: this project is on zod
 * v4, which unified those two into a single `error`.
 */

/**
 * Requires a value, THEN applies the kind's own type. A pipe is used rather than
 * a bare `z.any().refine()` so the type error a reviewer sees for a bad value
 * ("Passport number must be at most 50 characters.") still comes from the
 * kind's schema, and only an absent one reports "is required".
 */
function requiredField(base, message) {
  return z
    .custom((value) => value !== undefined && value !== null && value !== '', { message })
    .pipe(base)
}

function detailFieldSchema(field) {
  const label = field.label || field.key
  const requiredMessage = `${label} is required.`
  const maxLength = field.maxLength || 0

  if (field.valueKind === 'Date') {
    const isoDate = z
      .string()
      .refine((value) => /^\d{4}-\d{2}-\d{2}$/.test(value), {
        message: `${label} must be a valid date.`,
      })

    return field.required
      ? requiredField(isoDate, requiredMessage)
      : isoDate.optional()
  }

  if (field.valueKind === 'Number') {
    const numeric = z.number({ error: `${label} must be a number.` })
    return field.required
      ? requiredField(numeric, requiredMessage)
      : numeric.optional()
  }

  if (field.valueKind === 'Boolean') {
    const bool = z.boolean()
    return field.required ? requiredField(bool, requiredMessage) : bool.optional()
  }

  if (field.required) {
    return requiredField(
      z
        .string()
        .refine((value) => value.trim().length > 0, { message: requiredMessage })
        .refine((value) => !maxLength || value.length <= maxLength, {
          message: `${label} must be at most ${maxLength} characters.`,
        }),
      requiredMessage,
    )
  }

  return z
    .string()
    .optional()
    .refine((value) => !value || !maxLength || value.length <= maxLength, {
      message: `${label} must be at most ${maxLength} characters.`,
    })
}

function detailsShape(fields) {
  const shape = {}
  for (const field of Array.isArray(fields) ? fields : []) {
    if (!field?.key) continue
    shape[field.key] = detailFieldSchema(field)
  }
  return shape
}

/**
 * The full confirmation form schema.
 *
 * Every rule here restates a server rule for immediate feedback; none of them
 * replace it. `ConfirmDocumentCommandValidator` requires exactly one of
 * employeeId / clientEntityId, caps DocumentNumber at 100, and requires
 * expiryDate >= issueDate when both are present, and `DocumentDetailsValidator`
 * re-checks `details` against the same type schema with validateRequired: true.
 */
export function buildConfirmSchema(fields) {
  return z
    .object({
      type: z.string({ error: 'Choose a document type.' }).refine((value) => value in DOCUMENT_TYPES, {
        message: 'Choose a document type.',
      }),
      documentNumber: z
        .string({ error: 'A document number is required.' })
        .refine((value) => value.trim().length > 0, {
          message: 'A document number is required.',
        })
        .refine((value) => value.length <= DOCUMENT_NUMBER_LIMIT, {
          message: `Document number must be at most ${DOCUMENT_NUMBER_LIMIT} characters.`,
        }),
      issueDate: z
        .string()
        .optional()
        .refine((value) => !value || /^\d{4}-\d{2}-\d{2}$/.test(value), {
          message: 'Issue date must be a valid date.',
        }),
      expiryDate: z
        .string()
        .optional()
        .refine((value) => !value || /^\d{4}-\d{2}-\d{2}$/.test(value), {
          message: 'Expiry date must be a valid date.',
        }),
      ownerKind: z.enum([OWNER_KIND.employee, OWNER_KIND.entity], {
        error: 'Choose whether this document belongs to an employee or an entity.',
      }),
      employeeId: z.string().optional(),
      clientEntityId: z.string().optional(),
      details: z.object(detailsShape(fields)),
    })
    .superRefine((values, ctx) => {
      // The owner's two id fields are a choice, not two optional inputs: the one
      // matching `ownerKind` must be present. Erroring on the field the reviewer
      // is looking at is the difference between "pick an owner" and "something is
      // wrong".
      //
      // Only the SELECTED id is required, and only the selected id is sent —
      // buildConfirmPayload picks the owner key from `ownerKind` and omits the
      // other. Switching picker therefore leaves a stale id behind in the hidden
      // field, which is harmless: it is never in the payload, so the server's
      // "exactly one of" rule still holds. The reviewer sees one picker at a
      // time, so there is no state here in which both ids look chosen.
      if (values.ownerKind === OWNER_KIND.employee) {
        if (!values.employeeId) {
          ctx.addIssue({
            code: 'custom',
            path: ['employeeId'],
            message: 'Select the employee who owns this document.',
          })
        }
      } else if (!values.clientEntityId) {
        ctx.addIssue({
          code: 'custom',
          path: ['clientEntityId'],
          message: 'Select the client entity that owns this document.',
        })
      }

      if (values.issueDate && values.expiryDate && values.expiryDate < values.issueDate) {
        ctx.addIssue({
          code: 'custom',
          path: ['expiryDate'],
          message: 'Expiry date must be on or after the issue date.',
        })
      }
    })
}

// ---------------------------------------------------------------------
// VALUES
// ---------------------------------------------------------------------

/**
 * Interprets an extracted Boolean without guessing.
 *
 * The extractor's `suggestedDetails` is free-form JSON, so a Boolean field can
 * arrive as `false`, as the string `"false"`, or as `"no"`. Passing that through
 * JavaScript's own `Boolean(...)` is the trap: every non-empty string is
 * truthy, so `"false"`, `"no"` and `"0"` all prefill as TRUE — the form would
 * then show a reviewer the opposite of what the document says, pre-filled and
 * looking authoritative.
 *
 * So the string forms are matched explicitly, and anything unrecognised is
 * refused outright. Refusing means the field is simply not pre-filled and the
 * reviewer picks it, which is the only safe outcome for a value this ambiguous:
 * a wrong pre-fill is far more dangerous than an empty one.
 */
function toBooleanValue(value) {
  if (typeof value === 'boolean') return value
  if (typeof value === 'number') {
    if (value === 1) return true
    if (value === 0) return false
    return undefined
  }
  if (typeof value !== 'string') return undefined

  const text = value.trim().toLowerCase()
  if (text === 'true' || text === 'yes') return true
  if (text === 'false' || text === 'no') return false
  return undefined
}

function suggestionDetails(extraction, fields) {
  const suggested = extraction?.suggestedDetails
  if (!suggested || typeof suggested !== 'object' || Array.isArray(suggested)) {
    return {}
  }
  // Only the keys this type actually declares are pre-filled, and each is
  // coerced to the shape its control expects.
  const next = {}
  for (const field of Array.isArray(fields) ? fields : []) {
    if (!field?.key) continue
    const value = suggested[field.key]
    if (value === null || value === undefined || value === '') continue

    if (field.valueKind === 'Date') {
      const date = toDateInputValue(value)
      if (date) next[field.key] = date
    } else if (field.valueKind === 'Number') {
      const numeric = Number(value)
      if (!Number.isNaN(numeric)) next[field.key] = numeric
    } else if (field.valueKind === 'Boolean') {
      const bool = toBooleanValue(value)
      // Undefined here means "not pre-filled", not "false".
      if (bool !== undefined) next[field.key] = bool
    } else {
      next[field.key] = String(value)
    }
  }
  return next
}

/**
 * The type-specific suggestions, coerced to the shapes their controls expect and
 * filtered to the keys this type actually declares.
 *
 * Exported because the review drawer seeds `details` in a second pass: the draft
 * arrives before the schema does, so the top-level fields can be seeded
 * immediately while the detail fields have to wait for the schema.
 */
export function prefillSuggestedDetails(extraction, fields) {
  return suggestionDetails(extraction, fields)
}

/**
 * Seeds the form from the extraction's suggestions.
 *
 * The whole set is a PREFILL, never an answer: every value stays editable, and
 * the owner id is deliberately left empty. The extractor reads a file and has no
 * way to know which employee or entity it belongs to, so a suggestion there
 * could only ever be a guess — and a guessed owner id would be worse than no
 * owner at all, because it is indistinguishable from a real one.
 */
export function prefillFromExtraction(extraction, fields) {
  return {
    type: extraction?.suggestedType ?? '',
    documentNumber: extraction?.suggestedDocumentNumber ?? '',
    issueDate: toDateInputValue(extraction?.suggestedIssueDate),
    expiryDate: toDateInputValue(extraction?.suggestedExpiryDate),
    ownerKind: normaliseSuggestedOwnerType(extraction?.suggestedOwnerType),
    employeeId: '',
    clientEntityId: '',
    details: suggestionDetails(extraction, fields),
  }
}

/**
 * The confirmation payload, and nothing else.
 *
 * The request record also accepts fileUrl / fileName / contentType / fileSize.
 * They are NOT sent. The server holds the authoritative stored reference for the
 * draft and copies it onto the Document during confirmation; anything sent from
 * the browser would be a second, unverified claim about where the file lives,
 * and the whole point of removing `fileUrl` from the read contracts is that the
 * browser never gets to name it.
 */
export function buildConfirmPayload(values) {
  const details = {}
  for (const [key, value] of Object.entries(values?.details ?? {})) {
    if (value === null || value === undefined || value === '') continue
    details[key] = value
  }

  const payload = {
    type: values.type,
    documentNumber: values.documentNumber.trim(),
    details,
  }

  if (values.issueDate) payload.issueDate = values.issueDate
  if (values.expiryDate) payload.expiryDate = values.expiryDate

  if (values.ownerKind === OWNER_KIND.employee) {
    payload.employeeId = values.employeeId
  } else {
    payload.clientEntityId = values.clientEntityId
  }

  return payload
}
