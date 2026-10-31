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
import {
  ADENI_DISCOVER_CONFIG,
  adeniDiscoverConfigFactory,
} from "./core/adeni-config";
import { MarketContextService } from "./core/services/market-context.service";
import { SeoService } from "./core/services/seo.service";
import { AuthReturnService } from "./core/services/auth-return.service";
import { provideDiscoverRealtimeAuth } from "./core/adeni-realtime-auth.provider";

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
    ...provideAdeniDocumentTitle("Adeni Customer"),
    ...provideAdeniLocale({ storage: "cookie" }),
    provideClientHydration(),
    {
      provide: ADENI_DISCOVER_CONFIG,
      useFactory: adeniDiscoverConfigFactory,
    },
    provideDiscoverRealtimeAuth(),
    provideAppInitializer(() => {
      const config = inject(ADENI_DISCOVER_CONFIG);
      inject(SeoService).setGoogleSiteVerification(config.googleSiteVerification);
      // Browser-only no-op elsewhere: honors Auth0 appState.target after the redirect callback.
      inject(AuthReturnService).start();
      return inject(MarketContextService).bootstrap();
    }),
  ],
};
