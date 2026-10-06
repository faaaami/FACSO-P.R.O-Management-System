/**
 * Attaches backend validation messages to React Hook Form fields.
 *
 * The API returns two different 400 bodies (both documented in
 * src/utils/apiError.js, which is deliberately left untouched):
 *
 *   1. `{ error: { message, code, details } }` - produced by
 *      GlobalExceptionMiddleware when a handler raises a ValidationException.
 *      `details` is a Dictionary<string, string[]> keyed by the C# *property*
 *      name. Dictionary keys are data rather than member names, so the JSON
 *      serializer does not camelCase them: they arrive PascalCase, e.g.
 *      "CurrentPassword", "NewPassword", "ConfirmPassword", "FullName", "Phone".
 *
 *   2. RFC 7807 ProblemDetails `{ errors }` - produced by the automatic
 *      [ApiController] model-state response, which short-circuits before the
 *      middleware ever runs. Its keys are already camelCase.
 *
 * extractApiErrorMessage collapses both bodies to one string, so it cannot be
 * used to attach a message to a specific field. This helper covers that gap by
 * normalising either body into `{ camelCaseField: message }` plus an iterable
 * form. RHF fields in this codebase are camelCase, so the only work needed is a
 * lower-cased first character.
 *
 * Every function here is total: a missing error, a missing response, a missing
 * `details`, or a `details` value of any shape yields an empty result rather
 * than throwing, because these helpers run directly in `onError` callbacks.
 */

/** Lower-cases only the first character, leaving the rest of the word intact. */
export function toCamelCaseKey(key) {
  if (typeof key !== 'string' || key.length === 0) {
    return ''
  }
  if (key.charAt(0) !== key.charAt(0).toUpperCase()) {
    return key
  }
  return key.charAt(0).toLowerCase() + key.slice(1)
}

/**
 * Flattens one `details` value into an array of messages. The backend types this
 * as string[], but hand-written callers, nested `ErrorDetails`-shaped objects and
 * bare strings all appear in practice, so every shape is reduced to text.
 */
function toMessages(value) {
  if (value === null || value === undefined) {
    return []
  }
  if (typeof value === 'string') {
    return value.trim() ? [value] : []
  }
  if (Array.isArray(value)) {
    return value.flatMap(toMessages)
  }
  if (typeof value === 'object') {
    if (typeof value.message === 'string') {
      return toMessages(value.message)
    }
    return Object.values(value).flatMap(toMessages)
  }
  return [String(value)]
}

/**
 * Returns the raw key/value map of validation details, preferring the
 * ApiResponse envelope and falling back to ProblemDetails. Returns null when
 * neither body carries field-level information.
 */
export function apiErrorDetails(error) {
  const data = error?.response?.data

  const envelopeDetails = data?.error?.details
  if (envelopeDetails && typeof envelopeDetails === 'object' && !Array.isArray(envelopeDetails)) {
    return envelopeDetails
  }

  const problemDetailsErrors = data?.errors
  if (
    problemDetailsErrors &&
    typeof problemDetailsErrors === 'object' &&
    !Array.isArray(problemDetailsErrors)
  ) {
    return problemDetailsErrors
  }

  return null
}

/**
 * Normalised field errors as `{ [camelCaseField]: firstMessage }`.
 * Safe to call with anything; returns an empty object when there is nothing to map.
 */
export function apiFieldErrors(error) {
  const details = apiErrorDetails(error)
  if (!details) {
    return {}
  }

  const result = {}
  for (const [rawKey, rawValue] of Object.entries(details)) {
    const field = toCamelCaseKey(rawKey)
    if (!field) {
      continue
    }
    const messages = toMessages(rawValue)
    if (messages.length > 0 && result[field] === undefined) {
      result[field] = messages[0]
    }
  }
  return result
}

/**
 * The same errors in a shape a form can iterate, keeping every message rather
 * than only the first. Each entry is `{ field, message, messages }`.
 */
export function apiFieldErrorEntries(error) {
  const details = apiErrorDetails(error)
  if (!details) {
    return []
  }

  const entries = []
  const seen = new Set()
  for (const [rawKey, rawValue] of Object.entries(details)) {
    const field = toCamelCaseKey(rawKey)
    if (!field || seen.has(field)) {
      continue
    }
    const messages = toMessages(rawValue)
    if (messages.length === 0) {
      continue
    }
    seen.add(field)
    entries.push({ field, message: messages[0], messages })
  }
  return entries
}

/** True when the failure carries at least one mappable field error. */
export function hasApiFieldErrors(error) {
  return apiFieldErrorEntries(error).length > 0
}
