import { inject, Injectable, Injector } from "@angular/core";
import { AdeniApiClient } from "@adeni/api-client";
import { auth0FirstValueFrom } from "../auth0-rxjs";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../adeni-config";

@Injectable({ providedIn: "root" })
export class CustomerApiService {
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly injector = inject(Injector);

  createPublicClient(): AdeniApiClient {
    return new AdeniApiClient({ baseUrl: this.config.apiBaseUrl });
  }

  /** @deprecated use createPublicClient */
  createClient(): AdeniApiClient {
    return this.createPublicClient();
  }

  async withAuthorizedClient<T>(
    fn: (client: AdeniApiClient) => Promise<T>,
  ): Promise<T> {
    const client = await this.createAuthorizedClient();
    return fn(client);
  }

  async createAuthorizedClient(): Promise<AdeniApiClient> {
    const client = this.createPublicClient();

    if (isAuth0Configured(this.config)) {
      const token = await this.getAccessToken();
      if (!token) {
        throw new Error("Sign in to continue.");
      }
      client.setAccessToken(token);
    } else if (isDiscoverCustomerDevMode(this.config)) {
      client.setDevAuth0Sub(this.config.devCustomerAuth0Sub);
    } else {
      throw new Error("Customer API access requires Auth0 or devCustomerAuth0Sub.");
    }

    return client;
  }

  async isLoggedIn(): Promise<boolean> {
    if (isDiscoverCustomerDevMode(this.config)) {
      return true;
    }

    if (!isAuth0Configured(this.config)) {
      return false;
    }

    const auth = await this.loadAuthService();
    if (!auth) {
      return false;
    }

    return auth0FirstValueFrom(auth.isAuthenticated$);
  }

  async login(returnPath: string): Promise<void> {
    const auth = await this.loadAuthService();
    if (!auth) {
      return;
    }

    await auth0FirstValueFrom(
      auth.loginWithRedirect({
        appState: { target: returnPath },
      }),
    );
  }

  async logout(): Promise<void> {
    const auth = await this.loadAuthService();
    if (!auth) {
      return;
    }

    auth.logout({
      logoutParams: {
        returnTo: this.config.publicAppUrl,
      },
    });
  }

  private async getAccessToken(): Promise<string | null> {
    const auth = await this.loadAuthService();
    if (!auth) {
      return null;
    }

    const token = await auth0FirstValueFrom(
      auth.getAccessTokenSilently({
        authorizationParams: {
          audience: this.config.auth0.audience,
        },
      }),
    );
    return token ?? null;
  }

  private async loadAuthService(): Promise<
    import("@auth0/auth0-angular").AuthService | null
  > {
    if (typeof window === "undefined" || !isAuth0Configured(this.config)) {
      return null;
    }

    const { AuthService } = await import("@auth0/auth0-angular");
    try {
      return this.injector.get(AuthService);
    } catch {
      return null;
    }
  }
}
