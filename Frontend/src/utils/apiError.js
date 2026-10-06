/**
 * Normalises an Axios error into a user-facing message.
 *
 * The API returns two different error bodies, so both have to be handled:
 *
 *  1. `{ error: { message, code, details? } }` - produced by
 *     GlobalExceptionMiddleware for FluentValidation, ArgumentException,
 *     NotFoundException and friends raised inside a handler.
 *  2. RFC 7807 ProblemDetails - `{ type, title, status, errors, traceId }` -
 *     produced by the automatic [ApiController] model-state response, which
 *     short-circuits before the middleware ever runs. ModelStateInvalidFilter
 *     is not suppressed, so this is the body for every binding/validation 400.
 *
 * A specific field message is preferred over the generic ProblemDetails title,
 * and Axios's own "Request failed with status code 4xx" is used only as a last
 * resort before the caller's fallback.
 */
export function extractApiErrorMessage(error, fallback) {
  const data = error?.response?.data

  const envelope = data?.error
  if (typeof envelope === 'string' && envelope.trim()) {
    return envelope
  }
  if (envelope?.message) {
    return envelope.message
  }

  if (data?.errors) {
    const fieldMessage = Object.values(data.errors)
      .flat()
      .find((message) => typeof message === 'string' && message.trim())
    if (fieldMessage) {
      return fieldMessage
    }
  }
  if (data?.title) {
    return data.title
  }

  if (typeof data === 'string' && data.trim()) {
    return data
  }

  if (error?.message) {
    return error.message
  }
  return fallback
}
