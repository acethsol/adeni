import { AdeniApiClient } from "@adeni/api-client";
import { getAccessToken } from "./auth/session";
import { isAuth0Configured } from "./auth/config";

export function getApiBaseUrl() {
  return process.env.ADENI_API_URL ?? "http://localhost:5169";
}

export function createApiClient() {
  return new AdeniApiClient({ baseUrl: getApiBaseUrl() });
}

export async function createAuthenticatedApiClient() {
  const client = createApiClient();
  if (isAuth0Configured()) {
    const token = await getAccessToken();
    client.setAccessToken(token);
  }

  return client;
}
