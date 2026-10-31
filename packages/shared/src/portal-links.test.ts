import { describe, expect, it } from "vitest";
import {
  resolveBusinessPortalRegisterUrl,
  resolveBusinessPortalWelcomeUrl,
} from "./portal-links";

describe("resolveBusinessPortalWelcomeUrl", () => {
  it("appends welcome and trims trailing slash", () => {
    expect(resolveBusinessPortalWelcomeUrl("http://localhost:5173/")).toBe(
      "http://localhost:5173/welcome",
    );
  });

  it("returns null when base is empty", () => {
    expect(resolveBusinessPortalWelcomeUrl("  ")).toBeNull();
  });
});

describe("resolveBusinessPortalRegisterUrl", () => {
  it("appends register path", () => {
    expect(resolveBusinessPortalRegisterUrl("https://portal.adeni.io")).toBe(
      "https://portal.adeni.io/register",
    );
  });
});
