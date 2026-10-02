import {
  ApplicationConfig,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  inject,
} from "@angular/core";
import { provideRouter, withComponentInputBinding } from "@angular/router";
import { provideClientHydration } from "@angular/platform-browser";
import { routes } from "./app.routes";
import { ADENI_DISCOVER_CONFIG, adeniDiscoverConfigFactory } from "./core/adeni-config";
import { MarketContextService } from "./core/services/market-context.service";

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideClientHydration(),
    {
      provide: ADENI_DISCOVER_CONFIG,
      useFactory: adeniDiscoverConfigFactory,
    },
    provideAppInitializer(() => inject(MarketContextService).bootstrap()),
  ],
};
