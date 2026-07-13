"use client";

import { useEffect, useState } from "react";
import {
  DEFAULT_CONTENT_SOURCE_LOCALE,
  shouldTranslateContent,
  type LocaleId,
} from "@adeni/shared";
import { useTranslation } from "@/components/locale-provider";
import { cn } from "@/lib/cn";

const clientCache = new Map<string, string>();

type Props = {
  text: string;
  className?: string;
  showBadge?: boolean;
  as?: "span" | "p";
};

export function TranslatedText({
  text,
  className,
  showBadge = false,
  as: Component = "span",
}: Props) {
  const { locale, t, translateContent } = useTranslation();
  const [display, setDisplay] = useState(text);
  const [wasTranslated, setWasTranslated] = useState(false);

  useEffect(() => {
    if (!shouldTranslateContent(locale, translateContent, text)) {
      setDisplay(text);
      setWasTranslated(false);
      return;
    }

    const cacheKey = `${locale}:${text}`;
    const cached = clientCache.get(cacheKey);
    if (cached) {
      setDisplay(cached);
      setWasTranslated(cached !== text);
      return;
    }

    let cancelled = false;

    const load = async () => {
      try {
        const response = await fetch("/api/v1/translate", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            texts: [text],
            source: DEFAULT_CONTENT_SOURCE_LOCALE,
            target: locale,
          }),
        });

        if (!response.ok) {
          throw new Error("translate failed");
        }

        const payload = (await response.json()) as { translations?: Record<string, string> };
        const translated = payload.translations?.[text]?.trim() || text;

        if (!cancelled) {
          clientCache.set(cacheKey, translated);
          setDisplay(translated);
          setWasTranslated(translated !== text);
        }
      } catch {
        if (!cancelled) {
          setDisplay(text);
          setWasTranslated(false);
        }
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, [locale, text, translateContent]);

  return (
    <Component className={className}>
      {display}
      {showBadge && wasTranslated ? (
        <span className={cn("ml-2 text-[10px] font-semibold uppercase tracking-wide text-muted")}>
          {t("content.translated")}
        </span>
      ) : null}
    </Component>
  );
}

export async function prefetchTranslations(
  texts: string[],
  locale: LocaleId,
  translateContent: boolean,
) {
  const unique = texts.filter((text) => shouldTranslateContent(locale, translateContent, text));
  if (unique.length === 0) {
    return;
  }

  const missing = unique.filter((text) => !clientCache.has(`${locale}:${text}`));
  if (missing.length === 0) {
    return;
  }

  try {
    const response = await fetch("/api/v1/translate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        texts: missing,
        source: DEFAULT_CONTENT_SOURCE_LOCALE,
        target: locale,
      }),
    });

    if (!response.ok) {
      return;
    }

    const payload = (await response.json()) as { translations?: Record<string, string> };
    for (const text of missing) {
      const translated = payload.translations?.[text];
      if (translated) {
        clientCache.set(`${locale}:${text}`, translated);
      }
    }
  } catch {
    // Best-effort prefetch.
  }
}
