import {
  ApplicationConfig,
  provideZoneChangeDetection,
} from "@angular/core";
import { provideRouter } from "@angular/router";
import { provideAdeniDocumentTitle } from "@adeni/ui";
import { provideHttpClient } from "@angular/common/http";
import { provideAuth0 } from "@auth0/auth0-angular";
import { routes } from "./app.routes";
import {
  ADENI_PORTAL_CONFIG,
  adeniPortalConfigFactory,
  isAuth0Configured,
} from "./core/adeni-config";

function auth0Providers() {
  const config = adeniPortalConfigFactory();
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

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideAdeniDocumentTitle("Adeni Business"),
    provideHttpClient(),
    {
      provide: ADENI_PORTAL_CONFIG,
      useFactory: adeniPortalConfigFactory,
    },
    ...auth0Providers(),
  ],
};
