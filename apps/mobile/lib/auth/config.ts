import * as AuthSession from "expo-auth-session";

export function isAuth0Configured(): boolean {
  return Boolean(
    process.env.EXPO_PUBLIC_AUTH0_DOMAIN?.trim() &&
      process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID?.trim(),
  );
}

export function getAuth0Domain(): string {
  const domain = process.env.EXPO_PUBLIC_AUTH0_DOMAIN?.trim();
  if (!domain) {
    throw new Error("EXPO_PUBLIC_AUTH0_DOMAIN is not configured.");
  }

  return domain;
}

export function getAuth0ClientId(): string {
  const clientId = process.env.EXPO_PUBLIC_AUTH0_CLIENT_ID?.trim();
  if (!clientId) {
    throw new Error("EXPO_PUBLIC_AUTH0_CLIENT_ID is not configured.");
  }

  return clientId;
}

export function getAuth0Audience(): string {
  return process.env.EXPO_PUBLIC_AUTH0_AUDIENCE?.trim() || "https://api.adeni.io";
}

export function getAuth0RedirectUri(): string {
  return AuthSession.makeRedirectUri({
    scheme: "adeni",
    path: "callback",
  });
}

export function getAuth0DiscoveryDocument() {
  const domain = getAuth0Domain();
  return {
    authorizationEndpoint: `https://${domain}/authorize`,
    tokenEndpoint: `https://${domain}/oauth/token`,
    revocationEndpoint: `https://${domain}/oauth/revoke`,
  };
}
