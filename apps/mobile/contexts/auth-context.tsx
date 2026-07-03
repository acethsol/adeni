import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import * as AuthSession from "expo-auth-session";
import * as WebBrowser from "expo-web-browser";
import type { AuthSession as ApiAuthSession } from "@adeni/shared";
import { AdeniRoles } from "@adeni/shared";
import { AdeniApiClient } from "@adeni/api-client";
import {
  getAuth0Audience,
  getAuth0ClientId,
  getAuth0DiscoveryDocument,
  getAuth0RedirectUri,
  isAuth0Configured,
} from "@/lib/auth/config";
import {
  clearStoredAuthTokens,
  loadStoredAuthTokens,
  saveStoredAuthTokens,
  type StoredAuthTokens,
} from "@/lib/auth/storage";
import {
  decodeJwtPayload,
  exchangeAuthorizationCode,
  isAccessTokenExpired,
  refreshAccessToken,
} from "@/lib/auth/tokens";
import { getApiBaseUrl } from "@/lib/api";

WebBrowser.maybeCompleteAuthSession();

type AuthContextValue = {
  loading: boolean;
  isAuthenticated: boolean;
  accessToken: string | null;
  apiSession: ApiAuthSession | null;
  profileName: string | null;
  profileEmail: string | null;
  isBusinessUser: boolean;
  tenantId: string | null;
  isBookingEnabled: boolean;
  isBusinessInboxEnabled: boolean;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  createApiClient: (mode?: "customer" | "business") => AdeniApiClient;
  refreshSession: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

function getDevCustomerAuth0Sub(): string | undefined {
  return process.env.EXPO_PUBLIC_DEV_CUSTOMER_AUTH0_SUB?.trim() || undefined;
}

function getDevBusinessAuth0Sub(): string | undefined {
  return process.env.EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB?.trim() || undefined;
}

function resolveDevAuth0Sub(mode: "customer" | "business"): string | undefined {
  return mode === "business" ? getDevBusinessAuth0Sub() : getDevCustomerAuth0Sub();
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const auth0Configured = isAuth0Configured();
  const discovery = useMemo(
    () => (auth0Configured ? getAuth0DiscoveryDocument() : null),
    [auth0Configured],
  );
  const redirectUri = useMemo(() => getAuth0RedirectUri(), []);

  const [loading, setLoading] = useState(true);
  const [tokens, setTokens] = useState<StoredAuthTokens | null>(null);
  const [apiSession, setApiSession] = useState<ApiAuthSession | null>(null);

  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: auth0Configured ? getAuth0ClientId() : "unused",
      redirectUri,
      scopes: ["openid", "profile", "email", "offline_access"],
      extraParams: {
        audience: getAuth0Audience(),
      },
      usePKCE: true,
    },
    discovery,
  );

  const idTokenClaims = useMemo(
    () => (tokens?.idToken ? decodeJwtPayload(tokens.idToken) : null),
    [tokens?.idToken],
  );

  const profileName =
    typeof idTokenClaims?.name === "string"
      ? idTokenClaims.name
      : typeof idTokenClaims?.nickname === "string"
        ? idTokenClaims.nickname
        : null;

  const profileEmail =
    typeof idTokenClaims?.email === "string" ? idTokenClaims.email : null;

  const accessToken = tokens?.accessToken ?? null;
  const isAuthenticated = Boolean(accessToken);
  const tenantId = apiSession?.tenantId ?? null;
  const isBusinessUser =
    apiSession?.roles.includes(AdeniRoles.Business) ||
    apiSession?.roles.includes(AdeniRoles.Admin) ||
    false;

  const isBookingEnabled = auth0Configured
    ? Boolean(accessToken)
    : Boolean(accessToken || getDevCustomerAuth0Sub());

  const isBusinessInboxEnabled = auth0Configured
    ? Boolean(accessToken && isBusinessUser && tenantId)
    : Boolean(getDevBusinessAuth0Sub() || (accessToken && isBusinessUser && tenantId));

  const syncApiSession = useCallback(async (token: string | null, devSub?: string) => {
    if (!token && !devSub) {
      setApiSession(null);
      return;
    }

    const client = new AdeniApiClient({
      baseUrl: getApiBaseUrl(),
      accessToken: token,
      devAuth0Sub: token ? null : devSub,
    });

    try {
      const session = await client.getMe();
      setApiSession(session);
    } catch {
      setApiSession(null);
    }
  }, []);

  const applyTokens = useCallback(
    async (nextTokens: StoredAuthTokens | null) => {
      if (!nextTokens) {
        setTokens(null);
        setApiSession(null);
        await clearStoredAuthTokens();
        return;
      }

      await saveStoredAuthTokens(nextTokens);
      setTokens(nextTokens);
      await syncApiSession(nextTokens.accessToken);
    },
    [syncApiSession],
  );

  const ensureFreshTokens = useCallback(async (): Promise<StoredAuthTokens | null> => {
    const stored = await loadStoredAuthTokens();
    if (!stored) {
      return null;
    }

    if (
      stored.refreshToken &&
      isAccessTokenExpired(stored.expiresAt) &&
      auth0Configured
    ) {
      try {
        const refreshed = await refreshAccessToken(stored.refreshToken);
        await saveStoredAuthTokens(refreshed);
        setTokens(refreshed);
        return refreshed;
      } catch {
        await clearStoredAuthTokens();
        setTokens(null);
        setApiSession(null);
        return null;
      }
    }

    setTokens(stored);
    return stored;
  }, [auth0Configured]);

  const refreshSession = useCallback(async () => {
    const stored = await ensureFreshTokens();
    if (stored?.accessToken) {
      await syncApiSession(stored.accessToken);
      return;
    }

    const devSub = getDevBusinessAuth0Sub() ?? getDevCustomerAuth0Sub();
    if (!auth0Configured && devSub) {
      await syncApiSession(null, devSub);
    }
  }, [auth0Configured, ensureFreshTokens, syncApiSession]);

  useEffect(() => {
    let cancelled = false;

    async function bootstrap() {
      setLoading(true);

      const stored = await ensureFreshTokens();
      if (cancelled) {
        return;
      }

      if (stored?.accessToken) {
        await syncApiSession(stored.accessToken);
      } else if (!auth0Configured) {
        const devSub = getDevCustomerAuth0Sub();
        if (devSub) {
          await syncApiSession(null, devSub);
        }
      }

      if (!cancelled) {
        setLoading(false);
      }
    }

    void bootstrap();

    return () => {
      cancelled = true;
    };
  }, [auth0Configured, ensureFreshTokens, syncApiSession]);

  useEffect(() => {
    if (response?.type !== "success" || !request?.codeVerifier) {
      return;
    }

    const code = response.params.code;
    if (!code) {
      return;
    }

    let cancelled = false;

    async function completeLogin() {
      setLoading(true);
      try {
        const nextTokens = await exchangeAuthorizationCode(code, request!.codeVerifier!);
        if (!cancelled) {
          await applyTokens(nextTokens);
        }
      } catch {
        if (!cancelled) {
          setTokens(null);
          setApiSession(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void completeLogin();

    return () => {
      cancelled = true;
    };
  }, [applyTokens, request?.codeVerifier, response]);

  const login = useCallback(async () => {
    if (!auth0Configured) {
      throw new Error("Auth0 is not configured for this build.");
    }

    if (!request) {
      throw new Error("Auth0 login is still initializing.");
    }

    await promptAsync();
  }, [auth0Configured, promptAsync, request]);

  const logout = useCallback(async () => {
    await applyTokens(null);
  }, [applyTokens]);

  const createApiClient = useCallback(
    (mode: "customer" | "business" = "customer"): AdeniApiClient => {
      const devSub = accessToken ? null : resolveDevAuth0Sub(mode);
      const client = new AdeniApiClient({
        baseUrl: getApiBaseUrl(),
        accessToken,
        devAuth0Sub: devSub,
      });

      if (mode === "business" && tenantId) {
        client.setTenantId(tenantId);
      } else if (mode === "business" && devSub && apiSession?.tenantId) {
        client.setTenantId(apiSession.tenantId);
      }

      return client;
    },
    [accessToken, apiSession?.tenantId, tenantId],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      loading,
      isAuthenticated,
      accessToken,
      apiSession,
      profileName,
      profileEmail,
      isBusinessUser,
      tenantId,
      isBookingEnabled,
      isBusinessInboxEnabled,
      login,
      logout,
      createApiClient,
      refreshSession,
    }),
    [
      loading,
      isAuthenticated,
      accessToken,
      apiSession,
      profileName,
      profileEmail,
      isBusinessUser,
      tenantId,
      isBookingEnabled,
      isBusinessInboxEnabled,
      login,
      logout,
      createApiClient,
      refreshSession,
    ],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }

  return context;
}
