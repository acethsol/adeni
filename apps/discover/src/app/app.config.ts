import {
  ApplicationConfig,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  inject,
} from "@angular/core";
import {
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
} from "@angular/router";
import { provideAdeniDocumentTitle, provideAdeniLocale } from "@adeni/ui";
import { provideClientHydration } from "@angular/platform-browser";
import { routes } from "./app.routes";
import { provideAuth0 } from "@auth0/auth0-angular";
import {
  ADENI_DISCOVER_CONFIG,
  adeniDiscoverConfigFactory,
  isAuth0Configured,
} from "./core/adeni-config";
import { MarketContextService } from "./core/services/market-context.service";

function auth0Providers() {
  const config = adeniDiscoverConfigFactory();
  if (!isAuth0Configured(config)) {
    return [];
  }

  return [
    provideAuth0({
      domain: config.auth0.domain,
      clientId: config.auth0.clientId,
      authorizationParams: {
        redirect_uri: typeof window !== "undefined" ? window.location.origin : config.publicAppUrl,
        audience: config.auth0.audience,
      },
      cacheLocation: "localstorage",
    }),
  ];
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({
        anchorScrolling: "enabled",
        scrollPositionRestoration: "enabled",
      }),
    ),
    provideAdeniDocumentTitle("Adeni Discover"),
    ...provideAdeniLocale({ storage: "cookie" }),
    provideClientHydration(),
    {
      provide: ADENI_DISCOVER_CONFIG,
      useFactory: adeniDiscoverConfigFactory,
    },
    ...auth0Providers(),
    provideAppInitializer(() => inject(MarketContextService).bootstrap()),
  ],
};
