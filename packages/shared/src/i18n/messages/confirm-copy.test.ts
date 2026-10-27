import { describe, expect, it } from "vitest";
import { t } from "../index";

/** Mirrors `AdeniConfirmKind` in `@adeni/ui` — keep in sync when adding a kind. */
const CONFIRM_KINDS = [
  "save",
  "submit",
  "send",
  "create",
  "activate",
  "approve",
  "grant",
  "reactivate",
  "delete",
  "remove",
  "deactivate",
  "reject",
  "deny",
  "revoke",
  "cancel",
] as const;

describe("common.confirm copy", () => {
  for (const locale of ["en", "fr"] as const) {
    it(`defines title, message and action for every kind (${locale})`, () => {
      for (const kind of CONFIRM_KINDS) {
        for (const part of ["title", "message", "action"]) {
          const key = `common.confirm.${kind}.${part}`;
          expect(t(locale, key, { subject: "X" }), key).not.toBe(key);
        }
      }
    });

    it(`defines shared labels (${locale})`, () => {
      for (const key of ["confirmLabel", "cancelLabel", "understand", "thisItem"]) {
        const full = `common.confirm.${key}`;
        expect(t(locale, full), full).not.toBe(full);
      }
    });
  }

  it("interpolates the subject into the default message", () => {
    expect(t("en", "common.confirm.delete.message", { subject: "Braids" })).toContain("Braids");
  });
});
