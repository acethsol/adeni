"use client";

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import {
  getMessages,
  translate as translateMessage,
  type LocaleId,
  type Messages,
} from "@adeni/shared";

type LocaleContextValue = {
  locale: LocaleId;
  messages: Messages;
  translateContent: boolean;
};

const LocaleContext = createContext<LocaleContextValue | null>(null);

type Props = {
  locale: LocaleId;
  translateContent: boolean;
  children: ReactNode;
};

export function LocaleProvider({ locale, translateContent, children }: Props) {
  const [activeLocale, setActiveLocale] = useState(locale);
  const [contentTranslationEnabled, setContentTranslationEnabled] = useState(translateContent);
  const messages = useMemo(() => getMessages(activeLocale), [activeLocale]);

  useEffect(() => {
    setActiveLocale(locale);
  }, [locale]);

  useEffect(() => {
    setContentTranslationEnabled(translateContent);
  }, [translateContent]);

  return (
    <LocaleContext.Provider
      value={{
        locale: activeLocale,
        messages,
        translateContent: contentTranslationEnabled,
      }}
    >
      {children}
    </LocaleContext.Provider>
  );
}

export function useLocale(): LocaleContextValue {
  const context = useContext(LocaleContext);

  if (!context) {
    throw new Error("useLocale must be used within LocaleProvider");
  }

  return context;
}

export function useTranslation() {
  const { locale, messages, translateContent } = useLocale();

  function t(key: string, vars?: Record<string, string | number>): string {
    return translateMessage(messages, key, vars);
  }

  return { locale, t, translateContent };
}
