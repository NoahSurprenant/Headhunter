import { vi } from 'vitest';

/** Marker a stubFetch responder can return to simulate the API being unavailable. */
export const API_UNAVAILABLE = Symbol('API_UNAVAILABLE');

/**
 * Replaces the global fetch used by the resource() loaders. Each call is
 * answered with `respond(url, init)` serialised as JSON. Returning
 * API_UNAVAILABLE answers with a 502 HTML page instead, which is what the
 * client sees when the API is down, so `response.json()` rejects.
 */
export function stubFetch(respond: (url: string, init?: RequestInit) => unknown) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const body = respond(String(input), init);
    if (body === API_UNAVAILABLE)
      return new Response('<!doctype html><title>Bad Gateway</title>', {
        status: 502,
        headers: { 'Content-Type': 'text/html' },
      });
    return new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}
