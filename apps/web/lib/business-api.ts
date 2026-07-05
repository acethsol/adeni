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

async function resolveBusinessTenantId(client: AdeniApiClient): Promise<string | null> {
  if (isAuth0Configured()) {
    try {
      const session = await client.getMe();
      if (session.tenantId) {
        return session.tenantId;
      }
    } catch {
      // Fall through to business context lookup.
    }
  }

  try {
    const context = await client.getBusinessContext();
    return context.tenantId;
  } catch {
    return null;
  }
}

export async function createBusinessApiClient(): Promise<AdeniApiClient> {
  const client = createApiClient();
  const accessToken = await getAccessToken();

  if (accessToken) {
    client.setAccessToken(accessToken);
  } else {
    const devSub = getDevBusinessAuth0Sub();
    if (devSub) {
      client.setDevAuth0Sub(devSub);
    } else {
      throw new Error("Business API access requires Auth0 or DEV_BUSINESS_AUTH0_SUB.");
    }
  }

  const tenantId = await resolveBusinessTenantId(client);
  if (tenantId) {
    client.setTenantId(tenantId);
  }

  return client;
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

  const client = createApiClient();
  if (accessToken) {
    client.setAccessToken(accessToken);
  } else {
    client.setDevAuth0Sub(getDevBusinessAuth0Sub()!);
  }

  const tenantId = await resolveBusinessTenantId(client);
  if (tenantId) {
    headers.set("X-Tenant-Id", tenantId);
  }

  return fetch(`${getApiBaseUrl()}${path}`, {
    ...init,
    headers,
  });
}
