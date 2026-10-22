import notificationTemplatesJson from "./data/notification-templates.json";
import type { LocaleId } from "./i18n";
import { defaultLocale, isLocaleId } from "./i18n";
import {
  notificationTemplateIdSchema,
  type NotificationTemplateId,
} from "./schemas";

export const NOTIFICATION_TEMPLATE_IDS = notificationTemplateIdSchema.options;

export type { NotificationTemplateId };

/** Templates editable in portal (Pro+ overrides). */
export const PORTAL_EDITABLE_NOTIFICATION_TEMPLATE_IDS = [
  "booking_confirmed",
  "appointment_reminder_24h",
  "appointment_reminder_2h",
  "waitlist_slot_opened",
] as const satisfies readonly NotificationTemplateId[];

type TemplateLocaleCopy = { subject: string; body: string };

type Catalog = {
  templates: Record<NotificationTemplateId, Record<string, TemplateLocaleCopy>>;
};

const catalog = notificationTemplatesJson as Catalog;

export function getDefaultNotificationTemplate(
  templateId: NotificationTemplateId,
  locale: string,
): TemplateLocaleCopy | null {
  const normalized = isLocaleId(locale) ? locale : defaultLocale;
  const entry = catalog.templates[templateId];
  if (!entry) {
    return null;
  }

  return entry[normalized] ?? entry[defaultLocale] ?? null;
}

export function renderNotificationTemplateText(
  template: TemplateLocaleCopy,
  vars?: Record<string, string | number>,
): TemplateLocaleCopy {
  const subject = applyVars(template.subject, vars);
  const body = applyVars(template.body, vars);
  return { subject, body };
}

function applyVars(text: string, vars?: Record<string, string | number>): string {
  if (!vars) {
    return text;
  }

  return Object.entries(vars).reduce(
    (result, [name, value]) => result.replaceAll(`{${name}}`, String(value)),
    text,
  );
}

export function notificationTemplateLabel(
  templateId: NotificationTemplateId,
  locale: LocaleId | string = defaultLocale,
): string {
  const loc = isLocaleId(locale) ? locale : defaultLocale;
  const labels: Record<LocaleId, Record<NotificationTemplateId, string>> = {
    en: {
      booking_confirmed: "Booking confirmed",
      booking_rejected: "Booking declined",
      booking_cancelled: "Booking cancelled",
      appointment_reminder_24h: "Reminder (24 hours before)",
      appointment_reminder_2h: "Reminder (2 hours before)",
      appointment_followup_no_show: "Missed appointment follow-up",
      waitlist_slot_opened: "Waitlist slot opened",
      payment_failed: "Payment failed",
      payment_receipt: "Payment receipt",
      conversation_customer: "New message (customer)",
      conversation_business: "New message (business)",
      staff_invite: "Staff portal invite",
      queue_called: "Walk-in called",
      billing_document_emailed: "Billing document emailed",
    },
    fr: {
      booking_confirmed: "Réservation confirmée",
      booking_rejected: "Réservation refusée",
      booking_cancelled: "Réservation annulée",
      appointment_reminder_24h: "Rappel (24 h avant)",
      appointment_reminder_2h: "Rappel (2 h avant)",
      appointment_followup_no_show: "Suivi rendez-vous manqué",
      waitlist_slot_opened: "Créneau waitlist",
      payment_failed: "Paiement échoué",
      payment_receipt: "Reçu de paiement",
      conversation_customer: "Nouveau message (client)",
      conversation_business: "Nouveau message (établissement)",
      staff_invite: "Invitation personnel",
      queue_called: "Walk-in appelé",
      billing_document_emailed: "Document de facturation",
    },
  };

  return labels[loc][templateId] ?? templateId;
}
