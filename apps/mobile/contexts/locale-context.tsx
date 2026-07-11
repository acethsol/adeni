import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  defaultLocale,
  getMessages,
  isLocaleId,
  translate,
  type LocaleId,
  type Messages,
} from "@adeni/shared";
import { readStoredLocale, writeStoredLocale } from "@/lib/locale-storage";

type LocaleContextValue = {
  locale: LocaleId;
  messages: Messages;
  loading: boolean;
  setLocale: (locale: LocaleId) => Promise<void>;
  t: (key: string, vars?: Record<string, string | number>) => string;
};

const LocaleContext = createContext<LocaleContextValue | null>(null);

export function LocaleProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<LocaleId>(defaultLocale);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadLocale() {
      const stored = await readStoredLocale();

      if (!cancelled) {
        setLocaleState(stored);
        setLoading(false);
      }
    }

    void loadLocale();

    return () => {
      cancelled = true;
    };
  }, []);

  const setLocale = useCallback(async (nextLocale: LocaleId) => {
    if (!isLocaleId(nextLocale)) {
      return;
    }

    setLocaleState(nextLocale);
    await writeStoredLocale(nextLocale);
  }, []);

  const messages = useMemo(() => getMessages(locale), [locale]);

  const value = useMemo<LocaleContextValue>(
    () => ({
      locale,
      messages,
      loading,
      setLocale,
      t: (key: string, vars?: Record<string, string | number>) =>
        translate(messages, key, vars),
    }),
    [locale, messages, loading, setLocale],
  );

  return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>;
}

export function useLocale(): LocaleContextValue {
  const context = useContext(LocaleContext);

  if (!context) {
    throw new Error("useLocale must be used within LocaleProvider");
  }

  return context;
}
