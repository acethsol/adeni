import * as SecureStore from "expo-secure-store";

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
  const accessToken = await SecureStore.getItemAsync(ACCESS_TOKEN_KEY);
  if (!accessToken) {
    return null;
  }

  const refreshToken = await SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
  const idToken = await SecureStore.getItemAsync(ID_TOKEN_KEY);
  const expiresAtRaw = await SecureStore.getItemAsync(EXPIRES_AT_KEY);

  return {
    accessToken,
    refreshToken,
    idToken,
    expiresAt: expiresAtRaw ? Number(expiresAtRaw) : null,
  };
}

export async function saveStoredAuthTokens(tokens: StoredAuthTokens): Promise<void> {
  await SecureStore.setItemAsync(ACCESS_TOKEN_KEY, tokens.accessToken);

  if (tokens.refreshToken) {
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, tokens.refreshToken);
  } else {
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
  }

  if (tokens.idToken) {
    await SecureStore.setItemAsync(ID_TOKEN_KEY, tokens.idToken);
  } else {
    await SecureStore.deleteItemAsync(ID_TOKEN_KEY);
  }

  if (tokens.expiresAt) {
    await SecureStore.setItemAsync(EXPIRES_AT_KEY, String(tokens.expiresAt));
  } else {
    await SecureStore.deleteItemAsync(EXPIRES_AT_KEY);
  }
}

export async function clearStoredAuthTokens(): Promise<void> {
  await Promise.all([
    SecureStore.deleteItemAsync(ACCESS_TOKEN_KEY),
    SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY),
    SecureStore.deleteItemAsync(ID_TOKEN_KEY),
    SecureStore.deleteItemAsync(EXPIRES_AT_KEY),
  ]);
}
