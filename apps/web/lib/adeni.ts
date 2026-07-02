import { AdeniApiClient } from "@adeni/api-client";
import { getMarket, resolveMarketId } from "@adeni/shared";
import { getAccessToken } from "./auth/session";
import { isAuth0Configured } from "./auth/config";

export function getApiBaseUrl() {
  return process.env.ADENI_API_URL ?? "http://localhost:5169";
}

export function getConfiguredMarket() {
  return getMarket(resolveMarketId(process.env.NEXT_PUBLIC_ADENI_MARKET));
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
