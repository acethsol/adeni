import { AdeniApiClient } from "@adeni/api-client";
import { createApiClient, getApiBaseUrl } from "./adeni";
import { getAccessToken } from "./auth/session";
import { isAuth0Configured } from "./auth/config";

function getDevCustomerAuth0Sub(): string | undefined {
  return process.env.DEV_CUSTOMER_AUTH0_SUB?.trim() || undefined;
}

export function isCustomerDevMode(): boolean {
  return !isAuth0Configured() && Boolean(getDevCustomerAuth0Sub());
}

export async function createCustomerApiClient(): Promise<AdeniApiClient> {
  const client = createApiClient();
  const accessToken = await getAccessToken();

  if (accessToken) {
    client.setAccessToken(accessToken);
    return client;
  }

  const devSub = getDevCustomerAuth0Sub();
  if (devSub) {
    client.setDevAuth0Sub(devSub);
    return client;
  }

  throw new Error("Customer API access requires Auth0 or DEV_CUSTOMER_AUTH0_SUB.");
}

export async function customerApiFetch(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");

  const accessToken = await getAccessToken();
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  } else if (isCustomerDevMode()) {
    headers.set("X-Dev-Auth0-Sub", getDevCustomerAuth0Sub()!);
  } else {
    return new Response(JSON.stringify({ title: "Unauthorized" }), { status: 401 });
  }

  return fetch(`${getApiBaseUrl()}${path}`, {
    ...init,
    headers,
  });
}
