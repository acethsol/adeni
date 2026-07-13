"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { FormEvent, useEffect, useId, useLayoutEffect, useRef, useState, type CSSProperties } from "react";
import { createPortal } from "react-dom";
import { MapPin, Search, Sparkles } from "lucide-react";
import {
  discoverSearchToPath,
  getAskAdeniPrompts,
  getCategoryLabel,
  resolveDiscoverySearch,
} from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";
import { useTranslation } from "@/components/locale-provider";

export type DiscoverySearchVariant = "hero" | "default" | "compact";

type Props = {
  className?: string;
  placeholder?: string;
  marketName?: string;
  syncFromUrl?: boolean;
  variant?: DiscoverySearchVariant;
};

export function DiscoverySearch({
  className,
  placeholder,
  marketName,
  syncFromUrl = true,
  variant = "default",
}: Props) {
  const router = useRouter();
  const { t, locale } = useTranslation();
  const resolvedPlaceholder = placeholder ?? t("search.placeholder");
  const searchParams = useSearchParams();
  const listboxId = useId();
  const containerRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const urlQuery = syncFromUrl ? (searchParams.get("q") ?? "") : "";
  const urlCategory = syncFromUrl ? (searchParams.get("category") ?? "") : "";
  const [value, setValue] = useState(urlQuery);
  const [open, setOpen] = useState(false);
  const [hint, setHint] = useState<string | null>(null);
  const [mounted, setMounted] = useState(false);
  const [panelStyle, setPanelStyle] = useState<CSSProperties>({});

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (syncFromUrl) {
      setValue(urlQuery);
    }
  }, [syncFromUrl, urlQuery]);

  const isHeaderCompact = variant === "compact";

  useEffect(() => {
    if (!open) {
      return;
    }

    function handlePointerDown(event: MouseEvent) {
      const target = event.target as Node;
      if (
        containerRef.current?.contains(target) ||
        panelRef.current?.contains(target)
      ) {
        return;
      }
      setOpen(false);
    }

    function handleEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
        inputRef.current?.blur();
      }
    }

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleEscape);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleEscape);
    };
  }, [open]);

  useLayoutEffect(() => {
    if (!open || !isHeaderCompact || !containerRef.current) {
      return;
    }

    function updatePanelPosition() {
      const anchor = containerRef.current;
      if (!anchor) {
        return;
      }

      const rect = anchor.getBoundingClientRect();
      setPanelStyle({
        position: "fixed",
        top: rect.bottom + 8,
        left: rect.left,
        width: rect.width,
        zIndex: 200,
      });
    }

    updatePanelPosition();
    window.addEventListener("scroll", updatePanelPosition, { passive: true });
    window.addEventListener("resize", updatePanelPosition);

    return () => {
      window.removeEventListener("scroll", updatePanelPosition);
      window.removeEventListener("resize", updatePanelPosition);
    };
  }, [open, isHeaderCompact]);

  useEffect(() => {
    if (open && isHeaderCompact) {
      inputRef.current?.focus();
    }
  }, [open, isHeaderCompact]);

  function navigate(text: string) {
    const trimmed = text.trim();
    if (!trimmed) {
      router.push("/discover");
      setOpen(false);
      return;
    }

    const params = resolveDiscoverySearch(trimmed);
    setHint(params.summary ?? null);
    setValue(trimmed);
    setOpen(false);
    router.push(discoverSearchToPath(params));
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    navigate(value);
  }

  const queryLabel = value.trim() || t("search.searchOrAsk");
  const locationLabel = marketName ?? t("search.nearYou");
  const categoryLabel = urlCategory
    ? getCategoryLabel(locale, urlCategory)
    : t("search.anyCategory");

  function openSearch() {
    setOpen(true);
  }

  const panelContent = open ? (
    <div
      id={listboxId}
      ref={isHeaderCompact ? panelRef : undefined}
      role="listbox"
      style={isHeaderCompact ? panelStyle : undefined}
      className={cn(
        "overflow-hidden rounded-2xl border border-border bg-surface shadow-lg",
        isHeaderCompact
          ? undefined
          : "absolute left-0 right-0 top-[calc(100%+0.5rem)] z-50",
      )}
    >
      <AskAdeniPanelContent
        value={value}
        hint={hint}
        onNavigate={navigate}
        onSubmit={() => navigate(value)}
      />
    </div>
  ) : null;

  return (
    <div ref={containerRef} className={cn("relative w-full", className)}>
      {isHeaderCompact ? (
        <>
          {!open ? (
            <button
              type="button"
              onClick={openSearch}
              aria-expanded={open}
              aria-controls={listboxId}
              aria-label={t("nav.search")}
              className="flex w-full min-w-0 cursor-pointer items-center rounded-full border border-border bg-surface text-left shadow-md transition-shadow hover:shadow-lg"
            >
              <span className="pointer-events-none hidden min-w-0 flex-1 truncate px-4 py-2.5 text-sm font-semibold text-foreground sm:block sm:border-r sm:border-border">
                {queryLabel}
              </span>
              <span className="pointer-events-none hidden min-w-0 flex-1 truncate px-4 py-2.5 text-sm text-muted md:block md:border-r md:border-border">
                <MapPin className="mr-1 inline h-3.5 w-3.5 shrink-0" aria-hidden />
                {locationLabel}
              </span>
              <span className="pointer-events-none min-w-0 flex-1 truncate px-4 py-2.5 text-sm text-muted sm:border-r sm:border-border">
                {categoryLabel}
              </span>
              <span className="pointer-events-none m-1.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-accent text-primary-foreground">
                <Search className="h-4 w-4" aria-hidden />
              </span>
            </button>
          ) : (
            <form onSubmit={handleSubmit} role="search" className="relative">
              <Search
                className="pointer-events-none absolute left-3.5 top-1/2 z-10 h-4 w-4 -translate-y-1/2 text-muted-foreground"
                aria-hidden
              />
              <input
                ref={inputRef}
                type="search"
                value={value}
                onChange={(event) => setValue(event.target.value)}
                onFocus={() => setOpen(true)}
                placeholder={resolvedPlaceholder}
                role="combobox"
                aria-expanded={open}
                aria-controls={listboxId}
                aria-label={t("search.searchOrAsk")}
                className="w-full rounded-full border border-accent bg-surface py-2.5 pl-10 pr-4 text-sm text-foreground shadow-md ring-2 ring-accent/20 outline-none"
              />
            </form>
          )}
          {open && mounted && isHeaderCompact
            ? createPortal(panelContent, document.body)
            : null}
        </>
      ) : (
        <form onSubmit={handleSubmit} role="search">
          <Search
            className={cn(
              "pointer-events-none absolute left-4 top-1/2 z-10 -translate-y-1/2 text-muted-foreground",
              variant === "hero" ? "h-5 w-5" : "h-4 w-4",
            )}
            aria-hidden
          />
          <input
            ref={inputRef}
            type="search"
            value={value}
            onChange={(event) => setValue(event.target.value)}
            onFocus={() => setOpen(true)}
            placeholder={resolvedPlaceholder}
            role="combobox"
            aria-expanded={open}
            aria-controls={listboxId}
            aria-label={t("search.searchOrAsk")}
            className={cn(
              "w-full rounded-full border bg-surface text-foreground shadow-sm outline-none transition-shadow placeholder:text-muted-foreground",
              variant === "hero"
                ? "border-border-strong py-4 pl-12 pr-5 text-base shadow-md focus:border-accent focus:ring-2 focus:ring-accent/20"
                : "border-border-strong py-2.5 pl-10 pr-4 text-sm focus:border-accent focus:ring-2 focus:ring-accent/20",
              open && "border-accent ring-2 ring-accent/20",
            )}
          />
        </form>
      )}

      {!isHeaderCompact ? panelContent : null}
    </div>
  );
}

function AskAdeniPanelContent({
  value,
  hint,
  onNavigate,
  onSubmit,
}: {
  value: string;
  hint: string | null;
  onNavigate: (text: string) => void;
  onSubmit: () => void;
}) {
  const { locale, t } = useTranslation();
  const prompts = getAskAdeniPrompts(locale);

  return (
    <>
      <div className="border-b border-border bg-gradient-to-br from-surface to-accent/5 px-4 py-3">
        <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
          <Sparkles className="h-4 w-4 text-accent" aria-hidden />
          {t("search.askTitle")}
        </div>
        <p className="mt-1 text-xs text-muted">{t("search.askDescription")}</p>
      </div>

      <div className="space-y-3 p-4">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t("search.tryAsking")}
        </p>
        <div className="flex flex-wrap gap-2">
          {prompts.map((example) => (
            <button
              key={example}
              type="button"
              role="option"
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => onNavigate(example)}
              className="rounded-full border border-border bg-subtle px-3 py-1.5 text-xs font-semibold text-muted transition-colors hover:border-accent/40 hover:text-foreground"
            >
              {example}
            </button>
          ))}
        </div>
        <Button
          type="button"
          size="sm"
          className="w-full sm:w-auto"
          disabled={!value.trim()}
          onMouseDown={(event) => event.preventDefault()}
          onClick={onSubmit}
        >
          {t("search.findServices")}
        </Button>
        {hint ? <p className="text-xs text-accent">{hint}</p> : null}
      </div>
    </>
  );
}
