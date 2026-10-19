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
import { SeoService } from "./core/services/seo.service";
import { provideDiscoverRealtimeAuth } from "./core/adeni-realtime-auth.provider";

function auth0Providers() {
  const config = adeniDiscoverConfigFactory();
  if (!isAuth0Configured(config)) {
    return [];
  }

  const browser = typeof window !== "undefined";
  return [
    provideAuth0({
      domain: config.auth0.domain,
      clientId: config.auth0.clientId,
      authorizationParams: {
        redirect_uri: browser ? window.location.origin : config.publicAppUrl,
        audience: config.auth0.audience,
      },
      // Avoid localStorage on SSR (NG0203 / Auth0 browser APIs).
      cacheLocation: browser ? "localstorage" : "memory",
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
    ...provideAdeniDocumentTitle("Adeni Discover"),
    ...provideAdeniLocale({ storage: "cookie" }),
    provideClientHydration(),
    {
      provide: ADENI_DISCOVER_CONFIG,
      useFactory: adeniDiscoverConfigFactory,
    },
    ...auth0Providers(),
    provideDiscoverRealtimeAuth(),
    provideAppInitializer(() => {
      const config = inject(ADENI_DISCOVER_CONFIG);
      inject(SeoService).setGoogleSiteVerification(config.googleSiteVerification);
      return inject(MarketContextService).bootstrap();
    }),
  ],
};
