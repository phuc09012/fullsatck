const API_BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

export async function apiRequest(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (!headers.has('Content-Type') && options.body && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  const normalizedPath = path.startsWith('/api') ? path : `${API_BASE}${path}`;
  const response = await fetch(normalizedPath, {
    ...options,
    headers
  });

  const contentType = response.headers.get('content-type') || '';
  const data = contentType.includes('application/json') ? await response.json() : await response.text();

  if (!response.ok) {
    const message = typeof data === 'string' ? data : data?.message || response.statusText;
    throw new Error(message || 'Request failed');
  }

  return data;
}
