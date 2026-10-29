import { Injector, type Provider } from "@angular/core";
import { type AdeniRealtimeAuth, ADENI_REALTIME_AUTH } from "@adeni/ui";
import { auth0FirstValueFrom, resolveAuthService } from "./auth0-rxjs";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
  type AdeniDiscoverConfig,
} from "./adeni-config";

export function provideDiscoverRealtimeAuth(): Provider {
  return {
    provide: ADENI_REALTIME_AUTH,
    deps: [ADENI_DISCOVER_CONFIG, Injector],
    useFactory: (config: AdeniDiscoverConfig, injector: Injector): AdeniRealtimeAuth => ({
      getHubUrl(hub) {
        const base = `${config.apiBaseUrl}/hubs/${hub}`;
        if (!isAuth0Configured(config) && isDiscoverCustomerDevMode(config)) {
          const sub = encodeURIComponent(config.devCustomerAuth0Sub);
          return `${base}?dev_auth0_sub=${sub}`;
        }
        return base;
      },
      async getAccessToken() {
        if (!isAuth0Configured(config)) {
          return "";
        }
        const auth = resolveAuthService(injector);
        if (!auth) {
          return "";
        }
        const token = await auth0FirstValueFrom(
          auth.getAccessTokenSilently({
            authorizationParams: { audience: config.auth0.audience },
          }),
        );
        return token ?? "";
      },
    }),
  };
}
