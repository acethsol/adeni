"use client";

import { useEffect, useMemo, useState, useTransition } from "react";
import { createPortal } from "react-dom";
import { useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { Check, Globe, X } from "lucide-react";
import {
  buildLocaleRegionPresets,
  countryName,
  defaultLocale,
  findLocaleRegionPreset,
  formatFooterLanguageLabel,
  getCurrencySymbol,
  mapApiMarketToConfig,
  marketsResponseSchema,
  type LocaleRegionPreset,
} from "@adeni/shared";
import { setLocaleRegion, setTranslationPreference } from "@/app/actions/locale";
import { useTranslation } from "@/components/locale-provider";
import { cn } from "@/lib/cn";

type Props = {
  currentMarketId: string;
  currentCurrency: string;
  countryCode: string;
  className?: string;
  mode?: "footer" | "trigger";
  presets?: LocaleRegionPreset[];
  trigger?: (props: { open: () => void }) => ReactNode;
};

export function LocaleCurrencySwitcher({
  currentMarketId,
  currentCurrency,
  countryCode,
  className,
  mode = "footer",
  presets: presetsProp,
  trigger,
}: Props) {
  const router = useRouter();
  const { locale, t, translateContent } = useTranslation();
  const [open, setOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [applyingId, setApplyingId] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const [fetchedPresets, setFetchedPresets] = useState<LocaleRegionPreset[] | null>(null);

  useEffect(() => {
    if (presetsProp) {
      return;
    }

    let cancelled = false;

    async function loadPresets() {
      try {
        const response = await fetch("/api/v1/markets");
        if (!response.ok) {
          return;
        }
        const payload = marketsResponseSchema.parse(await response.json());
        if (cancelled) {
          return;
        }
        const catalog = payload.items.map(mapApiMarketToConfig);
        setFetchedPresets(buildLocaleRegionPresets(catalog));
      } catch {
        if (!cancelled) {
          setFetchedPresets(buildLocaleRegionPresets());
        }
      }
    }

    void loadPresets();

    return () => {
      cancelled = true;
    };
  }, [presetsProp]);

  const presets = useMemo(
    () => presetsProp ?? fetchedPresets ?? buildLocaleRegionPresets(),
    [presetsProp, fetchedPresets],
  );
  const currentPresetId = `${locale}-${currentMarketId}`;

  const languageLabel = formatFooterLanguageLabel(locale, countryCode);
  const currencyLabel = `${getCurrencySymbol(currentCurrency)} ${currentCurrency}`;

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (!open) {
      return;
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }

    document.body.style.overflow = "hidden";
    document.addEventListener("keydown", onKeyDown);

    return () => {
      document.body.style.overflow = "";
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  function applyPreset(preset: LocaleRegionPreset) {
    if (preset.id === currentPresetId) {
      setOpen(false);
      return;
    }

    setApplyingId(preset.id);

    startTransition(async () => {
      await setLocaleRegion(preset.locale, preset.marketId);
      setApplyingId(null);
      setOpen(false);
      router.refresh();
    });
  }

  function toggleTranslation() {
    startTransition(async () => {
      await setTranslationPreference(!translateContent);
      router.refresh();
    });
  }

  const footerButtonClass =
    "inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm font-semibold text-foreground shadow-sm transition-colors hover:border-border-strong";

  return (
    <>
      {trigger ? (
        trigger({ open: () => setOpen(true) })
      ) : mode === "footer" ? (
        <div className={cn("flex flex-wrap items-center gap-3", className)}>
          <button
            type="button"
            className={footerButtonClass}
            onClick={() => setOpen(true)}
            aria-label={t("locale.title")}
          >
            <Globe className="h-4 w-4 shrink-0" aria-hidden />
            {languageLabel}
          </button>
          <button
            type="button"
            className={footerButtonClass}
            onClick={() => setOpen(true)}
            aria-label={t("locale.currency")}
          >
            {currencyLabel}
          </button>
        </div>
      ) : null}

      {open && mounted
        ? createPortal(
            <div className="fixed inset-0 z-[200] flex items-end justify-center sm:items-center sm:p-6">
              <button
                type="button"
                className="absolute inset-0 bg-foreground/35 backdrop-blur-[2px]"
                aria-label={t("locale.cancel")}
                onClick={() => setOpen(false)}
              />

              <div
                role="dialog"
                aria-modal="true"
                aria-labelledby="locale-dialog-title"
                className="relative z-10 flex max-h-[min(90vh,680px)] w-full max-w-xl flex-col overflow-hidden rounded-t-3xl border border-border bg-surface shadow-2xl sm:rounded-3xl"
              >
                <div className="relative border-b border-border px-5 pb-5 pt-4 sm:px-6">
                  <div
                    className="pointer-events-none absolute -right-8 -top-10 h-32 w-32 rounded-full bg-accent/10 blur-2xl"
                    aria-hidden
                  />
                  <div
                    className="pointer-events-none absolute -left-6 bottom-0 h-20 w-20 rounded-full bg-primary/8 blur-xl"
                    aria-hidden
                  />

                  <button
                    type="button"
                    onClick={() => setOpen(false)}
                    className="absolute right-4 top-4 inline-flex h-7 w-7 items-center justify-center rounded-full text-muted transition-colors hover:bg-subtle hover:text-foreground"
                    aria-label={t("locale.cancel")}
                  >
                    <X className="h-3.5 w-3.5" aria-hidden />
                  </button>

                  <div className="flex items-start gap-3 pr-8">
                    <span className="mt-0.5 flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-primary to-accent text-primary-foreground shadow-sm">
                      <Globe className="h-5 w-5" aria-hidden />
                    </span>
                    <div>
                      <h2
                        id="locale-dialog-title"
                        className="text-lg font-bold tracking-tight text-foreground"
                      >
                        {t("locale.pickerTitle")}
                      </h2>
                      <p className="mt-1 text-sm text-muted">{t("locale.pickerSubtitle")}</p>
                    </div>
                  </div>
                </div>

                <div className="flex-1 overflow-y-auto px-5 pb-6 pt-5 sm:px-6">
                  <div className="grid grid-cols-3 gap-2.5 sm:gap-3">
                    {presets.map((preset) => {
                      const selected = preset.id === currentPresetId;
                      const busy = isPending && applyingId === preset.id;

                      return (
                        <button
                          key={preset.id}
                          type="button"
                          disabled={isPending}
                          onClick={() => applyPreset(preset)}
                          className={cn(
                            "group relative flex min-h-[5.5rem] flex-col items-start rounded-2xl border px-3 py-3 text-left transition-all",
                            selected
                              ? "border-accent bg-accent/8 shadow-sm"
                              : "border-border bg-surface hover:border-accent/40 hover:bg-subtle/50",
                            isPending && !busy && "opacity-60",
                          )}
                        >
                          {selected ? (
                            <span className="absolute right-2 top-2 text-accent">
                              <Check className="h-3.5 w-3.5" aria-hidden />
                            </span>
                          ) : busy ? (
                            <span className="absolute right-2 top-2 text-xs font-semibold text-accent">
                              …
                            </span>
                          ) : null}

                          <span className="text-sm font-bold leading-tight text-foreground">
                            {preset.regionLabel}
                          </span>
                          <span className="mt-1 text-xs font-medium text-muted">
                            {preset.languageLabel}
                          </span>
                          <span className="mt-auto pt-2 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                            {countryName(preset.countryCode)} · {getCurrencySymbol(preset.currency)}
                            {preset.currency}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </div>

                <div className="border-t border-border px-5 py-4 sm:px-6">
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <p className="text-sm font-semibold text-foreground">{t("locale.translation")}</p>
                      <p className="mt-1 text-xs text-muted">{t("locale.translationHint")}</p>
                    </div>
                    <button
                      type="button"
                      role="switch"
                      aria-checked={translateContent}
                      aria-label={
                        translateContent
                          ? t("locale.translationEnabled")
                          : t("locale.translationDisabled")
                      }
                      disabled={locale === defaultLocale || isPending}
                      onClick={toggleTranslation}
                      className={cn(
                        "relative inline-flex h-7 w-12 shrink-0 rounded-full border transition-colors",
                        locale === defaultLocale
                          ? "cursor-not-allowed border-border bg-subtle opacity-50"
                          : translateContent
                            ? "border-accent bg-accent"
                            : "border-border-strong bg-surface",
                      )}
                    >
                      <span
                        className={cn(
                          "absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-transform",
                          translateContent ? "translate-x-5" : "translate-x-1",
                        )}
                      />
                    </button>
                  </div>
                </div>
              </div>
            </div>,
            document.body,
          )
        : null}
    </>
  );
}
