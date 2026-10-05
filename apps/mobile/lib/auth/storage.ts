import { secureDeleteItem, secureGetItem, secureSetItem } from "@/lib/secure-storage";

const ACCESS_TOKEN_KEY = "adeni.accessToken";
const REFRESH_TOKEN_KEY = "adeni.refreshToken";
const ID_TOKEN_KEY = "adeni.idToken";
const EXPIRES_AT_KEY = "adeni.expiresAt";

export type StoredAuthTokens = {
  accessToken: string;
  refreshToken: string | null;
  idToken: string | null;
  expiresAt: number | null;
};

export async function loadStoredAuthTokens(): Promise<StoredAuthTokens | null> {
  const accessToken = await secureGetItem(ACCESS_TOKEN_KEY);
  if (!accessToken) {
    return null;
  }

  const refreshToken = await secureGetItem(REFRESH_TOKEN_KEY);
  const idToken = await secureGetItem(ID_TOKEN_KEY);
  const expiresAtRaw = await secureGetItem(EXPIRES_AT_KEY);

  return {
    accessToken,
    refreshToken,
    idToken,
    expiresAt: expiresAtRaw ? Number(expiresAtRaw) : null,
  };
}

export async function saveStoredAuthTokens(tokens: StoredAuthTokens): Promise<void> {
  await secureSetItem(ACCESS_TOKEN_KEY, tokens.accessToken);

  if (tokens.refreshToken) {
    await secureSetItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
  } else {
    await secureDeleteItem(REFRESH_TOKEN_KEY);
  }

  if (tokens.idToken) {
    await secureSetItem(ID_TOKEN_KEY, tokens.idToken);
  } else {
    await secureDeleteItem(ID_TOKEN_KEY);
  }

  if (tokens.expiresAt) {
    await secureSetItem(EXPIRES_AT_KEY, String(tokens.expiresAt));
  } else {
    await secureDeleteItem(EXPIRES_AT_KEY);
  }
}

export async function clearStoredAuthTokens(): Promise<void> {
  await Promise.all([
    secureDeleteItem(ACCESS_TOKEN_KEY),
    secureDeleteItem(REFRESH_TOKEN_KEY),
    secureDeleteItem(ID_TOKEN_KEY),
    secureDeleteItem(EXPIRES_AT_KEY),
  ]);
}
