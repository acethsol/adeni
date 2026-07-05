import { AdeniApiClient } from "@adeni/api-client";
import type { BusinessContextResponse } from "@adeni/shared";
import { getApiBaseUrl } from "@/lib/api";

type BusinessClientOptions = {
  accessToken: string | null;
  devAuth0Sub?: string | null;
  tenantId?: string | null;
};

export async function resolveBusinessTenantId(
  client: AdeniApiClient,
  fallbackTenantId?: string | null,
): Promise<string | null> {
  if (fallbackTenantId) {
    return fallbackTenantId;
  }

  try {
    const session = await client.getMe();
    if (session.tenantId) {
      return session.tenantId;
    }
  } catch {
    // Fall through to business context lookup.
  }

  try {
    const context = await client.getBusinessContext();
    return context.tenantId;
  } catch {
    return null;
  }
}

export async function loadBusinessContext(
  options: BusinessClientOptions,
): Promise<BusinessContextResponse | null> {
  const client = new AdeniApiClient({
    baseUrl: getApiBaseUrl(),
    accessToken: options.accessToken,
    devAuth0Sub: options.devAuth0Sub ?? null,
  });

  try {
    return await client.getBusinessContext();
  } catch {
    return null;
  }
}

export async function createBusinessApiClient(
  options: BusinessClientOptions,
): Promise<AdeniApiClient> {
  const client = new AdeniApiClient({
    baseUrl: getApiBaseUrl(),
    accessToken: options.accessToken,
    devAuth0Sub: options.devAuth0Sub ?? null,
  });

  const tenantId = await resolveBusinessTenantId(client, options.tenantId);
  if (tenantId) {
    client.setTenantId(tenantId);
  }

  return client;
}
