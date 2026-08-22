import { useCallback, useEffect, useState } from "react";
import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { SymbolView } from "expo-symbols";
import type { AdeniApiError } from "@adeni/api-client";
import type { BusinessLocation, MarketConfig } from "@adeni/shared";
import { listMarkets } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { Card } from "@/components/ui/Card";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Callout } from "@/components/ui/Callout";
import { EmptyState } from "@/components/ui/EmptyState";
import { useAuth } from "@/contexts/auth-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

type LocationDraft = {
  slug: string;
  name: string;
  addressLine: string;
  area: string;
  marketId: string;
  isPrimary: boolean;
};

const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

function emptyDraft(marketId: string): LocationDraft {
  return { slug: "", name: "", addressLine: "", area: "", marketId, isPrimary: false };
}

function validateDraft(draft: LocationDraft): string | null {
  const slug = draft.slug.trim().toLowerCase();
  if (!slug) return "A URL slug is required.";
  if (!SLUG_PATTERN.test(slug)) return "Use lowercase letters, numbers, and hyphens only.";
  if (!draft.addressLine.trim()) return "Address is required.";
  if (!draft.area.trim()) return "Area is required.";
  return null;
}

function sortLocations(items: BusinessLocation[]): BusinessLocation[] {
  return [...items].sort((a, b) =>
    a.isPrimary === b.isPrimary ? a.name.localeCompare(b.name) : a.isPrimary ? -1 : 1,
  );
}

export default function BusinessLocationsScreen() {
  const {
    loading: authLoading,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    createBusinessApiClient,
  } = useAuth();

  const [markets, setMarkets] = useState<MarketConfig[]>(listMarkets());
  const [locations, setLocations] = useState<BusinessLocation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [draft, setDraft] = useState<LocationDraft>(() => emptyDraft(listMarkets()[0]?.id ?? "lagos"));
  const [draftError, setDraftError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<LocationDraft | null>(null);
  const [editError, setEditError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const loadLocations = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const items = await client.getTenantLocations();
      setLocations(sortLocations(items));
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(
        apiError.statusCode === 401
          ? "Sign in with a business account to manage locations."
          : "Could not load locations.",
      );
      setLocations([]);
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    const marketItems = listMarkets();
    if (marketItems.length > 0) {
      setMarkets(marketItems);
    }
  }, []);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    if (isBusinessPortalEnabled && hasBusinessAccount) {
      void loadLocations();
      return;
    }

    setLoading(false);
  }, [authLoading, hasBusinessAccount, isBusinessPortalEnabled, loadLocations]);

  async function handleCreate() {
    const validationError = validateDraft(draft);
    setDraftError(validationError);
    if (validationError) {
      return;
    }

    setCreating(true);
    setError(null);
    setMessage(null);

    try {
      const client = await createBusinessApiClient();
      const created = await client.addTenantLocation({
        slug: draft.slug.trim().toLowerCase(),
        name: draft.name.trim() || undefined,
        addressLine: draft.addressLine.trim(),
        area: draft.area.trim(),
        marketId: draft.marketId,
        isPrimary: draft.isPrimary || undefined,
      });
      setLocations((current) => {
        const next = draft.isPrimary
          ? current.map((item) => ({ ...item, isPrimary: false }))
          : current;
        return sortLocations([...next, created]);
      });
      setDraft(emptyDraft(draft.marketId));
      setMessage("Location added.");
    } catch {
      setError("Could not add location. Check the details and try again.");
    } finally {
      setCreating(false);
    }
  }

  function startEdit(location: BusinessLocation) {
    setEditingId(location.id);
    setEditDraft({
      slug: location.slug,
      name: location.name,
      addressLine: location.addressLine,
      area: location.area,
      marketId: location.marketId,
      isPrimary: location.isPrimary,
    });
    setEditError(null);
    setMessage(null);
  }

  async function handleUpdate(locationId: string) {
    if (!editDraft) {
      return;
    }

    const validationError = validateDraft(editDraft);
    setEditError(validationError);
    if (validationError) {
      return;
    }

    setBusyId(locationId);
    setError(null);
    setMessage(null);

    try {
      const client = await createBusinessApiClient();
      const updated = await client.updateTenantLocation(locationId, {
        slug: editDraft.slug.trim().toLowerCase(),
        name: editDraft.name.trim() || undefined,
        addressLine: editDraft.addressLine.trim(),
        area: editDraft.area.trim(),
        marketId: editDraft.marketId,
        isPrimary: editDraft.isPrimary,
      });
      setLocations((current) =>
        sortLocations(
          current.map((item) => {
            if (item.id === locationId) return updated;
            return updated.isPrimary ? { ...item, isPrimary: false } : item;
          }),
        ),
      );
      setEditingId(null);
      setEditDraft(null);
      setMessage("Location updated.");
    } catch {
      setError("Could not update location.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleDeactivate(location: BusinessLocation) {
    setBusyId(`deactivate-${location.id}`);
    setError(null);
    setMessage(null);

    try {
      const client = await createBusinessApiClient();
      await client.deactivateTenantLocation(location.id);
      setLocations((current) => current.filter((item) => item.id !== location.id));
      setMessage("Location removed.");
    } catch {
      setError("Could not remove location.");
    } finally {
      setBusyId(null);
    }
  }

  const canManage = isBusinessPortalEnabled && hasBusinessAccount;

  function renderDraftFields(
    value: LocationDraft,
    onChange: (next: LocationDraft) => void,
    validationError: string | null,
  ) {
    return (
      <>
        <Input
          label="URL slug"
          value={value.slug}
          onChangeText={(text) => onChange({ ...value, slug: text })}
          placeholder="lekki-branch"
          autoCapitalize="none"
          error={validationError ?? undefined}
        />
        <Input
          label="Display name (optional)"
          value={value.name}
          onChangeText={(text) => onChange({ ...value, name: text })}
          placeholder="Defaults to area"
        />
        <Input
          label="Address"
          value={value.addressLine}
          onChangeText={(text) => onChange({ ...value, addressLine: text })}
        />
        <Input
          label="Area"
          value={value.area}
          onChangeText={(text) => onChange({ ...value, area: text })}
        />
        <Text style={styles.fieldLabel}>Market</Text>
        <View style={styles.chipRow}>
          {markets.map((market) => (
            <Pressable
              key={market.id}
              onPress={() => onChange({ ...value, marketId: market.id })}
              style={[styles.chip, value.marketId === market.id && styles.chipActive]}
            >
              <Text style={[styles.chipText, value.marketId === market.id && styles.chipTextActive]}>
                {market.name}
              </Text>
            </Pressable>
          ))}
        </View>
        <Pressable
          style={styles.primaryToggle}
          onPress={() => onChange({ ...value, isPrimary: !value.isPrimary })}
        >
          <View style={[styles.checkbox, value.isPrimary && styles.checkboxChecked]}>
            {value.isPrimary ? (
              <SymbolView
                name={{ ios: "checkmark", android: "check", web: "check" }}
                tintColor={adeniTheme.primaryForeground}
                size={11}
              />
            ) : null}
          </View>
          <Text style={styles.primaryToggleLabel}>Set as primary location</Text>
        </Pressable>
      </>
    );
  }

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Locations"
          subtitle="Manage branches and public profile URLs for your business."
        />

        {canManage ? <BusinessTabs /> : null}

        <View style={styles.section}>
          {!isBusinessPortalEnabled ? (
            <Callout title="Sign in required">
              {isAuth0Configured()
                ? "Sign in from the Account tab to manage locations."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode."}
            </Callout>
          ) : null}

          {isBusinessPortalEnabled && !hasBusinessAccount ? (
            <Callout title="No business yet">Register your business first to add locations.</Callout>
          ) : null}

          {message ? <Callout tone="success">{message}</Callout> : null}
          {error ? <Callout tone="error">{error}</Callout> : null}

          {canManage ? (
            <>
              <Card style={styles.formCard} title="Add location">
                {renderDraftFields(draft, setDraft, draftError)}
                <Button
                  title={creating ? "Adding…" : "Add location"}
                  onPress={() => void handleCreate()}
                  loading={creating}
                  containerStyle={styles.submitButton}
                />
              </Card>

              <Text style={styles.sectionLabel}>Your locations ({locations.length})</Text>

              {locations.length === 0 ? (
                <EmptyState
                  title="No locations yet"
                  description="Add your first branch above to start accepting bookings there."
                />
              ) : (
                <View style={styles.list}>
                  {locations.map((location) =>
                    editingId === location.id && editDraft ? (
                      <Card key={location.id} style={styles.locationCard}>
                        {renderDraftFields(editDraft, setEditDraft, editError)}
                        <View style={styles.editActions}>
                          <Button
                            title="Save"
                            size="sm"
                            loading={busyId === location.id}
                            onPress={() => void handleUpdate(location.id)}
                          />
                          <Button
                            title="Cancel"
                            size="sm"
                            variant="secondary"
                            onPress={() => {
                              setEditingId(null);
                              setEditDraft(null);
                            }}
                          />
                        </View>
                      </Card>
                    ) : (
                      <Card key={location.id} style={styles.locationCard}>
                        <View style={styles.locationHeader}>
                          <Text style={styles.locationName}>{location.name}</Text>
                          {location.isPrimary ? <Badge label="Primary" tone="accent" /> : null}
                        </View>
                        <Text style={styles.locationMeta}>{location.addressLine}</Text>
                        <Text style={styles.locationMeta}>
                          {location.area} · {location.marketId}
                        </Text>
                        <Text style={styles.locationSlug}>/businesses/{location.slug}</Text>
                        <View style={styles.editActions}>
                          <Button
                            title="Edit"
                            size="sm"
                            variant="secondary"
                            onPress={() => startEdit(location)}
                          />
                          {locations.length > 1 ? (
                            <Button
                              title="Remove"
                              size="sm"
                              variant="destructive"
                              loading={busyId === `deactivate-${location.id}`}
                              onPress={() => void handleDeactivate(location)}
                            />
                          ) : null}
                        </View>
                      </Card>
                    ),
                  )}
                </View>
              )}
            </>
          ) : null}
        </View>
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  content: {
    paddingBottom: adeniTheme.spacing["3xl"],
  },
  section: {
    paddingHorizontal: adeniTheme.spacing.xl,
  },
  formCard: {
    marginTop: adeniTheme.spacing.xl,
  },
  fieldLabel: {
    marginTop: adeniTheme.spacing.lg,
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.textSubtle,
  },
  chipRow: {
    marginTop: adeniTheme.spacing.sm,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
  },
  chip: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: adeniTheme.radius.full,
    paddingHorizontal: adeniTheme.spacing.lg,
    paddingVertical: adeniTheme.spacing.sm,
    backgroundColor: adeniTheme.surface,
  },
  chipActive: {
    backgroundColor: adeniTheme.primary,
    borderColor: adeniTheme.primary,
  },
  chipText: {
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  chipTextActive: {
    color: adeniTheme.primaryForeground,
  },
  primaryToggle: {
    marginTop: adeniTheme.spacing.lg,
    flexDirection: "row",
    alignItems: "center",
    gap: adeniTheme.spacing.md,
  },
  checkbox: {
    width: 20,
    height: 20,
    borderRadius: 6,
    borderWidth: 1.5,
    borderColor: adeniTheme.borderStrong,
    alignItems: "center",
    justifyContent: "center",
  },
  checkboxChecked: {
    backgroundColor: adeniTheme.primary,
    borderColor: adeniTheme.primary,
  },
  primaryToggleLabel: {
    fontSize: 14,
    color: adeniTheme.textMuted,
  },
  submitButton: {
    marginTop: adeniTheme.spacing.xl,
    alignSelf: "stretch",
  },
  sectionLabel: {
    marginTop: adeniTheme.spacing["2xl"],
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 0.8,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  list: {
    marginTop: adeniTheme.spacing.md,
    gap: adeniTheme.spacing.md,
  },
  locationCard: {},
  locationHeader: {
    flexDirection: "row",
    alignItems: "center",
    gap: adeniTheme.spacing.sm,
  },
  locationName: {
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  locationMeta: {
    marginTop: 4,
    fontSize: 13,
    color: adeniTheme.textMuted,
  },
  locationSlug: {
    marginTop: 6,
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.accent,
  },
  editActions: {
    marginTop: adeniTheme.spacing.lg,
    flexDirection: "row",
    gap: adeniTheme.spacing.sm,
  },
});
