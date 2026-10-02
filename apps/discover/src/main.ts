import { mergeApplicationConfig } from "@angular/core";
import { bootstrapApplication } from "@angular/platform-browser";
import { appConfig } from "./app/app.config";
import { discoverBrowserAuthProviders } from "./app/auth-browser.config";
import { App } from "./app/app";

bootstrapApplication(
  App,
  mergeApplicationConfig(appConfig, {
    providers: discoverBrowserAuthProviders(),
  }),
).catch((err) => console.error(err));
