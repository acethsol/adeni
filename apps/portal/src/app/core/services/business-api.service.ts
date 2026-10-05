import { inject, Injectable, Injector } from "@angular/core";
import { AdeniApiClient } from "@adeni/api-client";
import type { BusinessProfile } from "@adeni/shared";
import { auth0FirstValueFrom, resolveAuthService } from "../auth0-rxjs";
import {
  ADENI_PORTAL_CONFIG,
  isAuth0Configured,
  isBusinessPortalDevMode,
} from "../adeni-config";

@Injectable({ providedIn: "root" })
export class BusinessApiService {
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly injector = inject(Injector);

  createPublicClient(): AdeniApiClient {
    return new AdeniApiClient({ baseUrl: this.config.apiBaseUrl });
  }

  private createClient(): AdeniApiClient {
    return this.createPublicClient();
  }

  async withAuthorizedClient<T>(
    fn: (client: AdeniApiClient) => Promise<T>,
  ): Promise<T> {
    const client = await this.createAuthorizedClient();
    return fn(client);
  }

  async createAuthorizedClient(): Promise<AdeniApiClient> {
    const client = this.createClient();

    const auth = isAuth0Configured(this.config) ? resolveAuthService(this.injector) : null;
    if (auth) {
      const token = await auth0FirstValueFrom<string>(
        auth.getAccessTokenSilently({
          authorizationParams: {
            audience: this.config.auth0.audience,
          },
        }),
      );
      client.setAccessToken(token);
    } else if (isBusinessPortalDevMode(this.config)) {
      client.setDevAuth0Sub(this.config.devBusinessAuth0Sub);
    } else {
      throw new Error("Business API access requires Auth0 or devBusinessAuth0Sub.");
    }

    const tenantId = await this.resolveTenantId(client);
    if (tenantId) {
      client.setTenantId(tenantId);
    }

    return client;
  }

  async getTenantProfile(): Promise<BusinessProfile | null> {
    try {
      const client = await this.createAuthorizedClient();
      return await client.getTenantProfile();
    } catch {
      return null;
    }
  }

  private async resolveTenantId(client: AdeniApiClient): Promise<string | null> {
    if (isAuth0Configured(this.config)) {
      try {
        const session = await client.getMe();
        if (session.tenantId) {
          return session.tenantId;
        }
      } catch {
        // Fall through to business context.
      }
    }

    try {
      const context = await client.getBusinessContext();
      return context.tenantId;
    } catch {
      return null;
    }
  }
}
