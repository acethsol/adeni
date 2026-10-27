import {
  localizeErrorResponse,
  type ApiErrorResponse,
} from "@adeni/shared";
export class AdeniApiError extends Error {
  constructor(
    message: string,
    readonly statusCode?: number,
    readonly code?: string,
    readonly params?: ApiErrorResponse["params"],
  ) {
    super(message);
    this.name = "AdeniApiError";
  }
}

export type AdeniApiClientOptions = {
  baseUrl: string;
  accessToken?: string | null;
  tenantId?: string | null;
  /** Development only — maps to X-Dev-Auth0-Sub when Auth0 is disabled on the API. */
  devAuth0Sub?: string | null;
  /** Locale for translating API error codes (defaults to en). Pass a getter to follow runtime locale changes. */
  locale?: string | (() => string);
  fetchImpl?: typeof fetch;
};

export type ApiClientContext = {
  readonly baseUrl: string;
  request(path: string, init?: RequestInit & { idempotencyKey?: string }): Promise<Response>;
};

export class ApiTransport {
  private accessToken: string | null;

  private tenantId: string | null;

  private devAuth0Sub: string | null;

  private readonly locale: string | (() => string);

  protected readonly baseUrl: string;

  private readonly fetchImpl: typeof fetch;

  constructor(options: AdeniApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.accessToken = options.accessToken ?? null;
    this.tenantId = options.tenantId ?? null;
    this.devAuth0Sub = options.devAuth0Sub ?? null;
    this.locale = options.locale ?? "en";
    this.fetchImpl = options.fetchImpl ?? fetch;
  }

  setAccessToken(token: string | null) {
    this.accessToken = token;
  }

  setDevAuth0Sub(auth0Sub: string | null) {
    this.devAuth0Sub = auth0Sub;
  }

  setTenantId(tenantId: string | null) {
    this.tenantId = tenantId;
  }

  protected async request(
    path: string,
    init: RequestInit & { idempotencyKey?: string } = {},
  ): Promise<Response> {
    const { idempotencyKey, ...requestInit } = init;
    const headers = new Headers(requestInit.headers);
    headers.set("Accept", "application/json");

    if (this.accessToken) {
      headers.set("Authorization", `Bearer ${this.accessToken}`);
    } else if (this.devAuth0Sub) {
      headers.set("X-Dev-Auth0-Sub", this.devAuth0Sub);
    }

    if (this.tenantId) {
      headers.set("X-Tenant-Id", this.tenantId);
    }

    if (idempotencyKey) {
      headers.set("Idempotency-Key", idempotencyKey);
    }

    // Native fetch throws "Illegal invocation" when called as a method (`this.fetchImpl(...)`).
    const response = await this.fetchImpl.call(globalThis, `${this.baseUrl}${path}`, {
      ...requestInit,
      headers,
    });

    if (!response.ok) {
      const payload = (await response.json().catch(() => ({}))) as ApiErrorResponse;
      throw new AdeniApiError(
        localizeErrorResponse(typeof this.locale === "function" ? this.locale() : this.locale, payload),
        response.status,
        payload.code,
        payload.params,
      );
    }

    return response;
  }
}
