import { Platform } from "react-native";
import { AdeniApiClient } from "@adeni/api-client";

/** Platform-aware default for local API during development. */
export function getApiBaseUrl(): string {
  const configured = process.env.EXPO_PUBLIC_ADENI_API_URL?.trim();
  if (configured) {
    return configured.replace(/\/$/, "");
  }

  if (Platform.OS === "android") {
    return "http://10.0.2.2:5169";
  }

  return "http://localhost:5169";
}

/** Unauthenticated API client — prefer `useAuth().createApiClient()` in app code. */
export function createPublicApiClient(): AdeniApiClient {
  return new AdeniApiClient({
    baseUrl: getApiBaseUrl(),
  });
}
