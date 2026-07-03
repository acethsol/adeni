import * as AuthSession from "expo-auth-session";
import {
  getAuth0ClientId,
  getAuth0DiscoveryDocument,
  getAuth0RedirectUri,
} from "./config";
import type { StoredAuthTokens } from "./storage";

export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const payload = token.split(".")[1];
    if (!payload) {
      return null;
    }

    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const json = globalThis.atob(normalized);
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}

export function isAccessTokenExpired(expiresAt: number | null, skewMs = 60_000): boolean {
  if (!expiresAt) {
    return false;
  }

  return Date.now() >= expiresAt - skewMs;
}

export function toStoredAuthTokens(
  tokenResponse: AuthSession.TokenResponse,
): StoredAuthTokens {
  const expiresAt = tokenResponse.expiresIn
    ? Date.now() + tokenResponse.expiresIn * 1000
    : null;

  return {
    accessToken: tokenResponse.accessToken,
    refreshToken: tokenResponse.refreshToken ?? null,
    idToken: tokenResponse.idToken ?? null,
    expiresAt,
  };
}

export async function refreshAccessToken(
  refreshToken: string,
): Promise<StoredAuthTokens> {
  const tokenResponse = await AuthSession.refreshAsync(
    {
      clientId: getAuth0ClientId(),
      refreshToken,
    },
    getAuth0DiscoveryDocument(),
  );

  return toStoredAuthTokens(tokenResponse);
}

export async function exchangeAuthorizationCode(
  code: string,
  codeVerifier: string,
): Promise<StoredAuthTokens> {
  const tokenResponse = await AuthSession.exchangeCodeAsync(
    {
      clientId: getAuth0ClientId(),
      code,
      redirectUri: getAuth0RedirectUri(),
      extraParams: {
        code_verifier: codeVerifier,
      },
    },
    getAuth0DiscoveryDocument(),
  );

  return toStoredAuthTokens(tokenResponse);
}
