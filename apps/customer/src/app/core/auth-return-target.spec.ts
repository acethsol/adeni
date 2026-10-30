import { isSafeAuthReturnTarget, resolveAuthReturnTarget } from "./auth-return-target";

describe("auth return target allowlist", () => {
  it("accepts same-origin app and public deep links", () => {
    expect(isSafeAuthReturnTarget("/app/home")).toBe(true);
    expect(isSafeAuthReturnTarget("/app/bookings/abc/intake")).toBe(true);
    expect(isSafeAuthReturnTarget("/businesses/lekki-cuts?service=1#book")).toBe(true);
    expect(isSafeAuthReturnTarget("/discover?market=lagos")).toBe(true);
    expect(isSafeAuthReturnTarget("/pay")).toBe(true);
    expect(isSafeAuthReturnTarget("/")).toBe(true);
  });

  it("rejects open redirects and traversal", () => {
    expect(isSafeAuthReturnTarget("https://evil.example/phish")).toBe(false);
    expect(isSafeAuthReturnTarget("//evil.example")).toBe(false);
    expect(isSafeAuthReturnTarget("/app/../evil")).toBe(false);
    expect(isSafeAuthReturnTarget("/admin")).toBe(false);
    expect(isSafeAuthReturnTarget("/lagos")).toBe(false);
    expect(isSafeAuthReturnTarget("")).toBe(false);
    expect(isSafeAuthReturnTarget(null)).toBe(false);
  });

  it("falls back when target is unsafe", () => {
    expect(resolveAuthReturnTarget("//evil", "/app/home")).toBe("/app/home");
    expect(resolveAuthReturnTarget("/app/messages", "/app/home")).toBe("/app/messages");
  });
});
