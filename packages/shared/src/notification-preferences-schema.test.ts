import { describe, expect, it } from "vitest";
import { notificationPreferencesSchema } from "./schemas";

describe("notificationPreferencesSchema", () => {
  it("parses GET /tenant/notification-preferences default payload with JSON nulls", () => {
    const parsed = notificationPreferencesSchema.parse({
      emailEnabled: true,
      pushEnabled: false,
      smsRemindersEnabled: false,
      defaultNotificationLocale: "en",
      secondaryNotificationLocale: null,
      notificationLanguageMode: "single",
      templateOverrides: [],
    });

    expect(parsed.secondaryNotificationLocale).toBeNull();
  });

  it("parses template overrides with null subject and body", () => {
    const parsed = notificationPreferencesSchema.parse({
      emailEnabled: true,
      pushEnabled: false,
      smsRemindersEnabled: false,
      defaultNotificationLocale: "en",
      secondaryNotificationLocale: null,
      notificationLanguageMode: "single",
      templateOverrides: [
        {
          templateId: "booking_confirmed",
          locale: "en",
          subject: null,
          body: null,
        },
      ],
    });

    expect(parsed.templateOverrides[0]?.subject).toBeNull();
  });
});
