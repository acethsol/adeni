import { useEffect, useMemo, useState, useTransition } from "react";
import {
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { SymbolView } from "expo-symbols";
import {
  buildLocaleRegionPresets,
  countryName,
  getCurrencySymbol,
  type LocaleRegionPreset,
} from "@adeni/shared";
import { useLocale } from "@/contexts/locale-context";
import { useMarket } from "@/contexts/market-context";
import { adeniTheme } from "@/lib/theme";

type Props = {
  visible: boolean;
  onClose: () => void;
};

export function LocaleCurrencySheet({ visible, onClose }: Props) {
  const { locale, setLocale, t } = useLocale();
  const { market, setMarketId } = useMarket();
  const [applyingId, setApplyingId] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const presets = useMemo(() => buildLocaleRegionPresets(), []);
  const currentPresetId = `${locale}-${market.id}`;

  useEffect(() => {
    if (!visible) {
      setApplyingId(null);
    }
  }, [visible]);

  async function applyPreset(preset: LocaleRegionPreset) {
    if (preset.id === currentPresetId) {
      onClose();
      return;
    }

    setApplyingId(preset.id);

    startTransition(() => {
      void (async () => {
        if (preset.locale !== locale) {
          await setLocale(preset.locale);
        }

        if (preset.marketId !== market.id) {
          await setMarketId(preset.marketId);
        }

        setApplyingId(null);
        onClose();
      })();
    });
  }

  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <View style={styles.overlay}>
        <Pressable style={styles.backdrop} onPress={onClose} />

        <View style={styles.sheet}>
          <View style={styles.header}>
            <Pressable onPress={onClose} style={styles.closeButton} hitSlop={8}>
              <Text style={styles.close}>✕</Text>
            </Pressable>

            <View style={styles.headerCopy}>
              <View style={styles.globeBadge}>
                <SymbolView
                  name={{ ios: "globe", android: "language", web: "language" }}
                  tintColor="#ffffff"
                  size={18}
                />
              </View>
              <View style={styles.headerText}>
                <Text style={styles.title}>{t("locale.pickerTitle")}</Text>
                <Text style={styles.subtitle}>{t("locale.pickerSubtitle")}</Text>
              </View>
            </View>
          </View>

          <ScrollView contentContainerStyle={styles.content}>
            <View style={styles.grid}>
              {presets.map((preset) => {
                const selected = preset.id === currentPresetId;
                const busy = isPending && applyingId === preset.id;

                return (
                  <Pressable
                    key={preset.id}
                    disabled={isPending}
                    onPress={() => void applyPreset(preset)}
                    style={[styles.tile, selected && styles.tileSelected]}
                  >
                    {selected ? (
                      <View style={styles.tileCheck}>
                        <SymbolView
                          name={{ ios: "checkmark", android: "check", web: "check" }}
                          tintColor={adeniTheme.accent}
                          size={12}
                        />
                      </View>
                    ) : busy ? (
                      <Text style={styles.busy}>…</Text>
                    ) : null}

                    <Text style={styles.tileCity} numberOfLines={1}>
                      {preset.regionLabel}
                    </Text>
                    <Text style={styles.tileLanguage} numberOfLines={1}>
                      {preset.languageLabel}
                    </Text>
                    <Text style={styles.tileMeta} numberOfLines={1}>
                      {countryName(preset.countryCode)} · {getCurrencySymbol(preset.currency)}{" "}
                      {preset.currency}
                    </Text>
                  </Pressable>
                );
              })}
            </View>
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}

export function FooterLocaleButtons({ onOpen }: { onOpen: () => void }) {
  const { locale } = useLocale();
  const { market } = useMarket();

  return (
    <View style={styles.footerButtons}>
      <Pressable onPress={onOpen} style={styles.footerButton}>
        <Text style={styles.footerButtonText}>
          {locale.toUpperCase()} ({market.countryCode})
        </Text>
      </Pressable>
      <Pressable onPress={onOpen} style={styles.footerButton}>
        <Text style={styles.footerButtonText}>
          {getCurrencySymbol(market.currency)} {market.currency}
        </Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    justifyContent: "flex-end",
  },
  backdrop: {
    ...StyleSheet.absoluteFill,
    backgroundColor: "rgba(15, 23, 42, 0.4)",
  },
  sheet: {
    maxHeight: "86%",
    backgroundColor: adeniTheme.surface,
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    overflow: "hidden",
  },
  header: {
    borderBottomWidth: 1,
    borderBottomColor: adeniTheme.border,
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 16,
  },
  closeButton: {
    position: "absolute",
    top: 12,
    right: 14,
    zIndex: 2,
    width: 28,
    height: 28,
    borderRadius: 14,
    alignItems: "center",
    justifyContent: "center",
  },
  close: {
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  headerCopy: {
    flexDirection: "row",
    alignItems: "flex-start",
    gap: 12,
    paddingRight: 28,
  },
  globeBadge: {
    width: 40,
    height: 40,
    borderRadius: 14,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: adeniTheme.primary,
  },
  headerText: {
    flex: 1,
    minWidth: 0,
  },
  title: {
    fontSize: 18,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  subtitle: {
    marginTop: 4,
    fontSize: 13,
    color: adeniTheme.textMuted,
    lineHeight: 18,
  },
  content: {
    paddingHorizontal: 14,
    paddingTop: 16,
    paddingBottom: 24,
  },
  grid: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
  },
  tile: {
    width: "31.5%",
    minHeight: 88,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: 14,
    paddingHorizontal: 10,
    paddingVertical: 10,
    backgroundColor: adeniTheme.surface,
  },
  tileSelected: {
    borderColor: adeniTheme.accent,
    backgroundColor: "rgba(22, 101, 52, 0.08)",
  },
  tileCheck: {
    position: "absolute",
    top: 8,
    right: 8,
  },
  tileCity: {
    fontSize: 13,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  tileLanguage: {
    marginTop: 4,
    fontSize: 11,
    fontWeight: "500",
    color: adeniTheme.textMuted,
  },
  tileMeta: {
    marginTop: "auto",
    paddingTop: 8,
    fontSize: 9,
    fontWeight: "600",
    letterSpacing: 0.4,
    textTransform: "uppercase",
    color: adeniTheme.textMuted,
  },
  busy: {
    position: "absolute",
    top: 8,
    right: 10,
    fontSize: 12,
    fontWeight: "700",
    color: adeniTheme.accent,
  },
  footerButtons: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 10,
    marginTop: 16,
  },
  footerButton: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: 10,
    backgroundColor: adeniTheme.surface,
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  footerButtonText: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
});
