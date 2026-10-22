import { describe, expect, it } from "vitest";
import { localizeErrorResponse, resolveApiErrorMessage } from "./api-errors";

describe("localizeErrorResponse", () => {
  it("translates dotted error codes with params", () => {
    const message = localizeErrorResponse("en", {
      code: "subscription.booking_limit_reached",
      params: { limit: 25 },
    });

    expect(message).toContain("25");
    expect(message).toContain("booking limit");
  });

  it("falls back to title when translation is missing", () => {
    const message = localizeErrorResponse("en", {
      code: "custom.unknown_code",
      title: "Legacy English title",
    });

    expect(message).toBe("Legacy English title");
  });

  it("uses legacy category fallback for generic validation code", () => {
    const message = localizeErrorResponse("fr", {
      code: "validation",
      title: "Phone is required.",
    });

    expect(message).toBe("Vérifiez vos informations et réessayez.");
  });

  it("returns detail then code as last resort", () => {
    expect(localizeErrorResponse("en", { detail: "Detail text" })).toBe("Detail text");
    expect(localizeErrorResponse("en", { code: "some.code.without.translation" })).toBe(
      "some.code.without.translation",
    );
  });
});

describe("resolveApiErrorMessage", () => {
  it("translates API error codes with params in the active locale", () => {
    const error = { code: "subscription.booking_limit_reached", params: { limit: 25 } };

    expect(resolveApiErrorMessage("en", error, "toast.somethingWrong")).toContain("25");
    expect(resolveApiErrorMessage("fr", error, "toast.somethingWrong")).toContain("réservations");
  });

  it("falls back to the localized key for unknown codes, network and plain errors", () => {
    expect(resolveApiErrorMessage("en", { code: "custom.unknown" }, "toast.somethingWrong")).toBe(
      "Something went wrong",
    );
    expect(resolveApiErrorMessage("fr", new Error("Failed to fetch"), "toast.somethingWrong")).toBe(
      "Une erreur s'est produite",
    );
    expect(resolveApiErrorMessage("fr", undefined)).toBe("Une erreur s'est produite");
  });

  it("uses the generic category message for legacy validation codes", () => {
    expect(resolveApiErrorMessage("fr", { code: "validation" }, "toast.somethingWrong")).toContain(
      "Vérifiez vos informations",
    );
  });
});