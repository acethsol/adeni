import { useCallback, useEffect, useState } from "react";
import { ScrollView, StyleSheet, Text, View } from "react-native";
import type { AdeniApiError } from "@adeni/api-client";
import type { ServiceOffering } from "@adeni/shared";
import { formatPrice } from "@/lib/format";
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

type ServiceDraft = {
  name: string;
  description: string;
  priceAmount: string;
  currency: string;
  durationMinutes: string;
};

const emptyDraft = (currency = "NGN"): ServiceDraft => ({
  name: "",
  description: "",
  priceAmount: "",
  currency,
  durationMinutes: "30",
});

export default function BusinessServicesScreen() {
  const {
    loading: authLoading,
    isBusinessPortalEnabled,
    hasBusinessAccount,
    createBusinessApiClient,
  } = useAuth();

  const [services, setServices] = useState<ServiceOffering[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [draft, setDraft] = useState<ServiceDraft>(emptyDraft());
  const [creating, setCreating] = useState(false);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<ServiceDraft | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const loadServices = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const items = await client.getTenantServices();
      setServices(items);
    } catch (err) {
      const apiError = err as AdeniApiError;
      setError(
        apiError.statusCode === 401
          ? "Sign in with a business account to manage services."
          : "Could not load services.",
      );
      setServices([]);
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    if (isBusinessPortalEnabled && hasBusinessAccount) {
      void loadServices();
      return;
    }

    setLoading(false);
  }, [authLoading, hasBusinessAccount, isBusinessPortalEnabled, loadServices]);

  async function handleCreate() {
    setCreating(true);
    setError(null);
    setMessage(null);

    try {
      const client = await createBusinessApiClient();
      const created = await client.createTenantService({
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        priceAmount: Number(draft.priceAmount),
        currency: draft.currency.trim().toUpperCase(),
        durationMinutes: Number(draft.durationMinutes),
      });
      setServices((current) => [...current, created]);
      setDraft(emptyDraft(draft.currency));
      setMessage("Service added.");
    } catch {
      setError("Could not create service. Check the details and try again.");
    } finally {
      setCreating(false);
    }
  }

  function startEdit(service: ServiceOffering) {
    setEditingId(service.id);
    setEditDraft({
      name: service.name,
      description: service.description ?? "",
      priceAmount: String(service.priceAmount),
      currency: service.currency,
      durationMinutes: String(service.durationMinutes),
    });
    setMessage(null);
    setError(null);
  }

  async function handleUpdate(service: ServiceOffering) {
    if (!editDraft) {
      return;
    }

    setBusyId(service.id);
    setError(null);
    setMessage(null);

    try {
      const client = await createBusinessApiClient();
      const updated = await client.updateTenantService(service.id, {
        name: editDraft.name.trim(),
        description: editDraft.description.trim() || null,
        priceAmount: Number(editDraft.priceAmount),
        currency: editDraft.currency.trim().toUpperCase(),
        durationMinutes: Number(editDraft.durationMinutes),
        isActive: service.isActive,
      });
      setServices((current) => current.map((item) => (item.id === service.id ? updated : item)));
      setEditingId(null);
      setEditDraft(null);
      setMessage("Service updated.");
    } catch {
      setError("Could not update service.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleDeactivate(service: ServiceOffering) {
    setBusyId(service.id);
    setError(null);
    setMessage(null);

    try {
      const client = await createBusinessApiClient();
      await client.deactivateTenantService(service.id);
      setServices((current) =>
        current.map((item) => (item.id === service.id ? { ...item, isActive: false } : item)),
      );
      setMessage("Service deactivated.");
    } catch {
      setError("Could not deactivate service.");
    } finally {
      setBusyId(null);
    }
  }

  const canManage = isBusinessPortalEnabled && hasBusinessAccount;

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Services"
          subtitle="Add, edit, and deactivate bookable services on your public profile."
        />

        {canManage ? <BusinessTabs /> : null}

        <View style={styles.section}>
          {!isBusinessPortalEnabled ? (
            <Callout title="Sign in required">
              {isAuth0Configured()
                ? "Sign in from the Account tab to manage services."
                : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB in .env for local business mode."}
            </Callout>
          ) : null}

          {isBusinessPortalEnabled && !hasBusinessAccount ? (
            <Callout title="No business yet">Register your business first to add services.</Callout>
          ) : null}

          {message ? <Callout tone="success">{message}</Callout> : null}
          {error ? (
            <Callout tone="error">{error}</Callout>
          ) : null}

          {canManage ? (
            <>
              <Card style={styles.formCard} title="Add service">
                <Input
                  label="Name"
                  value={draft.name}
                  onChangeText={(value) => setDraft({ ...draft, name: value })}
                  placeholder="e.g. Signature haircut"
                />
                <Input
                  label="Description (optional)"
                  value={draft.description}
                  onChangeText={(value) => setDraft({ ...draft, description: value })}
                  multiline
                />
                <View style={styles.row}>
                  <View style={styles.rowInput}>
                    <Input
                      label="Price"
                      value={draft.priceAmount}
                      onChangeText={(value) => setDraft({ ...draft, priceAmount: value })}
                      keyboardType="decimal-pad"
                    />
                  </View>
                  <View style={styles.rowInput}>
                    <Input
                      label="Currency"
                      value={draft.currency}
                      onChangeText={(value) => setDraft({ ...draft, currency: value.toUpperCase() })}
                      maxLength={3}
                      autoCapitalize="characters"
                    />
                  </View>
                </View>
                <Input
                  label="Duration (minutes)"
                  value={draft.durationMinutes}
                  onChangeText={(value) => setDraft({ ...draft, durationMinutes: value })}
                  keyboardType="number-pad"
                />
                <Button
                  title={creating ? "Adding…" : "Add service"}
                  onPress={() => void handleCreate()}
                  loading={creating}
                  disabled={!draft.name.trim() || !draft.priceAmount || creating}
                  containerStyle={styles.submitButton}
                />
              </Card>

              <Text style={styles.sectionLabel}>Your services ({services.length})</Text>

              {services.length === 0 ? (
                <EmptyState
                  title="No services yet"
                  description="Add your first service above so customers can start booking."
                />
              ) : (
                <View style={styles.list}>
                  {services.map((service) =>
                    editingId === service.id && editDraft ? (
                      <Card key={service.id} style={styles.serviceCard}>
                        <Input
                          label="Name"
                          value={editDraft.name}
                          onChangeText={(value) => setEditDraft({ ...editDraft, name: value })}
                        />
                        <View style={styles.row}>
                          <View style={styles.rowInput}>
                            <Input
                              label="Price"
                              value={editDraft.priceAmount}
                              onChangeText={(value) =>
                                setEditDraft({ ...editDraft, priceAmount: value })
                              }
                              keyboardType="decimal-pad"
                            />
                          </View>
                          <View style={styles.rowInput}>
                            <Input
                              label="Duration (min)"
                              value={editDraft.durationMinutes}
                              onChangeText={(value) =>
                                setEditDraft({ ...editDraft, durationMinutes: value })
                              }
                              keyboardType="number-pad"
                            />
                          </View>
                        </View>
                        <View style={styles.editActions}>
                          <Button
                            title="Save"
                            size="sm"
                            loading={busyId === service.id}
                            onPress={() => void handleUpdate(service)}
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
                      <Card key={service.id} style={styles.serviceCard}>
                        <View style={styles.serviceHeader}>
                          <View style={styles.serviceInfo}>
                            <Text style={styles.serviceName}>{service.name}</Text>
                            <Text style={styles.serviceMeta}>
                              {service.durationMinutes} min · {formatPrice(service.priceAmount, service.currency)}
                            </Text>
                            {service.description ? (
                              <Text style={styles.serviceDescription}>{service.description}</Text>
                            ) : null}
                          </View>
                          <Badge
                            label={service.isActive ? "Active" : "Inactive"}
                            tone={service.isActive ? "success" : "default"}
                          />
                        </View>
                        {service.isActive ? (
                          <View style={styles.editActions}>
                            <Button title="Edit" size="sm" variant="secondary" onPress={() => startEdit(service)} />
                            <Button
                              title="Deactivate"
                              size="sm"
                              variant="destructive"
                              loading={busyId === service.id}
                              onPress={() => void handleDeactivate(service)}
                            />
                          </View>
                        ) : null}
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
  row: {
    flexDirection: "row",
    gap: adeniTheme.spacing.md,
  },
  rowInput: {
    flex: 1,
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
  serviceCard: {},
  serviceHeader: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.md,
  },
  serviceInfo: {
    flex: 1,
  },
  serviceName: {
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  serviceMeta: {
    marginTop: 4,
    fontSize: 13,
    color: adeniTheme.textMuted,
  },
  serviceDescription: {
    marginTop: 6,
    fontSize: 13,
    lineHeight: 18,
    color: adeniTheme.textMuted,
  },
  editActions: {
    marginTop: adeniTheme.spacing.lg,
    flexDirection: "row",
    gap: adeniTheme.spacing.sm,
  },
});
