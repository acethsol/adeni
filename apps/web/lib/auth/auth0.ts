import { Auth0Client } from "@auth0/nextjs-auth0/server";
import { getAuth0Audience } from "./config";

let client: Auth0Client | null = null;

/** Lazily created so dev without Auth0 env vars does not warn at import time. */
export function getAuth0(): Auth0Client {
  if (!client) {
    client = new Auth0Client({
      authorizationParameters: {
        audience: getAuth0Audience(),
        scope: "openid profile email offline_access",
      },
    });
  }

  return client;
}
