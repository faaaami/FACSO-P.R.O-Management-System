import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Loads a document file through an authorized blob endpoint and hands back an
 * object URL for the browser to render or download.
 *
 * SHARED BY AGENT, ADMIN AND CLIENT. The DTOs expose a relative storage
 * reference that a browser cannot fetch, so file access always goes through a
 * role-scoped endpoint that returns raw bytes (`responseType: 'blob'`).
 *
 * WHY THIS IS NOT A react-query CACHE. The file is not server state that more
 * than one component renders from: it is fetched when a reviewer asks to see it,
 * and it is unusable outside a blob URL. A cache entry would have to keep the
 * Blob alive in memory anyway, with no deduplication to gain, so the bytes are
 * held in local state and the URL is revoked promptly.
 *
 * `identity` is the equivalent of a query key: a `[role, id]` pair naming which
 * document's bytes are on screen. It is not used to cache anything — it is used
 * to make it impossible for one document's file to be shown under another, which
 * is the guarantee a query key would otherwise have provided. Changing identity
 * drops the previous bytes immediately.
 *
 * OBJECT URL LIFETIME: every URL handed out here is revoked when it is replaced
 * and again on unmount. A blob URL is not garbage collected, so a drawer that
 * is opened and closed repeatedly would otherwise pin the whole file in memory
 * for the life of the tab.
 *
 * ERROR HANDLING: a blob endpoint still answers failures with the normal JSON
 * error envelope, but axios hands it back as a Blob, so the usual
 * `error.response.data.error.message` path finds nothing and the user would see
 * a bare "Request failed with status code 4xx". The blob is decoded here so the
 * real message reaches the UI.
 */
export function useDocumentFileBlob(fetcher, { enabled = true, identity = null } = {}) {
  const [url, setUrl] = useState(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState(null)

  // Kept in a ref so load() can revoke a previous URL immediately, even if the
  // two loads resolve out of order.
  const activeUrlRef = useRef(null)

  // Orders the in-flight requests. A response is only applied if it is still the
  // newest one, so a slow load for a document the reviewer has already moved on
  // from can never land on top of the file they are now looking at. Tracking
  // only the URL is not enough: the stale response still creates a fresh URL, so
  // revoking "the previous" URL would revoke the wrong one.
  const requestIdRef = useRef(0)

  useEffect(() => {
    return () => {
      if (activeUrlRef.current) {
        URL.revokeObjectURL(activeUrlRef.current)
        activeUrlRef.current = null
      }
    }
  }, [])

  // A new document means a new file. Anything still on screen belongs to the
  // previous one, so it is revoked rather than left to be attributed to this
  // document.
  useEffect(() => {
    requestIdRef.current += 1
    if (activeUrlRef.current) {
      URL.revokeObjectURL(activeUrlRef.current)
      activeUrlRef.current = null
    }
    setUrl(null)
    setError(null)
    setIsLoading(false)
  }, [identity])

  const load = useCallback(async () => {
    if (typeof fetcher !== 'function' || !enabled) return null

    requestIdRef.current += 1
    const requestId = requestIdRef.current

    setIsLoading(true)
    setError(null)
    try {
      const blob = await fetcher()
      // Superseded: the reviewer switched documents or asked again while this was
      // in flight, so this result is stale and must not be shown. No URL was ever
      // created for it, so there is nothing to revoke.
      if (requestId !== requestIdRef.current) return null

      const nextUrl = URL.createObjectURL(blob)
      if (activeUrlRef.current) {
        URL.revokeObjectURL(activeUrlRef.current)
      }
      activeUrlRef.current = nextUrl
      setUrl(nextUrl)
      return nextUrl
    } catch (caught) {
      if (requestId !== requestIdRef.current) return null
      setError(await decodeBlobError(caught))
      return null
    } finally {
      if (requestId === requestIdRef.current) setIsLoading(false)
    }
  }, [fetcher, enabled])

  const clear = useCallback(() => {
    requestIdRef.current += 1
    if (activeUrlRef.current) {
      URL.revokeObjectURL(activeUrlRef.current)
      activeUrlRef.current = null
    }
    setUrl(null)
    setError(null)
    setIsLoading(false)
  }, [])

  return { url, isLoading, error, load, clear }
}

/**
 * Pulls a readable message out of an Axios error whose body arrived as a Blob.
 * Falls back to the plain message, then to the caller's fallback text.
 */
async function decodeBlobError(caught) {
  const data = caught?.response?.data
  if (typeof Blob !== 'undefined' && data instanceof Blob) {
    try {
      const text = await data.text()
      const parsed = JSON.parse(text)
      const message = parsed?.error?.message ?? parsed?.title ?? parsed?.error
      if (typeof message === 'string' && message.trim()) return message
    } catch {
      // Not JSON, or the body was already consumed: fall through.
    }
  }
  return (
    caught?.message ??
    'The file could not be loaded. It may have expired or you may not have access.'
  )
}

export default useDocumentFileBlob
