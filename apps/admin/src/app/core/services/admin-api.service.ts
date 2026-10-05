import { inject, Injectable, Injector } from "@angular/core";
import { AdeniApiClient } from "@adeni/api-client";
import { auth0FirstValueFrom, resolveAuthService } from "../auth0-rxjs";
import {
  ADENI_ADMIN_CONFIG,
  isAdminPortalDevMode,
  isAuth0Configured,
} from "../adeni-config";

@Injectable({ providedIn: "root" })
export class AdminApiService {
  private readonly config = inject(ADENI_ADMIN_CONFIG);
  private readonly injector = inject(Injector);

  async withAuthorizedClient<T>(
    fn: (client: AdeniApiClient) => Promise<T>,
  ): Promise<T> {
    const client = await this.createAuthorizedClient();
    return fn(client);
  }

  async createAuthorizedClient(): Promise<AdeniApiClient> {
    const client = new AdeniApiClient({ baseUrl: this.config.apiBaseUrl });

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
    } else if (isAdminPortalDevMode(this.config)) {
      client.setDevAuth0Sub(this.config.devAdminAuth0Sub);
    } else {
      throw new Error("Admin API access requires Auth0 or devAdminAuth0Sub.");
    }

    return client;
  }
}
