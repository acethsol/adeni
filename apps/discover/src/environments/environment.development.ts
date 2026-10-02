export const environment = {
  production: false,
  apiBaseUrl: "http://localhost:5169",
  publicAppUrl: "http://localhost:5190",
  envMarketId: "",
  defaultMarketId: "lagos",
  defaultLocation: { lat: 6.5244, lng: 3.3792 },
  /** Matches API seeder `auth0|local-customer` when Auth0 is disabled locally. */
  devCustomerAuth0Sub: "auth0|local-customer",
  auth0: {
    domain: "",
    clientId: "",
    audience: "https://api.adeni.io",
  },
};
