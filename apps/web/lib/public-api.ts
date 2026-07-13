import { AdeniApiClient } from "@adeni/api-client";

function getServerApiBaseUrl() {
  return (
    process.env.NEXT_PUBLIC_ADENI_API_URL ??
    process.env.ADENI_API_URL ??
    "http://localhost:5169"
  );
}

/** Client-safe API base URL (no Auth0 imports). */
export function getApiBaseUrl() {
  // Browser calls same-origin; Next.js rewrites /api/v1/* to the backend.
  if (typeof window !== "undefined") {
    return "";
  }

  return getServerApiBaseUrl();
}

/** Unauthenticated API client — safe for client components and public routes. */
export function createPublicApiClient() {
  return new AdeniApiClient({ baseUrl: getApiBaseUrl() });
}
