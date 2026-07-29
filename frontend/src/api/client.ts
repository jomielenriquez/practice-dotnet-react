/** Thrown for any failed API call. `status` is 0 when the request never landed. */
export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/**
 * Fetches JSON from the API and returns it as `T`.
 *
 * Paths are relative (`/api/hello`) so the Vite dev proxy can forward them to
 * the backend; in production the same relative path is served by whatever sits
 * in front of the app.
 *
 * Throws `ApiError` on a network failure or a non-2xx response, so callers only
 * need one catch to cover both.
 */
export async function apiFetch<T>(
  path: string,
  init?: RequestInit,
): Promise<T> {
  let response: Response

  try {
    response = await fetch(path, {
      ...init,
      headers: { Accept: 'application/json', ...init?.headers },
    })
  } catch (cause) {
    // An aborted request is a caller-initiated cancellation, not a failure.
    if (cause instanceof DOMException && cause.name === 'AbortError') {
      throw cause
    }
    throw new ApiError(
      'Could not reach the API. Is the backend running on http://localhost:5080?',
      0,
    )
  }

  if (!response.ok) {
    // The Vite dev proxy answers 502/503/504 when it cannot reach the backend,
    // so surface that as the actionable message rather than a raw status.
    if (response.status >= 502 && response.status <= 504) {
      throw new ApiError(
        'Could not reach the API. Is the backend running on http://localhost:5080?',
        response.status,
      )
    }
    throw new ApiError(
      `GET ${path} failed: ${response.status} ${response.statusText}`,
      response.status,
    )
  }

  return (await response.json()) as T
}
