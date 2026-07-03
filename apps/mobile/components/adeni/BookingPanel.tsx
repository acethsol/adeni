import { useCallback, useMemo, useState } from "react";
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import type { AdeniApiError } from "@adeni/api-client";
import type { BookingResponse, ServiceOffering } from "@adeni/shared";
import { useAuth } from "@/contexts/auth-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { formatPrice, formatSlotTime, slotRange } from "@/lib/format";
import { adeniTheme } from "@/lib/theme";

type Props = {
  slug: string;
  tenantId: string;
  services: ServiceOffering[];
};

type Step = "service" | "slot" | "confirm" | "done";

export function BookingPanel({ slug, tenantId, services }: Props) {
  const { isBookingEnabled, createApiClient } = useAuth();
  const auth0Configured = isAuth0Configured();
  const [step, setStep] = useState<Step>("service");
  const [selectedService, setSelectedService] = useState<ServiceOffering | null>(null);
  const [slots, setSlots] = useState<{ startAt: string; endAt: string }[]>([]);
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null);
  const [notes, setNotes] = useState("");
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [booking, setBooking] = useState<BookingResponse | null>(null);

  const activeServices = useMemo(
    () => services.filter((service) => service.isActive),
    [services],
  );

  const loadSlots = useCallback(
    async (service: ServiceOffering) => {
      setLoadingSlots(true);
      setError(null);
      setSlots([]);
      setSelectedSlot(null);

      try {
        const client = createApiClient("customer");
        const range = slotRange(new Date(), 7);
        const items = await client.getBusinessSlots(slug, {
          serviceId: service.id,
          from: range.from,
          to: range.to,
        });
        setSlots(items);
        setStep("slot");
      } catch {
        setError("Could not load available times. Try again in a moment.");
      } finally {
        setLoadingSlots(false);
      }
    },
    [slug],
  );

  async function handleSelectService(service: ServiceOffering) {
    setSelectedService(service);
    await loadSlots(service);
  }

  async function handleConfirmBooking() {
    if (!selectedService || !selectedSlot) {
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const client = createApiClient("customer");
      const result = await client.createBooking({
        tenantId,
        serviceOfferingId: selectedService.id,
        startAt: selectedSlot,
        customerNotes: notes.trim() || undefined,
      });
      setBooking(result);
      setStep("done");
    } catch (err) {
      const apiError = err as AdeniApiError;
      if (apiError.statusCode === 401) {
        setError("Sign in to complete your booking.");
      } else {
        setError("Booking failed. That slot may have been taken.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (activeServices.length === 0) {
    return (
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Book online</Text>
        <Text style={styles.hint}>Online booking is not available yet for this business.</Text>
      </View>
    );
  }

  if (step === "done" && booking) {
    return (
      <View style={[styles.section, styles.successSection]}>
        <Text style={styles.eyebrow}>Booking submitted</Text>
        <Text style={styles.doneTitle}>{booking.serviceName}</Text>
        <Text style={styles.doneTime}>{formatSlotTime(booking.startAt)}</Text>
        <Text style={styles.hint}>
          Status: pending confirmation from the business. You will be notified once they accept.
        </Text>
      </View>
    );
  }

  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>Book online</Text>
      <Text style={styles.hint}>Choose a service and pick an available time.</Text>

      {!isBookingEnabled ? (
        <Text style={styles.authHint}>
          {auth0Configured
            ? "Sign in from the Account tab to complete your booking."
            : "Set EXPO_PUBLIC_DEV_CUSTOMER_AUTH0_SUB in .env for local booking, or sign in with Auth0."}
        </Text>
      ) : null}

      {error ? <Text style={styles.error}>{error}</Text> : null}

      {step === "service" && (
        <View style={styles.list}>
          {activeServices.map((service) => (
            <Pressable
              key={service.id}
              onPress={() => void handleSelectService(service)}
              disabled={loadingSlots}
              style={({ pressed }) => [
                styles.serviceRow,
                pressed && styles.rowPressed,
                loadingSlots && styles.rowDisabled,
              ]}
            >
              <View style={styles.serviceBody}>
                <Text style={styles.serviceName}>{service.name}</Text>
                {service.description ? (
                  <Text style={styles.serviceDescription}>{service.description}</Text>
                ) : null}
                <Text style={styles.serviceDuration}>{service.durationMinutes} min</Text>
              </View>
              <Text style={styles.servicePrice}>
                {formatPrice(service.priceAmount, service.currency)}
              </Text>
            </Pressable>
          ))}
          {loadingSlots ? (
            <ActivityIndicator color={adeniTheme.accent} style={styles.loader} />
          ) : null}
        </View>
      )}

      {step === "slot" && selectedService && (
        <View style={styles.stepBlock}>
          <Pressable
            onPress={() => {
              setStep("service");
              setSelectedService(null);
              setSlots([]);
            }}
          >
            <Text style={styles.backAction}>← Change service</Text>
          </Pressable>
          <Text style={styles.selectedLabel}>{selectedService.name}</Text>

          {loadingSlots ? (
            <Text style={styles.hint}>Loading available times…</Text>
          ) : slots.length === 0 ? (
            <Text style={styles.hint}>No open slots in the next 7 days. Check back soon.</Text>
          ) : (
            <View style={styles.slotGrid}>
              {slots.map((slot) => (
                <Pressable
                  key={slot.startAt}
                  onPress={() => {
                    setSelectedSlot(slot.startAt);
                    setStep("confirm");
                  }}
                  style={({ pressed }) => [styles.slotChip, pressed && styles.rowPressed]}
                >
                  <Text style={styles.slotChipText}>{formatSlotTime(slot.startAt)}</Text>
                </Pressable>
              ))}
            </View>
          )}
        </View>
      )}

      {step === "confirm" && selectedService && selectedSlot && (
        <View style={styles.stepBlock}>
          <Pressable onPress={() => setStep("slot")}>
            <Text style={styles.backAction}>← Pick another time</Text>
          </Pressable>

          <View style={styles.summary}>
            <SummaryRow label="Service" value={selectedService.name} />
            <SummaryRow label="Time" value={formatSlotTime(selectedSlot)} />
            <SummaryRow
              label="Price"
              value={formatPrice(selectedService.priceAmount, selectedService.currency)}
            />
          </View>

          <Text style={styles.notesLabel}>Notes (optional)</Text>
          <TextInput
            value={notes}
            onChangeText={setNotes}
            multiline
            maxLength={1000}
            placeholder="Anything the business should know?"
            placeholderTextColor={adeniTheme.textSubtle}
            style={styles.notesInput}
          />

          {isBookingEnabled ? (
            <Pressable
              onPress={() => void handleConfirmBooking()}
              disabled={submitting}
              style={({ pressed }) => [
                styles.confirmButton,
                (submitting || pressed) && styles.confirmButtonDisabled,
              ]}
            >
              <Text style={styles.confirmButtonText}>
                {submitting ? "Booking…" : "Confirm booking"}
              </Text>
            </Pressable>
          ) : null}
        </View>
      )}
    </View>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.summaryRow}>
      <Text style={styles.summaryLabel}>{label}</Text>
      <Text style={styles.summaryValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    marginTop: 24,
    backgroundColor: adeniTheme.surface,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    padding: 20,
  },
  successSection: {
    borderColor: "rgba(64, 145, 108, 0.35)",
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  eyebrow: {
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 1,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  doneTitle: {
    marginTop: 8,
    fontSize: 20,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  doneTime: {
    marginTop: 8,
    fontSize: 15,
    color: adeniTheme.textMuted,
  },
  hint: {
    marginTop: 8,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  authHint: {
    marginTop: 12,
    fontSize: 13,
    lineHeight: 19,
    color: adeniTheme.textSubtle,
  },
  error: {
    marginTop: 12,
    padding: 10,
    borderRadius: 8,
    backgroundColor: "#fef2f2",
    color: "#991b1b",
    fontSize: 14,
  },
  list: {
    marginTop: 16,
    gap: 10,
  },
  serviceRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: 12,
    padding: 14,
  },
  rowPressed: {
    borderColor: adeniTheme.accent,
    opacity: 0.95,
  },
  rowDisabled: {
    opacity: 0.6,
  },
  serviceBody: {
    flex: 1,
    paddingRight: 12,
  },
  serviceName: {
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  serviceDescription: {
    marginTop: 4,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.textMuted,
  },
  serviceDuration: {
    marginTop: 6,
    fontSize: 12,
    color: adeniTheme.textSubtle,
  },
  servicePrice: {
    fontSize: 15,
    fontWeight: "700",
    color: adeniTheme.accent,
  },
  loader: {
    marginTop: 8,
  },
  stepBlock: {
    marginTop: 16,
  },
  backAction: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.accent,
  },
  selectedLabel: {
    marginTop: 10,
    fontSize: 16,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  slotGrid: {
    marginTop: 12,
    gap: 8,
  },
  slotChip: {
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 10,
    paddingVertical: 12,
    paddingHorizontal: 14,
    backgroundColor: adeniTheme.background,
  },
  slotChipText: {
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  summary: {
    marginTop: 16,
    gap: 10,
  },
  summaryRow: {},
  summaryLabel: {
    fontSize: 13,
    color: adeniTheme.textSubtle,
  },
  summaryValue: {
    marginTop: 2,
    fontSize: 15,
    fontWeight: "600",
    color: adeniTheme.text,
  },
  notesLabel: {
    marginTop: 16,
    fontSize: 14,
    fontWeight: "600",
    color: adeniTheme.textMuted,
  },
  notesInput: {
    marginTop: 8,
    minHeight: 88,
    borderWidth: 1,
    borderColor: adeniTheme.borderStrong,
    borderRadius: 10,
    padding: 12,
    fontSize: 14,
    color: adeniTheme.text,
    textAlignVertical: "top",
    backgroundColor: adeniTheme.background,
  },
  confirmButton: {
    marginTop: 20,
    alignSelf: "flex-start",
    backgroundColor: adeniTheme.primary,
    borderRadius: 999,
    paddingHorizontal: 22,
    paddingVertical: 14,
  },
  confirmButtonDisabled: {
    opacity: 0.65,
  },
  confirmButtonText: {
    color: "#ffffff",
    fontSize: 15,
    fontWeight: "600",
  },
});
