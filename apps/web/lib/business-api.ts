import { AdeniApiClient } from "@adeni/api-client";
import { createApiClient, getApiBaseUrl } from "./adeni";
import { getAccessToken } from "./auth/session";
import { isAuth0Configured } from "./auth/config";

function getDevBusinessAuth0Sub(): string | undefined {
  return process.env.DEV_BUSINESS_AUTH0_SUB?.trim() || undefined;
}

export function isBusinessPortalDevMode(): boolean {
  return !isAuth0Configured() && Boolean(getDevBusinessAuth0Sub());
}

export async function createBusinessApiClient(): Promise<AdeniApiClient> {
  const client = createApiClient();
  const accessToken = await getAccessToken();

  if (accessToken) {
    client.setAccessToken(accessToken);
    try {
      const session = await client.getMe();
      if (session.tenantId) {
        client.setTenantId(session.tenantId);
      }
    } catch {
      // Tenant may still resolve from JWT claims on tenant routes.
    }
    return client;
  }

  const devSub = getDevBusinessAuth0Sub();
  if (devSub) {
    client.setDevAuth0Sub(devSub);
    return client;
  }

  throw new Error("Business API access requires Auth0 or DEV_BUSINESS_AUTH0_SUB.");
}

export async function businessApiFetch(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");

  const accessToken = await getAccessToken();
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  } else if (isBusinessPortalDevMode()) {
    headers.set("X-Dev-Auth0-Sub", getDevBusinessAuth0Sub()!);
  } else {
    return new Response(JSON.stringify({ title: "Unauthorized" }), { status: 401 });
  }

  return fetch(`${getApiBaseUrl()}${path}`, {
    ...init,
    headers,
  });
}
