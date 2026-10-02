import { ApplicationConfig, provideBrowserGlobalErrorListeners } from "@angular/core";
import { provideRouter } from "@angular/router";
import { provideClientHydration } from "@angular/platform-browser";
import { routes } from "./app.routes";
import {
  ADENI_DISCOVER_CONFIG,
  adeniDiscoverConfigFactory,
} from "./core/adeni-config";

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(),
    {
      provide: ADENI_DISCOVER_CONFIG,
      useFactory: adeniDiscoverConfigFactory,
    },
  ],
};
