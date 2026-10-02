export const environment = {
  production: false,
  apiBaseUrl: "http://localhost:5169",
  discoverWebUrl: "http://localhost:3000",
  /** Set when Auth0 is not configured; maps to X-Dev-Auth0-Sub (API Auth0.Enabled = false). */
  devBusinessAuth0Sub: "",
  auth0: {
    domain: "",
    clientId: "",
    audience: "https://api.adeni.io",
  },
};
