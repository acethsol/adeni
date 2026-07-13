export {
  getApiBaseUrl,
  createPublicApiClient,
  createPublicApiClient as createApiClient,
} from "./public-api";

export async function createAuthenticatedApiClient() {
  const { createPublicApiClient } = await import("./public-api");
  const { isAuth0Configured } = await import("./auth/config");
  const client = createPublicApiClient();

  if (isAuth0Configured()) {
    const { getAccessToken } = await import("./auth/session");
    const token = await getAccessToken();
    client.setAccessToken(token);
  }

  return client;
}
