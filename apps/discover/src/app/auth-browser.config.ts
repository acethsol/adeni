import { EnvironmentProviders } from "@angular/core";
import { provideAuth0 } from "@auth0/auth0-angular";
import {
  adeniDiscoverConfigFactory,
  isAuth0Configured,
} from "./core/adeni-config";

/** Browser-only — merged in `main.ts`, not `main.server.ts`. */
export function discoverBrowserAuthProviders(): EnvironmentProviders[] {
  const config = adeniDiscoverConfigFactory();
  if (!isAuth0Configured(config)) {
    return [];
  }

  return [
    provideAuth0({
      domain: config.auth0.domain,
      clientId: config.auth0.clientId,
      authorizationParams: {
        redirect_uri: window.location.origin,
        audience: config.auth0.audience,
      },
      cacheLocation: "localstorage",
      httpInterceptor: {
        allowedList: [
          {
            uri: `${config.apiBaseUrl}/api/*`,
            tokenOptions: {
              authorizationParams: {
                audience: config.auth0.audience,
              },
            },
          },
        ],
      },
    }),
  ];
}
