/** Stable notification template identifiers (API + catalog + portal). */
export const NOTIFICATION_TEMPLATE_IDS = [
  "booking_confirmed",
  "booking_rejected",
  "booking_cancelled",
  "appointment_reminder_24h",
  "appointment_reminder_2h",
  "appointment_followup_no_show",
  "waitlist_slot_opened",
  "payment_failed",
  "payment_receipt",
  "conversation_customer",
  "conversation_business",
  "staff_invite",
  "queue_called",
  "billing_document_emailed",
] as const;

export type NotificationTemplateId = (typeof NOTIFICATION_TEMPLATE_IDS)[number];
