import { Auth0Client } from "@auth0/nextjs-auth0/server";
import { getAuth0Audience } from "./config";

export const auth0 = new Auth0Client({
  authorizationParameters: {
    audience: getAuth0Audience(),
    scope: "openid profile email offline_access",
  },
});
